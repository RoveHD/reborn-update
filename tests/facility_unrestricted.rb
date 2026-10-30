# encoding: UTF-8
require 'json'
require 'ostruct'
GAME, MOD, REPORT = ARGV.map { |path| File.expand_path(path) }

load File.join(GAME, 'Scripts/OrgBattleRules.rb')
allgen = File.read(File.join(GAME, 'patch/Mods/AllGen Reborn.rb'), encoding: 'UTF-8')
restriction = allgen[/^ class StandardRestriction\r?\n.*?^ end\r?$/m] || raise('AllGen restriction missing')
eval(restriction, TOPLEVEL_BINDING)

# Deterministic data doubles; execute the actual installed restriction methods.
def pbBaseStatTotal(species); species == :ARCEUS ? 720 : 600; end
def pbIsZCrystal?(item); item == :NORMALIUMZ; end
def _INTL(text, *args); text; end
$game_switches = Hash.new(false)
$Trainer = OpenStruct.new(party: [])
Pokemon = Struct.new(:species, :ability, :item, :form, :hp, :egg, :level) do
  def isEgg?; egg; end
  def hasAnItem?; !item.nil?; end
end
def mon(**values)
  Pokemon.new(*{species: :MEW, ability: :SYNCHRONIZE, item: nil, form: 0,
    hp: 100, egg: false, level: 100}.merge(values).values)
end
cases = []
[:GRASSYSURGE, :MISTYSURGE, :PSYCHICSURGE, :ELECTRICSURGE, :DARKSURGE,
 :WASTINGSURGE, :BEASTBOOST, :PROTOSYNTHESIS, :QUARKDRIVE, :POWERCONSTRUCT].each do |ability|
  cases << mon(ability: ability)
end
[:GRENINJA, :GARCHOMP].each { |species| cases << mon(species: species, ability: :BATTLEBOND) }
[:KYOGRE, :GROUDON, :KORAIDON, :MIRAIDON, :ARCEUS].each { |species| cases << mon(species: species) }
[:NORMALIUMZ, :DIANCITE, :LATIASITE, :LATIOSITE, :DRAGONINITE, :SALAMENCITE,
 :METAGROSSITE, :TYRANITARITE, :GARCHOMPITE, :GENESISORB, :PRIMORDIALORB,
 :RUSTEDSHIELD, :RUSTEDSWORD].each { |item| cases << mon(item: item) }
[:KYUREM, :NECROZMA].each { |species| cases << mon(species: species, form: 1) }
rule = StandardRestriction.new
checks = 0
cases.each { |pokemon| raise 'Baseline ban not reproduced' if rule.isValid?(pokemon); checks += 1 }
load MOD
cases.each do |pokemon|
  $Trainer.party = [pokemon]
  raise 'Player still banned' unless rule.isValid?(pokemon)
  raise 'Opponent still banned' unless rule.isValid?(pokemon.dup)
  checks += 2
end
[[nil, false], [mon(egg: true), false], [mon, true]].each do |pokemon, expected|
  $Trainer.party = [pokemon]
  raise 'Validity changed' unless rule.isValid?(pokemon) == expected
  checks += 1
end
fainted = mon(species: :ARCEUS, hp: 0)
$Trainer.party = [fainted]
$game_switches[:Nuzlocke_Mode] = true
raise 'Nuzlocke bypassed' if rule.isValid?(fainted)
$game_switches[:Nuzlocke_Mode] = false
raise 'Normal fainted behavior changed' unless rule.isValid?(fainted)
checks += 2

# The engine reloads classes in-place on F12, then loads mods again.
eval(restriction, TOPLEVEL_BINDING)
load MOD
raise 'Duplicate hook' unless StandardRestriction.ancestors.count(REDiscoOhneBans) == 1
raise 'Reload lost player override' unless rule.isValid?(fainted)
raise 'Reload lost opponent override' unless rule.isValid?(fainted.dup)
checks += 3
raise 'Level restriction changed' if MaximumLevelRestriction.new(50).isValid?(mon)
raise 'Species clause changed' if SpeciesClause.new.isValid?([mon, mon])
raise 'Item clause changed' if ItemClause.new.isValid?([mon(item: :LEFTOVERS), mon(species: :CELEBI, item: :LEFTOVERS)])
checks += 3
result = {success: true, checks: checks, ban_cases: cases.size,
  player_bans_removed: true, opponent_bans_removed: true, reload_checked: true,
  visual_gameplay_test: false}
File.write(REPORT, JSON.pretty_generate(result))
puts JSON.generate(result)
