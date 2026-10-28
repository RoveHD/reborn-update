"""Prepare only the reviewed loader block; never modify the game installation."""
import json
from pathlib import Path
import re

WORK = Path(__file__).resolve().parent
GAME = WORK / 'baseline'
source = (GAME / 'Scripts/ScriptLoader.rb').read_text(encoding='utf-8-sig')
block = '''# BEGIN RE MOD NAMES
re_mod_names = {
  "RE - Beutel-Sortierung-und-TM-Infos.rb" => "Bag.rb",
  "RE - Zufallsmusik-Onlinekaempfe.rb" => "Battle_Online.rb",
  "RE - Sprachauswahl.rb" => "Languages.rb",
  "RE - Mitnahme-Overworld.rb" => "mitnahme.rb",
  "RE - Begleiter-Formen.rb" => "PokemonFormItems.rb",
  "RE - PulseDex-Versteckte-Bilder.rb" => "PulseDex.rb",
  "RE - Zufallsmusik-Wildkaempfe.rb" => "randomwildmusic.rb",
  "RE - Alte-Megasteine-Kompatibilitaet.rb" => "Retired Mega Stone Compatibility.rb",
  "RE - Begegnungen-Neue-Pokemon.rb" => "smartrepel.rb",
  "RE - Zeit-und-Wetter-Fixes.rb" => "TimeWeather.rb",
  "RE - Deutsche-Zusatztexte.rb" => "zzLocalGermanText.rb",
  "RE - Pokedex-Ansichtsauswahl.rb" => "zzPokedexFrontMenu.rb",
  "RE_Beutel-Sortierung-und-TM-Infos.rb" => "Bag.rb",
  "RE_Zufallsmusik-Onlinekaempfe.rb" => "Battle_Online.rb",
  "RE_Mitnahme-Overworld.rb" => "mitnahme.rb",
  "RE_Begleiter-Formen.rb" => "PokemonFormItems.rb",
  "RE_PulseDex-Versteckte-Bilder.rb" => "PulseDex.rb",
  "RE_Zufallsmusik-Wildkaempfe.rb" => "randomwildmusic.rb",
  "RE_Alte-Megasteine-Kompatibilitaet.rb" => "Retired Mega Stone Compatibility.rb",
  "RE_Begegnungen-Neue-Pokemon.rb" => "smartrepel.rb",
  "RE_Zeit-und-Wetter-Fixes.rb" => "TimeWeather.rb",
  "RE_Pokedex-Ansichtsauswahl.rb" => "zzPokedexFrontMenu.rb",
  "RE_Box-Markierungen.rb" => "RE - Box-Markierungen.rb",
  "RE_LegendaryCatchDifficulty.rb" => "zzLegendaryCatchDifficulty.rb",
  "RE_LegendaryWildMusic.rb" => "zzLegendaryWildMusic.rb",
  "RE_BattleSound_099_Compatibility.rb" => "zzzz_RE_BattleSound_099_Compatibility.rb",
  "RE_ByteonEvolutionHotfix.rb" => "zzzzByteonEvolutionHotfix.rb",
  "RE_Deutsch.rb" => "zzzzzzGermanItemReview.rb",
}
mod_files = Dir["./patch/Mods/*.rb"]
# Reject old/new copies before executing any mod: aliases and prepends must not
# run twice, even when both renamed variants replaced the same original file.
mod_files.group_by { |file| re_mod_names.fetch(File.basename(file), File.basename(file)) }.each_value do |files|
  if files.length > 1
    raise "RE-Mod doppelt vorhanden: #{files.map { |file| File.basename(file) }.join(' und ')}. Bitte die Installation anhand der RE-Modliste bereinigen."
  end
end
# The German file contains these components in their former internal order.
# Retained custom/modified originals require an explicit migration decision.
if mod_files.include?("./patch/Mods/RE_Deutsch.rb")
  re_german_originals = [
    "Languages.rb", "RE - Sprachauswahl.rb",
    "zzLocalGermanText.rb", "RE - Deutsche-Zusatztexte.rb",
    "zzLocalGermanVisible.rb", "zzzzGermanGrammarAndClub.rb",
    "zzzzzEnglishStreetNames.rb", "zzzzzGermanFloorLabels.rb",
    "zzzzzzGermanItemReview.rb",
  ]
  conflicts = mod_files.select { |file| re_german_originals.include?(File.basename(file)) }
  unless conflicts.empty?
    raise "RE-Deutsch doppelt vorhanden: RE_Deutsch.rb und #{conflicts.map { |file| File.basename(file) }.join(', ')}. Bitte die Installation anhand der RE-Modliste bereinigen."
  end
end
mod_files.sort_by { |file| re_mod_names.fetch(File.basename(file), File.basename(file)) }.each do |file|
  loadScript(file)
end
# END RE MOD NAMES'''
source, count = re.subn(r'# BEGIN RE MOD NAMES.*?# END RE MOD NAMES', lambda _: block, source, flags=re.S)
assert count == 1, 'Expected the existing reviewed RE loader block exactly once'
target = WORK / 'Payload/Scripts/ScriptLoader.rb'
target.parent.mkdir(parents=True, exist_ok=True)
with target.open('w', encoding='utf-8', newline='\n') as stream:
    stream.write(source)
print(target)
