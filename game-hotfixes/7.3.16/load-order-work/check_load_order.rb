# Runs the actual shipped mod-loader block against isolated directory fixtures.
require 'tmpdir'
require 'fileutils'
require 'json'
require 'digest'

work = File.expand_path(__dir__)
proof_path = File.join(work, 'load-order-checks.json')
File.delete(proof_path) if File.exist?(proof_path)
game = ARGV.fetch(0, File.join(work, 'baseline'))
loader_path = File.join(work, 'Payload/Scripts/ScriptLoader.rb')
old_source = File.read(File.join(game, 'Scripts/ScriptLoader.rb'))
new_source = File.read(loader_path)
pattern = /# BEGIN RE MOD NAMES.*?# END RE MOD NAMES/m
old_block = old_source[pattern] or raise 'Old block missing'
new_block = new_source[pattern] or raise 'New block missing'
raise 'Changed code outside the mod-loader block' unless old_source.sub(pattern, '') == new_source.sub(pattern, '')

renames = {
  'RE - Beutel-Sortierung-und-TM-Infos.rb' => 'RE_Beutel-Sortierung-und-TM-Infos.rb',
  'RE - Zufallsmusik-Onlinekaempfe.rb' => 'RE_Zufallsmusik-Onlinekaempfe.rb',
  'RE - Mitnahme-Overworld.rb' => 'RE_Mitnahme-Overworld.rb',
  'RE - Begleiter-Formen.rb' => 'RE_Begleiter-Formen.rb',
  'RE - PulseDex-Versteckte-Bilder.rb' => 'RE_PulseDex-Versteckte-Bilder.rb',
  'RE - Zufallsmusik-Wildkaempfe.rb' => 'RE_Zufallsmusik-Wildkaempfe.rb',
  'RE - Alte-Megasteine-Kompatibilitaet.rb' => 'RE_Alte-Megasteine-Kompatibilitaet.rb',
  'RE - Begegnungen-Neue-Pokemon.rb' => 'RE_Begegnungen-Neue-Pokemon.rb',
  'RE - Zeit-und-Wetter-Fixes.rb' => 'RE_Zeit-und-Wetter-Fixes.rb',
  'RE - Pokedex-Ansichtsauswahl.rb' => 'RE_Pokedex-Ansichtsauswahl.rb',
  'RE - Box-Markierungen.rb' => 'RE_Box-Markierungen.rb',
  'zzLegendaryCatchDifficulty.rb' => 'RE_LegendaryCatchDifficulty.rb',
  'zzLegendaryWildMusic.rb' => 'RE_LegendaryWildMusic.rb',
  'zzzz_RE_BattleSound_099_Compatibility.rb' => 'RE_BattleSound_099_Compatibility.rb',
  'zzzzByteonEvolutionHotfix.rb' => 'RE_ByteonEvolutionHotfix.rb',
}
german = ['RE - Sprachauswahl.rb', 'RE - Deutsche-Zusatztexte.rb', 'zzLocalGermanVisible.rb',
          'zzzzGermanGrammarAndClub.rb', 'zzzzzEnglishStreetNames.rb',
          'zzzzzGermanFloorLabels.rb', 'zzzzzzGermanItemReview.rb']
baseline_names = File.join(game, 'mod-names.json')
base = if File.exist?(baseline_names)
  JSON.parse(File.read(baseline_names))
else
  Dir[File.join(game, 'patch/Mods/*.rb')].map { |file| File.basename(file) }
end
german.each { |file| base << file unless base.include?(file) }
base.concat(['aaaThirdParty.rb', 'RE_ThirdParty.rb', 'zzPersonalMod.rb', 'zzzzzzzzLast.rb'])

def run_loader(block, files)
  result = []
  context = Object.new
  context.define_singleton_method(:loadScript) { |file| result << File.basename(file) }
  Dir.mktmpdir('RE-load-order-') do |dir|
    FileUtils.mkdir_p(File.join(dir, 'patch/Mods'))
    files.each { |name| File.write(File.join(dir, 'patch/Mods', name), '') }
    begin
      Dir.chdir(dir) { context.instance_eval(block, 'Scripts/ScriptLoader.rb') }
    rescue RuntimeError => e
      e.instance_variable_set(:@executed_mods, result.dup)
      raise
    end
  end
  result
end

checks = 0
assert = lambda do |value, label|
  raise label unless value
  checks += 1
end
expected = run_loader(old_block, base)
# Compatibility before migration, including third-party entries at both ends.
assert.call(run_loader(new_block, base) == expected, 'Legacy order changed')
renames.each do |old, renamed|
  files = base.map { |name| name == old ? renamed : name }
  order = run_loader(new_block, files).map { |name| name == renamed ? old : name }
  assert.call(order == expected, 'Individual rename changes order: ' + renamed)
end
all_renamed = base.map { |name| renames.fetch(name, name) }
assert.call(run_loader(new_block, all_renamed).map { |name| renames.key(name) || name } == expected,
            'Combined renames change order')
final = all_renamed.reject { |name| german.include?(name) } + ['RE_Deutsch.rb']
final_expected = expected.reject { |name| german.include?(name) }
last_german = expected.index('zzzzzzGermanItemReview.rb')
german_insertion = expected[0...last_german].count { |name| !german.include?(name) }
final_expected.insert(german_insertion, 'RE_Deutsch.rb')
final_order = run_loader(new_block, final)
assert.call(final_order.map { |name| renames.key(name) || name } == final_expected,
            'Final order differs beyond intentional German consolidation')
assert.call(final_order.count('RE_Deutsch.rb') == 1, 'German component loaded more than once')

# Conflicts must fail before a mod can alias or prepend anything.
map = new_block.scan(/"([^"\/\\]+\.rb)"\s*=>\s*"([^"\/\\]+\.rb)"/).to_h
conflicts = map.map { |renamed, original| [renamed, original] }
map.group_by { |_, original| original }.each_value do |pairs|
  aliases = pairs.map(&:first)
  aliases.combination(2) { |pair| conflicts << pair }
end
(german + ['Languages.rb', 'zzLocalGermanText.rb']).each { |old| conflicts << ['RE_Deutsch.rb', old] }
conflicts.uniq.each do |pair|
  error = nil
  begin
    run_loader(new_block, pair)
  rescue RuntimeError => e
    error = e
  end
  assert.call(error && error.message.include?('doppelt vorhanden'), 'Missing conflict guard: ' + pair.join(' + '))
  assert.call(error.instance_variable_get(:@executed_mods).empty?, 'Conflict detected after mod execution')
end
duplicate_case_count = conflicts.uniq.length
# Ruby eval and the original loadScript install top-level methods; no cached queue
# or one-shot guard may skip a mod after F12 resets the script loader.
assert.call(run_loader(new_block, final) == final_order, 'Repeated loader call changed order')

# Execute the complete delivered loader, including its real loadScript/eval path,
# with base scripts and working alias chains rather than only recording filenames.
module System
  def self.platform; 'Windows'; end
end
SCRIPTS = ['Base']
GAMETITLE = 'Isolated load-order test'
GAMEVERSION = '7.3.16'
Dir.mktmpdir('RE-complete-loader-', work) do |dir|
  FileUtils.mkdir_p(File.join(dir, 'Scripts'))
  FileUtils.mkdir_p(File.join(dir, 'patch/Mods'))
  File.write(File.join(dir, 'Scripts/Base.rb'), <<~RUBY)
    def re_order_chain; ['base']; end
    def mainFunction; throw :re_loader_finished; end
  RUBY
  final.each_with_index do |name, index|
    File.write(File.join(dir, 'patch/Mods', name), <<~RUBY)
      alias re_order_before_#{index} re_order_chain
      def re_order_chain
        re_order_before_#{index} + [#{name.inspect}]
      end
    RUBY
  end
  2.times do
    Dir.chdir(dir) { catch(:re_loader_finished) { eval(new_source, TOPLEVEL_BINDING, loader_path) } }
    assert.call(re_order_chain == ['base'] + final_order, 'Full loader changed the alias chain or base-first phase')
    assert.call(!File.exist?(File.join(dir, 'errorlog.txt')), 'Full loader silently swallowed a mod failure')
  end
end

proof = { success: true, checks: checks, ruby: RUBY_VERSION,
          loader_sha256: Digest::SHA256.file(loader_path).hexdigest,
          harness_sha256: Digest::SHA256.file(__FILE__).hexdigest,
          unchanged_outside_mod_block: true, final_mod_order: final_order,
          legacy_mod_order: expected, duplicate_cases: duplicate_case_count,
          intentional_change: 'Seven German components consolidated at former last German position',
          complete_loader_alias_chain_runs: 2,
          limit: 'Complete real loader executed with isolated base/mod alias fixtures; full game not launched' }
File.write(proof_path, JSON.pretty_generate(proof) + "\n")
puts "RE_LOAD_ORDER_OK #{checks} checks; #{duplicate_case_count} duplicate cases"
