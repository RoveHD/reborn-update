# The SCRIPTS constant is initialized in game's respective Bootstrap.rb file which is loaded before this one.
# Note that ScriptLoader needs to be loaded using Scripts.rxdata. Loading it using preloadScript in mkxp.json breaks F12 reset.

# macOS version of mkxp-z has the standard libraries located elsewhere but they're in load path by default.
$:.push('stdlib') unless System.platform[/macOS/]
# Fix loading of rbconfig gem.
$:.push('stdlib/x64-mingw32') if System.platform[/Windows/]
$:.push('stdlib/x86_64-linux') if System.platform[/Linux/]
$:.push('../Resources/Ruby/3.1.0/x86_64-darwin') if System.platform[/macOS/]
# Add external gems to load path.
$:.push('gems')

# Let Net::HTTP know where to look for cacert file
ENV['SSL_CERT_FILE'] = 'cacert.pem'

# JoiPlay RPG Maker Plugin 1.20.51 completely broke require so we have to hack the path to make it work.
def gem(name)
  if $joiplay
    require File.expand_path("../gems/" + name + ".rb", __FILE__)
  else
    require name
  end
end

def fileExists?(file)
  # System.file_exist? respects paths defined in "patches" directive in mkxp.json, unlike File.exist?.
  return System.file_exist?(file) if defined?(System.file_exist?)

  # JoiPlay however doesn't use mkxp.json, nor does it have a System.file_exist? so we need to use ugly workaround.
  return File.exist?(file) || File.exist?("patch/" + file)
end

def logError(e)
  btrace = ""
  if e.backtrace
    maxlength = $INTERNAL ? 25 : 10
    e.backtrace[0, maxlength].each do |i|
      btrace = btrace + "#{i}\n"
    end
  end
  message = "[#{GAMETITLE} #{GAMEVERSION}]\nException: #{e.class}\nMessage: #{e.message}\n#{btrace}\n"
  errorlog = "errorlog.txt"
  if (Object.const_defined?(:RTP) rescue false)
    errorlog = RTP.getSaveFileName("errorlog.txt")
  end
  File.open(errorlog, "ab") { |f| f.write(message) }
end

if $DEBUG
  begin
    gem 'pp'
  rescue LoadError
  end
end

if $joiplay
  if !File.exist?("Game.exe") || File.size("Game.exe") > 0
    raise "Please download the JoiPlay version of the game instead of the Windows version."
  end

  plugin = MKXP.plugin_version.to_i rescue 0
  if plugin < 12053
    raise "Upgrade JoiPlay RPG Maker Plugin to version 1.20.53 or newer."
  end
end

if Dir.pwd.downcase.include?("/temp/")
  raise "Please extract the game before playing it.\n- RebornVerse Team"
end

def loadScript(file)
  begin
    code = File.open(file, 'r') { |f| f.read }
    # We don't want these JoiPlay hacks anymore.
    # MKXP.run_postload(code)
    # code = MKXP.apply_overrides(code)
    eval(code, nil, file)
  rescue
    e = $!
    btrace = ""
    if e.backtrace
      maxlength = $INTERNAL ? 25 : 10
      e.backtrace[0, maxlength].each do |i|
        btrace = btrace + "#{i}\n"
      end
    end
    message = "[#{GAMETITLE} #{GAMEVERSION}]\nException: #{e.class}\nMessage: #{e.message}\n#{btrace}"
    errorlog = "errorlog.txt"
    if (Object.const_defined?(:RTP) rescue false)
      errorlog = RTP.getSaveFileName("errorlog.txt")
    end
    errorlogline = errorlog.sub(Dir.pwd + "\\", "").sub(Dir.pwd + "/", "")
    if errorlogline.length > 20
      errorlogline = "\n" + errorlogline
    end
    File.open(errorlog, "ab") { |f| f.write(message) }
    print("#{message}\nThis exception was logged in #{errorlogline}.\nPress Ctrl+C to copy this message to the clipboard.")
  end
end

# Load base game scripts
SCRIPTS.each do |path|
  next if path.nil?
  loadScript('Scripts/' + path + '.rb')
end

# Load script mods. RE names retain their original override/alias order.
# BEGIN RE MOD NAMES
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
# END RE MOD NAMES

# Main game loop
loop do
  retval = mainFunction
  if retval == 0 # failed
    loop do
      Graphics.update
    end
  elsif retval == 1 # ended successfully
    break
  end
end

# Prevents game from forcibly closing after an error so you can restart using F12.
loop do
  Graphics.update
  Input.update
end
