# =========================================================
# 🔥 MAGNETIC LURE = NUR NEUE POKÉMON (NO FALLBACK)
# =========================================================

class PokemonEncounters

  alias magnetic_only_new_old_encountered pbEncounteredPokemon

  def pbEncounteredPokemon(enctype, tries = 1)
    if enctype < 0 || enctype > EncounterTypes::EnctypeChances.length
      raise ArgumentError.new(_INTL("Encounter type out of range"))
    end
    return nil if @enctypes[enctype] == nil

    encounters = []
    chances    = []

    @enctypes[enctype].each do |key, x|
      x.each { |y|
        encounters.push([key, y[1], y[2]])
        chances.push(y[0])
      }
    end

    # 🔥 ORIGINAL FILTER (Magnetic / etc.)
    forcedEncounter = pbForceEncounterUncapturedPkmn(encounters, chances)

    # 🔥 FIX: Magnetic = KEIN FALLBACK
    if $Trainer.party[0] &&
       $Trainer.party[0].item == :MAGNETICLURE

      # ✔ neues Pokémon gefunden
      return forcedEncounter if forcedEncounter

      # ❌ kein neues → kein Encounter
      return nil
    end

    # 👉 normales Verhalten
    return forcedEncounter if forcedEncounter

    # -------------------------------
    # ORIGINAL CODE (unverändert)
    # -------------------------------

    if !$Trainer.party[0].egg?
      firstpoke = $Trainer.party[0]
    else
      firstpoke = false
    end

    if firstpoke && rand(2) == 0
      type = -1
      if firstpoke.ability == :STATIC
        type = :ELECTRIC
      elsif firstpoke.ability == :MAGNETPULL
        type = :STEEL
      elsif firstpoke.ability == :FLASHFIRE
        type = :FIRE
      elsif firstpoke.ability == :HARVEST
        type = :GRASS
      elsif firstpoke.ability == :LIGHTNINGROD
        type = :ELECTRIC
      elsif firstpoke.ability == :STORMDRAIN
        type = :WATER
      elsif firstpoke.ability == :TOXICSINK
        type = :POISON
      end

      if type != -1
        newencs = []; newchances = []
        for i in 0...encounters.length
          species = encounters[i][0]
          form = pbISActuallyDifferentForm(species)
          t1 = $cache.pkmn[species, form].Type1
          t2 = $cache.pkmn[species, form].Type2
          if t1 == type || t2 == type
            newencs.push(encounters[i])
            newchances.push(chances[i])
          end
        end
        if newencs.length > 0
          encounters = newencs
          chances    = newchances
        end
      end
    end

    chancetotal = 0
    chances.each { |a| chancetotal += a }

    rnd = 0
    tries.times do
      r = rand(chancetotal)
      rnd = r if rnd < r
    end

    chosenpkmn = 0
    chance = 0

    for i in 0...chances.length
      chance += chances[i]
      if rnd < chance
        chosenpkmn = i
        break
      end
    end

    encounter = encounters[chosenpkmn]
    return nil if !encounter

    level = pbGetEncounterLevel(encounter)
    return [encounter[0], level]
  end

end