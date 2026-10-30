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

$checks = 0
def check(label)
  raise label unless yield
  $checks += 1
end
Dir.chdir(GAME) do
  ref = ->(type) { LegendaryWildMusic::TrainerReference.new(type) }
  $game_switches[3074] = true
  baseline = pbGetTrainerBattleBGM(OpenStruct.new(trainertype: :PAST))
  check('Reproduce original missing Future selection') { baseline.name == 'CLBattle- Future' && !File.file?('patch/Audio/BGM/CLBattle- Future.ogg') && !File.file?('Audio/BGM/CLBattle- Future.ogg') }
  load MOD
  [:PAST, :FUTURE].each do |type|
    [true, false].each do |enabled|
      $game_switches[3074] = enabled
      audio = pbGetTrainerBattleBGM(ref.call(type))
      check("Available stock theme #{type} #{enabled}") { audio.name == 'Battle- Misc.ogg' && LegendaryWildMusic.available?(audio) }
      check("Volume/pitch unchanged #{type} #{enabled}") { audio.volume == 100 && audio.pitch == 100 }
    end
  end
  $game_switches[3074] = true
  check('Double Paradox stock theme') { pbGetTrainerBattleBGM([ref.call(:PAST),ref.call(:FUTURE)]).name == 'Battle- Misc.ogg' }
  check('Koraidon/Miraidon unaffected') { pbGetTrainerBattleBGM([ref.call(:KORAIDON),ref.call(:MIRAIDON)]).name == 'CLBattle- Paradox' }
  check('Other trainer unaffected') { pbGetTrainerBattleBGM(ref.call(:JULIA)).name == 'CBattle- Sparks' }
  check('Normal wild unaffected') { pbGetWildBattleBGM(:PIKACHU).name == 'Battle- Wild' }
  check('Legendary wild unaffected') { pbGetWildBattleBGM(:DIALGA).name == 'CLBattle- Dialga-Palkia' }
  explicit = RPG::AudioFile.new('Story-Exact', 63, 92)
  $PokemonGlobal.nextBattleBGM = explicit
  check('Explicit event track preserved') { pbGetTrainerBattleBGM(ref.call(:PAST)).to_a == explicit.to_a }
  check('Event state not mutated') { $PokemonGlobal.nextBattleBGM.equal?(explicit) && explicit.to_a == ['Story-Exact',63,92] }
  $PokemonGlobal.nextBattleBGM = nil
  # Simulate a future pack providing the missing track; no audio files modified.
  class << File
    alias_method :paradox_original_file?, :file?
    def file?(path)
      return true if path.to_s == 'patch/Audio/BGM/CLBattle- Future.ogg'
      paradox_original_file?(path)
    end
  end
  check('Supplied Future track preserved') { pbGetTrainerBattleBGM(ref.call(:FUTURE)).name == 'CLBattle- Future' }
  class << File
    alias_method :file?, :paradox_original_file?
    remove_method :paradox_original_file?
  end
  check('Music switch stays enabled') { $game_switches[3074] == true }
  2.times do
    load music_pack
    load MOD
    check('Single hook after reload') { Object.ancestors.count(LegendaryWildMusic::ParadoxBattleHook) == 1 }
    check('Reload still fixes missing track') { pbGetTrainerBattleBGM(ref.call(:PAST)).name == 'Battle- Misc.ogg' }
  end
  # Pass the selected track through the installed randomizer and engine playback
  # function, capturing the audio backend rather than playing on the user's PC.
  eval(audio_source[/^def pbResolveAudioFile[(].*?^end/m], TOPLEVEL_BINDING)
  eval(audio_source[/^def pbBGMPlay[(].*?^end/m], TOPLEVEL_BINDING)
  $Settings = OpenStruct.new(volume: 65)
  $game_system = Object.new
  def $game_system.bgm_play(audio); @captured = audio; end
  def $game_system.captured; @captured; end
  load File.join(GAME, 'patch/Mods/RE_Zufallsmusik-Wildkaempfe.rb')
  [:PAST, :FUTURE].each do |type|
    pbBGMPlay(pbGetTrainerBattleBGM(ref.call(type)))
    check("Playback receives audible existing track #{type}") { $game_system.captured.name == 'Battle- Misc.ogg' && $game_system.captured.volume == 65 && LegendaryWildMusic.available?($game_system.captured) }
  end
  check('User volume unchanged') { $Settings.volume == 65 }
  report = {success: true, checks: $checks, reproduced_missing_track: baseline.name,
    fixed_track: 'Battle- Misc.ogg', mod_sha256: Digest::SHA256.file(MOD).hexdigest,
    actual_selectors_and_randomizer: true, playback_backend_captured: true, audible_playback_tested: false}
  File.write(REPORT, JSON.pretty_generate(report))
  puts JSON.generate(report)
end
