# Results of battle:
#    0 - Undecided or aborted
#    1 - Player won
#    2 - Player lost
#    3 - Player or wild Pokémon ran from battle, or player forfeited the match
#    4 - Wild Pokémon was caught
#    5 - Draw
################################################################################
class PokeBattle_Move
  def battle=(value)
    @battle = value
  end
end

################################################################################
# Main battle class.
################################################################################
$OnlineBattle = nil
class PokeBattle_OnlineBattle < PokeBattle_Battle
  attr_accessor(:tiebreak) # for speed ties

  include PokeBattle_BattleCommon

  ONLINEEXPGAIN = false
  ONLINEGAINMONEY = false
  MAXPARTYSIZE = 6

  class BattleAbortedException < Exception; end

  def isOnline?
    return true
  end

  ################################################################################
  # Initialise battle class.
  ################################################################################
  def initialize(scene, p1, p2, player, opponent, tiebreak)
    @scene = scene
    @turncount = 0
    super(scene, p1, p2, player, opponent)
    @battleRandom = Random.new(receive_seed.to_i)
    @field = PokeBattle_FieldOnline.new($feonline)
    @tiebreak = tiebreak # for speed ties
  end

  ################################################################################
  # Tie break.
  ################################################################################
  def pbTieBreak
    return @tiebreak
  end

  ################################################################################
  # Switching Pokémon.
  ################################################################################
  def pbSwitch(favorDraws = false)
    if !favorDraws
      return if @decision > 0

      pbJudge()
      return if @decision > 0
    else
      return if @decision == 5

      pbJudge()
      return if @decision > 0
    end
    switched = []
    oneByYou = -1
    oneByOpponent = -1
    for index in 0...4
      next if !@doublebattle && pbIsDoubleBattler?(index)
      next if @battlers[index] && !@battlers[index].isFainted?
      next if !pbCanChooseNonActive?(index)

      oneByYou = index if pbOwnedByPlayer?(index)
      oneByOpponent = index if !pbOwnedByPlayer?(index)
    end
    if oneByYou != -1 && oneByOpponent != -1 # both trainers need to send in a mon
      meIsFaster = false
      if @battlers[oneByYou].speed > @battlers[oneByOpponent].speed
        meIsFaster = true
      elsif @battlers[oneByYou].speed == @battlers[oneByOpponent].speed
        if pbGetPriority(@battlers[oneByYou]) < pbGetPriority(@battlers[oneByOpponent])
          meIsFaster = true
        end
      end
      indexAry = [0, 1, 2, 3] if meIsFaster
      indexAry = [1, 0, 3, 2] if !meIsFaster
    else
      indexAry = [0, 1, 2, 3]
    end
    for index in indexAry
      next if !@doublebattle && pbIsDoubleBattler?(index)
      next if @battlers[index] && @battlers[index].hp > 0
      next if !pbCanChooseNonActive?(index)

      if !pbOwnedByPlayer?(index)
        if @opponent
          newenemy = waitnewenemy
          pbRecallAndReplace(index, newenemy)
          switched.push(index)
        end
      elsif @opponent
        newpoke = pbSwitchInBetween(index, true, false)
        pbRecallAndReplace(index, newpoke)
        switched.push(index)
      end
    end
    if switched.length > 0
      priority = pbPriority
      for i in priority
        i.pbAbilitiesOnSwitchIn(true) if switched.include?(i.index)
      end
    end
  end

  def pbSendOut(index, pokemon)
    $Trainer.pokedex.setSeen(pokemon)
    @peer.pbOnEnteringBattle(self, pokemon)
    if pbIsOpposing?(index)
      @scene.pbTrainerSendOut(index, pokemon)
    else
      @scene.pbSendOut(index, pokemon)
    end
    @scene.pbResetMoveIndex(index)
  end

  def pbSwitchInBetween(index, lax, cancancel, activatedata = false)
    if !pbOwnedByPlayer?(index)
      newenemy = waitnewenemy
      return newenemy
    else
      newpoke = pbSwitchPlayer(index, lax, cancancel)
      $network.send("<BAT new=#{newpoke}>")
      return newpoke
    end
  end

  ################################################################################
  # Fleeing from battle.
  ################################################################################
  def pbRun(idxPokemon, duringBattle = false)
    if pbIsOpposing?(idxPokemon)
      if @opponent
        return 0
      else
        @choices[i] = :run
        return -1
      end
    end
    if @opponent
      if pbDisplayConfirm(_INTL("Would you like to forfeit the match and quit now?"))
        pbDisplay(_INTL("{1} forfeited the match!", self.pbPlayer.name))
        $network.send("<BAT dead>")
        @decision = 3
        return 1
      end
      return 0
    end
  end

  ################################################################################
  # Gaining Experience.
  ################################################################################
  def pbGainEXP
    return if !ONLINEEXPGAIN

    super
  end

  ################################################################################
  # Battle core.
  ################################################################################
  def pbStartBattle(canlose = false)
    begin
      tracks = Dir.glob("Audio/BGM/Battle- Online*.ogg").map { |f| File.basename(f) }

	  if tracks.length > 0
	    pbBGMPlay(tracks.sample)
	  else
		pbBGMPlay("Battle- Trainer.ogg")  # fallback falls nix gefunden
	  end
      super
      loop do # Now begin the battle loop
        PBDebug.log("***Round #{@turncount}***") if $INTERNAL
        if @debug && @turncount >= 101
          @decision = pbDecisionOnTime()
          PBDebug.log("***[Undecided after 100 rounds]")
          pbAbort
          break
        end
        PBDebug.logonerr {
          pbCommandPhase
        }
        # need to quickly delete this or else it crashes bc of size
        for i in 0...4
          @battlers[0].moves[i].battle = nil if @battlers[0].moves[i]
          @battlers[2].moves[i].battle = nil if @battlers[2].moves[i] && @doublebattle
        end
        @choices[0][2].battle = nil if @choices[0][2]
        @choices[2][2].battle = nil if @choices[2][2] && @doublebattle
        choices = serializeData(@choices[0])
        choices2 = @doublebattle ? serializeData(@choices[2]) : ""
        # restore them bishes
        for i in 0...4
          @battlers[0].moves[i].battle = self if @battlers[0].moves[i]
          @battlers[2].moves[i].battle = self if @battlers[2].moves[i] && @doublebattle
        end
        @choices[0][2].battle = self if @choices[0][2]
        @choices[2][2].battle = self if @choices[2][2] && @doublebattle
        specarr = [@megaEvolution[0][0], @zMove[0][0], @ultraBurst[0][0]].join(",")
        $network.send("<BAT choices=#{choices} rseed=#{choices2} special=#{specarr}>")
        pbDisplay(_INTL("Waiting..."))
        loop do
          if !$network.loggedin
            @decision = 0
            Kernel.pbMessage("Disconnected from the server.")
            return pbEndOfBattle
          end
          Graphics.update
          Input.update
          message = $network.listen
          case message
            when /<ACTI>/ then $network.send("<ACTI>")
            when /<BAT choices=(.*) rseed=(.*) special=(.*)>/
              (@choices[1] = deserializeData($1)) rescue nil
              (@choices[3] = deserializeData($2) if @doublebattle) rescue nil
              for i in 0...4
                @battlers[1].moves[i].battle = self if @battlers[1].moves[i]
                @battlers[3].moves[i].battle = self if @battlers[3].moves[i] && @doublebattle
              end
              @choices[1][2].battle = self if @choices[1][2]
              @choices[3][2].battle = self if @choices[3][2] && @doublebattle
              specials = $3
              newspec = specials.split(",").map { |s| (s.to_i) }
              @megaEvolution[1][0]        = newspec[0]
              @zMove[1][0]                = newspec[1]
              @ultraBurst[1][0]           = newspec[2]

              # Fixing the index registered
              @megaEvolution[1][0] += 1    if @megaEvolution[1][0] == 0 || @megaEvolution[1][0] == 2
              @zMove[1][0] += 1            if @zMove[1][0] == 0 || @zMove[1][0] == 2
              @ultraBurst[1][0] += 1       if @ultraBurst[1][0] == 0 || @ultraBurst[1][0] == 2
              PBDebug.log(@choices[1].join(", ") + "\n\n")
              PBDebug.log(@choices[3].join(", ") + "\n\n")
              PBDebug.log(newspec.join(", ") + "\n\n")
              break
            when /<BAT choices=(.*)>/
              raise "crash"
            when /<BAT dead>/
              @decision = 1
              Kernel.pbMessage("Other player disconnected.")
              return pbEndOfBattle
          end
        end
        # register action of opponent correctly if necessary
        if @choices[1][2] && @choices[1][2].move != :STRUGGLE && @zMove[1][0] < 0
          @choices[1][2] = @battlers[1].moves[@choices[1][1]] if @choices[1][0] == :move # Need to reset move object
          @choices[1][3] = @choices[1][3] ^ 1 if @doublebattle && @choices[1][0] == :move && @choices[1][3] >= 0 # reset target
          @battlers[1].selectedMove = @battlers[1].moves[@choices[1][1]].move if @choices[1][0] == :move
        end

        if @choices[3][2] && @choices[3][2].move != :STRUGGLE && @zMove[1][0] < 0
          @choices[3][2] = @battlers[3].moves[@choices[3][1]] if @choices[3][0] == :move # Need to reset move object
          @choices[3][3] = @choices[3][3] ^ 1 if @doublebattle && @choices[3][0] == :move && @choices[3][3] >= 0 # reset target
          @battlers[3].selectedMove = @battlers[3].moves[@choices[3][1]].move if @choices[3][0] == :move
        end

        break if @decision > 0

        PBDebug.logonerr {
          pbAttackPhase
        }
        break if @decision > 0

        PBDebug.logonerr {
          pbEndOfRoundPhase
        }
        break if @decision > 0

        @turncount += 1
      end
      return pbEndOfBattle(canlose)
    rescue BattleAbortedException
      @decision = 0
      @scene.pbEndBattle(@decision)
    end
    return @decision
  end

  def serializeData(data)
    s = Marshal.dump(data)
    begin
      Marshal.load(s)
    rescue
      pp data
    end
    return [s].pack("m").delete("\n")
  end

  def deserializeData(serialized)
    return Marshal.load(serialized.unpack("m")[0])
  end

  ################################################################################
  # End of battle.
  ################################################################################
  def pbEndOfBattle(canlose = false)
    super
    # $network.send("<DSC>")
    return @decision
  end

  ################################################################################
  # Waits to receive a change in battler after fainting.
  ################################################################################
  def waitnewenemy
    pbDisplay(_INTL("Waiting..."))
    loop do
      @scene.pbGraphicsUpdate
      Input.update
      message = $network.listen
      case message
        when /<ACTI>/ then $network.send("<ACTI>")
        when /<BAT new=(.*)>/
          PBDebug.log("\n\nnewenemy: " + $1.to_s + "\n\n")
          return $1.to_i
          break
      end
    end
  end

  ################################################################################
  # Waits for the server to send a new seed.
  ################################################################################
  def receive_seed
    $network.send("<BAT seed turn=#{@turncount}>")
    loop do
      @scene.pbGraphicsUpdate
      Input.update
      message = $network.listen
      case message
        when /<ACTI>/ then $network.send("<ACTI>")
        when /<BAT seed=(.*)>/
          seed = $1
          PBDebug.log("\n\nnewseed: " + $1.to_s + "\n\n")
          @tiebreak = @tiebreak ^ pbRandom(2)
          return seed
      end
    end
  end
end
