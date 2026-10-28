# ================================
# CONFIG
# ================================

PULSE_EXCEPTIONS = [
  :Arc_Pulse,
  :Pulse_Eternatus
]

PULSE_BEAT = {
  :Pulse_Abra => 2500,
  :Pulse_Avalugg => 2501,
  :Pulse_Camerupt => 2502,
  :Pulse_Clawitzer => 2503,
  :Pulse_Garbodor => 2504,
  :Pulse_Hypno => 2505,
  :Pulse_Magnezone => 2506,
  :Pulse_Mime => 2507,
  :Pulse_Muk => 2508,
  :Pulse_Swalot => 2509,
  :Pulse_Tangrowth_A => 2510,
  :Pulse_Tangrowth_B => 2511,
  :Pulse_Tangrowth_C => 2512
}

# ================================
# SAVEFILE SYNC (läuft nur 1x)
# ================================

class Scene_Map
  alias pulse_sync_update update

  def update
    pulse_sync_update

    # 👉 nur einmal ausführen
    if !$game_switches[2999]

      PULSEDATA.keys.each do |pulse|
        if $game_switches[pulse]

          case pulse
          when :Pulse_Abra
            $game_switches[2500] = true
          when :Pulse_Avalugg
            $game_switches[2501] = true
          when :Pulse_Camerupt
            $game_switches[2502] = true
          when :Pulse_Clawitzer
            $game_switches[2503] = true
          when :Pulse_Garbodor
            $game_switches[2504] = true
          when :Pulse_Hypno
            $game_switches[2505] = true
          when :Pulse_Magnezone
            $game_switches[2506] = true
          when :Pulse_Mime
            $game_switches[2507] = true
          when :Pulse_Muk
            $game_switches[2508] = true
          when :Pulse_Swalot
            $game_switches[2509] = true
          when :Pulse_Tangrowth_A
            $game_switches[2510] = true
          when :Pulse_Tangrowth_B
            $game_switches[2511] = true
          when :Pulse_Tangrowth_C
            $game_switches[2512] = true
          end

        end
      end

      $game_switches[2999] = true
    end

  end
end

# ================================
# IMAGE LOGIC
# ================================

def getPulseImage(pulse)
  name = pulse.to_s

  return name if PULSE_EXCEPTIONS.include?(pulse)
  return name unless $game_switches[pulse]

  beat_switch = PULSE_BEAT[pulse]

  if !beat_switch || !$game_switches[beat_switch]
    hidden_name = "#{name}_Hidden"

    if pbResolveBitmap("Graphics/Pictures/PulseDex/#{hidden_name}")
      return hidden_name
    else
      return "Pulse_Hidden"
    end
  end

  return name
end

# ================================
# PREPARE SCREEN
# ================================

def preparePulseDexInfo(pulse)
  sprites = {}
  viewport = Viewport.new(0, 0, Graphics.width, Graphics.height)
  viewport.z = 99999

  sprites["background"] = IconSprite.new(0, 0, viewport)
  image = getPulseImage(pulse)
  sprites["background"].setBitmap("Graphics/Pictures/PulseDex/#{image}")
  sprites["background"].z = 255

  return viewport, sprites
end

# ================================
# INFO SCREEN
# ================================

class Scene_PulseDex_Info
  def initialize(pulse, index)
    @pulse = pulse
    @index = index
  end

  def main
    @viewport, @sprites = preparePulseDexInfo(@pulse)
    ttsPulse(@pulse)

    Graphics.transition

    loop do
      Graphics.update
      Input.update
      update
      break if $scene != self
    end

    Graphics.freeze
    pbDisposeSpriteHash(@sprites)
    @viewport.dispose
  end

  def update
    pbUpdateSpriteHash(@sprites)

    if Input.trigger?(Input::RIGHT)
      changePulse(1)
    elsif Input.trigger?(Input::LEFT)
      changePulse(-1)
    end

    if Input.trigger?(Input::B)
      pbPlayCancelSE()
      $scene = Scene_PulseDex.new(@index)
    end
  end

  def changePulse(increment)
    loop do
      @index = (@index + increment) % PULSEDATA.keys.length
      break if $game_switches[PULSEDATA.keys[@index]]
    end

    pbPlayDecisionSE()
    pbDisposeSpriteHash(@sprites)
    @viewport.dispose

    @pulse = PULSEDATA.keys[@index]
    @viewport, @sprites = preparePulseDexInfo(@pulse)
    ttsPulse(@pulse)
  end
end

# ================================
# BATTLE VIEW
# ================================

class Scene_PulseDex_Battle
  def pbSwitchPulseNotes
    image = getPulseImage(@pulses[@index])
    @sprites["background"].setBitmap("Graphics/Pictures/PulseDex/#{image}")
    ttsPulse(@pulses[@index])
  end
end


class PokeBattle_Battle
  unless method_defined?(:pulse_detect_sendout)
    alias pulse_detect_sendout pbSendOut
  end

  def pbSendOut(idxBattler, pkmn, batonpass=false)
    pulse_detect_sendout(idxBattler, pkmn, batonpass)

    return if pkmn.nil?

    species = pkmn.species
    form    = pkmn.form

    case species
    when :ABRA
      $game_switches[2500] = true if form == 1

    when :AVALUGG
      $game_switches[2501] = true if form == 2

    when :CAMERUPT
      $game_switches[2502] = true if form == 2

    when :CLAWITZER
      $game_switches[2503] = true if form == 1

    when :GARBODOR
      $game_switches[2504] = true if form == 2

    when :HYPNO
      $game_switches[2505] = true if form == 2

    when :MAGNEZONE
      $game_switches[2506] = true if form == 1

    when :MRMIME
      $game_switches[2507] = true if form == 2

    when :MUK
      $game_switches[2508] = true if form == 2

    when :SWALOT
      $game_switches[2509] = true if form == 1

    when :TANGROWTH
      case form
      when 1 then $game_switches[2512] = true
      when 2 then $game_switches[2511] = true
      when 3 then $game_switches[2510] = true
      end
    end
  end
end