# encoding: UTF-8
# Use the existing Reborn/AllGen boss theme in that Pokemon's wild catch battle.
# Additive mod: original scripts, audio, encounter data and saves stay untouched.
module LegendaryWildMusic
  # Most boss trainer IDs are species IDs. Group bosses use the aliases below.
  # Resolve the actual track through pbGetTrainerBattleBGM so AllGen's Music
  # Switch (3074), stock themes and future music-pack changes remain authoritative.
  TRAINER_TYPES = {
    :MEW => :MEW, :MEWTWO => :MEWTWO,
    :ENTEI => :ENTEI, :RAIKOU => :RAIKOU, :SUICUNE => :SUICUNE,
    :LUGIA => :LUGIA, :HOOH => :HOOH,
    :LATIAS => :LATIAS, :LATIOS => :LATIOS,
    :GROUDON => :GROUDON, :KYOGRE => :KYOGRE, :RAYQUAZA => :RAYQUAZA,
    :DEOXYS => :DEOXYS,
    :REGIROCK => :REGIS, :REGICE => :REGIS, :REGISTEEL => :REGIS,
    :REGIELEKI => :REGIS, :REGIDRAGO => :REGIS, :REGIGIGAS => :REGIGIGAS,
    :DARKRAI => :DARKRAI, :CRESSELIA => :CRESSELIA, :HEATRAN => :HEATRAN,
    :DIALGA => :DIALGA, :PALKIA => :PALKIA, :GIRATINA => :GIRATINA,
    :RESHIRAM => :RESHIRAM, :ZEKROM => :ZEKROM, :KYUREM => :KYUREM1,
    :KELDEO => :KELDEO,
    :COBALION => :MUSKETEER1, :TERRAKION => :MUSKETEER2, :VIRIZION => :MUSKETEER3,
    :TORNADUS => :GENIES, :THUNDURUS => :GENIES, :LANDORUS => :GENIES, :ENAMORUS => :GENIES,
    :NECROZMA => :NECROZMA,
    :XERNEAS => :XERNEAS, :YVELTAL => :YVELTAL, :ZYGARDE => :ZYGARDE,
    :TAPUBULU => :TAPUBULU, :TAPUFINI => :TAPUFINI, :TAPULELE => :TAPULELE, :TAPUKOKO => :TAPUKOKO,
    :JIRACHI => :JIRACHI, :MAGEARNA => :MAGEARNA, :DIANCIE => :DIANCIE,
    :VOLCANION => :VOLCANION, :HOOPA => :HOOPA, :GENESECT => :GENESECT,
    :MELOETTA => :MELOETTA, :MANAPHY => :MANAPHY, :SHAYMIN => :SHAYMIN1, :CELEBI => :CELEBI,
    :ARCEUS => :ARCEUS, :KORAIDON => :KORAIDON, :MIRAIDON => :MIRAIDON,
    :ETERNATUS => :ETERNATUS,
    :ZACIAN => :GUARDOGS, :ZAMAZENTA => :GUARDOGS, :ZARUDE => :GUARDOGS,
    :WOCHIEN => :RUINT, :CHIENPAO => :RUINS, :TINGLU => :RUINV, :CHIYU => :RUINB,
    :OGERPON => :OGERPON, :PECHARUNT => :PECHARUNT,
    :OKIDOGI => :LOYALS, :MUNKIDORI => :LOYALS, :FEZANDIPITI => :LOYALS
  }.freeze unless const_defined?(:TRAINER_TYPES, false)

  TrainerReference = Struct.new(:trainertype) unless const_defined?(:TrainerReference, false)

  def self.available?(audio)
    return false unless audio && audio.respond_to?(:name) && audio.name && !audio.name.empty?
    # AllGen mounts patch/Audio over Audio. Check both physical locations, and
    # preserve the original wild selector when an optional music pack is absent.
    ["patch/Audio/BGM", "Audio/BGM"].any? do |root|
      path = File.join(root, audio.name)
      ["", ".ogg", ".mp3", ".wav", ".mid", ".midi"].any? { |ext| File.file?(path + ext) }
    end
  end

  module WildBattleHook
    def pbGetWildBattleBGM(species)
      # Story/dynamic music and explicit next-battle overrides keep their priority.
      return super if $PokemonGlobal.nextBattleBGM
      trainer_type = LegendaryWildMusic::TRAINER_TYPES[species]
      return super unless trainer_type && $cache.trainertypes[trainer_type]

      audio = pbGetTrainerBattleBGM(LegendaryWildMusic::TrainerReference.new(trainer_type))
      return audio if LegendaryWildMusic.available?(audio)
      super
    end
    private :pbGetWildBattleBGM
  end
end

# Prepending survives later redefinitions of the base selector and F12 reloads.
Object.prepend(LegendaryWildMusic::WildBattleHook) unless Object.ancestors.include?(LegendaryWildMusic::WildBattleHook)
