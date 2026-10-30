# encoding: UTF-8
# Run the existing real Battle/Ball/Item/SWM regression with the release mod.
ARGV[0] ||= "swm-first"
ARGV[1] ||= File.expand_path("../custom-mods/legendary-catch/patch/Mods/RE_LegendaryCatchDifficulty.rb", __dir__)
load File.join(__dir__, "catch_event_balance.rb")

class FakeMon
  attr_accessor :shiny
  def isShiny?; !!shiny; end
  def makeShiny; self.shiny = true; end
end
class FakeBattler
  attr_accessor :ultra
end
def pbIsUltraBeast?(battler); !!battler.ultra; end

# Compare the real original handler with the patched one, including F12 reload.
old_verbose = $VERBOSE
begin
  $VERBOSE = nil
  load File.join(GAME, "Scripts/Balls.rb")
ensure
  $VERBOSE = old_verbose
end
original = BallHandlers::ModifyCatchRate[:GLITTERBALL]
glitter_checks = 0
2.times do
  old_verbose = $VERBOSE
  begin
    $VERBOSE = nil
    load CATCH_MOD
  ensure
    $VERBOSE = old_verbose
  end
  [:REGIROCK, :MEW, :MEWTWO, :DIALGA, :GIRATINA, :ARCEUS, :TERAPAGOS, :PIKACHU, :RATTATA, :ABYSSEON, :DIVINEON].each do |species|
    [true, false].each do |shiny|
      [true, false].each do |ultra|
        [3, 45, 255].each do |rate|
          target = FakeBattler.new(species, 1)
          target.pokemon.shiny = shiny
          target.ultra = ultra
          before = original.call(:GLITTERBALL, rate, nil, target)
          expected = shiny && LegendaryCatchDifficulty.legendary?(species) ? before / 2 : before
          actual = BallHandlers::ModifyCatchRate.trigger(:GLITTERBALL, rate, nil, target)
          raise "Wrong Glitzerball rate: #{species}/#{shiny}/#{ultra}/#{rate}" unless actual == expected
          glitter_checks += 1
        end
      end
    end
  end
end
mon = FakeMon.new(:PIKACHU)
BallHandlers.onCatch(:GLITTERBALL, nil, mon)
raise "OnCatch changed" unless mon.isShiny?
raise "Hyperball changed" unless BallHandlers::ModifyCatchRate.trigger(:ULTRABALL, 3, nil, FakeBattler.new(:ARCEUS, 1)) == 6

check("actual shiny legendary throw uses the x4 Gen3 threshold") do
  battle = PokeBattle_Battle.new
  target = battle.battlers[1]
  target.pokemon.shiny = true
  target.pokemon.species = :REGIROCK
  # At full HP: base3 * bonus4 /3 = 4, exact Gen3 integer threshold.
  expected = LegendaryCatchDifficulty.gen3_shake_threshold(12, target)
  observed = nil
  battle.scene.on_throw = proc { observed = battle.instance_variable_get(:@legendary_catch_context)[:gen3_threshold] }
  throw_ball(battle, :GLITTERBALL)
  assert(observed == expected)
end
puts "GLITTER_BALL_OK #{glitter_checks + 3} checks; TOTAL #{$tests + glitter_checks + 2}"
