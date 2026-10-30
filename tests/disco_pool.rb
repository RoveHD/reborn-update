# encoding: UTF-8
require 'json'
require 'ostruct'
require 'timeout'
GAME, MOD, REPORT = ARGV.map { |p| File.expand_path(p) }
Reborn = true
Rejuv = false
Desolation = false
MAXIMUMLEVEL = 150
SHINYPOKEMONCHANCE = 700
INITIALMONEY = 3000
DistortionWorld = []
load File.join(GAME, 'Scripts/DataObjects.rb')
load File.join(GAME, 'Scripts/Cache.rb')
$cache = Cache_Game.allocate
{pkmn: 'mons', moves: 'moves', items: 'items', abil: 'abil', natures: 'natures',
 trainertypes: 'ttypes', btmons: 'btmons', bttrainers: 'bttrainers'}.each do |field, file|
  path = File.join(GAME, 'patch/Data', file + '.dat')
  path = File.join(GAME, 'Data', file + '.dat') unless File.exist?(path)
  $cache.instance_variable_set("@#{field}", Marshal.load(File.binread(path)))
end

# Replace only surrounding UI/save services; Pokemon construction, forms,
# stat calculation, BTPokemon, trainer generation and rule evaluation are real.
class Pokedex
  def setFormSeen(*); end
  def setFormOwned(*); end
end
def cancelledgenders; [:MEOWSTIC, :INDEEDEE, :BASCULEGION]; end
def getMonName(species, form = 0); $cache.pkmn[species, form].name; end
def pbGetTimeNow; Time.utc(2026, 10, 29); end
def pbGetLanguage; 1; end
def _INTL(text, *args); text; end
def pbIsZCrystal?(item); $cache.items[item]&.checkFlag?(:zcrystal); end
$game_switches = Hash.new(false)
$game_variables = Hash.new(0)
$game_map = OpenStruct.new(map_id: 1)
$Settings = OpenStruct.new(unrealTimeDiverge: 0)
$PokemonStorage = Object.new
def $PokemonStorage.contains?(*); false; end
$PokemonBag = Object.new
def $PokemonBag.pbQuantity(*); 0; end
$Trainer = OpenStruct.new(id: 1, name: 'Test', language: 1, party: [], pokedex: Pokedex.new)
module Events
  def self.onBattleFacilityChallenge; self; end
  def self.trigger(*); end
end
module RPG
  AudioFile = Struct.new(:name)
end
$PokemonGlobal = OpenStruct.new
$challenge = OpenStruct.new(currentChallenge: 'towersingle', battleNumber: 1, wins: 42)
def pbBattleChallenge; $challenge; end
load File.join(GAME, 'Scripts/PBExperience.rb')
load File.join(GAME, 'Scripts/PBMove.rb')
load File.join(GAME, 'Scripts/Pokemon.rb')
load File.join(GAME, 'patch/Mods/Pokemon.rb')
load File.join(GAME, 'Scripts/Battle_Trainer.rb')
load File.join(GAME, 'Scripts/OrgBattleRules.rb')
org = File.read(File.join(GAME, 'Scripts/OrgBattle.rb'), encoding: 'UTF-8')
eval(org[/^class BTPokemon\r?\n.*?^end\r?$/m] || raise('BTPokemon missing'), TOPLEVEL_BINDING)
eval(org[/^def pbGenerateBattleTrainer\(trainerid, rule\).*?(?=^def pbOrganizedBattleEx)/m] || raise('Generation missing'), TOPLEVEL_BINDING)
allgen = File.read(File.join(GAME, 'patch/Mods/AllGen Reborn.rb'), encoding: 'UTF-8')
eval(allgen[/^ class StandardRestriction\r?\n.*?^ end\r?$/m], TOPLEVEL_BINDING)
load File.join(File.dirname(MOD), 'RE_DiscoOhneBans.rb')
load MOD
srand(20261029)

checks = 0
generated = []
original_mons, original_trainers = $cache.btmons, $cache.bttrainers
before = Marshal.dump([original_mons, original_trainers, $cache.pkmn, $cache.items, $cache.moves])
rows = REDiscoPoolData::SETS
raise 'Duplicate IDs' unless rows.map { |r| r[:id] }.uniq.size == rows.size
checks += 1
required_ultra = %i[NIHILEGO BUZZWOLE PHEROMOSA XURKITREE CELESTEELA KARTANA GUZZLORD NAGANADEL STAKATAKA BLACEPHALON]
required_paradox = %i[GREATTUSK SCREAMTAIL BRUTEBONNET FLUTTERMANE SLITHERWING SANDYSHOCKS ROARINGMOON WALKINGWAKE GOUGINGFIRE RAGINGBOLT IRONTREADS IRONBUNDLE IRONHANDS IRONJUGULIS IRONMOTH IRONTHORNS IRONVALIANT IRONLEAVES IRONBOULDER IRONCROWN KORAIDON MIRAIDON]
missing = (required_ultra + required_paradox) - rows.map { |r| r[:species] }
raise "Missing requested species: #{missing.inspect}" unless missing.empty?
checks += 1
all_variants = rows + REDiscoGegnerPool.battle_rows(true).select { |r| r[:double_moves] }
universal_source = File.read(File.join(GAME, 'patch/Mods/PBStuff.rb'), encoding: 'UTF-8')
universal_moves = universal_source[/UNIVERSALTMS = \[(.*?)\]/m, 1].scan(/:(\w+)/).flatten.map(&:to_sym)
all_variants.each do |row|
  label = row[:id]
  raise "Unknown species #{label}" unless $cache.pkmn.key?(row[:species])
  raise "Unknown form #{label}" unless $cache.pkmn[row[:species]].hasForm?(row[:form])
  raise "Unknown item #{label}" unless $cache.items.key?(row[:item])
  raise "Unknown ability #{label}" unless $cache.abil.key?(row[:ability])
  raise "Nature #{label}" unless $cache.natures.key?(row[:nature])
  raise "Moves #{label}" unless row[:moves].uniq.length == 4 && row[:moves].all? { |m| $cache.moves.key?(m) }
  raise "EVs #{label}" unless row[:ev].size == 6 && row[:ev].all? { |v| (0..252).include?(v) } && row[:ev].sum <= 510
  data = $cache.pkmn[row[:species], row[:form]]
  abilities = data.Abilities + [data.HiddenAbility].compact
  raise "Form ability #{label}: #{abilities.inspect}" unless abilities.include?(row[:ability])
  learnable = data.Moveset.map(&:last) + data.compatiblemoves + Array(data.EggMoves) + Array(data.RelearnerMoves) + (universal_moves - data.moveexceptions)
  raise "Unlearnable moves #{label}: #{(row[:moves] - learnable).inspect}" unless (row[:moves] - learnable).empty?
  raise "Choice/vest status move #{label}" if [:CHOICEBAND, :CHOICESPECS, :CHOICESCARF, :ASSAULTVEST].include?(row[:item]) && row[:moves].any? { |m| $cache.moves[m].category == :status }
  raise "Destroys own terrain #{label}" if row[:theme] == :electric && (row[:moves] & [:ICESPINNER, :DEFOG, :STEELROLLER, :MISTYTERRAIN, :GRASSYTERRAIN, :PSYCHICTERRAIN]).any?
  raise "Competing terrain #{label}" if row[:theme] == :electric && [:MISTYSURGE, :GRASSYSURGE, :PSYCHICSURGE].include?(row[:ability])
  set = BTPokemon.new(row.select { |k, _| REDiscoGegnerPool::DATA_FIELDS.include?(k) })
  pokemon = set.createPokemon(100, 31, $Trainer)
  raise "Changed form #{label}: #{pokemon.form}" unless pokemon.form == row[:form]
  raise "Changed ability #{label}" unless pokemon.ability == row[:ability]
  raise "Changed moves #{label}" unless pokemon.moves.map(&:move) == row[:moves]
  raise "Changed item #{label}" unless pokemon.item == row[:item]
  raise "Invalid stats #{label}" unless pokemon.totalhp > 0 && pokemon.hp == pokemon.totalhp && pokemon.ev == row[:ev]
  raise "Restricted #{label}" unless StandardRestriction.new.isValid?(pokemon)
  if [:ZACIAN, :ZAMAZENTA].include?(pokemon.species)
    pokemon.changeMoveOnBattleStart
    expected_move = pokemon.species == :ZACIAN ? :BEHEMOTHBLADE : :BEHEMOTHBASH
    raise 'Crowned move conversion failed' unless pokemon.moves.map(&:move).include?(expected_move)
    checks += 1
  end
  generated << {id: label, name: pokemon.name, species: pokemon.species, form: pokemon.form,
    item: pokemon.item, ability: pokemon.ability, stats: [pokemon.totalhp, pokemon.attack, pokemon.defense, pokemon.spatk, pokemon.spdef, pokemon.speed]}
  checks += 18
end

# Force only the branch selection; the native random team construction still runs.
module DiscoSelectionProbe
  def with_team(cache, trainerid, selected_rows, &block)
    $last_disco_selected_rows = selected_rows
    super
  end
end
REDiscoGegnerPool.singleton_class.prepend(DiscoSelectionProbe)
REDiscoGegnerPool.battle_rows(true).each do |row|
  unsafe = row[:moves].select { |m| $cache.moves[m].target == :AllNonUsers && $cache.moves[m].category != :status }
  raise "Unprotected partner damage #{row[:id]}: #{unsafe.inspect}" unless unsafe.empty?
  raise "Electric terrain blocks own Rest #{row[:id]}" if row[:theme] == :electric && row[:moves].include?(:REST)
  checks += 2
end
def REDiscoGegnerPool.roll; 0; end
samples = []
selection_started = Process.clock_gettime(Process::CLOCK_MONOTONIC)
[false, true].each do |double|
  [3, 4, 6].each do |number|
    rows.each do |anchor|
      team = REDiscoGegnerPool.select_team(number, double, anchor[:id])
      raise "Unreachable #{anchor[:id]} / #{number} / #{double}" unless team && team.any? { |r| r[:id] == anchor[:id] }
      raise 'Duplicate species' unless team.map { |r| r[:species] }.uniq.size == number
      raise 'Duplicate items' unless team.map { |r| r[:item] }.uniq.size == number
      raise 'Missing environment setter' if [:sun, :rain, :electric].include?(team.first[:theme]) && !REDiscoGegnerPool.weather_lead(team)
      if team.any? { |r| r[:ability] == :PRIMORDIALSEA }
        raise 'Fire ally blocked by own rain' if team.any? { |r| r[:moves].any? { |m| $cache.moves[m].type == :FIRE } }
      end
      if team.any? { |r| r[:ability] == :DESOLATELAND }
        raise 'Water ally blocked by own sun' if team.any? { |r| r[:moves].any? { |m| $cache.moves[m].type == :WATER && m != :HYDROSTEAM && $cache.moves[m].category != :status } }
      end
      if team.any? { |r| r[:ability] == :MISTYSURGE }
        raise 'Misty terrain blocks ally plan' if team.any? { |r| (r[:moves] & [:REST, :SPORE, :SLEEPPOWDER, :HYPNOSIS, :TOXIC, :WILLOWISP, :THUNDERWAVE, :YAWN]).any? }
      end
      if team.any? { |r| r[:ability] == :PSYCHICSURGE }
        raise 'Psychic terrain blocks ally priority' if team.any? { |r| (r[:moves] & [:EXTREMESPEED, :FAKEOUT, :SUCKERPUNCH, :THUNDERCLAP, :SHADOWSNEAK, :MACHPUNCH, :BULLETPUNCH, :AQUAJET, :GRASSYGLIDE, :FIRSTIMPRESSION, :ICESHARD]).any? }
      end
      if team.any? { |r| r[:ability] == :GRASSYSURGE }
        raise 'Grassy terrain weakens ally ground attack' if team.any? { |r| (r[:moves] & [:EARTHQUAKE, :MAGNITUDE, :BULLDOZE]).any? }
      end
      checks += 9
    end
  end
end
raise 'Impossible unique-item pool accepted' if REDiscoGegnerPool.fill_team(rows.first(3).map { |r| r.merge(item: :LEFTOVERS) }, [], 3)
checks += 1
matching_fixture = [rows[0].merge(species: :MEWTWO, item: :LIFEORB),
                    rows[0].merge(species: :MEWTWO, item: :LEFTOVERS),
                    rows[0].merge(species: :RAYQUAZA, item: :LIFEORB),
                    rows[0].merge(species: :DIALGA, item: :CHOICESPECS)]
30.times do
  team = REDiscoGegnerPool.fill_team(matching_fixture, [], 3)
  raise 'Matching missed viable alternate item' unless team && team.map { |r| r[:species] }.uniq.size == 3 && team.map { |r| r[:item] }.uniq.size == 3
  checks += 1
end
selection_seconds = Process.clock_gettime(Process::CLOCK_MONOTONIC) - selection_started
samples_by_theme = Hash.new(0)
1000.times do
  team = REDiscoGegnerPool.select_team(4, true)
  raise 'Random selection failed' unless team && team.size == 4
  samples_by_theme[team.first[:theme]] += 1
  checks += 1
end
raise 'Theme never sampled' unless (rows.map { |r| r[:theme] }.uniq - samples_by_theme.keys).empty?
checks += 1
[3, 4, 6].each do |number|
  $challenge.currentChallenge = number == 3 ? 'towersingle' : 'towerdouble'
  rule = pbBattleTowerRules(number != 3, false).setNumber(number)
  30.times do |i|
    trainerid = i * 10
    opponent = Timeout.timeout(10) { pbGenerateBattleTrainer(trainerid, rule) }
    raise 'Wrong number' unless opponent.party.size == number
    raise 'Invalid native team' unless rule.ruleset.isValid?(opponent.party)
    raise 'Identity changed' unless opponent.name == original_trainers[trainerid].name
    raise 'Cache not restored' unless $cache.btmons.equal?(original_mons) && $cache.bttrainers.equal?(original_trainers)
    samples << {number: number, trainer: opponent.name, species: opponent.party.map(&:species)}
    source_rows = $last_disco_selected_rows
    if (lead = REDiscoGegnerPool.weather_lead(source_rows))
      raise 'Weather setter not leading' unless opponent.party.first.species == lead[:species]
    end
    checks += 5
  end
end

# The ordinary two-thirds branch must produce the same native team for the
# same RNG state and retain the normal IV progression.
$challenge.currentChallenge = 'towersingle'
def REDiscoGegnerPool.roll; 1; end
normal_rule = pbBattleTowerRules(false, false).setNumber(3)
fingerprint = lambda { |t| t.party.map { |p| [p.species, p.form, p.item, p.moves.map(&:move), p.iv, p.ev] } }
srand(43210)
unchanged = pbGenerateBattleTrainer(0, normal_rule)
srand(43210)
native = method(:pbGenerateBattleTrainer).super_method.call(0, normal_rule)
raise 'Ordinary generation changed' unless fingerprint.call(unchanged) == fingerprint.call(native)
checks += 1
def REDiscoGegnerPool.roll; 0; end

# Exception safety is part of the cache isolation contract.
begin
  REDiscoGegnerPool.with_team($cache, 0, rows.first(3)) { raise 'injected failure' }
rescue RuntimeError => error
  raise unless error.message == 'injected failure'
end
raise 'Exception leaked pool' unless $cache.btmons.equal?(original_mons) && $cache.bttrainers.equal?(original_trainers) && !$cache.instance_variable_get(:@re_disco_pool_active)
checks += 1
%w[factorysingleopen factorydoubleopen palace arena].each do |mode|
  $challenge.currentChallenge = mode
  raise 'Wrong mode enabled' if REDiscoGegnerPool.enabled?($challenge, $cache)
  checks += 1
end
$challenge.currentChallenge = 'towersingle'
$challenge.battleNumber = 5
sentinel = Object.new
$game_variables[575] = [sentinel, :INDOOR]
raise 'Boss changed' unless pbGenerateBattleTrainer(0, nil).equal?(sentinel)
checks += 1
$challenge.battleNumber = 1
eval(org[/^def pbGenerateBattleTrainer\(trainerid, rule\).*?(?=^def pbOrganizedBattleEx)/m], TOPLEVEL_BINDING)
load MOD
raise 'Duplicate reload hook' unless Object.ancestors.count(REDiscoGegnerPool::Generation) == 1
raise 'Original data changed' unless Marshal.dump([original_mons, original_trainers, $cache.pkmn, $cache.items, $cache.moves]) == before
checks += 2
result = {success: true, checks: checks, sets: rows.size, species: rows.map { |r| r[:species] }.uniq.size,
  actual_pokemon_construction: true, actual_opponent_generation: true,
  generated_teams: samples.size, selection_seconds: selection_seconds, sampled_themes: samples_by_theme, untouched_base_data: true, visual_battle_test: false,
  generated_sets: generated, sampled_teams: samples}
File.write(REPORT, JSON.pretty_generate(result))
puts JSON.generate(result.reject { |k, _| [:generated_sets, :sampled_teams].include?(k) })
