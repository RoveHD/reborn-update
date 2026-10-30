# encoding: UTF-8
# Separate Fang-Mod: keine Pokemon-/PBS-/Originaldateien werden veraendert.
# Nach Updates beibehalten und den eingebauten Kompatibilitaetstest beachten.
# Legendaere nutzen die Gen-3-Fangformel ohne Fangamulette, Bossbonus oder
# kritische Faenge. Die Stufen multiplizieren diese fertige Fangchance.
require "digest"

module LegendaryCatchDifficulty
  class Incompatible < StandardError; end
  # ScriptLoader can evaluate the scripts again in the same Ruby VM (F12).
  # Recheck the original after that reload, even if a prior throw validated it.
  @validated = false

  # Upstream lists these custom Eevee evolutions as legendary; do not apply
  # legendary rarity, shake rules or Glitter Ball penalties to them.
  NONLEGENDARY_EVOLUTIONS = [:ABYSSEON, :DIVINEON].freeze
  SCALE = 1_000_000
  # Scale each species' starting factor to the ordinary catch chance over
  # 200 previous failed non-Master Ball throws.
  THROW_PROGRESSION_THROWS = 200
  ARCEUS_REBORN_REQUIREMENT = 30
  STRONG_REBORN_REQUIREMENT = 20
  ORDINARY_GROUPS = [
    :ARTICUNO, :ZAPDOS, :MOLTRES, :RAIKOU, :ENTEI, :SUICUNE,
    :REGIROCK, :REGICE, :REGISTEEL, :REGIELEKI, :REGIDRAGO,
    :UXIE, :MESPRIT, :AZELF, :COBALION, :TERRAKION, :VIRIZION,
    :TORNADUS, :THUNDURUS, :LANDORUS, :ENAMORUS,
    :TAPULELE, :TAPUBULU, :TAPUFINI,
    :WOCHIEN, :CHIENPAO, :TINGLU, :CHIYU, :OKIDOGI, :MUNKIDORI, :FEZANDIPITI
  ].freeze
  BOX_LEGENDS = [
    :LUGIA, :HOOH, :MEWTWO, :GROUDON, :KYOGRE, :RESHIRAM, :ZEKROM,
    :XERNEAS, :YVELTAL, :SOLGALEO, :LUNALA, :ZACIAN, :ZAMAZENTA,
    :CALYREX, :KORAIDON, :MIRAIDON
  ].freeze
  STRONG_THIRDS = [:GIRATINA, :RAYQUAZA, :KYUREM, :ZYGARDE,
                   :NECROZMA, :ETERNATUS, :TERAPAGOS].freeze
  # Official Mythical/Event Pokemon, including Phione, Meltan and Melmetal.
  # Arceus keeps its stricter tier; Darkrai has the Dialga/Palkia tier.
  MYTHICAL_EVENTS = [
    :MEW, :CELEBI, :JIRACHI, :DEOXYS, :PHIONE, :MANAPHY, :DARKRAI,
    :SHAYMIN, :ARCEUS, :VICTINI, :KELDEO, :MELOETTA, :GENESECT,
    :DIANCIE, :HOOPA, :VOLCANION, :MAGEARNA, :MARSHADOW, :ZERAORA,
    :MELTAN, :MELMETAL, :ZARUDE, :PECHARUNT
  ].freeze
  RAYQUAZA_EVENTS = (MYTHICAL_EVENTS - [:ARCEUS, :DARKRAI] + [:REGIGIGAS]).freeze
  DIALGA_PALKIA_TIER = [:DIALGA, :PALKIA, :DARKRAI, :CRESSELIA].freeze
  LOWERED_GROUPS = [:TAPUKOKO].freeze
  # The current AllGen list omits several DLC legendaries/mythicals.
  ADDITIONAL_LEGENDS = [:WOCHIEN, :CHIENPAO, :TINGLU, :CHIYU, :OKIDOGI,
                       :MUNKIDORI, :FEZANDIPITI, :OGERPON, :TERAPAGOS,
                       :PECHARUNT].freeze
  # Guard the actual (possibly SWM-aliased) original method, not a wrapper.
  # Changes require reviewing the catch protocol and updating this checksum.
  THROW_SHA256 = "8d719a95f403f5431c425a30e13df606b235fd81ae314982c0f44485fa6e90cd"

  def self.factor_units(species)
    return SCALE if NONLEGENDARY_EVOLUTIONS.include?(species)
    return 150_000 if species == :ARCEUS
    return 300_000 if STRONG_THIRDS.include?(species) || RAYQUAZA_EVENTS.include?(species)
    return 450_000 if DIALGA_PALKIA_TIER.include?(species)
    return 600_000 if BOX_LEGENDS.include?(species)
    return SCALE if ORDINARY_GROUPS.include?(species)
    return 800_000 if LOWERED_GROUPS.include?(species) || ADDITIONAL_LEGENDS.include?(species) ||
                     PBStuff::LEGENDARYLIST.include?(species)
    SCALE
  end

  def self.factor(species)
    factor_units(species).to_f / SCALE
  end

  def self.factor_units_after_throws(species, previous_throws)
    base = factor_units(species)
    return base if base >= SCALE
    throws = [[previous_throws, 0].max, THROW_PROGRESSION_THROWS].min
    base + ((SCALE - base) * throws / THROW_PROGRESSION_THROWS)
  end

  def self.reborn_requirement(species)
    return ARCEUS_REBORN_REQUIREMENT if species == :ARCEUS
    return STRONG_REBORN_REQUIREMENT if BOX_LEGENDS.include?(species) ||
      STRONG_THIRDS.include?(species) || RAYQUAZA_EVENTS.include?(species) ||
      DIALGA_PALKIA_TIER.include?(species)
    0
  end

  def self.base_rate_cap(species)
    3 if legendary?(species)
  end

  def self.legendary?(species)
    return false if NONLEGENDARY_EVOLUTIONS.include?(species)
    MYTHICAL_EVENTS.include?(species) || DIALGA_PALKIA_TIER.include?(species) ||
      ORDINARY_GROUPS.include?(species) || BOX_LEGENDS.include?(species) ||
      STRONG_THIRDS.include?(species) || ADDITIONAL_LEGENDS.include?(species) ||
      PBStuff::LEGENDARYLIST.include?(species)
  end

  # Battle moves belong to a Pokemon object, not to a Battler. Match that exact
  # object on the opposing side so players, allies, trainers and online battles
  # keep normal PP. Use its original species even after Transform.
  def self.infinite_wild_pp?(battle, pokemon)
    return false unless battle && pokemon && battle.opponent.nil?
    return false if battle.isOnline? || battle.decision != 0
    return false unless legendary?(pokemon.species)
    Array(battle.battlers).any? do |battler|
      battler && battle.pbIsOpposing?(battler.index) &&
        battler.pokemon.equal?(pokemon)
    end
  end

  module WildMovePPHook
    def pp
      value = super
      if value && value >= 0 && totalpp && totalpp > 0 &&
         LegendaryCatchDifficulty.infinite_wild_pp?(@battle, @user)
        return totalpp
      end
      value
    end

    # Also covers direct PP drains (Eerie Spell), not just ordinary move use.
    def pp=(value)
      if value && value >= 0 && totalpp && totalpp > 0 &&
         LegendaryCatchDifficulty.infinite_wild_pp?(@battle, @user)
        value = totalpp
      end
      super(value)
    end
  end

  module WildBattlerPPHook
    def pbSetPP(move, pp)
      if pp && pp >= 0 && move.totalpp && move.totalpp > 0 &&
         LegendaryCatchDifficulty.infinite_wild_pp?(@battle, @pokemon)
        pp = move.totalpp
      end
      # Keep the underlying Pokemon's move in sync for capture/Transform.
      super(move, pp)
    end
  end

  def self.gen3_shake_threshold(ball_rate, battler)
    # Emerald's integer divisions and nested integer square roots matter at
    # low catch rates. Ball handlers have already applied the ball modifier.
    maximum = battler.totalhp
    odds = (ball_rate.to_i * (3 * maximum - 2 * battler.hp)) / (3 * maximum)
    if battler.status == :SLEEP || battler.status == :FROZEN
      odds *= 2
    elsif !battler.status.nil?
      odds = odds * 15 / 10
    end
    return 0 if odds <= 0
    return 65536 if odds > 254
    1048560 / Integer.sqrt(Integer.sqrt(16711680 / odds))
  end

  def self.other_ball_throws(battle, pokemon)
    counts = battle.instance_variable_get(:@legendary_catch_ball_counts)
    counts ? counts.fetch(pokemon, 0) : 0
  end

  def self.count_other_ball(battle, pokemon)
    counts = battle.instance_variable_get(:@legendary_catch_ball_counts)
    unless counts
      counts = {}.compare_by_identity
      battle.instance_variable_set(:@legendary_catch_ball_counts, counts)
    end
    counts[pokemon] = counts.fetch(pokemon, 0) + 1
  end

  def self.validate!
    return if @validated
    unless defined?(PBStuff::LEGENDARYLIST) &&
           BallHandlers.respond_to?(:modifyCatchRate) &&
           BallHandlers.respond_to?(:isUnconditional?) &&
           PokeBattle_Battle.method_defined?(:pbRandom)
      raise Incompatible, "Fang-Mod: Die benoetigte Kampf-API fehlt. Mod nach dem Update pruefen."
    end
    name = if PokeBattle_BattleCommon.method_defined?(:swm_reusablePremierBalls_oldpbThrowPokeBall)
             :swm_reusablePremierBalls_oldpbThrowPokeBall
           else
             :pbThrowPokeBall
           end
    method = PokeBattle_BattleCommon.instance_method(name)
    # Walk past our prepend if validating explicitly after installation.
    method = method.super_method while method && method.owner == BattleHook
    file, line = method.source_location if method
    unless file && File.file?(file)
      raise Incompatible, "Fang-Mod: Original-Fangroutine nicht lesbar; Mod nach dem Update pruefen."
    end
    source = File.binread(file).force_encoding("UTF-8").gsub("\r\n", "\n")
    tail = source.lines.drop(line - 1).join
    boundary = tail.index("  def addFusion(")
    body = boundary ? tail[0...boundary].strip : ""
    unless Digest::SHA256.hexdigest(body) == THROW_SHA256
      raise Incompatible,
        "Fang-Mod: Die Original-Fangroutine hat sich geaendert. " \
        "Mod deaktivieren und fuer dieses Update pruefen lassen; es wurden keine Fangwerte geaendert."
    end
    @throw_source_file = File.expand_path(file).tr("\\", "/").downcase
    @validated = true
  end

  def self.capture_caller?(location)
    location && location.base_label == "pbThrowPokeBall" &&
      File.expand_path(location.path).tr("\\", "/").downcase == @throw_source_file
  end

  module BattleHook
    def pbThrowPokeBall(*args, **kwargs, &block)
      previous = @legendary_catch_context
      LegendaryCatchDifficulty.validate!
      context = { :tag => Object.new }
      if args[1] == :MASTERBALL
        context[:forms] = {}.compare_by_identity
        battlers.compact.each { |battler| context[:forms][battler] = battler.form }
      end
      @legendary_catch_context = context
      result = catch(context[:tag]) { super }
      if context[:blocked]
        target = context[:battler]
        target.form = context[:forms].fetch(target)
        pbDisplay("Die Aura von #{target.pokemon.name} ist zu mächtig! Es muss erst erschöpft werden. " \
                  "Andere Bälle in diesem Kampf: #{context[:count]}/#{context[:requirement]}.")
        # The original item handler deletes the ball AFTER pbThrowPokeBall.
        # Leave that handler before its deletion, without refunding/duplicating
        # an item or leaving a pending exemption on the bag.
        item_tag = instance_variable_get(:@legendary_catch_item_tag)
        throw item_tag, nil if item_tag
      end
      result
    ensure
      @legendary_catch_context = previous
    end
  end

  module ItemHook
    def triggerUseInBattle(item, battler, battle)
      return super unless item == :MASTERBALL
      previous = battle.instance_variable_get(:@legendary_catch_item_tag)
      tag = Object.new
      battle.instance_variable_set(:@legendary_catch_item_tag, tag)
      begin
        catch(tag) { super }
      ensure
        battle.instance_variable_set(:@legendary_catch_item_tag, previous)
      end
    end
  end

  module BallHook
    def modifyCatchRate(ball, catch_rate, battle, battler)
      context = battle.instance_variable_get(:@legendary_catch_context)
      return super unless context
      # The original routine calls this only after target selection and all
      # trainer/online/boss/no-catching restrictions. Never predict the target.
      raise Incompatible, "Fang-Mod: Unerwarteter zweiter Fangraten-Aufruf." if context[:handled]
      context[:handled] = true
      context[:battler] = battler
      species = battler.pokemon.species
      requirement = LegendaryCatchDifficulty.reborn_requirement(species)
      cap = LegendaryCatchDifficulty.base_rate_cap(species)
      base_units = LegendaryCatchDifficulty.factor_units(species)
      previous_throws = if cap && ball != :MASTERBALL
                          LegendaryCatchDifficulty.other_ball_throws(battle, battler.pokemon)
                        else
                          0
                        end
      if ball == :MASTERBALL && requirement > 0
        context[:requirement] = requirement
        context[:count] = LegendaryCatchDifficulty.other_ball_throws(battle, battler.pokemon)
        if context[:count] < requirement
          context[:blocked] = true
          throw context[:tag], nil
        end
      end
      catch_rate = [catch_rate, cap].min if cap
      value = super(ball, catch_rate, battle, battler)
      if cap && ball != :MASTERBALL && (requirement > 0 || base_units < SCALE)
        LegendaryCatchDifficulty.count_other_ball(battle, battler.pokemon)
      end
      units = LegendaryCatchDifficulty.factor_units_after_throws(species, previous_throws)
      return value unless cap && ball != :MASTERBALL
      context[:gen3_threshold] = LegendaryCatchDifficulty.gen3_shake_threshold(value, battler)
      # One independent gate multiplies the four-shake Gen-3 probability by
      # precisely units/SCALE; ordinary legends need no additional RNG draw.
      context[:deny_shakes] = units < SCALE && battle.pbRandom(SCALE) >= units
      # The guarded original's additive charm+boss bonus is at most seven.
      # This proxy keeps x<=17.5, so it always reaches its four shake checks.
      # RandomHook supplies the Gen-3 results instead of its modern formula.
      0.0
    end

    def isUnconditional?(ball, battle, battler)
      result = super
      context = battle.instance_variable_get(:@legendary_catch_context)
      if result && context && context[:battler].equal?(battler)
        context.delete(:gen3_threshold)
        context[:deny_shakes] = false
      end
      result
    end
  end

  module RandomHook
    def pbRandom(limit)
      context = @legendary_catch_context
      if context && context.key?(:gen3_threshold) &&
         LegendaryCatchDifficulty.capture_caller?(caller_locations(1, 1).first)
        # Gen 3 has no critical capture. Four independent uniform draws are
        # compared to its exact threshold, then mapped onto the original's
        # guaranteed positive, sub-65535 proxy threshold. Other RNG is intact.
        return 255 if limit == 256
        if limit == 65536
          return 65535 if context[:deny_shakes]
          return super < context[:gen3_threshold] ? 0 : 65535
        end
      end
      super
    end
  end
end

# SWM aliases BattleCommon#pbThrowPokeBall while loading. Prepending here would
# let that alias capture our wrapper on a reload, causing wrapper -> SWM ->
# wrapper -> missing super. Keep the wrapper on the consuming battle classes,
# outside the module SWM aliases, so either load order reaches the real method.
unless PokeBattle_Battle.ancestors.include?(LegendaryCatchDifficulty::BattleHook)
  PokeBattle_Battle.prepend(LegendaryCatchDifficulty::BattleHook)
end
BallHandlers.singleton_class.prepend(LegendaryCatchDifficulty::BallHook)
ItemHandlers.singleton_class.prepend(LegendaryCatchDifficulty::ItemHook)
PokeBattle_Battle.prepend(LegendaryCatchDifficulty::RandomHook)
if defined?(PokeBattle_Move) && !PokeBattle_Move.ancestors.include?(LegendaryCatchDifficulty::WildMovePPHook)
  PokeBattle_Move.prepend(LegendaryCatchDifficulty::WildMovePPHook)
end
if defined?(PokeBattle_Battler) && !PokeBattle_Battler.ancestors.include?(LegendaryCatchDifficulty::WildBattlerPPHook)
  PokeBattle_Battler.prepend(LegendaryCatchDifficulty::WildBattlerPPHook)
end
if defined?(PokeBattle_SafariZone)
  unless PokeBattle_SafariZone.ancestors.include?(LegendaryCatchDifficulty::BattleHook)
    PokeBattle_SafariZone.prepend(LegendaryCatchDifficulty::BattleHook)
  end
  PokeBattle_SafariZone.prepend(LegendaryCatchDifficulty::RandomHook)
end

# encoding: UTF-8
# Additive balance for the existing LegendaryCatchDifficulty mod.
# Only already-shiny legendary targets get x4; ordinary shinies keep x8.
# OnCatch (turning the caught Pokemon shiny) is deliberately unchanged.
BallHandlers::ModifyCatchRate.add(:GLITTERBALL, proc { |ball, catch_rate, battle, battler|
  catch_rate = (catch_rate * 0.1).floor if pbIsUltraBeast?(battler)
  if battler.pokemon.isShiny?
    bonus = LegendaryCatchDifficulty.legendary?(battler.pokemon.species) ? 4 : 8
    catch_rate = (catch_rate * bonus).floor
  end
  next catch_rate
})
