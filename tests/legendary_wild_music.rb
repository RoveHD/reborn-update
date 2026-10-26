# encoding: UTF-8
require 'json'
require 'ostruct'
require 'digest'

GAME = File.expand_path(ARGV.fetch(0))
MOD = File.expand_path(ARGV.fetch(1))
REPORT = File.expand_path(ARGV.fetch(2))
module RPG
  AudioFile = Struct.new(:name, :volume, :pitch)
end
module PBStuff; end
stuff = File.read(File.join(GAME, 'patch/Mods/PBStuff.rb'))
PBStuff.module_eval(stuff[/  LEGENDARYLIST = \[.*?\n  \]/m] || raise('Missing legendary list'))
load File.join(GAME, 'patch/Mods/PBS/ttypetext.rb')
TrainerType = Struct.new(:data) do
  def battleBGM; data[:battleBGM]; end
  def checkFlag?(flag); data[flag]; end
end
$PokemonGlobal = OpenStruct.new(nextBattleBGM: nil)
$game_switches = Hash.new(false)
$game_map = OpenStruct.new(map_id: 1)
$cache = OpenStruct.new(trainertypes: TTYPEHASH.transform_values { |v| TrainerType.new(v) },
  mapdata: {1 => OpenStruct.new(WildBattleBGM: 'Battle- Wild', TrainerBattleBGM: 'Battle- Trainer')},
  metadata: {WildBattle: 'Battle- Wild', TrainerBattle: 'Battle- Trainer'})
audio_source = File.read(File.join(GAME, 'Scripts/Audio.rb'))
eval(audio_source[/^def pbStringToAudioFile\(str\).*?^end/m] || raise('Missing audio parser'), TOPLEVEL_BINDING)
utilities_source = File.read(File.join(GAME, 'Scripts/Utilities.rb'))
wild_source = utilities_source[/^def pbGetWildBattleBGM\(species\).*?^end/m] || raise('Missing wild selector')
eval(wild_source, TOPLEVEL_BINDING)
music_pack = File.join(GAME, 'patch/Mods/AGMusicPack.rb')
load music_pack
load MOD
$checks = 0
def check(label)
  raise label unless yield
  $checks += 1
end

Dir.chdir(GAME) do
  rows = []
  [false, true].each do |music_enabled|
    $game_switches[3074] = music_enabled
    LegendaryWildMusic::TRAINER_TYPES.each do |species, trainer_type|
      check("Unknown trainer type #{trainer_type}") { $cache.trainertypes[trainer_type] }
      expected = pbGetTrainerBattleBGM(LegendaryWildMusic::TrainerReference.new(trainer_type))
      check("Missing supplied boss audio #{species}: #{expected&.name}") { LegendaryWildMusic.available?(expected) }
      selected = pbGetWildBattleBGM(species)
      check("Wrong theme #{species}, music=#{music_enabled}") { selected.to_a == expected.to_a }
      rows << {species: species, music_pack: music_enabled, theme: selected.name}
    end
  end
  check('Dialga pack theme') { pbGetWildBattleBGM(:DIALGA).name == 'CLBattle- Dialga-Palkia' }
  check('Groudon pack theme') { pbGetWildBattleBGM(:GROUDON).name == 'CLBattle- Weather Trio' }
  check('Giratina pack theme') { pbGetWildBattleBGM(:GIRATINA).name == 'CLBattle- Giratina' }
  check('Arceus pack theme') { pbGetWildBattleBGM(:ARCEUS).name == 'CBattle- Supreme' }
  check('Zacian group theme') { pbGetWildBattleBGM(:ZACIAN).name == 'CLBattle- Guardians' }
  check('Ruin trainer aliases') do
    LegendaryWildMusic::TRAINER_TYPES.values_at(:TINGLU, :CHIENPAO, :WOCHIEN, :CHIYU) == [:RUINV, :RUINS, :RUINT, :RUINB]
  end
  [:ARTICUNO, :ZAPDOS, :MOLTRES, :UXIE, :AZELF, :MESPRIT, :ABYSSEON, :DIVINEON].each do |species|
    check("Unmapped fallback #{species}") { pbGetWildBattleBGM(species).name == 'Battle- Legendary' }
  end
  check('Ordinary wild music unchanged') { pbGetWildBattleBGM(:PIKACHU).name == 'Battle- Wild' }
  check('Sentinel used by Field.rb unchanged') { pbGetWildBattleBGM(0).name == 'Battle- Wild' }
  explicit = RPG::AudioFile.new('Story-Exact', 63, 92)
  $PokemonGlobal.nextBattleBGM = explicit
  actual = pbGetWildBattleBGM(:DIALGA)
  check('Explicit theme, volume, pitch preserved without mutation') { actual.to_a == explicit.to_a && !actual.equal?(explicit) && $PokemonGlobal.nextBattleBGM.equal?(explicit) }
  $PokemonGlobal.nextBattleBGM = RPG::AudioFile.new('Battle- Legendary.ogg', 72, 100)
  check('Explicit generic override also preserved') { pbGetWildBattleBGM(:ARCEUS).name == 'Battle- Legendary.ogg' }
  $PokemonGlobal.nextBattleBGM = nil
  # Model an optional audio pack being absent without deleting or moving any file.
  class << File
    alias_method :legendary_test_original_file?, :file?
    def file?(path)
      return false if path.to_s.include?('CLBattle- Dialga-Palkia')
      legendary_test_original_file?(path)
    end
  end
  check('Missing custom track falls back to original wild selector') { pbGetWildBattleBGM(:DIALGA).name == 'Battle- Legendary' }
  class << File
    alias_method :file?, :legendary_test_original_file?
    remove_method :legendary_test_original_file?
  end
  saved_type = $cache.trainertypes.delete(:DIALGA)
  check('Missing trainer data falls back safely') { pbGetWildBattleBGM(:DIALGA).name == 'Battle- Legendary' }
  $cache.trainertypes[:DIALGA] = saved_type
  3.times do
    eval(wild_source, TOPLEVEL_BINDING)
    load music_pack
    load MOD
    check('Reload keeps one wrapper') { Object.ancestors.count(LegendaryWildMusic::WildBattleHook) == 1 }
    check('Reload preserves theme resolution') { pbGetWildBattleBGM(:DIALGA).name == 'CLBattle- Dialga-Palkia' }
  end
  check('Trainer battle selection untouched') { pbGetTrainerBattleBGM(LegendaryWildMusic::TrainerReference.new(:JULIA)).name == 'CBattle- Sparks' }
  report = {success: true, checks: $checks, mapped_species: LegendaryWildMusic::TRAINER_TYPES.size,
    selected_tracks: rows, mod_sha256: Digest::SHA256.file(MOD).hexdigest,
    actual_sources: ['Scripts/Utilities.rb', 'Scripts/Audio.rb', 'patch/Mods/AGMusicPack.rb', 'patch/Mods/PBS/ttypetext.rb'],
    audio_files_checked: true, game_started: false, audible_playback_tested: false}
  File.write(REPORT, JSON.pretty_generate(report) + "\n")
  puts "LEGENDARY_MUSIC_OK #{$checks} checks; #{LegendaryWildMusic::TRAINER_TYPES.size} species; actual game selectors and audio files"
end
