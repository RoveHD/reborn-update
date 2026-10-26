# encoding: UTF-8
# Isolated checks using the original throw routine, ball handlers and SWM mod.
# No game startup, save data, original files or player data are written.
require "digest"
require "stringio"
require "json"
ROOT = File.expand_path("..", __dir__)
GAME = File.expand_path("../update-0.9.9-20261025/working-game", ROOT)
Reborn = true
Rejuv = false
Desolation = false
GAMEVERSION = "19.5.43"
LOAD_ORDER = ARGV.fetch(0, "swm-first")
raise "Unknown load order" unless ["swm-first", "original", "catch-first", "script-reload"].include?(LOAD_ORDER)
CATCH_MOD = File.expand_path(ARGV.fetch(1, File.join(ROOT, "custom-mods/legendary-catch/patch/Mods/zzLegendaryCatchDifficulty.rb")))

class ItemHandlerHash
  def initialize; @handlers = {}; end
  def add(item, value); @handlers[item] = value; end
  def [](item); @handlers[item]; end
  def trigger(item, *args); @handlers.fetch(item).call(item, *args); end
end

def _INTL(text, *args)
  args.each_with_index { |arg, index| text = text.gsub("{#{index + 1}}", arg.to_s) }
  text
end
def getItemName(item); item.to_s; end
def pbIsUltraBeast?(battler); false; end
def pbIsSnagBall?(ball); false; end
module Input
  CTRL = :ctrl
  class << self; attr_accessor :ctrl; end
  def self.press?(key); !!ctrl; end
end
module PBStuff; end
pb_stuff = File.read(File.join(GAME, "patch/Mods/PBStuff.rb"))
list_source = pb_stuff[/  LEGENDARYLIST = \[.*?\n  \]/m]
raise "Legendary list missing" unless list_source
PBStuff.module_eval(list_source)
PBStuff.module_eval(pb_stuff[/  ULTRABEASTS = \[.*?\]/m])
load File.join(GAME, "Scripts/Balls.rb")

module PokeBattle_BattleCommon; end
throw_file = File.join(GAME, "patch/Mods/Battle.rb")
throw_source = File.read(throw_file)
start_index = throw_source.index("  def pbThrowPokeBall(")
end_index = throw_source.index("  def addFusion(")
body = throw_source[start_index...end_index]
first_line = throw_source[0...start_index].count("\n") + 1
eval("module PokeBattle_BattleCommon\n#{body}\nend", TOPLEVEL_BINDING, throw_file, first_line - 1)
fetch_start = throw_source.index("  def pbBallFetch(")
fetch_line = throw_source[0...fetch_start].count("\n") + 1
fetch_body = throw_source[fetch_start...start_index]
eval("module PokeBattle_BattleCommon\n#{fetch_body}\nend", TOPLEVEL_BINDING, throw_file, fetch_line - 1)

FakeMon = Struct.new(:species, :catchRate, :form, :target_form, :name,
                     :obtainMode, :ballused) do
  def getForm(mon); target_form; end
  def pbRecordFirstMoves; end
  def isShadow?; false; end
end
class FakeBattler
  attr_accessor :pokemon, :index, :form, :hp, :totalhp, :status, :isbossmon,
                :issossmon, :capturable, :shieldCount, :partner, :opposite,
                :ability, :item, :effects, :ev
  def initialize(species, index, hp = 100)
    @index = index; @hp = hp; @totalhp = 100; @form = 0
    @pokemon = FakeMon.new(species, 3, 0, 0, species.to_s)
    @isbossmon = false; @issossmon = false; @capturable = true; @shieldCount = 0
    @effects = {}; @ev = [0] * 6
  end
  def species; pokemon.species; end
  def isFainted?; hp <= 0; end
  def pbPartner; partner; end
  def pbOppositeOpposing; opposite; end
  def isShadow?; false; end
end
class FakeScene
  attr_reader :events
  attr_accessor :choices, :on_throw
  def initialize; @events = []; @choices = []; end
  def pbChooseBallTarget(index); @choices.empty? ? index : @choices.shift; end
  def pbThrow(*args); @events << [:throw, *args]; on_throw.call if on_throw; end
  def pbThrowAndDeflect(*args); @events << [:deflect, *args]; end
  def pbWildBattleSuccess; end
  def pbThrowSuccess; end
  def pbHideCaptureBall; end
end
class FakeBag
  attr_accessor :quantities
  def initialize; @quantities = Hash.new(0); end
  def pbQuantity(item); quantities[item]; end
  def pbCanStore?(item); true; end
  def pbStoreItem(item); quantities[item] += 1; end
  def pbDeleteItem(item); quantities[item] -= 1 if quantities[item] > 0; end
end
class FakeDex
  attr_accessor :owned
  def initialize; @owned = 0; end
  def getOwnedCount; owned; end
  def dexList; Hash.new { |hash, key| hash[key] = { :owned? => true } }; end
  def setFormOwned(mon); end
end
FakeTrainer = Struct.new(:name, :pokedex)
class ScriptedRandom
  attr_reader :calls
  attr_accessor :values
  def initialize; @values = {}; @calls = []; end
  def rand(limit)
    @calls << limit
    result = values.fetch(limit, limit - 1)
    result.is_a?(Array) ? (result.empty? ? limit - 1 : result.shift) : result
  end
end
module FakeBattleMethods
  attr_reader :battlers, :scene, :messages, :stored, :rng
  attr_accessor :opponent
  def initialize(species = :ARCEUS)
    @battlers = [FakeBattler.new(:EEVEE, 0), FakeBattler.new(species, 1),
                 FakeBattler.new(:EEVEE, 2, 0), FakeBattler.new(:EEVEE, 3, 0)]
    @battlers.each_with_index do |battler, index|
      battler.partner = @battlers[index ^ 2]
      battler.opposite = @battlers[index ^ 1]
    end
    @scene = FakeScene.new; @messages = []; @stored = []; @snaggedpokemon = []
    @rng = @battleRandom = ScriptedRandom.new
  end
  def pbRandom(limit); @battleRandom.rand(limit); end
  def pbIsOpposing?(index); index.odd?; end
  def isOnline?; false; end
  def pbPlayer; $Trainer; end
  def pbDisplayBrief(message); @messages << message; end
  def pbDisplay(message); @messages << message; end
  def pbDisplayPaused(message); @messages << message; end
  def pbGainEXP; end
  def pbStorePokemon(mon); @stored << mon; end
  def addFusion(mon); end
end
class PokeBattle_Battle
  include PokeBattle_BattleCommon
  include FakeBattleMethods
end
# The real Safari class independently includes BattleCommon; it does not
# inherit Battle. Keep that structure so hook installation is checked too.
class PokeBattle_SafariZone
  include PokeBattle_BattleCommon
  include FakeBattleMethods
  def pbRandom(limit); @battleRandom.rand(limit); end
end
# Load the actual wrapper: its aliasing, repeated doubles target selection and
# reusable Premier Ball hook are part of the integration being checked.
swm_file = File.join(GAME, "patch/Mods/SWM - ReusablePremierBalls.rb")
load swm_file unless ["original", "catch-first"].include?(LOAD_ORDER)
# Execute the real item dispatch method and the real ball-use callback too.
module ItemHandlers
  UseInBattle = ItemHandlerHash.new
end
items_source = File.read(File.join(GAME, "Scripts/Items.rb"))
dispatch = items_source[/  def self.triggerUseInBattle\(.*?^  end/m]
raise "Item dispatch missing" unless dispatch
ItemHandlers.module_eval(dispatch)
effects_source = File.read(File.join(GAME, "patch/Mods/ItemEffects.rb"))
callback = effects_source[/proc \{ \|item, battler, battle\| # Any Poké Ball.*?^   \}/m]
raise "Ball callback missing" unless callback
[:MASTERBALL, :ULTRABALL].each { |item| ItemHandlers::UseInBattle.add(item, eval(callback)) }
load CATCH_MOD
load swm_file if LOAD_ORDER == "catch-first"

$tests = 0
def check(name)
  $PokemonBag = FakeBag.new
  $Trainer = FakeTrainer.new("Test", FakeDex.new)
  $game_switches = Hash.new(false)
  $game_variables = Hash.new(0)
  $DEBUG = false; Input.ctrl = false
  previous_stdout = $stdout
  $stdout = StringIO.new
  yield
  $stdout = previous_stdout
  $tests += 1
  puts "PASS #{name}"
ensure
  $stdout = previous_stdout if previous_stdout
end
def assert(value, message = "assertion failed"); raise message unless value; end
def throw_ball(battle, ball = :ULTRABALL)
  battle.pbThrowPokeBall(1, ball)
end
def passed_gate(battle)
  battle.rng.values[LegendaryCatchDifficulty::SCALE] = 0
end
def caught_rolls(battle)
  battle.rng.values[256] = 255
  battle.rng.values[65536] = 0
end
def count(battle, index = 1)
  LegendaryCatchDifficulty.other_ball_throws(battle, battle.battlers[index].pokemon)
end

if LOAD_ORDER == "script-reload"
  check("first throw succeeds before real ScriptLoader-order reload") do
    battle = PokeBattle_Battle.new
    throw_ball(battle)
    assert(count(battle) == 1)
    assert(LegendaryCatchDifficulty.instance_variable_get(:@validated))
  end
  # ScriptLoader evals the base scripts, then alphabetically sorted mods.
  # Replay the actual relevant definitions in that order in the SAME VM.
  eval("module PokeBattle_BattleCommon\n#{body}\nend", TOPLEVEL_BINDING, throw_file, first_line - 1)
  load swm_file
  load CATCH_MOD
end

check("compatibility guard accepts #{LOAD_ORDER} routine") do
  LegendaryCatchDifficulty.validate!
end
check("tier factors and pre-ball rarity cap") do
  expected = { :REGICE => 1.0, :REGIGIGAS => 0.3, :MEW => 0.3,
    :GROUDON => 0.6, :DIALGA => 0.45, :PALKIA => 0.45,
    :GIRATINA => 0.3, :RAYQUAZA => 0.3, :TERAPAGOS => 0.3,
    :ARCEUS => 0.15, :EEVEE => 1.0, :PECHARUNT => 0.3, :DARKRAI => 0.45, :CRESSELIA => 0.45, :TAPUKOKO => 0.8, :PHIONE => 0.3 }
  expected.each { |species, factor| assert(LegendaryCatchDifficulty.factor(species) == factor, species.to_s) }
  assert(LegendaryCatchDifficulty.base_rate_cap(:RAYQUAZA) == 3)
  assert(LegendaryCatchDifficulty.base_rate_cap(:REGICE) == 3)
  assert(LegendaryCatchDifficulty.base_rate_cap(:MEW) == 3)
  assert(LegendaryCatchDifficulty.base_rate_cap(:EEVEE).nil?)
  assert(LegendaryCatchDifficulty::ARCEUS_REBORN_REQUIREMENT == 30)
end
check("ordinary Pokemon unchanged and consumes no gate RNG") do
  battle = PokeBattle_Battle.new(:EEVEE); caught_rolls(battle)
  throw_ball(battle)
  assert(battle.stored.length == 1)
  assert(!battle.rng.calls.include?(1_000_000))
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
end
check("legendary captures always use four shakes without critical capture") do
  [255, 0].each do |critical_roll|
    battle = PokeBattle_Battle.new; passed_gate(battle); caught_rolls(battle)
    battle.battlers[1].hp = 1; battle.battlers[1].status = :SLEEP
    battle.rng.values[256] = critical_roll
    throw_ball(battle)
    assert(battle.stored.length == 1)
    assert(battle.scene.events.last[2] == 4)
    assert(battle.scene.events.last[3] == false)
    assert(!battle.rng.calls.include?(256))
    assert(battle.rng.calls.count(65536) == 4)
    assert(count(battle) == 1)
  end
end
check("ordinary Pokemon retains original critical capture") do
  battle = PokeBattle_Battle.new(:EEVEE); caught_rolls(battle)
  battle.battlers[1].hp = 1; battle.battlers[1].status = :SLEEP
  battle.rng.values[256] = 0
  throw_ball(battle)
  assert(battle.stored.length == 1 && battle.scene.events.last[3] == true)
  assert(battle.rng.calls.count(256) == 1 && battle.rng.calls.count(65536) == 1)
end
check("ordinary Pokemon still benefits from catching charms") do
  [0, 4].each do |charm_count|
    [:CATCHINGCHARM, :CATCHINGCHARM2, :CATCHINGCHARM3, :CATCHINGCHARM4].each_with_index do |item, index|
      $PokemonBag.quantities[item] = index < charm_count ? 1 : 0
    end
    battle = PokeBattle_Battle.new(:EEVEE)
    battle.rng.values[256] = 255; battle.rng.values[65536] = 27000
    throw_ball(battle)
    assert(battle.stored.length == (charm_count == 4 ? 1 : 0))
  end
end
check("rejected gate defeats every shake and critical path") do
  [255, 0].each do |critical_roll|
    battle = PokeBattle_Battle.new; caught_rolls(battle)
    battle.battlers[1].hp = 1; battle.battlers[1].status = :SLEEP
    battle.rng.values[256] = critical_roll
    throw_ball(battle)
    assert(battle.stored.empty?)
    assert(count(battle) == 1)
    assert(battle.scene.events.last[2] < 4)
  end
end
check("failed gate remains impossible with all charms and defeated boss bonus") do
  [:CATCHINGCHARM, :CATCHINGCHARM2, :CATCHINGCHARM3, :CATCHINGCHARM4].each { |item| $PokemonBag.quantities[item] = 1 }
  battle = PokeBattle_Battle.new
  battle.battlers[1].isbossmon = true; battle.battlers[1].hp = 1
  battle.battlers[1].status = :SLEEP
  caught_rolls(battle); battle.rng.values[256] = 0
  throw_ball(battle)
  assert(battle.stored.empty?)
  assert(battle.scene.events.last[3] == false)
  assert(battle.scene.events.last[4] == false)
end
check("gate boundary is exact and one draw applies to entire throw") do
  [149_999, 150_000].each do |gate_roll|
    battle = PokeBattle_Battle.new; caught_rolls(battle)
    battle.rng.values[LegendaryCatchDifficulty::SCALE] = gate_roll
    throw_ball(battle)
    assert(battle.stored.length == (gate_roll == 149_999 ? 1 : 0))
    assert(battle.rng.calls.count(1_000_000) == 1)
  end
end
check("gate scales Gen3 auto-capture without altering ball handlers") do
  BallHandlers::ModifyCatchRate.add(:POWERBALL, proc { |ball, rate, battle, battler| 500 })
  [0, 999_999].each do |gate_roll|
    battle = PokeBattle_Battle.new
    battle.battlers[1].pokemon.catchRate = 255
    battle.battlers[1].hp = 1; battle.battlers[1].status = :SLEEP
    battle.rng.values[LegendaryCatchDifficulty::SCALE] = gate_roll
    caught_rolls(battle); throw_ball(battle, :POWERBALL)
    assert(battle.stored.length == (gate_roll == 0 ? 1 : 0))
  end
end
check("exact Emerald integer thresholds, status multipliers and zero odds") do
  battler = FakeBattler.new(:REGICE, 1)
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(6, battler) == 19784)
  battler.hp = 1
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(6, battler) == 24965)
  battler.status = :BURN
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(6, battler) == 26886)
  [:SLEEP, :FROZEN].each do |status|
    battler.status = status
    assert(LegendaryCatchDifficulty.gen3_shake_threshold(6, battler) == 29958)
  end
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(0, battler) == 0)
  battler.hp = battler.totalhp; battler.status = nil
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(762, battler) == 65535)
  assert(LegendaryCatchDifficulty.gen3_shake_threshold(765, battler) == 65536)
end
check("ordinary legendary threshold is unchanged by all charms, boss bonus and dex progress") do
  charms = [:CATCHINGCHARM, :CATCHINGCHARM2, :CATCHINGCHARM3, :CATCHINGCHARM4]
  [nil, :SLEEP, :BURN].zip([19784, 23301, 21845]).each do |status, threshold|
    (0..4).each do |charm_count|
      charms.each_with_index { |item, index| $PokemonBag.quantities[item] = index < charm_count ? 1 : 0 }
      [false, true].each do |boss|
        [threshold - 1, threshold].each do |roll|
          battle = PokeBattle_Battle.new(:REGICE)
          battle.battlers[1].status = status; battle.battlers[1].isbossmon = boss
          battle.battlers[1].pokemon.catchRate = 6
          $Trainer.pokedex.owned = 1025
          battle.rng.values[256] = 0; battle.rng.values[65536] = roll
          throw_ball(battle)
          assert(battle.stored.length == (roll < threshold ? 1 : 0), "#{status}/#{charm_count}/#{boss}/#{roll}")
          assert(battle.rng.calls.count(65536) == 4 && !battle.rng.calls.include?(256))
          assert(!battle.rng.calls.include?(LegendaryCatchDifficulty::SCALE))
          assert(battle.scene.events.last[3] == false)
        end
      end
    end
  end
end
check("one failed Gen3 shake prevents capture and boundary is exclusive") do
  battle = PokeBattle_Battle.new(:REGICE)
  battle.rng.values[65536] = [19783, 19783, 19783, 19784]
  throw_ball(battle)
  assert(battle.stored.empty? && battle.scene.events.last[2] == 3)
end
check("zero Gen3 odds cannot catch even on zero random rolls") do
  battle = PokeBattle_Battle.new(:REGICE); caught_rolls(battle)
  battle.battlers[1].pokemon.catchRate = 1
  $game_variables[:LuckMoves] = 1
  throw_ball(battle)
  assert(battle.stored.empty? && battle.scene.events.last[2] == 0)
end
check("higher base rate is capped before Ultra Ball modifier; custom form unchanged") do
  seen = []
  BallHandlers::ModifyCatchRate.add(:TESTBALL, proc { |ball, rate, battle, battler| seen << rate; rate * 2 })
  battle = PokeBattle_Battle.new(:RAYQUAZA)
  battle.battlers[1].pokemon.catchRate = 45; battle.battlers[1].form = 8
  battle.battlers[1].pokemon.target_form = 8
  passed_gate(battle); throw_ball(battle, :TESTBALL)
  assert(seen == [3]); assert(battle.battlers[1].form == 8)
  battle = PokeBattle_Battle.new(:RAYQUAZA)
  battle.battlers[1].pokemon.catchRate = 1
  passed_gate(battle); throw_ball(battle, :TESTBALL)
  assert(seen == [3, 1])
end
check("unconditional custom balls remain guaranteed even on failed gate") do
  BallHandlers::IsUnconditional.add(:GUARANTEEDBALL, proc { true })
  battle = PokeBattle_Battle.new
  throw_ball(battle, :GUARANTEEDBALL)
  assert(battle.stored.length == 1)
end
check("Master on an ordinary trio legendary remains guaranteed immediately") do
  battle = PokeBattle_Battle.new(:ARTICUNO)
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.length == 1)
  assert(!battle.rng.calls.include?(1_000_000))
end
STRONG_SPECIES = [
  :LUGIA, :HOOH, :MEWTWO, :GROUDON, :KYOGRE, :RESHIRAM, :ZEKROM,
  :XERNEAS, :YVELTAL, :SOLGALEO, :LUNALA, :ZACIAN, :ZAMAZENTA,
  :CALYREX, :KORAIDON, :MIRAIDON, :GIRATINA, :RAYQUAZA, :KYUREM,
  :ZYGARDE, :NECROZMA, :ETERNATUS, :TERAPAGOS, :DIALGA, :PALKIA,
  :MEW, :CELEBI, :JIRACHI, :DEOXYS, :PHIONE, :MANAPHY, :DARKRAI,
  :SHAYMIN, :VICTINI, :KELDEO, :MELOETTA, :GENESECT, :DIANCIE,
  :HOOPA, :VOLCANION, :MAGEARNA, :MARSHADOW, :ZERAORA, :MELTAN,
  :MELMETAL, :ZARUDE, :PECHARUNT, :REGIGIGAS, :CRESSELIA
].freeze
check("Reborn requirements cover exactly strong class, Arceus and zero-wait species") do
  assert(LegendaryCatchDifficulty::STRONG_REBORN_REQUIREMENT == 20)
  expected = LegendaryCatchDifficulty::BOX_LEGENDS + LegendaryCatchDifficulty::STRONG_THIRDS + LegendaryCatchDifficulty::RAYQUAZA_EVENTS + LegendaryCatchDifficulty::DIALGA_PALKIA_TIER
  assert(STRONG_SPECIES.sort == expected.sort)
  STRONG_SPECIES.each { |species| assert(LegendaryCatchDifficulty.reborn_requirement(species) == 20, species.to_s) }
  assert(LegendaryCatchDifficulty.reborn_requirement(:ARCEUS) == 30)
  (LegendaryCatchDifficulty::ORDINARY_GROUPS + [:TAPUKOKO, :LATIAS, :OGERPON, :EEVEE]).each do |species|
    assert(LegendaryCatchDifficulty.reborn_requirement(species) == 0, species.to_s)
  end
end
STRONG_SPECIES.each do |species|
  check("#{species}: blocked at 0 and 19, guaranteed at 20 with exact item consumption") do
    battle = PokeBattle_Battle.new(species)
    mon = battle.battlers[1].pokemon
    mon.name = species == :GROUDON ? "Groudon" : species.to_s
    battle.battlers[1].form = 9
    $PokemonBag.quantities[:MASTERBALL] = 2
    $PokemonBag.quantities[:ULTRABALL] = 20
    battle.battlers[0].ability = :BALLFETCH
    [0, 19].each do |expected_count|
      (expected_count - count(battle)).times do
        ItemHandlers.triggerUseInBattle(:ULTRABALL, battle.battlers[1], battle)
      end
      battle.battlers[0].effects.delete(:BallFetch)
      events_before = battle.scene.events.length
      ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
      assert(battle.stored.empty? && count(battle) == expected_count)
      assert($PokemonBag.pbQuantity(:MASTERBALL) == 2)
      assert(battle.scene.events.length == events_before && battle.battlers[1].form == 9)
      assert(battle.battlers[0].effects[:BallFetch].nil?)
      assert(battle.messages.last.include?(mon.name) && battle.messages.last.include?("#{expected_count}/20"))
      assert(battle.messages.last.include?("Aura") && battle.messages.last.include?("erschöpft"))
      assert(!battle.messages.last.include?("Arceus"))
    end
    ItemHandlers.triggerUseInBattle(:ULTRABALL, battle.battlers[1], battle)
    assert(count(battle) == 20 && $PokemonBag.pbQuantity(:ULTRABALL) == 0)
    ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
    assert(battle.stored == [mon] && count(battle) == 20)
    assert($PokemonBag.pbQuantity(:MASTERBALL) == 1)
    assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
    assert(battle.instance_variable_get(:@legendary_catch_item_tag).nil?)
    assert(count(PokeBattle_Battle.new(species)) == 0)
  end
end
check("all ordinary groups and other species catch immediately with one Reborn consumed") do
  (LegendaryCatchDifficulty::ORDINARY_GROUPS + [:TAPUKOKO, :LATIAS, :OGERPON, :EEVEE]).each do |species|
    battle = PokeBattle_Battle.new(species)
    $PokemonBag.quantities[:MASTERBALL] = 1
    ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
    assert(battle.stored.length == 1 && count(battle) == 0, species.to_s)
    assert($PokemonBag.pbQuantity(:MASTERBALL) == 0, species.to_s)
    assert(!battle.rng.calls.include?(LegendaryCatchDifficulty::SCALE), species.to_s)
    assert(!battle.messages.any? { |message| message.include?("Aura") }, species.to_s)
  end
end
check("strong and Arceus restrictions cannot advance progress") do
  [:DIALGA, :GROUDON, :ARCEUS].each do |species|
    [:trainer, :online_trainer, :no_catching, :shield, :uncapturable, :sos, :fainted, :cancel].each do |restriction|
      battle = PokeBattle_Battle.new(species)
      case restriction
      when :trainer then battle.opponent = Object.new
      when :online_trainer
        battle.opponent = Object.new
        battle.define_singleton_method(:isOnline?) { true }
      when :no_catching then $game_switches[:No_Catching] = true
      when :shield then battle.battlers[1].isbossmon = true; battle.battlers[1].shieldCount = 1
      when :uncapturable then battle.battlers[1].isbossmon = true; battle.battlers[1].capturable = false
      when :sos then battle.battlers[1].issossmon = true
      when :fainted then battle.battlers[1].hp = 0
      when :cancel then battle.battlers[3].hp = 100; battle.scene.choices = [-1]
      end
      throw_ball(battle)
      assert(count(battle) == 0 && battle.stored.empty?, "#{species}/#{restriction}")
      assert(!battle.messages.any? { |message| message.include?("Aura") })
      $game_switches[:No_Catching] = false
    end
  end
end
check("strong counters use Pokemon identity even for equal-valued replacements") do
  battle = PokeBattle_Battle.new(:DIALGA)
  throw_ball(battle)
  old_mon = battle.battlers[1].pokemon
  replacement = old_mon.dup
  assert(replacement == old_mon && !replacement.equal?(old_mon))
  battle.battlers[1].pokemon = replacement
  assert(count(battle) == 0)
  assert(LegendaryCatchDifficulty.other_ball_throws(battle, old_mon) == 1)
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty? && battle.messages.last.include?("0/20"))
  battle.battlers[1].pokemon = old_mon
  assert(count(battle) == 1)
end
check("strong progress belongs to actual doubles target and does not gate ordinary trio") do
  battle = PokeBattle_Battle.new(:ARTICUNO)
  target = battle.battlers[3]; target.pokemon.species = :DIALGA
  target.pokemon.name = "Dialga"; target.hp = 100
  target.form = 8
  19.times do
    battle.scene.choices = [3, 3]
    throw_ball(battle)
  end
  assert(count(battle) == 0 && count(battle, 3) == 19)
  battle.scene.choices = [3, 3]
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty? && count(battle, 3) == 19 && target.form == 8)
  assert(battle.messages.last.include?("Dialga") && battle.messages.last.include?("19/20"))
  battle.scene.choices = [3, 3]
  throw_ball(battle)
  battle.scene.choices = [3, 3]
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored == [target.pokemon] && count(battle, 3) == 20 && count(battle) == 0)
  battle.scene.choices = [1, 1]
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.length == 2 && battle.stored.last.species == :ARTICUNO)
end
check("last valid doubles selection controls gate despite different SWM preselection") do
  battle = PokeBattle_Battle.new(:ARTICUNO)
  target = battle.battlers[3]; target.pokemon.species = :GROUDON
  target.pokemon.name = "Groudon"; target.hp = 100
  battle.scene.choices = LOAD_ORDER == "original" ? [3] : [1, 3]
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty? && battle.messages.last.include?("Groudon"))
  assert(battle.messages.last.include?("0/20"))
end
check("Reborn ball below 30 is blocked, not counted, and restores old form") do
  battle = PokeBattle_Battle.new
  battle.battlers[1].form = 9
  $DEBUG = true; Input.ctrl = true
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty?); assert(count(battle) == 0)
  assert(battle.scene.events.empty?); assert(battle.battlers[1].form == 9)
  assert(battle.messages.any? { |message| message.include?("0/30") && message.include?("Aura") })
end
check("Reborn unlocks exactly after 30 other valid throws in same battle") do
  battle = PokeBattle_Battle.new
  29.times { throw_ball(battle) }
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty?); assert(count(battle) == 29)
  throw_ball(battle)
  assert(count(battle) == 30)
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.length == 1); assert(count(battle) == 30)
  assert(count(PokeBattle_Battle.new) == 0)
end
check("Arceus keeps item-safe 30 boundary with actual valid item throws") do
  battle = PokeBattle_Battle.new
  battle.battlers[1].pokemon.name = "Arceus"
  $PokemonBag.quantities[:MASTERBALL] = 1
  $PokemonBag.quantities[:ULTRABALL] = 30
  29.times { ItemHandlers.triggerUseInBattle(:ULTRABALL, battle.battlers[1], battle) }
  ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
  assert(battle.stored.empty? && count(battle) == 29 && $PokemonBag.pbQuantity(:MASTERBALL) == 1)
  assert(battle.messages.last.include?("Arceus") && battle.messages.last.include?("29/30"))
  assert(battle.messages.last.include?("erschöpft"))
  ItemHandlers.triggerUseInBattle(:ULTRABALL, battle.battlers[1], battle)
  ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
  assert(battle.stored.length == 1 && count(battle) == 30)
  assert($PokemonBag.pbQuantity(:MASTERBALL) == 0 && $PokemonBag.pbQuantity(:ULTRABALL) == 0)
end
check("restrictions and cancelled target selection do not count") do
  [:trainer, :no_catching, :shield, :sos, :fainted, :cancel].each do |restriction|
    battle = PokeBattle_Battle.new
    case restriction
    when :trainer then battle.opponent = Object.new
    when :no_catching then $game_switches[:No_Catching] = true
    when :shield then battle.battlers[1].isbossmon = true; battle.battlers[1].shieldCount = 1
    when :sos then battle.battlers[1].issossmon = true
    when :fainted then battle.battlers[1].hp = 0
    when :cancel then battle.battlers[3].hp = 100; battle.scene.choices = [-1]
    end
    throw_ball(battle)
    assert(count(battle) == 0, restriction.to_s)
    assert(battle.stored.empty?, restriction.to_s)
    $game_switches[:No_Catching] = false
  end
end
check("actual selected doubles target controls factor and counter") do
  battle = PokeBattle_Battle.new(:EEVEE)
  target = battle.battlers[3]; target.pokemon.species = :ARCEUS; target.hp = 100
  battle.scene.choices = [3, 3]
  throw_ball(battle)
  assert(count(battle) == 0); assert(count(battle, 3) == 1)
  assert(battle.stored.empty?)
  battle.scene.choices = [3, 3]
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty?); assert(battle.messages.last.include?("1/30"))
end
check("per-Pokemon counters do not share progress") do
  battle = PokeBattle_Battle.new
  throw_ball(battle)
  old_mon = battle.battlers[1].pokemon
  battle.battlers[1].pokemon = FakeMon.new(:ARCEUS, 3, 0, 0, "Arceus")
  assert(count(battle) == 0)
  assert(LegendaryCatchDifficulty.other_ball_throws(battle, old_mon) == 1)
end
check("debug CTRL normal capture still overrides the difficulty gate") do
  battle = PokeBattle_Battle.new; $DEBUG = true; Input.ctrl = true
  throw_ball(battle)
  assert(battle.stored.length == 1)
end
check("reusable Premier handler runs once and is preserved") do
  battle = PokeBattle_Battle.new; $PokemonBag.quantities[:PREMIERBALL] = 100
  throw_ball(battle, :PREMIERBALL)
  assert($PokemonBag.pbQuantity(:PREMIERBALL) == 101)
  assert(count(battle) == 1)
end unless LOAD_ORDER == "original"
check("ordinary RNG outside throw and unrelated limit stay unchanged") do
  battle = PokeBattle_Battle.new
  throw_ball(battle)
  battle.rng.values[65536] = 1234
  battle.rng.values[16] = 7
  assert(battle.pbRandom(65536) == 1234)
  assert(battle.pbRandom(16) == 7)
end
check("same-limit RNG outside guarded catch method stays unchanged during active throw") do
  battle = PokeBattle_Battle.new
  battle.rng.values[65536] = 1234; battle.rng.values[256] = 17
  battle.scene.on_throw = proc do
    assert(battle.instance_variable_get(:@legendary_catch_context).key?(:gen3_threshold))
    assert(battle.pbRandom(65536) == 1234)
    assert(battle.pbRandom(256) == 17)
  end
  throw_ball(battle)
end
check("nested throw restores outer scope and consumes both counters exactly once") do
  battle = PokeBattle_Battle.new
  nested = false
  battle.scene.on_throw = proc do
    unless nested
      nested = true
      outer = battle.instance_variable_get(:@legendary_catch_context)
      throw_ball(battle)
      assert(battle.instance_variable_get(:@legendary_catch_context).equal?(outer))
    end
  end
  throw_ball(battle)
  assert(count(battle) == 2)
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
end
check("handler exception clears capture scope without swallowing failure") do
  BallHandlers::ModifyCatchRate.add(:BROKENBALL, proc { raise "handler failed" })
  battle = PokeBattle_Battle.new
  begin
    throw_ball(battle, :BROKENBALL)
    raise "expected exception"
  rescue RuntimeError => error
    assert(error.message == "handler failed")
  end
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
  assert(count(battle) == 0)
end
check("Safari custom RNG receives same scoped capture protection") do
  battle = PokeBattle_SafariZone.new
  caught_rolls(battle)
  throw_ball(battle)
  assert(battle.stored.empty?)
  assert(count(battle) == 1)
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
end
check("Safari independent class also enforces strong legendary 20 boundary") do
  battle = PokeBattle_SafariZone.new(:DIALGA)
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.empty? && count(battle) == 0 && battle.messages.last.include?("0/20"))
  20.times { throw_ball(battle) }
  throw_ball(battle, :MASTERBALL)
  assert(battle.stored.length == 1 && count(battle) == 20)
end
check("rejected Reborn stays in bag through original item handler, without Ball Fetch duplicate") do
  battle = PokeBattle_Battle.new
  $PokemonBag.quantities[:MASTERBALL] = 999
  battle.battlers[0].ability = :BALLFETCH
  2.times { ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle) }
  assert($PokemonBag.pbQuantity(:MASTERBALL) == 999)
  assert(battle.battlers[0].effects[:BallFetch].nil?)
  assert(count(battle) == 0 && battle.stored.empty?)
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
  assert(battle.instance_variable_get(:@legendary_catch_item_tag).nil?)
  # No stale exemption: another species consumes the ball normally.
  battle.battlers[1].pokemon.species = :REGICE
  ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
  assert($PokemonBag.pbQuantity(:MASTERBALL) == 998 && battle.stored.length == 1)
end
check("unlocked Reborn is consumed normally through original item handler") do
  battle = PokeBattle_Battle.new
  $PokemonBag.quantities[:MASTERBALL] = 1
  30.times { LegendaryCatchDifficulty.count_other_ball(battle, battle.battlers[1].pokemon) }
  ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
  assert($PokemonBag.pbQuantity(:MASTERBALL) == 0 && battle.stored.length == 1)
  assert(battle.instance_variable_get(:@legendary_catch_item_tag).nil?)
end
check("item dispatch exception cleans scope without changing inventory") do
  battle = PokeBattle_Battle.new
  $PokemonBag.quantities[:MASTERBALL] = 1
  battle.define_singleton_method(:pbDisplay) { |message| raise "display failure" }
  begin
    ItemHandlers.triggerUseInBattle(:MASTERBALL, battle.battlers[1], battle)
    raise "expected failure"
  rescue RuntimeError => error
    assert(error.message == "display failure")
  end
  assert($PokemonBag.pbQuantity(:MASTERBALL) == 1)
  assert(battle.instance_variable_get(:@legendary_catch_item_tag).nil?)
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
end
check("incompatible original routine fails clearly before throwing") do
  LegendaryCatchDifficulty.instance_variable_set(:@validated, false)
  guarded_name = LOAD_ORDER == "original" ? :pbThrowPokeBall : :swm_reusablePremierBalls_oldpbThrowPokeBall
  PokeBattle_BattleCommon.alias_method :legend_test_real_original, guarded_name
  PokeBattle_BattleCommon.define_method(guarded_name) { |*args| raise "must not run" }
  battle = PokeBattle_Battle.new
  begin
    throw_ball(battle)
    raise "expected incompatibility"
  rescue LegendaryCatchDifficulty::Incompatible => error
    assert(error.message.include?("Fangroutine"))
  ensure
    PokeBattle_BattleCommon.alias_method guarded_name, :legend_test_real_original
    LegendaryCatchDifficulty.instance_variable_set(:@validated, false)
    LegendaryCatchDifficulty.validate!
  end
  assert(battle.instance_variable_get(:@legendary_catch_context).nil?)
  assert(battle.messages.empty?)
end
check("complete published balance fixture covers every legendary and every requested event") do
  rows = JSON.parse(File.read(File.join(ROOT, "custom-mods/legendary-catch/fangstufen.json")))
  expected = (PBStuff::LEGENDARYLIST + LegendaryCatchDifficulty::ADDITIONAL_LEGENDS + LegendaryCatchDifficulty::MYTHICAL_EVENTS).uniq.sort - [:ABYSSEON, :DIVINEON].sort
  assert(rows.map { |row| row["species"].to_sym }.sort == expected)
  rows.each do |row|
    species = row["species"].to_sym
    assert(LegendaryCatchDifficulty.factor_units(species) == row["factor_units"], species.to_s)
    assert(LegendaryCatchDifficulty.reborn_requirement(species) == row["reborn_other_throws"], species.to_s)
    assert(LegendaryCatchDifficulty.legendary?(species), species.to_s)
    assert(LegendaryCatchDifficulty.base_rate_cap(species) == 3, species.to_s)
  end
end
check("all event species get their tier without leaking changes to ordinary Pokemon or Ultra Beasts") do
  assert(LegendaryCatchDifficulty.factor(:KELDEO) == 0.3)
  assert(LegendaryCatchDifficulty.factor(:PHIONE) == 0.3)
  [:PIKACHU, :RATTATA, :NIHILEGO, :NAGANADEL, :WALKINGWAKE, :ABYSSEON, :DIVINEON].each do |species|
    assert(LegendaryCatchDifficulty.factor(species) == 1.0, species.to_s)
    assert(LegendaryCatchDifficulty.base_rate_cap(species).nil?, species.to_s)
    assert(LegendaryCatchDifficulty.reborn_requirement(species) == 0, species.to_s)
  end
end
puts "LEGENDARY_CATCH_UNIT_OK #{$tests} checks"
