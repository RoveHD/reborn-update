# Development-only hook: load after all shipped mods in an isolated Game.exe fixture.
# Never install in the active game or distribute with a release.
require 'json'
require 'fileutils'
require 'digest'

module RebornRuntimeCatalog
  class Exporter
    def initialize(output)
      @output = File.expand_path(output)
      @entities = []
      @texts = []
      @errors = []
      @coverage = {}
      @by_text = Hash.new { |hash, key| hash[key] = [] }
      @sections = Hash.new { |hash, key| hash[key] = [] }
      @source_files = {}
      @unknown_objects = []
    end

    def physical(path)
      File.file?('patch/' + path) ? 'patch/' + path : path
    end

    def source(path)
      path = physical(path)
      unless @source_files.key?(path)
        @source_files[path] = File.file?(path) ? {sha256: Digest::SHA256.file(path).hexdigest, size: File.size(path)} : {missing: true}
      end
      path
    end

    def launcher_game_version
      raise 'Missing isolated launcher version.txt' unless File.file?('version.txt')
      value = File.read('version.txt', encoding: 'bom|utf-8').strip
      raise "Invalid isolated launcher version.txt: #{value.inspect}" unless value.match?(/\A\d+\.\d+\.\d+\z/)
      value
    end

    def launcher_version_proof(value)
      {path: 'version.txt', value: value, sha256: Digest::SHA256.file('version.txt').hexdigest}
    end

    def clean(value, depth = 0)
      return {unexpandedClass: value.class.name} if depth > 10
      case value
      when String, Numeric, TrueClass, FalseClass, NilClass then value
      when Symbol then value.to_s
      when Array then value.map { |item| clean(item, depth + 1) }
      when Hash then value.each_with_object({}) { |(key, item), result| result[key.to_s] = clean(item, depth + 1) }
      when Proc
        {dynamicCode: true, sourceLocation: value.source_location}
      else
        value.instance_variables.each_with_object({class: value.class.name}) do |ivar, result|
          result[ivar.to_s.sub('@', '')] = clean(value.instance_variable_get(ivar), depth + 1)
        end
      end
    end

    def guard(category, key)
      yield
    rescue StandardError => error
      @errors << {category: category, key: key.to_s, error: error.full_message}
    end

    def parse_intl
      section, lines = nil, []
      flush = lambda do
        next unless section && !lines.empty?
        indexed = lines[0][0].match?(/^\d+$/)
        width = indexed ? 3 : 2
        raise "Invalid intl row count in #{section}" unless lines.length % width == 0
        lines.each_slice(width).with_index do |chunk, ordinal|
          en, de = chunk[-2], chunk[-1]
          raw_index = indexed ? chunk[0][0] : nil
          id = indexed ? "#{section}/index/#{raw_index}" : "#{section}/ordinal/#{format('%06d', ordinal + 1)}"
          row = {entryId: id, section: section, rawIndex: raw_index, ordinal: ordinal + 1,
            en: MessageTypes.denormalizeValue(en[0]), de: MessageTypes.denormalizeValue(de[0]), sourceLines: {en: en[1], de: de[1]}}
          @sections[section] << row
          [row[:en], row[:de]].uniq.each { |text| @by_text[[section, MessageTypes.stringToKey(text)]] << row }
        end
      end
      File.readlines('intl.txt', encoding: 'bom|utf-8').each_with_index do |line, index|
        text = line.rstrip
        next if text.strip.empty? || text.start_with?('###')
        if text =~ /^\s*\[\s*([^\]]+)\s*\]\s*$/
          flush.call
          section, lines = Regexp.last_match(1), []
        else
          lines << [text, index + 1]
        end
      end
      flush.call
      source('intl.txt')
    end

    def references(section, *texts)
      texts.compact.select { |text| text.is_a?(String) && !text.empty? }.flat_map do |text|
        @by_text[[section.to_s, MessageTypes.stringToKey(text)]]
      end.uniq { |row| row[:entryId] }
    end

    def add_entity(category, key, path, raw_name, runtime_name, section = nil, metadata = {})
      refs = section ? references(section, raw_name, runtime_name) : []
      originals = refs.map { |row| row[:en] }.uniq
      id = "#{category}/#{key}"
      entity = {entityId: id, category: category, sourceFile: source(path), sourceKey: key.to_s,
        enName: originals.length == 1 ? originals[0] : nil, deName: runtime_name,
        sourceName: raw_name, nationalDex: nil, form: nil,
        coverageStatus: 'inventoried', coverageFlags: [], entryIds: refs.map { |row| row[:entryId] },
        intlRefs: refs.map { |row| row[:entryId] }, metadata: clean(metadata)}
      entity[:coverageFlags] << 'english_name_unknown' if entity[:enName].nil?
      entity[:coverageFlags] << 'original_column_not_linguistically_verified' unless entity[:enName].nil?
      entity[:coverageFlags] << 'no_intl_name_reference' if refs.empty?
      @entities << entity
      entity
    end

    def add_text(entity, type, raw, runtime, section = nil, context = {}, refs_override = nil)
      return if raw.nil? && runtime.nil?
      unless raw.is_a?(String) || runtime.is_a?(String)
        @unknown_objects << {entityId: entity[:entityId], reason: 'structured_text_value_requires_display_resolution', textType: type,
          sourceValue: clean(raw), runtimeValue: clean(runtime), context: clean(context)}
        return
      end
      refs = refs_override || (section ? references(section, raw, runtime) : [])
      originals = refs.map { |row| row[:en] }.uniq
      @texts << {observationId: "runtime/#{@texts.length + 1}", entityId: entity[:entityId], category: entity[:category],
        textType: type, sourceFile: entity[:sourceFile], sourceKey: entity[:sourceKey],
        sourceText: raw, en: originals.length == 1 ? originals[0] : nil, de: runtime,
        englishSourceStatus: originals.length == 1 ? 'intl_original_column_not_linguistically_verified' : originals.empty? ? 'unknown' : 'ambiguous',
        entryIds: refs.map { |row| row[:entryId] }, intlRefs: refs.map { |row| row[:entryId] },
        context: clean(context), linguisticStatus: 'unreviewed', runtimeLookupObserved: true}
      entity[:entryIds] |= refs.map { |row| row[:entryId] }
      entity[:intlRefs] = entity[:entryIds]
    end

    def checkpoint(category, expected)
      rows = @entities.select { |entity| entity[:category] == category }
      ids = rows.map { |entity| entity[:entityId] }
      @coverage[category] = {expected: expected, exported: rows.length, uniqueIds: ids.uniq.length,
        inventoryComplete: expected == rows.length && ids.uniq.length == rows.length,
        linguisticReviewComplete: false, missingEnglishNames: rows.count { |entity| entity[:enName].nil? }}
    end

    def export_species
      forms = 0
      $cache.pkmn.each do |key, wrapper|
        guard('species', key) do
          base = wrapper[0]
          source_name = base.instance_variable_get(:@name)
          entity = add_entity('species', key, 'Data/mons.dat', source_name, getMonName(key), 1,
            formCount: wrapper.pokemonData.length, flags: wrapper.flags, formInit: wrapper.formInit)
          entity[:nationalDex] = base.dexnum > 0 ? base.dexnum : nil
          add_text(entity, 'name', source_name, getMonName(key), 1)
          form_keys = wrapper.forms.values | wrapper.pokemonData.keys
          form_keys.each do |name|
            guard('speciesForms', "#{key}/#{name}") do
              form_id = wrapper.forms.key(name)
              data = wrapper.pokemonData.fetch(name)
              raw_name = data.instance_variable_get(:@name) || source_name
              form = add_entity('speciesForms', "#{key}/#{form_id.nil? ? 'unindexed-' + name : form_id}", 'Data/mons.dat', raw_name,
                form_id.nil? ? data.name : getMonName(key, form_id), 1,
                speciesEntityId: entity[:entityId], formName: name, formIndex: form_id, data: data)
              form[:nationalDex] = data.dexnum > 0 ? data.dexnum : nil
              form[:form] = form_id.nil? ? name : form_id
              form[:coverageFlags] << 'form_not_indexed_in_wrapper' if form_id.nil?
              add_text(form, 'name', raw_name, form[:deName], 1)
              # getFormName returns the wrapper label directly in this runtime.
              add_text(form, 'formName', name, name, 4)
              add_text(form, 'kind', data.instance_variable_get(:@kind) || base.instance_variable_get(:@kind), data.kind, 2)
              add_text(form, 'pokedexEntry', data.instance_variable_get(:@dexentry) || base.instance_variable_get(:@dexentry), data.dexentry, 3)
              forms += 1
            end
          end
        end
      end
      checkpoint('species', $cache.pkmn.size)
      checkpoint('speciesForms', $cache.pkmn.values.sum { |wrapper| (wrapper.forms.values | wrapper.pokemonData.keys).length })
    end

    def export_core
      [ ['moves', $cache.moves, 'moves.dat', 5, 6, :getMoveName, :getMoveDesc],
        ['abilities', $cache.abil, 'abil.dat', 9, 10, :getAbilityName, :getAbilityDesc],
        ['items', $cache.items, 'items.dat', 7, 8, :getItemName, :getItemDescription],
        ['types', $cache.types, 'types.dat', 11, nil, :getTypeName, nil] ].each do |category, data, file, name_section, desc_section, getter, desc_getter|
        data.each do |key, value|
          guard(category, key) do
            raw_name = category == 'abilities' ? value.fullName : category == 'moves' ? value.checkFlag?(:longname, value.name) : value.name
            entity = add_entity(category, key, 'Data/' + file, raw_name, Object.new.send(getter, key), name_section, data: value)
            add_text(entity, 'name', raw_name, entity[:deName], name_section)
            if desc_getter
              effective_section = desc_section
              raw_desc = category == 'abilities' ? value.fullDesc : value.desc
              context = {}
              if category == 'items' && pbIsTM?(key)
                move = pbGetTM(key)
                raw_desc = $cache.moves.fetch(move).desc
                effective_section = 6
                context = {descriptionInheritedFromMove: move, rawItemDescription: value.desc}
              end
              add_text(entity, 'description', raw_desc, Object.new.send(desc_getter, key), effective_section, context)
            end
            if category == 'abilities'
              add_text(entity, 'shortName', value.name, getAbilityName(key, true), 9)
              add_text(entity, 'shortDescription', value.desc, pbGetMessageFromHash(10, value.desc), 10)
            end
          end
        end
        checkpoint(category, data.size)
      end
      $cache.trainertypes.each do |key, value|
        guard('trainerClasses', key) do
          entity = add_entity('trainerClasses', key, 'Data/ttypes.dat', value.title, pbGetMessageFromHash(12, value.title), 12, data: value)
          add_text(entity, 'name', value.title, entity[:deName], 12)
        end
      end
      checkpoint('trainerClasses', $cache.trainertypes.size)
      $cache.natures.each do |key, value|
        entity = add_entity('natures', key, 'Data/natures.dat', value.name, getNatureName(key), 22, data: value)
        add_text(entity, 'name', value.name, entity[:deName], 22)
      end
      checkpoint('natures', $cache.natures.size)
    end

    def strings(value, path = [], depth = 0, &block)
      return if depth > 10
      case value
      when String then yield value, path unless value.empty?
      when Array then value.each_with_index { |item, i| strings(item, path + [i], depth + 1, &block) }
      when Hash
        value.each do |key, item|
          yield key, path + ['key', key] if key.is_a?(String) && !key.empty?
          strings(item, path + [key.to_s], depth + 1, &block)
        end
      when Numeric, Symbol, TrueClass, FalseClass, NilClass, Proc then nil
      else
        value.instance_variables.each { |ivar| strings(value.instance_variable_get(ivar), path + [ivar.to_s], depth + 1, &block) }
      end
    end

    def export_other_data
      $cache.FEData.each do |key, value|
        guard('fields', key) do
          entity = add_entity('fields', key, 'Data/fields.dat', value.name, value.name, 22, data: value)
          strings(value) do |text, path|
            next if path.any? { |part| part.to_s.match?(/graphic|condition|effect|Power/i) } && !path.any? { |part| part.to_s.match?(/message/i) }
            add_text(entity, 'fieldText', text, _INTL(text), 22, fieldPath: path)
          end
        end
      end
      checkpoint('fields', $cache.FEData.size)
      $cache.FENotes.each_with_index do |value, index|
        next unless value
        guard('fieldNotes', index) do
          raw = value.instance_variable_get(:@text)
          entity = add_entity('fieldNotes', index, 'Data/fieldnotes.dat', raw, value.text, 22, field: value.fieldeffect, data: value)
          [:text, :elaboration, :cogwheeltext].each do |method|
            add_text(entity, method.to_s, value.instance_variable_get('@' + method.to_s), value.send(method), 22)
          end
        end
      end
      checkpoint('fieldNotes', $cache.FENotes.count { |value| !value.nil? })
      trainer_count = 0
      $cache.trainers.each do |type, names|
        names.each do |name, teams|
          trainer_count += 1
          guard('trainers', "#{type}/#{name}") do
            entity = add_entity('trainers', "#{type}/#{name}", 'Data/trainers.dat', name, pbGetMessageFromHash(13, name), 13,
              trainerClass: type, teamIds: teams.map { |team| team[0] })
            add_text(entity, 'name', name, entity[:deName], 13)
            teams.each do |team|
              [[3, 15, 'aceSpeech'], [4, 16, 'defeatSpeech']].each do |index, section, type_name|
                add_text(entity, type_name, team[index], pbGetMessageFromHash(section, team[index]), section, teamId: team[0]) if team[index].is_a?(String)
              end
            end
          end
        end
      end
      checkpoint('trainers', trainer_count)
      $cache.bttrainers.each_with_index do |value, index|
        entity = add_entity('battleTowerTrainers', index, 'Data/bttrainers.dat', value.name, pbGetMessageFromHash(13, value.name), 13, data: value)
        [[:introSpeech, 14], [:winSpeech, 15], [:loseSpeech, 16]].each do |method, section|
          text = value.send(method)
          add_text(entity, method.to_s, text, pbGetMessageFromHash(section, text), section)
        end
      end
      checkpoint('battleTowerTrainers', $cache.bttrainers.size)
      $cache.town_map.each do |key, value|
        guard('townMapPoints', key) do
          name = value.is_a?(Hash) ? value[:name] : value.respond_to?(:name) ? value.name : nil
          entity = add_entity('townMapPoints', key, 'Data/townmap.dat', name, name.is_a?(String) ? _INTL(name) : nil, 22, data: value)
          strings(value) { |text, path| add_text(entity, 'mapPointText', text, _INTL(text), 22, dataPath: path) }
        end
      end
      checkpoint('townMapPoints', $cache.town_map.size)
      $cache.bosses.each do |key, value|
        entity = add_entity('bosses', key, 'Data/bossdata.dat', value.name, _INTL(value.name), 22, data: value)
        strings(value) { |text, path| add_text(entity, 'bossText', text, _INTL(text), 22, dataPath: path) }
      end
      checkpoint('bosses', $cache.bosses.size)
    end

    def event_commands(entity, list, map_id, context)
      paragraph = ''
      start = nil
      flush = lambda do
        unless paragraph.empty?
          text = paragraph.gsub(/([^\.\!\?])\s\s+/, '\\1 ')
          refs = references("Map#{map_id}", text)
          refs = references('Map0', text) if refs.empty? && map_id != 0
          add_text(entity, 'eventDialogue', text, MessageTypes.getFromMapHash(map_id, text), "Map#{map_id}", context.merge(commandIndex: start), refs)
        end
        paragraph, start = '', nil
      end
      list.each_with_index do |command, index|
        code, parameters = command.code, command.parameters
        flush.call unless [101, 401].include?(code)
        case code
        when 101, 401
          flush.call if code == 101 && !paragraph.empty?
          start ||= index
          paragraph += ' ' unless paragraph.empty?
          paragraph += parameters[0].to_s
        when 102
          parameters[0].each_with_index do |text, choice|
            add_text(entity, 'eventChoice', text, MessageTypes.getFromMapHash(map_id, text), "Map#{map_id}", context.merge(commandIndex: index, choiceIndex: choice))
          end
        when 355, 655
          event_script(entity, parameters[0].to_s, map_id, context.merge(commandIndex: index))
        when 111
          event_script(entity, parameters[1].to_s, map_id, context.merge(commandIndex: index)) if parameters[0] == 12
        when 209
          route = parameters[1]
          route.list.each_with_index do |move, route_index|
            event_script(entity, move.parameters[0].to_s, map_id, context.merge(commandIndex: index, routeIndex: route_index)) if move.code == 45
          end
        end
      end
      flush.call
    end

    def event_script(entity, script, map_id, context)
      texts = []
      pbAddScriptTexts(texts, script)
      texts.each do |text|
        add_text(entity, 'eventScriptText', text, MessageTypes.getFromMapHash(map_id, text), "Map#{map_id}", context)
      end
      # Computed Ruby text and nonliteral interpolation cannot be proven exhaustively by a static scan.
      if script.match?(/(?:_I|_INTL|pbMessage)\s*\(/) && texts.empty?
        @unknown_objects << {entityId: entity[:entityId], sourceFile: entity[:sourceFile], reason: 'dynamic_or_nonliteral_event_text', context: context, rubySource: script}
      end
    end

    def export_maps
      physical_ids = Dir['Data/Map[0-9]*.rxdata', 'patch/Data/Map[0-9]*.rxdata'].map { |path| File.basename(path)[/Map(\d+)/, 1].to_i }.uniq
      map_ids = ($cache.mapinfos.keys | physical_ids).sort
      map_ids.each_with_index do |id, index|
        info = $cache.mapinfos[id]
        entity = add_entity('maps', id, format('Data/Map%03d.rxdata', id), info&.name, info ? pbGetMapNameFromId(id) : nil, 20,
          mapId: id, registeredInMapInfos: !info.nil?, mapInfo: info, mapInfosSource: source('Data/MapInfos.rxdata'))
        map_refs = @sections["Map#{id}"]
        entity[:entryIds] |= map_refs.map { |row| row[:entryId] }
        entity[:intlRefs] = entity[:entryIds]
        entity[:metadata][:intlEntryCount] = map_refs.size
        entity[:coverageFlags] << 'no_intl_map_section' if map_refs.empty?
        entity[:coverageFlags] << 'map_file_missing' unless File.file?(entity[:sourceFile])
        entity[:coverageFlags] << 'missing_mapinfo' unless info
        guard('maps', id) do
          map = $cache.map_load(id)
          entity[:metadata][:width], entity[:metadata][:height] = map.width, map.height
          entity[:metadata][:eventCount] = map.events.size
          map.events.each do |event_id, event|
            event.pages.each_with_index do |page, page_index|
              event_commands(entity, page.list, id, eventId: event_id, eventName: event.name, pageIndex: page_index, x: event.x, y: event.y)
            end
          end
        end
        Graphics.update if index % 5 == 0
      end
      checkpoint('maps', map_ids.size)
      @coverage['maps'][:registryCount] = $cache.mapinfos.size
      @coverage['maps'][:physicalMapFileIds] = physical_ids.size
      $cache.RXevents.each_with_index do |event, index|
        next unless event
        entity = add_entity('commonEvents', index, 'Data/CommonEvents.rxdata', event.name, event.name, nil)
        guard('commonEvents', index) { event_commands(entity, event.list, 0, commonEventId: index, eventName: event.name) }
      end
      checkpoint('commonEvents', $cache.RXevents.count { |event| !event.nil? })
    end

    def export_scripts
      # All base scripts and mods were loaded before this hook; the later ItemRuntimeSmoke is test-only.
      files = Dir['Scripts/**/*.rb', 'patch/Mods/**/*.rb'].reject { |path| File.basename(path).match?(/RuntimeSmoke\.rb$/) }.sort
      files.each_with_index do |path, index|
        guard('scripts', path) do
          text = File.read(path, encoding: 'bom|utf-8')
          entity = add_entity('scripts', path, path, File.basename(path), File.basename(path), nil,
            activeBaseScript: SCRIPTS.compact.any? { |script| path == 'Scripts/' + script + '.rb' },
            mod: path.start_with?('patch/Mods/'), dataDefinition: path.start_with?('patch/Mods/PBS/'))
          found = []
          pbAddRgssScriptTexts(found, text)
          found.uniq.each { |literal| add_text(entity, 'scriptLiteral', literal, _INTL(literal), 22) }
          entity[:metadata][:literalCount] = found.uniq.size
          entity[:coverageFlags] << 'computed_and_non_INTL_script_text_not_exhaustively_resolved'
        end
        Graphics.update if index % 20 == 0
      end
      checkpoint('scripts', files.size)
    end

    def write(name, data)
      File.write(File.join(@output, name), JSON.pretty_generate(data))
    end

    def run
      raise 'Use only an isolated translation-review fixture' unless Dir.pwd.tr('\\', '/').include?('/translation-review/')
      raise 'Wrong AllGen runtime' unless GAMETITLE.include?('0.9.9') && $cache.items.size == 1158
      launcher_version = launcher_game_version
      launcher_proof = launcher_version_proof(launcher_version)
      parse_intl
      $Settings = PokemonOptions.new
      $Settings.language = 0
      pbLoadMessages('Data/german.dat')
      %w[Game.exe Game.ini Data/Scripts.rxdata Data/german.dat Data/messages.dat Scripts/ScriptLoader.rb Scripts/PBIntl.rb].each { |path| source(path) }
      export_species
      export_core
      export_other_data
      export_maps
      export_scripts
      diagnostic_maps = [9, 11, 13].map do |id|
        map = $cache.map_load(id)
        {mapId: id, name: $cache.mapinfos[id].name, sourceFile: source(format('Data/Map%03d.rxdata', id)),
          events: map.events.map do |event_id, event|
            {eventId: event_id, eventName: event.name, x: event.x, y: event.y,
              pages: event.pages.each_with_index.map do |page, page_index|
                {pageIndex: page_index, conditions: clean(page.condition),
                  commands: page.list.each_with_index.map { |command, index| {commandIndex: index, code: command.code, indent: command.indent, parameters: clean(command.parameters)} }}
              end}
          end}
      end
      write('event-context-maps-9-11-13.json', schemaVersion: 1, actualEngineExport: true, maps: diagnostic_maps)
      # Record every runtime cache group, including groups with no currently identified player-facing text.
      covered = %w[pkmn moves items trainertypes trainers FEData FENotes abil types bosses natures mapinfos town_map RXevents]
      cache_inventory = $cache.instance_variables.to_h do |ivar|
        value = $cache.instance_variable_get(ivar)
        key = ivar.to_s.delete_prefix('@')
        [key, {class: value.class.name, count: value.respond_to?(:size) ? value.size : nil,
          coverageStatus: covered.include?(key) ? 'inventoried' : 'unknown',
          coverageFlag: covered.include?(key) ? nil : 'not_a_text_catalog_or_not_yet_structurally_extracted'}]
      end
      dynamic_groups = {
        quests: 'No uniform runtime quest registry identified; event/script text is inventoried without inventing quest membership.',
        achievements: 'Progress text occurs in script/mod logic; no independent complete achievement definition registry proven.',
        eventSemanticRoles: 'NPC/story/book/sign/postgame classification is not derivable safely from command text alone.',
        conditionalRuntimeTexts: 'Computed Ruby, interpolation, save-dependent and input-dependent strings remain explicitly unresolved.',
        nonIntlScriptStrings: 'Only literal _INTL/_ISPRINTF double-quoted calls use the engine extraction helper; other strings need source/context review.',
        encodedResources: 'Text rendered in images/audio/fonts is outside the structured text export.'
      }.map { |key, reason| {category: key, coverageStatus: 'unknown', coverageFlag: reason} }
      duplicate_ids = @entities.group_by { |entity| entity[:entityId] }.select { |_, rows| rows.size > 1 }.keys
      source_info = {runtimeRoot: Dir.pwd, gameTitle: GAMETITLE, gameVersion: GAMEVERSION,
        launcherGameVersion: launcher_version, launcherVersionProof: launcher_proof,
        engine: 'actual bundled Game.exe / mkxp-z Ruby runtime', exportedAt: Time.now.utc.strftime('%Y-%m-%dT%H:%M:%SZ'),
        language: 'de', baseline: '7.3.23 / AllGen 0.9.9',
        exporterSha256: Digest::SHA256.file(__FILE__).hexdigest, sourceFilesProof: 'source-files.json'}
      @source_files.each do |path, proof|
        next if proof[:missing]
        raise "Runtime source changed during export: #{path}" unless Digest::SHA256.file(path).hexdigest == proof[:sha256]
      end
      raise 'Launcher version changed during export' unless Digest::SHA256.file('version.txt').hexdigest == launcher_proof[:sha256]
      write('entities.json', schemaVersion: 1, source: source_info, entities: @entities,
        categories: @entities.group_by { |entity| entity[:category] }.transform_values { |rows| rows.map { |entity| entity[:entityId] } })
      write('runtime-texts.json', schemaVersion: 1, source: source_info, observations: @texts)
      write('source-files.json', schemaVersion: 1, source: source_info, files: @source_files)
      write('coverage.json', schemaVersion: 1, success: @errors.empty? && duplicate_ids.empty? && @coverage.values.all? { |row| row[:inventoryComplete] },
        actualEngineExport: true, linguisticReviewComplete: false, source: source_info, categories: @coverage,
        entityCount: @entities.size, uniqueEntityIds: @entities.map { |entity| entity[:entityId] }.uniq.size,
        textObservationCount: @texts.size, textObservationsWithoutIntlRefs: @texts.count { |row| row[:entryIds].empty? },
        unknownEnglishObservations: @texts.count { |row| row[:en].nil? }, duplicateEntityIds: duplicate_ids,
        cacheInventory: cache_inventory, unresolvedGroups: dynamic_groups, unresolvedEventExpressions: @unknown_objects,
        errors: @errors)
      puts "CATALOG_EXPORT_COMPLETE entities=#{@entities.size} texts=#{@texts.size} errors=#{@errors.size}"
      @errors.empty? && duplicate_ids.empty? ? 0 : 24
    end
  end
end

begin
  output = ENV.fetch('REBORN_CATALOG_OUTPUT')
  FileUtils.mkdir_p(output)
  File.delete(File.join(output, 'export-error.txt')) if File.file?(File.join(output, 'export-error.txt'))
  Process.exit!(RebornRuntimeCatalog::Exporter.new(output).run)
rescue Exception => error
  File.write(File.join(output || Dir.pwd, 'export-error.txt'), error.full_message)
  Process.exit!(23)
end
