# encoding: UTF-8
# Isolated checks using the installed AllGen PP methods; no game/save writes.
require 'json'
require 'digest'
MOD = File.expand_path(ARGV.fetch(0))
GAME = File.expand_path(ARGV.fetch(1))
REPORT = ARGV[2]

module PBStuff; end
stuff = File.read(File.join(GAME, 'patch/Mods/PBStuff.rb'))
PBStuff.module_eval(stuff[/  LEGENDARYLIST = \[.*?\n  \]/m] || raise('Legendary list missing'))
module BallHandlers
  module ModifyCatchRate
    def self.add(*args); end
  end
end
module ItemHandlers; end
class PokeBattle_Battle
  attr_accessor :opponent, :decision, :battlers, :online
  def initialize
    @opponent = nil; @decision = 0; @battlers = []; @online = false
  end
  def isOnline?; @online; end
end
class PokeBattle_Move; end
class PokeBattle_Battler
  attr_accessor :index, :pokemon, :effects, :moves
end
move_source = File.read(File.join(GAME, 'patch/Mods/Battle_Move.rb'))
PokeBattle_Move.class_eval(move_source[/  attr_accessor.*?(?=  #+)/m] || raise('Move attributes missing'))
PokeBattle_Move.class_eval(move_source[/  def totalpp\n.*?^  end/m] || raise('totalpp missing'))
battle_source = File.read(File.join(GAME, 'patch/Mods/Battle.rb'))
PokeBattle_Battle.class_eval(battle_source[/  def pbIsOpposing\?\(index\).*?^  end/m] || raise('Side helper missing'))
battler_source = File.read(File.join(GAME, 'patch/Mods/Battler.rb'))
pp_source = battler_source[/  def pbSetPP\(move, pp\).*?(?=  #+\n  # Using a move)/m] || raise('PP methods missing')
PokeBattle_Battler.class_eval(pp_source)
load MOD

Mon = Struct.new(:species)
BaseMove = Struct.new(:move, :pp, :totalpp)
def fixture(species = :ARCEUS, index = 1, pp = 5, total = 5)
  battle = PokeBattle_Battle.new
  mon = Mon.new(species)
  battler = PokeBattle_Battler.new
  battler.index = index; battler.pokemon = mon
  battler.effects = Hash.new(0).merge(Transform: false)
  battler.instance_variable_set(:@battle, battle)
  battle.battlers[index] = battler
  move = PokeBattle_Move.allocate
  move.instance_variable_set(:@battle, battle)
  move.instance_variable_set(:@user, mon)
  move.instance_variable_set(:@pp, pp)
  move.move = :RECOVER; move.basemove = BaseMove.new(:RECOVER, pp, total)
  battler.moves = [move]
  [battle, battler, move]
end
$checks = 0
def check(label)
  yield
  $checks += 1
  puts "PASS #{label}"
end
def assert(value, message = 'assertion failed'); raise message unless value; end

species = (PBStuff::LEGENDARYLIST + LegendaryCatchDifficulty::ADDITIONAL_LEGENDS +
  LegendaryCatchDifficulty::MYTHICAL_EVENTS).uniq.select { |s| LegendaryCatchDifficulty.legendary?(s) }
species.each do |s|
  check("#{s}: repeated use, Pressure and PP-draining effects") do
    battle, battler, move = fixture(s)
    250.times do
      assert(battler.pbReducePP(move))
      3.times { battler.pbReducePPOther(move) }
      battler.pbSetPP(move, 0) # Grudge/Spite-style absolute drain.
      move.pp -= 3           # Eerie Spell bypasses pbSetPP.
      assert(move.pp == 5 && move.basemove.pp == 5)
    end
  end
end
check('player and ally legendaries spend PP normally') do
  [0, 2].each do |index|
    _, battler, move = fixture(:ARCEUS, index)
    5.times { assert(battler.pbReducePP(move)) }
    assert(move.pp == 0 && move.basemove.pp == 0)
    assert(!battler.pbReducePP(move))
  end
end
check('ordinary wild Pokemon, Eevee evolutions, Ultra Beasts and Paradox spend PP') do
  [:EEVEE, :ABYSSEON, :DIVINEON, :NIHILEGO, :IRONVALIANT].each do |s|
    _, battler, move = fixture(s)
    5.times { battler.pbReducePP(move) }
    assert(move.pp == 0 && !battler.pbReducePP(move))
  end
end
check('trainer and online opponents spend PP normally') do
  [:trainer, :online].each do |kind|
    battle, battler, move = fixture
    kind == :trainer ? battle.opponent = Object.new : battle.online = true
    5.times { battler.pbReducePP(move) }
    assert(move.pp == 0 && move.basemove.pp == 0)
  end
end
check('mixed double wild battle protects only legendary opponent') do
  battle, legend, move = fixture(:DIALGA, 3)
  _, ordinary, normalmove = fixture(:EEVEE, 1)
  ordinary.instance_variable_set(:@battle, battle)
  normalmove.instance_variable_set(:@battle, battle)
  battle.battlers[1] = ordinary
  5.times { legend.pbReducePP(move); ordinary.pbReducePP(normalmove) }
  assert(move.pp == 5 && normalmove.pp == 0)
end
check('original species governs Transform; underlying moves are not overwritten') do
  _, legend, move = fixture(:MEW)
  legend.effects[:Transform] = true
  move.basemove.pp = 2
  legend.pbSetPP(move, 0)
  assert(move.pp == 5 && move.basemove.pp == 2)
  _, ditto, copied = fixture(:DITTO)
  ditto.effects[:Transform] = true
  ditto.pbSetPP(copied, 0)
  assert(copied.pp == 0)
end
check('zero PP starts usable but negative and zero-total special moves stay intact') do
  _, _, depleted = fixture(:ARCEUS, 1, 0)
  assert(depleted.pp == 5)
  _, battler, special = fixture(:ARCEUS, 1, -1, 0)
  assert(special.pp == -1 && battler.pbReducePP(special))
  special.pp = -1
  assert(special.pp == -1)
end
check('same-species player object and detached battle moves are not protected') do
  battle, battler, move = fixture
  move.instance_variable_set(:@user, Mon.new(:ARCEUS))
  move.pp = 0
  assert(move.pp == 0)
  move.instance_variable_set(:@user, battler.pokemon)
  battle.battlers.clear
  move.pp = 0
  assert(move.pp == 0)
end
check('capture and every other battle end restore normal PP behavior') do
  [1, 2, 3, 4, 5].each do |decision|
    battle, battler, move = fixture
    battler.pbReducePP(move)
    battle.decision = decision
    5.times { battler.pbReducePP(move) }
    assert(move.pp == 0 && move.basemove.pp == 0)
  end
end
check('script reload does not duplicate PP hooks') do
  old_verbose = $VERBOSE
  begin
    $VERBOSE = nil
    load MOD
  ensure
    $VERBOSE = old_verbose
  end
  assert(PokeBattle_Move.ancestors.count(LegendaryCatchDifficulty::WildMovePPHook) == 1)
  assert(PokeBattle_Battler.ancestors.count(LegendaryCatchDifficulty::WildBattlerPPHook) == 1)
  _, battler, move = fixture
  10.times { battler.pbReducePP(move) }
  assert(move.pp == 5)
end
report = { success: true, checks: $checks, species: species.size,
  simulated_turns: species.size * 250, mod_sha256: Digest::SHA256.file(MOD).hexdigest,
  harness_sha256: Digest::SHA256.file(__FILE__).hexdigest,
  engine_pp_sha256: Digest::SHA256.hexdigest(pp_source),
  live_audio_or_full_engine_test: false }
File.write(REPORT, JSON.pretty_generate(report)) if REPORT
puts "INFINITE_WILD_PP_OK #{$checks} checks, #{species.size} species"
