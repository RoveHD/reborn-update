# encoding: UTF-8
# Curated high-threat Disco-pool sets for the local Reborn/AllGen PBS.
# EV order: HP, Attack, Defense, Sp. Attack, Sp. Defense, Speed (installed patch calcStats order).
module REDiscoPoolData
  remove_const(:SETS) if const_defined?(:SETS, false)
  SETS = [
    { id: "arceus_extreme_speed", species: :ARCEUS, item: :LIFEORB, nature: :ADAMANT, moves: [:SWORDSDANCE, :EXTREMESPEED, :EARTHQUAKE, :SHADOWCLAW], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :MULTITYPE, double_moves: [:SWORDSDANCE, :EXTREMESPEED, :HIGHHORSEPOWER, :SHADOWCLAW], role: "Setup-Sweeper mit Priorität", theme: :physical },
    { id: "arceus_calm_mind", species: :ARCEUS, item: :LEFTOVERS, nature: :BOLD, moves: [:CALMMIND, :JUDGMENT, :RECOVER, :EARTHPOWER], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :MULTITYPE, role: "Calm-Mind-Wallbreaker", theme: :special },
    { id: "rayquaza_mixed", species: :RAYQUAZA, item: :EXPERTBELT, nature: :NAIVE, moves: [:DRAGONASCENT, :DRACOMETEOR, :EXTREMESPEED, :EARTHQUAKE], ev: [0, 252, 0, 4, 0, 252], form: 0, ability: :AIRLOCK, double_moves: [:DRAGONASCENT, :DRACOMETEOR, :EXTREMESPEED, :PROTECT], role: "Gemischter Wallbreaker", theme: :physical },
    { id: "mewtwo_nasty_plot", species: :MEWTWO, item: :FOCUSSASH, nature: :TIMID, moves: [:NASTYPLOT, :PSYSTRIKE, :AURASPHERE, :ICEBEAM], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PRESSURE, role: "Sash-Setup-Sweeper", theme: :special },
    { id: "mewtwo_choice_specs", species: :MEWTWO, item: :CHOICESPECS, nature: :TIMID, moves: [:PSYSTRIKE, :AURASPHERE, :ICEBEAM, :SHADOWBALL], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PRESSURE, role: "Specs-Wallbreaker", theme: :special },
    { id: "groudon_primal", species: :GROUDON, item: :REDORB, nature: :ADAMANT, moves: [:PRECIPICEBLADES, :FIREPUNCH, :STONEEDGE, :SWORDSDANCE], ev: [4, 252, 0, 0, 0, 252], form: 1, ability: :DESOLATELAND, role: "Primal-Sunbreaker", theme: :sun },
    { id: "kyogre_primal", species: :KYOGRE, item: :BLUEORB, nature: :MODEST, moves: [:ORIGINPULSE, :THUNDER, :ICEBEAM, :CALMMIND], ev: [4, 0, 0, 252, 0, 252], form: 1, ability: :PRIMORDIALSEA, role: "Primal-Wallbreaker", theme: :special },
    { id: "kyogre_rain_support", species: :KYOGRE, item: :DAMPROCK, nature: :MODEST, moves: [:HYDROPUMP, :THUNDER, :ICEBEAM, :CALMMIND], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :DRIZZLE, role: "Langzeit-Regen-Sweeper", theme: :rain },
    { id: "dialga_origin", species: :DIALGA, item: :ADAMANTCRYSTAL, nature: :MODEST, moves: [:DRACOMETEOR, :FLASHCANNON, :EARTHPOWER, :THUNDERBOLT], ev: [4, 0, 0, 252, 0, 252], form: 1, ability: :PRESSURE, role: "Origin-Wallbreaker", theme: :special },
    { id: "dialga_trick_room", species: :DIALGA, item: :LIFEORB, nature: :QUIET, moves: [:TRICKROOM, :DRACOMETEOR, :FLASHCANNON, :EARTHPOWER], ev: [252, 0, 4, 252, 0, 0], form: 0, ability: :PRESSURE, role: "Trick-Room-Angreifer", theme: :bulky },
    { id: "palkia_origin", species: :PALKIA, item: :LUSTROUSGLOBE, nature: :TIMID, moves: [:SPACIALREND, :HYDROPUMP, :AURASPHERE, :THUNDER], ev: [4, 0, 0, 252, 0, 252], form: 1, ability: :PRESSURE, role: "Origin-Spezialwallbreaker", theme: :special },
    { id: "palkia_setup", species: :PALKIA, item: :WISEGLASSES, nature: :TIMID, moves: [:RAINDANCE, :HYDROPUMP, :THUNDER, :SPACIALREND], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PRESSURE, role: "Eigenständiger Regen-Angreifer", theme: :rain },
    { id: "giratina_origin", species: :GIRATINA, item: :GRISEOUSORB, nature: :ADAMANT, moves: [:POLTERGEIST, :DRAGONCLAW, :SHADOWSNEAK, :WILLOWISP], ev: [4, 252, 0, 0, 0, 252], form: 1, ability: :LEVITATE, role: "Offensiver Utility-Kämpfer", theme: :physical },
    { id: "giratina_defog", species: :GIRATINA, item: :HEAVYDUTYBOOTS, nature: :BOLD, moves: [:WILLOWISP, :HEX, :DEFOG, :REST], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :PRESSURE, role: "Defensiver Status-Entferner", theme: :bulky },
    { id: "reshiram_sun", species: :RESHIRAM, item: :EXPERTBELT, nature: :TIMID, moves: [:BLUEFLARE, :DRACOMETEOR, :EARTHPOWER, :SOLARBEAM], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :TURBOBLAZE, role: "Sonnen-Wallbreaker", theme: :sun },
    { id: "zekrom_physical", species: :ZEKROM, item: :LUMBERRY, nature: :JOLLY, moves: [:BOLTSTRIKE, :DRAGONCLAW, :DRAGONDANCE, :ROOST], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :TERAVOLT, role: "Physischer Setup-Sweeper", theme: :physical },
    { id: "xerneas_geomancy", species: :XERNEAS, item: :POWERHERB, nature: :MODEST, moves: [:GEOMANCY, :MOONBLAST, :THUNDER, :FOCUSBLAST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :FAIRYAURA, role: "Einmaliger Geomancy-Sweeper", theme: :special },
    { id: "yveltal_life_orb", species: :YVELTAL, item: :LIFEORB, nature: :TIMID, moves: [:DARKPULSE, :OBLIVIONWING, :HEATWAVE, :ROOST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :DARKAURA, role: "Offensiver Sustain-Sweeper", theme: :special },
    { id: "solgaleo_setup", species: :SOLGALEO, item: :MUSCLEBAND, nature: :JOLLY, moves: [:SUNSTEELSTRIKE, :EARTHQUAKE, :FLAREBLITZ, :MORNINGSUN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :FULLMETALBODY, double_moves: [:SUNSTEELSTRIKE, :PROTECT, :FLAREBLITZ, :MORNINGSUN], role: "Physischer Wallbreaker mit Recovery", theme: :physical },
    { id: "lunala_specs", species: :LUNALA, item: :CHOICESPECS, nature: :TIMID, moves: [:MOONGEISTBEAM, :PSYSHOCK, :MOONBLAST, :FOCUSBLAST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :SHADOWSHIELD, role: "Specs-Wallbreaker", theme: :special },
    { id: "zacian_crowned", species: :ZACIAN, item: :RUSTEDSWORD, nature: :JOLLY, moves: [:IRONHEAD, :PLAYROUGH, :CLOSECOMBAT, :SWORDSDANCE], ev: [4, 252, 0, 0, 0, 252], form: 1, ability: :INTREPIDSWORD, role: "Kronen-Setup-Sweeper", theme: :physical },
    { id: "zamazenta_crowned", species: :ZAMAZENTA, item: :RUSTEDSHIELD, nature: :IMPISH, moves: [:IRONHEAD, :BODYPRESS, :CRUNCH, :IRONDEFENSE], ev: [252, 0, 252, 0, 0, 4], form: 1, ability: :DAUNTLESSSHIELD, role: "Defensiver Kronen-Brecher", theme: :bulky },
    { id: "koraidon_sun", species: :KORAIDON, item: :LIFEORB, nature: :JOLLY, moves: [:COLLISIONCOURSE, :FLAREBLITZ, :DRAGONCLAW, :SWORDSDANCE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :ORICHALCUMPULSE, role: "Sonnen-Setup-Angreifer", theme: :sun },
    { id: "miraidon_electric", species: :MIRAIDON, item: :CHOICESPECS, nature: :TIMID, moves: [:ELECTRODRIFT, :DRACOMETEOR, :OVERHEAT, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :HADRONENGINE, role: "Terrain-gestützter Pivot", theme: :electric },
    { id: "eternatus_pressure", species: :ETERNATUS, item: :LIFEORB, nature: :TIMID, moves: [:SLUDGEWAVE, :FLAMETHROWER, :DYNAMAXCANNON, :RECOVER], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PRESSURE, double_moves: [:SLUDGEBOMB, :FLAMETHROWER, :DYNAMAXCANNON, :RECOVER], role: "Schneller Spezialwallbreaker", theme: :special },
    { id: "calyrex_ice_rider", species: :CALYREX, item: :CLEARAMULET, nature: :ADAMANT, moves: [:GLACIALLANCE, :ZENHEADBUTT, :HIGHHORSEPOWER, :SWORDSDANCE], ev: [252, 252, 0, 0, 0, 4], form: 1, ability: :ASONECHILLING, role: "Bulky Eisreiter-Wallbreaker", theme: :bulky },
    { id: "calyrex_shadow_rider", species: :CALYREX, item: :LIFEORB, nature: :TIMID, moves: [:ASTRALBARRAGE, :PSYCHIC, :POLLENPUFF, :NASTYPLOT], ev: [4, 0, 0, 252, 0, 252], form: 2, ability: :ASONEGRIM, role: "Schneller Reiter-Setup-Sweeper", theme: :special },
    { id: "iron_hands_assault_vest", species: :IRONHANDS, item: :ASSAULTVEST, nature: :ADAMANT, moves: [:DRAINPUNCH, :WILDCHARGE, :ICEPUNCH, :HEAVYSLAM], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :QUARKDRIVE, role: "Assault-Vest-Brecher mit Heilung", theme: :electric },
    { id: "great_tusk_booster", species: :GREATTUSK, item: :LEFTOVERS, nature: :JOLLY, moves: [:HEADLONGRUSH, :CLOSECOMBAT, :RAPIDSPIN, :KNOCKOFF], ev: [0, 252, 4, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen-gestützter Utility-Angreifer", theme: :sun },
    { id: "flutter_mane_booster", species: :FLUTTERMANE, item: :CHOICESPECS, nature: :TIMID, moves: [:MOONBLAST, :SHADOWBALL, :MYSTICALFIRE, :THUNDERBOLT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen- oder Specs-Wallbreaker", theme: :sun },
    { id: "iron_valiant_mixed", species: :IRONVALIANT, item: :BOOSTERENERGY, nature: :NAIVE, moves: [:MOONBLAST, :CLOSECOMBAT, :KNOCKOFF, :SWORDSDANCE], ev: [0, 252, 0, 4, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Gemischter Tempo-Breaker", theme: :special },
    { id: "regigigas_primordial", species: :REGIGIGAS, item: :PRIMORDIALORB, nature: :ADAMANT, moves: [:HARDPRESS, :ICEPUNCH, :ROCKSLIDE, :DRAINPUNCH], ev: [252, 252, 4, 0, 0, 0], form: 1, ability: :PRIMORDIALCONSTRUCT, role: "Urform-Brecher mit defensiver Fähigkeit", theme: :bulky },
    { id: "walking_wake_sun", species: :WALKINGWAKE, item: :BOOSTERENERGY, nature: :TIMID, moves: [:HYDROSTEAM, :DRACOMETEOR, :FLAMETHROWER, :DRAGONPULSE], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen-gestützter Spezial-Sweeper", theme: :sun },
    { id: "iron_moth_special", species: :IRONMOTH, item: :BOOSTERENERGY, nature: :TIMID, moves: [:FIERYDANCE, :SLUDGEWAVE, :ENERGYBALL, :MORNINGSUN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:FIERYDANCE, :PROTECT, :ENERGYBALL, :MORNINGSUN], role: "QuarkDrive-Spezial-Angreifer", theme: :special },
    { id: "kingdra_rain", species: :KINGDRA, item: :CHOICESPECS, nature: :MODEST, moves: [:HYDROPUMP, :DRACOMETEOR, :ICEBEAM, :FLIPTURN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :SWIFTSWIM, role: "Regen-Sweeper", theme: :rain },
    { id: "barraskewda_rain", species: :BARRASKEWDA, item: :CHOICEBAND, nature: :JOLLY, moves: [:LIQUIDATION, :CLOSECOMBAT, :CRUNCH, :FLIPTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :SWIFTSWIM, role: "Regen-Pivot mit Swift Swim", theme: :rain },
    { id: "floatzel_rain", species: :FLOATZEL, item: :MYSTICWATER, nature: :JOLLY, moves: [:LIQUIDATION, :AQUAJET, :ICESPINNER, :FLIPTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :SWIFTSWIM, role: "Schneller Regen-Angreifer", theme: :rain },
    { id: "ludicolo_rain", species: :LUDICOLO, item: :ASSAULTVEST, nature: :MODEST, moves: [:SURF, :GIGADRAIN, :ICEBEAM, :FOCUSBLAST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :SWIFTSWIM, double_moves: [:SCALD, :GIGADRAIN, :ICEBEAM, :FOCUSBLAST], role: "Spezieller Regen-Angreifer", theme: :rain },

    { id: "nihilego_power_herb", species: :NIHILEGO, item: :POWERHERB, nature: :TIMID, moves: [:METEORBEAM, :SLUDGEWAVE, :THUNDERBOLT, :GRASSKNOT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:METEORBEAM, :SLUDGEBOMB, :THUNDERBOLT, :GRASSKNOT], role: "Meteorstrahl-Spezialkehrer", theme: :special },
    { id: "nihilego_assault_vest", species: :NIHILEGO, item: :ASSAULTVEST, nature: :MODEST, moves: [:SLUDGEWAVE, :POWERGEM, :THUNDERBOLT, :GRASSKNOT], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :BEASTBOOST, double_moves: [:SLUDGEBOMB, :POWERGEM, :THUNDERBOLT, :GRASSKNOT], role: "Robuster Spezialangreifer", theme: :bulky },
    { id: "nihilego_choice_scarf", species: :NIHILEGO, item: :CHOICESCARF, nature: :TIMID, moves: [:POWERGEM, :SLUDGEWAVE, :PSYSHOCK, :THUNDERBOLT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:POWERGEM, :SLUDGEBOMB, :PSYSHOCK, :THUNDERBOLT], role: "Schneller Aufräumer", theme: :special },
    { id: "buzzwole_bulk_up", species: :BUZZWOLE, item: :LEFTOVERS, nature: :ADAMANT, moves: [:DRAINPUNCH, :LEECHLIFE, :BULKUP, :ICEPUNCH], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :BEASTBOOST, role: "Sustain-Setup-Brecher", theme: :bulky },
    { id: "buzzwole_choice_band", species: :BUZZWOLE, item: :CHOICEBAND, nature: :ADAMANT, moves: [:CLOSECOMBAT, :LEECHLIFE, :ICEPUNCH, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:CLOSECOMBAT, :LEECHLIFE, :ICEPUNCH, :HIGHHORSEPOWER], role: "Band-Wallbreaker", theme: :physical },
    { id: "buzzwole_defense", species: :BUZZWOLE, item: :ROCKYHELMET, nature: :IMPISH, moves: [:DRAINPUNCH, :ROOST, :TOXIC, :ICEPUNCH], ev: [252, 4, 252, 0, 0, 0], form: 0, ability: :BEASTBOOST, role: "Physische Check- und Heilrolle", theme: :bulky },
    { id: "pheromosa_quiver", species: :PHEROMOSA, item: :LIFEORB, nature: :TIMID, moves: [:QUIVERDANCE, :BUGBUZZ, :ICEBEAM, :FOCUSBLAST], ev: [0, 0, 4, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Falterreigen-Spezialkehrer", theme: :special },
    { id: "pheromosa_band", species: :PHEROMOSA, item: :CHOICEBAND, nature: :JOLLY, moves: [:UTURN, :CLOSECOMBAT, :TRIPLEAXEL, :POISONJAB], ev: [0, 252, 0, 0, 4, 252], form: 0, ability: :BEASTBOOST, role: "Schneller physischer Pivot", theme: :physical },
    { id: "pheromosa_mixed_sash", species: :PHEROMOSA, item: :FOCUSSASH, nature: :NAIVE, moves: [:BUGBUZZ, :ICEBEAM, :CLOSECOMBAT, :UTURN], ev: [0, 4, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Sash-Pivot mit gemischten Angriffen", theme: :special },
    { id: "xurkitree_calm_mind", species: :XURKITREE, item: :LIFEORB, nature: :TIMID, moves: [:CALMMIND, :THUNDERBOLT, :DAZZLINGGLEAM, :ENERGYBALL], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Gedankengut-Spezialbrecher", theme: :special },
    { id: "xurkitree_choice_specs", species: :XURKITREE, item: :CHOICESPECS, nature: :MODEST, moves: [:THUNDERBOLT, :DAZZLINGGLEAM, :ENERGYBALL, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Specs-Brecher", theme: :special },
    { id: "xurkitree_choice_scarf", species: :XURKITREE, item: :CHOICESCARF, nature: :TIMID, moves: [:THUNDERBOLT, :ENERGYBALL, :DAZZLINGGLEAM, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Wahlschal-Pivot und Aufraeumer", theme: :special },
    { id: "celesteela_meteor_beam", species: :CELESTEELA, item: :POWERHERB, nature: :MODEST, moves: [:AUTOTOMIZE, :METEORBEAM, :AIRSLASH, :FLAMETHROWER], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Autotomie und Meteorstrahl", theme: :special },
    { id: "celesteela_special", species: :CELESTEELA, item: :ASSAULTVEST, nature: :MODEST, moves: [:AIRSLASH, :FLASHCANNON, :FLAMETHROWER, :GIGADRAIN], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :BEASTBOOST, role: "Spezialtank mit vier Angriffen", theme: :bulky },
    { id: "celesteela_leech", species: :CELESTEELA, item: :LEFTOVERS, nature: :CAREFUL, moves: [:HEAVYSLAM, :LEECHSEED, :PROTECT, :TOXIC], ev: [252, 0, 120, 0, 136, 0], form: 0, ability: :BEASTBOOST, role: "Egelsamen-Anker mit Schutzschild", theme: :bulky },
    { id: "kartana_swords_dance", species: :KARTANA, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :LEAFBLADE, :SMARTSTRIKE, :SACREDSWORD], ev: [0, 252, 4, 0, 0, 252], form: 0, ability: :BEASTBOOST, role: "Schwerttanz-Kehrer", theme: :physical },
    { id: "kartana_choice_scarf", species: :KARTANA, item: :CHOICESCARF, nature: :JOLLY, moves: [:LEAFBLADE, :SMARTSTRIKE, :SACREDSWORD, :KNOCKOFF], ev: [0, 252, 0, 0, 4, 252], form: 0, ability: :BEASTBOOST, role: "Scarf-Cleaner", theme: :physical },
    { id: "kartana_choice_band", species: :KARTANA, item: :CHOICEBAND, nature: :ADAMANT, moves: [:LEAFBLADE, :SMARTSTRIKE, :SACREDSWORD, :KNOCKOFF], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :BEASTBOOST, role: "Wahlband-Wallbreaker", theme: :physical },
    { id: "guzzlord_rest_talk", species: :GUZZLORD, item: :LEFTOVERS, nature: :CAREFUL, moves: [:KNOCKOFF, :DRAGONTAIL, :REST, :SLEEPTALK], ev: [0, 4, 252, 0, 252, 0], form: 0, ability: :BEASTBOOST, role: "Schlafrede-Tank und Phazer", theme: :bulky },
    { id: "guzzlord_choice_band", species: :GUZZLORD, item: :CHOICEBAND, nature: :ADAMANT, moves: [:CRUNCH, :DRAGONCLAW, :EARTHQUAKE, :HEAVYSLAM], ev: [0, 252, 128, 0, 128, 0], form: 0, ability: :BEASTBOOST, double_moves: [:CRUNCH, :DRAGONCLAW, :HIGHHORSEPOWER, :HEAVYSLAM], role: "Bulky Band-Brecher", theme: :physical },
    { id: "naganadel_nasty_plot", species: :NAGANADEL, item: :LIFEORB, nature: :TIMID, moves: [:NASTYPLOT, :SLUDGEWAVE, :DRACOMETEOR, :FIREBLAST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:NASTYPLOT, :SLUDGEBOMB, :DRACOMETEOR, :FIREBLAST], role: "Nasty-Plot-Kehrer", theme: :special },
    { id: "naganadel_choice_specs", species: :NAGANADEL, item: :CHOICESPECS, nature: :TIMID, moves: [:SLUDGEWAVE, :DRACOMETEOR, :FIREBLAST, :UTURN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:SLUDGEBOMB, :DRACOMETEOR, :FIREBLAST, :UTURN], role: "Specs-Speed-Pivot", theme: :special },
    { id: "naganadel_focus_sash", species: :NAGANADEL, item: :FOCUSSASH, nature: :TIMID, moves: [:DRACOMETEOR, :SLUDGEWAVE, :FIREBLAST, :TAILWIND], ev: [0, 0, 4, 252, 0, 252], form: 0, ability: :BEASTBOOST, double_moves: [:DRACOMETEOR, :SLUDGEBOMB, :FIREBLAST, :TAILWIND], role: "Sash-Lead mit Rückenwind", theme: :special },
    { id: "stakataka_trick_room", species: :STAKATAKA, item: :SHUCABERRY, nature: :BRAVE, moves: [:TRICKROOM, :GYROBALL, :STONEEDGE, :EARTHQUAKE], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :BEASTBOOST, double_moves: [:TRICKROOM, :GYROBALL, :STONEEDGE, :HIGHHORSEPOWER], role: "Bizarroraum-Setter und Angreifer", theme: :bulky },
    { id: "stakataka_body_press", species: :STAKATAKA, item: :LEFTOVERS, nature: :RELAXED, moves: [:IRONDEFENSE, :BODYPRESS, :GYROBALL, :STEALTHROCK], ev: [252, 0, 252, 0, 4, 0], form: 0, ability: :BEASTBOOST, double_moves: [:IRONDEFENSE, :BODYPRESS, :GYROBALL, :WIDEGUARD], role: "Iron-Defense-Setupwall", theme: :bulky },
    { id: "stakataka_choice_band", species: :STAKATAKA, item: :CHOICEBAND, nature: :BRAVE, moves: [:GYROBALL, :STONEEDGE, :EARTHQUAKE, :SUPERPOWER], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :BEASTBOOST, double_moves: [:GYROBALL, :STONEEDGE, :HIGHHORSEPOWER, :SUPERPOWER], role: "Langsamer Wahlband-Wallbreaker", theme: :physical },
    { id: "blacephalon_calm_mind", species: :BLACEPHALON, item: :FOCUSSASH, nature: :TIMID, moves: [:CALMMIND, :SHADOWBALL, :FIREBLAST, :PSYSHOCK], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Sash-Kehrer mit Gedankengut", theme: :special },
    { id: "blacephalon_choice_scarf", species: :BLACEPHALON, item: :CHOICESCARF, nature: :TIMID, moves: [:SHADOWBALL, :FIREBLAST, :PSYSHOCK, :OVERHEAT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Wahlschal-Revenge-Killer", theme: :special },
    { id: "blacephalon_specs", species: :BLACEPHALON, item: :CHOICESPECS, nature: :TIMID, moves: [:SHADOWBALL, :FIREBLAST, :DARKPULSE, :PSYSHOCK], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BEASTBOOST, role: "Specs-Durchbruch", theme: :special },
    { id: "scream_tail_screen_lead", species: :SCREAMTAIL, item: :LIGHTCLAY, nature: :JOLLY, moves: [:REFLECT, :LIGHTSCREEN, :THUNDERWAVE, :PLAYROUGH], ev: [252, 0, 4, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Schneller Doppelschild-Support", theme: :sun },
    { id: "scream_tail_bulky", species: :SCREAMTAIL, item: :LEFTOVERS, nature: :IMPISH, moves: [:WISH, :PROTECT, :PLAYROUGH, :THUNDERWAVE], ev: [252, 0, 252, 0, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Wish-Support und Statuskontrolle", theme: :sun },
    { id: "brute_bonnet_spore_breaker", species: :BRUTEBONNET, item: :LIFEORB, nature: :ADAMANT, moves: [:SPORE, :SUCKERPUNCH, :SEEDBOMB, :CLOSECOMBAT], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Pilzsporen-Brecher mit Prioritaet", theme: :sun },
    { id: "brute_bonnet_assault", species: :BRUTEBONNET, item: :ASSAULTVEST, nature: :ADAMANT, moves: [:SUCKERPUNCH, :SEEDBOMB, :CRUNCH, :CLOSECOMBAT], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Bulky Assault-Vest-Angreifer", theme: :sun },
    { id: "brute_bonnet_spore", species: :BRUTEBONNET, item: :LEFTOVERS, nature: :CAREFUL, moves: [:SPORE, :SEEDBOMB, :SUCKERPUNCH, :PROTECT], ev: [252, 4, 0, 0, 252, 0], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:SPORE, :SEEDBOMB, :SUCKERPUNCH, :RAGEPOWDER], role: "Pilzsporen-Support und Wutpulver im Doppel", theme: :sun },
    { id: "slither_wing_dance", species: :SLITHERWING, item: :LEFTOVERS, nature: :ADAMANT, moves: [:BULKUP, :LEECHLIFE, :CLOSECOMBAT, :FLAREBLITZ], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Protzer-Kaempfer mit Heilung", theme: :sun },
    { id: "slither_wing_choice", species: :SLITHERWING, item: :CHOICEBAND, nature: :ADAMANT, moves: [:FIRSTIMPRESSION, :UTURN, :CLOSECOMBAT, :FLAREBLITZ], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Band-Pivot mit Priorität", theme: :sun },
    { id: "slither_wing_recovery_pivot", species: :SLITHERWING, item: :HEAVYDUTYBOOTS, nature: :ADAMANT, moves: [:FIRSTIMPRESSION, :CLOSECOMBAT, :UTURN, :MORNINGSUN], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen-Pivot mit Morgengrauen", theme: :sun },
    { id: "sandy_shocks_booster", species: :SANDYSHOCKS, item: :BOOSTERENERGY, nature: :TIMID, moves: [:STEALTHROCK, :EARTHPOWER, :THUNDERBOLT, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:PROTECT, :EARTHPOWER, :THUNDERBOLT, :VOLTSWITCH], role: "Booster-Speziallead und Pivot", theme: :special },
    { id: "sandy_shocks_specs", species: :SANDYSHOCKS, item: :CHOICESPECS, nature: :TIMID, moves: [:EARTHPOWER, :THUNDERBOLT, :FLASHCANNON, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Specs-Pivot mit Doppel-STAB", theme: :sun },
    { id: "sandy_shocks_hazard_lead", species: :SANDYSHOCKS, item: :FOCUSSASH, nature: :TIMID, moves: [:STEALTHROCK, :SPIKES, :EARTHPOWER, :THUNDERBOLT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:PROTECT, :THUNDERWAVE, :EARTHPOWER, :THUNDERBOLT], role: "Sash-Hazardlead und Tempokontrolle im Doppel", theme: :sun },
    { id: "roaring_moon_booster", species: :ROARINGMOON, item: :BOOSTERENERGY, nature: :JOLLY, moves: [:DRAGONDANCE, :CRUNCH, :ACROBATICS, :EARTHQUAKE], ev: [0, 252, 4, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:DRAGONDANCE, :CRUNCH, :ACROBATICS, :STOMPINGTANTRUM], role: "Booster-Akrobatik-Kehrer", theme: :physical },
    { id: "roaring_moon_band", species: :ROARINGMOON, item: :CHOICEBAND, nature: :JOLLY, moves: [:CRUNCH, :OUTRAGE, :EARTHQUAKE, :UTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:CRUNCH, :DRAGONCLAW, :STOMPINGTANTRUM, :UTURN], role: "Schneller Band-Wallbreaker", theme: :sun },
    { id: "roaring_moon_roost", species: :ROARINGMOON, item: :HEAVYDUTYBOOTS, nature: :JOLLY, moves: [:DRAGONDANCE, :CRUNCH, :DRAGONCLAW, :ROOST], ev: [252, 4, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Langlebiger Dragon-Dance-Kämpfer", theme: :sun },
    { id: "gouging_fire_bulky", species: :GOUGINGFIRE, item: :HEAVYDUTYBOOTS, nature: :IMPISH, moves: [:BURNINGBULWARK, :FLAREBLITZ, :BREAKINGSWIPE, :MORNINGSUN], ev: [252, 4, 252, 0, 0, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Physischer Sonnen-Tank mit Heilung", theme: :sun },
    { id: "gouging_fire_dance", species: :GOUGINGFIRE, item: :LIFEORB, nature: :JOLLY, moves: [:DRAGONDANCE, :FLAREBLITZ, :DRAGONCLAW, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, double_moves: [:DRAGONDANCE, :FLAREBLITZ, :DRAGONCLAW, :STOMPINGTANTRUM], role: "Dragon-Dance-Wallbreaker", theme: :sun },
    { id: "gouging_fire_booster", species: :GOUGINGFIRE, item: :BOOSTERENERGY, nature: :ADAMANT, moves: [:DRAGONDANCE, :FLAREBLITZ, :DRAGONCLAW, :MORNINGSUN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Booster-Drachentanz mit Heilung", theme: :physical },
    { id: "raging_bolt_calm_mind", species: :RAGINGBOLT, item: :LEFTOVERS, nature: :MODEST, moves: [:CALMMIND, :THUNDERCLAP, :DRAGONPULSE, :THUNDERBOLT], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Bulky Calm-Mind-Angreifer", theme: :sun },
    { id: "raging_bolt_specs", species: :RAGINGBOLT, item: :CHOICESPECS, nature: :MODEST, moves: [:THUNDERCLAP, :DRACOMETEOR, :THUNDERBOLT, :WEATHERBALL], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen-Spezialbrecher mit Prioritaet", theme: :sun },
    { id: "raging_bolt_av", species: :RAGINGBOLT, item: :ASSAULTVEST, nature: :MODEST, moves: [:THUNDERCLAP, :DRAGONPULSE, :THUNDERBOLT, :VOLTSWITCH], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :PROTOSYNTHESIS, role: "Spezialtank und Pivot", theme: :sun },
    { id: "iron_treads_booster", species: :IRONTREADS, item: :BOOSTERENERGY, nature: :JOLLY, moves: [:EARTHQUAKE, :IRONHEAD, :KNOCKOFF, :RAPIDSPIN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:HIGHHORSEPOWER, :IRONHEAD, :KNOCKOFF, :RAPIDSPIN], role: "Booster-Utility-Cleaner", theme: :physical },
    { id: "iron_treads_assault", species: :IRONTREADS, item: :ASSAULTVEST, nature: :ADAMANT, moves: [:EARTHQUAKE, :IRONHEAD, :KNOCKOFF, :RAPIDSPIN], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :QUARKDRIVE, double_moves: [:HIGHHORSEPOWER, :IRONHEAD, :KNOCKOFF, :RAPIDSPIN], role: "Bulky Spinner mit Coverage", theme: :electric },
    { id: "iron_treads_rocks", species: :IRONTREADS, item: :LEFTOVERS, nature: :JOLLY, moves: [:STEALTHROCK, :EARTHQUAKE, :IRONHEAD, :KNOCKOFF], ev: [252, 4, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:PROTECT, :HIGHHORSEPOWER, :IRONHEAD, :KNOCKOFF], role: "Schneller Tarnstein-Support", theme: :electric },
    { id: "iron_bundle_booster", species: :IRONBUNDLE, item: :BOOSTERENERGY, nature: :TIMID, moves: [:FREEZEDRY, :HYDROPUMP, :ENCORE, :SUBSTITUTE], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:FREEZEDRY, :HYDROPUMP, :ENCORE, :PROTECT], role: "Booster-Angreifer mit Zugabe", theme: :special },
    { id: "iron_bundle_specs", species: :IRONBUNDLE, item: :CHOICESPECS, nature: :TIMID, moves: [:FREEZEDRY, :HYDROPUMP, :FLIPTURN, :ICEBEAM], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Elektrofeld-Specs-Pivot", theme: :electric },
    { id: "iron_bundle_sash", species: :IRONBUNDLE, item: :FOCUSSASH, nature: :TIMID, moves: [:ICYWIND, :FREEZEDRY, :HYDROPUMP, :TAUNT], ev: [0, 0, 4, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Sash-Angreifer mit Eissturm und Verhoehner", theme: :electric },
    { id: "iron_jugulis_booster", species: :IRONJUGULIS, item: :BOOSTERENERGY, nature: :TIMID, moves: [:SUBSTITUTE, :DARKPULSE, :AIRSLASH, :EARTHPOWER], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Booster-Speed-Angreifer", theme: :special },
    { id: "iron_jugulis_specs", species: :IRONJUGULIS, item: :CHOICESPECS, nature: :TIMID, moves: [:DARKPULSE, :AIRSLASH, :FLAMETHROWER, :UTURN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Specs- und Pivot-Angreifer", theme: :electric },
    { id: "iron_jugulis_support", species: :IRONJUGULIS, item: :HEAVYDUTYBOOTS, nature: :TIMID, moves: [:TAUNT, :UTURN, :DARKPULSE, :EARTHPOWER], ev: [252, 0, 0, 4, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Schneller Taunt- und Pivot-Support", theme: :electric },
    { id: "iron_thorns_dance", species: :IRONTHORNS, item: :LIFEORB, nature: :JOLLY, moves: [:DRAGONDANCE, :STONEEDGE, :WILDCHARGE, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:DRAGONDANCE, :STONEEDGE, :WILDCHARGE, :HIGHHORSEPOWER], role: "Dragon-Dance-Cleaner", theme: :electric },
    { id: "iron_thorns_assault", species: :IRONTHORNS, item: :ASSAULTVEST, nature: :ADAMANT, moves: [:STONEEDGE, :WILDCHARGE, :EARTHQUAKE, :ICEPUNCH], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :QUARKDRIVE, double_moves: [:STONEEDGE, :WILDCHARGE, :HIGHHORSEPOWER, :ICEPUNCH], role: "Bulky Vier-Attacken-Brecher", theme: :electric },
    { id: "iron_leaves_swords", species: :IRONLEAVES, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :PSYBLADE, :LEAFBLADE, :CLOSECOMBAT], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Schwerttanz-Wallbreaker", theme: :electric },
    { id: "iron_leaves_booster", species: :IRONLEAVES, item: :BOOSTERENERGY, nature: :JOLLY, moves: [:PSYBLADE, :LEAFBLADE, :CLOSECOMBAT, :MEGAHORN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Booster-Vierfach-Coverage", theme: :physical },
    { id: "iron_leaves_av", species: :IRONLEAVES, item: :ASSAULTVEST, nature: :ADAMANT, moves: [:PSYBLADE, :LEAFBLADE, :CLOSECOMBAT, :WILDCHARGE], ev: [252, 252, 4, 0, 0, 0], form: 0, ability: :QUARKDRIVE, role: "Bulky Assault-Vest-Kämpfer", theme: :electric },
    { id: "iron_boulder_dance", species: :IRONBOULDER, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :MIGHTYCLEAVE, :PSYCHOCUT, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:SWORDSDANCE, :MIGHTYCLEAVE, :PSYCHOCUT, :CLOSECOMBAT], role: "Schwerttanz-Kehrer", theme: :electric },
    { id: "iron_boulder_band", species: :IRONBOULDER, item: :CHOICEBAND, nature: :JOLLY, moves: [:MIGHTYCLEAVE, :PSYCHOCUT, :EARTHQUAKE, :MEGAHORN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:MIGHTYCLEAVE, :PSYCHOCUT, :CLOSECOMBAT, :MEGAHORN], role: "Band-Angreifer mit Coverage", theme: :electric },
    { id: "iron_boulder_booster", species: :IRONBOULDER, item: :BOOSTERENERGY, nature: :JOLLY, moves: [:TAUNT, :MIGHTYCLEAVE, :PSYCHOCUT, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, double_moves: [:TAUNT, :MIGHTYCLEAVE, :PSYCHOCUT, :CLOSECOMBAT], role: "Booster-Angreifer mit Verhoehner", theme: :physical },
    { id: "iron_crown_calm_mind", species: :IRONCROWN, item: :LEFTOVERS, nature: :MODEST, moves: [:CALMMIND, :PSYSHOCK, :TACHYONCUTTER, :FOCUSBLAST], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :QUARKDRIVE, role: "Robuster Gedankengut-Spezialbrecher", theme: :electric },
    { id: "iron_crown_booster", species: :IRONCROWN, item: :BOOSTERENERGY, nature: :TIMID, moves: [:PSYCHIC, :FOCUSBLAST, :TACHYONCUTTER, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Booster-Spezialbrecher", theme: :special },
    { id: "iron_crown_av", species: :IRONCROWN, item: :ASSAULTVEST, nature: :MODEST, moves: [:PSYCHIC, :TACHYONCUTTER, :FOCUSBLAST, :VOLTSWITCH], ev: [252, 0, 0, 252, 4, 0], form: 0, ability: :QUARKDRIVE, role: "Spezialtank und Pivot", theme: :electric },
    { id: "flutter_mane_calm_mind", species: :FLUTTERMANE, item: :BOOSTERENERGY, nature: :TIMID, moves: [:CALMMIND, :MOONBLAST, :SHADOWBALL, :MYSTICALFIRE], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Booster-Kehrer mit Gedankengut", theme: :special },
    { id: "great_tusk_bulk_up", species: :GREATTUSK, item: :BOOSTERENERGY, nature: :JOLLY, moves: [:BULKUP, :HEADLONGRUSH, :CLOSECOMBAT, :KNOCKOFF], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Booster-Protzer-Wallbreaker", theme: :physical },
    { id: "iron_hands_swords_dance", species: :IRONHANDS, item: :LEFTOVERS, nature: :ADAMANT, moves: [:SWORDSDANCE, :DRAINPUNCH, :WILDCHARGE, :ICEPUNCH], ev: [0, 252, 4, 0, 252, 0], form: 0, ability: :QUARKDRIVE, double_moves: [:SWORDSDANCE, :DRAINPUNCH, :WILDCHARGE, :PROTECT], role: "Schwerttanz-Tank mit Ableithieb", theme: :electric },
    { id: "iron_valiant_swords_dance", species: :IRONVALIANT, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :SPIRITBREAK, :CLOSECOMBAT, :KNOCKOFF], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Physischer Elektrofeld-Schwerttanzbrecher", theme: :electric },
    { id: "iron_moth_meteor_beam", species: :IRONMOTH, item: :POWERHERB, nature: :TIMID, moves: [:METEORBEAM, :FIERYDANCE, :ENERGYBALL, :DAZZLINGGLEAM], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :QUARKDRIVE, role: "Meteorstrahl-Spezialbrecher im Elektrofeld", theme: :electric },
    { id: "walking_wake_agility", species: :WALKINGWAKE, item: :LIFEORB, nature: :MODEST, moves: [:AGILITY, :HYDROSTEAM, :DRAGONPULSE, :WEATHERBALL], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTOSYNTHESIS, role: "Sonnen-Kehrer mit Agilitaet", theme: :sun },

    { id: "dragonite_dance", species: :DRAGONITE, item: :HEAVYDUTYBOOTS, nature: :ADAMANT, moves: [:DRAGONDANCE, :DUALWINGBEAT, :EXTREMESPEED, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :MULTISCALE, double_moves: [:DRAGONDANCE, :DUALWINGBEAT, :EXTREMESPEED, :PROTECT], role: "Multiscale-Setup mit Priorität", theme: :physical },
    { id: "garchomp_swords", species: :GARCHOMP, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :EARTHQUAKE, :DRAGONCLAW, :STONEEDGE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :ROUGHSKIN, double_moves: [:SWORDSDANCE, :DRAGONCLAW, :STONEEDGE, :PROTECT], role: "Setup-Wallbreaker", theme: :physical },
    { id: "kingambit_swords", species: :KINGAMBIT, item: :BLACKGLASSES, nature: :ADAMANT, moves: [:KOWTOWCLEAVE, :SUCKERPUNCH, :IRONHEAD, :SWORDSDANCE], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :SUPREMEOVERLORD, double_moves: [:KOWTOWCLEAVE, :SUCKERPUNCH, :IRONHEAD, :PROTECT], role: "Spätspiel-Cleaner", theme: :physical },
    { id: "gholdengo_choice", species: :GHOLDENGO, item: :CHOICESPECS, nature: :TIMID, moves: [:MAKEITRAIN, :SHADOWBALL, :FOCUSBLAST, :THUNDERBOLT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :GOODASGOLD, role: "Specs-Wallbreaker", theme: :special },
    { id: "dragapult_mixed", species: :DRAGAPULT, item: :LIFEORB, nature: :HASTY, moves: [:DRAGONDARTS, :UTURN, :FLAMETHROWER, :PHANTOMFORCE], ev: [0, 252, 0, 4, 0, 252], form: 0, ability: :INFILTRATOR, double_moves: [:DRAGONDARTS, :FLAMETHROWER, :PHANTOMFORCE, :PROTECT], role: "Schneller Mixed-Pivot", theme: :physical },
    { id: "volcarona_quiver", species: :VOLCARONA, item: :HEAVYDUTYBOOTS, nature: :TIMID, moves: [:QUIVERDANCE, :FIERYDANCE, :BUGBUZZ, :GIGADRAIN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :FLAMEBODY, double_moves: [:QUIVERDANCE, :HEATWAVE, :BUGBUZZ, :PROTECT], role: "Quiver-Dance-Sweeper", theme: :sun },
    { id: "rillaboom_grassy", species: :RILLABOOM, item: :CHOICEBAND, nature: :ADAMANT, moves: [:GRASSYGLIDE, :WOODHAMMER, :KNOCKOFF, :UTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :GRASSYSURGE, role: "Grasfeld-Prioritätsbrecher", theme: :physical },
    { id: "incineroar_pivot", species: :INCINEROAR, item: :SAFETYGOGGLES, nature: :CAREFUL, moves: [:FAKEOUT, :KNOCKOFF, :FLAREBLITZ, :UTURN], ev: [252, 4, 0, 0, 252, 0], form: 0, ability: :INTIMIDATE, double_moves: [:FAKEOUT, :KNOCKOFF, :FLAREBLITZ, :PROTECT], role: "Intimidate-Fake-Out-Pivot", theme: :bulky },
    { id: "urshifu_swords", species: :URSHIFU, item: :CHOICEBAND, nature: :JOLLY, moves: [:WICKEDBLOW, :CLOSECOMBAT, :SUCKERPUNCH, :UTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :UNSEENFIST, role: "Kritischer Choice-Breaker", theme: :physical },
    { id: "landorus_therian", species: :LANDORUS, item: :LIFEORB, nature: :JOLLY, moves: [:EARTHQUAKE, :UTURN, :STONEEDGE, :KNOCKOFF], ev: [4, 252, 0, 0, 0, 252], form: 1, ability: :INTIMIDATE, double_moves: [:EARTHPOWER, :ROCKSLIDE, :UTURN, :PROTECT], role: "Offensiver Intimidate-Pivot", theme: :physical },
    { id: "tornadus_therian", species: :TORNADUS, item: :LIFEORB, nature: :TIMID, moves: [:HURRICANE, :BLEAKWINDSTORM, :HEATWAVE, :UTURN], ev: [4, 0, 0, 252, 0, 252], form: 1, ability: :REGENERATOR, role: "Regenerator-Spezialpivot", theme: :rain },
    { id: "heatran_trap", species: :HEATRAN, item: :AIRBALLOON, nature: :TIMID, moves: [:MAGMASTORM, :EARTHPOWER, :FLASHCANNON, :TAUNT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :FLASHFIRE, double_moves: [:HEATWAVE, :EARTHPOWER, :FLASHCANNON, :PROTECT], role: "Schneller Stahl-Fallesteller", theme: :sun },
    { id: "raichu_alola_surfer", species: :RAICHU, item: :FOCUSSASH, nature: :TIMID, moves: [:THUNDERBOLT, :PSYCHIC, :FOCUSBLAST, :NASTYPLOT], ev: [4, 0, 0, 252, 0, 252], form: 1, ability: :SURGESURFER, double_moves: [:THUNDERBOLT, :PSYCHIC, :FOCUSBLAST, :PROTECT], role: "Surge Surfer Setup", theme: :electric },
    { id: "heliolisk_electric", species: :HELIOLISK, item: :WISEGLASSES, nature: :TIMID, moves: [:THUNDERBOLT, :HYPERVOICE, :GRASSKNOT, :VOLTSWITCH], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :DRYSKIN, role: "Specs Volt-Pivot", theme: :electric },
    { id: "kilowattrel_competitive", species: :KILOWATTREL, item: :HEAVYDUTYBOOTS, nature: :TIMID, moves: [:THUNDERBOLT, :HURRICANE, :VOLTSWITCH, :ROOST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :COMPETITIVE, double_moves: [:THUNDERBOLT, :HURRICANE, :VOLTSWITCH, :PROTECT], role: "Competitive Spezialpivot", theme: :electric },
    { id: "tapukoko_terrain", species: :TAPUKOKO, item: :LIFEORB, nature: :TIMID, moves: [:THUNDERBOLT, :DAZZLINGGLEAM, :UTURN, :ROOST], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :ELECTRICSURGE, double_moves: [:THUNDERBOLT, :DAZZLINGGLEAM, :UTURN, :PROTECT], role: "Elektrofeld-Pivot", theme: :electric },
    { id: "tapufini_calm", species: :TAPUFINI, item: :LEFTOVERS, nature: :CALM, moves: [:CALMMIND, :MOONBLAST, :SCALD, :TAUNT], ev: [252, 0, 0, 4, 252, 0], form: 0, ability: :MISTYSURGE, double_moves: [:MOONBLAST, :SCALD, :ICYWIND, :PROTECT], role: "Bulky Calm-Mind-Setup", theme: :bulky },
    { id: "tapulele_specs", species: :TAPULELE, item: :CHOICESPECS, nature: :TIMID, moves: [:PSYSHOCK, :MOONBLAST, :FOCUSBLAST, :SHADOWBALL], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PSYCHICSURGE, role: "Psychicfeld-Spezialbrecher", theme: :special },
    { id: "tapubulu_swords", species: :TAPUBULU, item: :CHOICEBAND, nature: :ADAMANT, moves: [:WOODHAMMER, :ZENHEADBUTT, :CLOSECOMBAT, :STONEEDGE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :GRASSYSURGE, role: "Grasfeld-Wallbreaker", theme: :physical },
    { id: "ninetales_sun", species: :NINETALES, item: :HEATROCK, nature: :TIMID, moves: [:SOLARBEAM, :HEATWAVE, :WILLOWISP, :OVERHEAT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :DROUGHT, double_moves: [:HEATWAVE, :SOLARBEAM, :WILLOWISP, :PROTECT], role: "Dürre-Sonnenstütze", theme: :sun },
    { id: "torkoal_sun", species: :TORKOAL, item: :HEATROCK, nature: :BOLD, moves: [:LAVAPLUME, :RAPIDSPIN, :STEALTHROCK, :YAWN], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :DROUGHT, double_moves: [:HEATWAVE, :EARTHPOWER, :YAWN, :PROTECT], role: "Dürre-Setter und Hazardkontrolle", theme: :sun },
    { id: "tyranitar_sand", species: :TYRANITAR, item: :CHOICEBAND, nature: :ADAMANT, moves: [:STONEEDGE, :CRUNCH, :DRAGONCLAW, :EARTHQUAKE], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :SANDSTREAM, double_moves: [:ROCKSLIDE, :CRUNCH, :STONEEDGE, :DRAGONCLAW], role: "Sandsturm-Wallbreaker", theme: :physical },
    { id: "excadrill_swords", species: :EXCADRILL, item: :LIFEORB, nature: :JOLLY, moves: [:EARTHQUAKE, :IRONHEAD, :SWORDSDANCE, :RAPIDSPIN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :MOLDBREAKER, double_moves: [:HIGHHORSEPOWER, :IRONHEAD, :ROCKSLIDE, :PROTECT], role: "Mold Breaker Cleaner", theme: :physical },
    { id: "corviknight_defog", species: :CORVIKNIGHT, item: :LEFTOVERS, nature: :IMPISH, moves: [:IRONDEFENSE, :BODYPRESS, :BRAVEBIRD, :UTURN], ev: [252, 0, 168, 0, 88, 0], form: 0, ability: :PRESSURE, double_moves: [:IRONDEFENSE, :BODYPRESS, :BRAVEBIRD, :PROTECT], role: "Defensiver Pivot", theme: :bulky },
    { id: "clefable_boost", species: :CLEFABLE, item: :LEFTOVERS, nature: :BOLD, moves: [:CALMMIND, :MOONBLAST, :FLAMETHROWER, :SOFTBOILED], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :MAGICGUARD, double_moves: [:MOONBLAST, :FLAMETHROWER, :SOFTBOILED, :PROTECT], role: "Magic Guard Setup-Wall", theme: :bulky },
    { id: "ferrothorn_hazards", species: :FERROTHORN, item: :LEFTOVERS, nature: :RELAXED, moves: [:SPIKES, :POWERWHIP, :GYROBALL, :STEALTHROCK], ev: [252, 0, 88, 0, 168, 0], form: 0, ability: :IRONBARBS, double_moves: [:POWERWHIP, :GYROBALL, :SPIKES, :PROTECT], role: "Hazard-Wall", theme: :bulky },
    { id: "rotomwash_pivot", species: :ROTOM, item: :LEFTOVERS, nature: :BOLD, moves: [:HYDROPUMP, :THUNDERBOLT, :WILLOWISP, :PAINSPLIT], ev: [252, 0, 200, 56, 0, 0], form: 2, ability: :LEVITATE, double_moves: [:HYDROPUMP, :THUNDERBOLT, :WILLOWISP, :PROTECT], role: "Wash Form Bulky Pivot", theme: :special },
    { id: "scizor_swords", species: :SCIZOR, item: :LIFEORB, nature: :ADAMANT, moves: [:SWORDSDANCE, :BULLETPUNCH, :UTURN, :CLOSECOMBAT], ev: [248, 252, 0, 0, 0, 8], form: 0, ability: :TECHNICIAN, double_moves: [:SWORDSDANCE, :BULLETPUNCH, :UTURN, :PROTECT], role: "Technician-Prioritäts-Sweeper", theme: :physical },
    { id: "breloom_spore", species: :BRELOOM, item: :FOCUSSASH, nature: :JOLLY, moves: [:BULLETSEED, :SWORDSDANCE, :MACHPUNCH, :CLOSECOMBAT], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :TECHNICIAN, double_moves: [:BULLETSEED, :MACHPUNCH, :CLOSECOMBAT, :PROTECT], role: "Technician-Prioritäts-Setup", theme: :physical },
    { id: "gliscor_poisonheal", species: :GLISCOR, item: :TOXICORB, nature: :JOLLY, moves: [:EARTHQUAKE, :ROOST, :TOXIC, :KNOCKOFF], ev: [244, 0, 0, 0, 12, 252], form: 0, ability: :POISONHEAL, double_moves: [:KNOCKOFF, :TOXIC, :ROOST, :PROTECT], role: "Poison-Heal Utility-Wall", theme: :bulky },
    { id: "hydreigon_nasty", species: :HYDREIGON, item: :LIFEORB, nature: :TIMID, moves: [:NASTYPLOT, :DARKPULSE, :DRACOMETEOR, :FLASHCANNON], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :LEVITATE, double_moves: [:DARKPULSE, :DRACOMETEOR, :HEATWAVE, :PROTECT], role: "Nasty Plot Wallbreaker", theme: :special },
    { id: "mimikyu_swords", species: :MIMIKYU, item: :LIFEORB, nature: :JOLLY, moves: [:SWORDSDANCE, :PLAYROUGH, :SHADOWSNEAK, :DRAINPUNCH], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :DISGUISE, double_moves: [:SWORDSDANCE, :PLAYROUGH, :SHADOWSNEAK, :PROTECT], role: "Disguise-Setup-Sweeper", theme: :physical },
    { id: "aegislash_king", species: :AEGISLASH, item: :SPELLTAG, nature: :QUIET, moves: [:SHADOWBALL, :FLASHCANNON, :SHADOWSNEAK, :KINGSSHIELD], ev: [252, 0, 4, 252, 0, 0], form: 0, ability: :STANCECHANGE, double_moves: [:SHADOWBALL, :FLASHCANNON, :SHADOWSNEAK, :KINGSSHIELD], role: "Stance-Wechsel-Breaker", theme: :special },
    { id: "kommoo_clangorous", species: :KOMMOO, item: :THROATSPRAY, nature: :TIMID, moves: [:CLANGOROUSSOUL, :CLANGINGSCALES, :AURASPHERE, :FLASHCANNON], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BULLETPROOF, double_moves: [:CLANGINGSCALES, :AURASPHERE, :FLASHCANNON, :PROTECT], role: "Clangorous Soul Setup", theme: :special },
    { id: "primarina_specs", species: :PRIMARINA, item: :CHOICESPECS, nature: :MODEST, moves: [:HYPERVOICE, :MOONBLAST, :ICEBEAM, :PSYCHIC], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :LIQUIDVOICE, role: "Liquid Voice Specs-Breaker", theme: :special },
    { id: "greninja_lifeorb", species: :GRENINJA, item: :LIFEORB, nature: :NAIVE, moves: [:HYDROPUMP, :DARKPULSE, :ICEBEAM, :UTURN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :PROTEAN, role: "Vielseitiger Mixed-Angreifer", theme: :special },
    { id: "meowscarada_protean", species: :MEOWSCARADA, item: :CHOICEBAND, nature: :JOLLY, moves: [:FLOWERTRICK, :KNOCKOFF, :PLAYROUGH, :UTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :PROTEAN, role: "Choice-Pivot mit garantiertem Krit", theme: :physical },
    { id: "baxcalibur_dance", species: :BAXCALIBUR, item: :LOADEDDICE, nature: :JOLLY, moves: [:ICICLESPEAR, :GLAIVERUSH, :SWORDSDANCE, :EARTHQUAKE], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :THERMALEXCHANGE, double_moves: [:ICICLESPEAR, :GLAIVERUSH, :SWORDSDANCE, :PROTECT], role: "Thermal Exchange Setup", theme: :physical },
    { id: "annihilape_ragefist", species: :ANNIHILAPE, item: :CHESTOBERRY, nature: :ADAMANT, moves: [:RAGEFIST, :DRAINPUNCH, :BULKUP, :REST], ev: [252, 252, 0, 0, 4, 0], form: 0, ability: :DEFIANT, double_moves: [:RAGEFIST, :DRAINPUNCH, :BULKUP, :REST], role: "ChestoRest Rage Fist Setup", theme: :bulky },
    { id: "garganacl_saltcure", species: :GARGANACL, item: :LEFTOVERS, nature: :CAREFUL, moves: [:SALTCURE, :RECOVER, :IRONDEFENSE, :BODYPRESS], ev: [252, 0, 4, 0, 252, 0], form: 0, ability: :PURIFYINGSALT, double_moves: [:SALTCURE, :BODYPRESS, :RECOVER, :PROTECT], role: "Salt-Cure-Wall", theme: :bulky },
    { id: "ceruledge_swords", species: :CERULEDGE, item: :LIFEORB, nature: :JOLLY, moves: [:BITTERBLADE, :SHADOWSNEAK, :SWORDSDANCE, :CLOSECOMBAT], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :WEAKARMOR, double_moves: [:BITTERBLADE, :SHADOWSNEAK, :SWORDSDANCE, :PROTECT], role: "Swords Dance Sustain-Sweeper", theme: :sun },
    { id: "skeledirge_torchsong", species: :SKELEDIRGE, item: :HEAVYDUTYBOOTS, nature: :BOLD, moves: [:TORCHSONG, :SHADOWBALL, :WILLOWISP, :EARTHPOWER], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :UNAWARE, double_moves: [:TORCHSONG, :SHADOWBALL, :WILLOWISP, :PROTECT], role: "Unaware-Defensivanker", theme: :bulky },
    { id: "lugia_multiscale", species: :LUGIA, item: :HEAVYDUTYBOOTS, nature: :BOLD, moves: [:ROOST, :TOXIC, :WHIRLWIND, :PSYCHIC], ev: [252, 0, 252, 4, 0, 0], form: 0, ability: :MULTISCALE, double_moves: [:ROOST, :PSYCHIC, :ICYWIND, :PROTECT], role: "Multiscale-Phazer", theme: :bulky },
    { id: "hooh_regenerator", species: :HOOH, item: :HEAVYDUTYBOOTS, nature: :CAREFUL, moves: [:SACREDFIRE, :BRAVEBIRD, :ROOST, :TOXIC], ev: [248, 0, 0, 0, 252, 8], form: 0, ability: :REGENERATOR, double_moves: [:SACREDFIRE, :BRAVEBIRD, :ROOST, :PROTECT], role: "Regenerator-Spezialwall", theme: :sun },
    { id: "latios_specs", species: :LATIOS, item: :CHOICESPECS, nature: :TIMID, moves: [:DRACOMETEOR, :PSYSHOCK, :SURF, :AURASPHERE], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :LEVITATE, double_moves: [:DRACOMETEOR, :PSYSHOCK, :THUNDERBOLT, :AURASPHERE], role: "Specs-Speedbreaker", theme: :special },
    { id: "manaphy_tailglow", species: :MANAPHY, item: :LEFTOVERS, nature: :TIMID, moves: [:TAILGLOW, :SURF, :ICEBEAM, :ENERGYBALL], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :HYDRATION, double_moves: [:TAILGLOW, :SCALD, :ICEBEAM, :PROTECT], role: "Tail Glow Wallbreaker", theme: :rain },
    { id: "darkrai_nasty", species: :DARKRAI, item: :LIFEORB, nature: :TIMID, moves: [:DARKPULSE, :SLUDGEBOMB, :FOCUSBLAST, :NASTYPLOT], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :BADDREAMS, role: "Schneller Nasty Plot Sweeper", theme: :special },
    { id: "genesect_scaleshot", species: :GENESECT, item: :CHOICESCARF, nature: :TIMID, moves: [:BUGBUZZ, :FLASHCANNON, :ICEBEAM, :UTURN], ev: [4, 0, 0, 252, 0, 252], form: 0, ability: :DOWNLOAD, role: "Scarf-Pivot mit Coverage", theme: :special },
    { id: "victini_choice", species: :VICTINI, item: :CHOICEBAND, nature: :JOLLY, moves: [:VCREATE, :ZENHEADBUTT, :BOLTSTRIKE, :UTURN], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :VICTORYSTAR, role: "Choice-Wallbreaker", theme: :sun },
    { id: "zeraora_pivot", species: :ZERAORA, item: :EXPERTBELT, nature: :JOLLY, moves: [:PLASMAFISTS, :CLOSECOMBAT, :KNOCKOFF, :VOLTSWITCH], ev: [4, 252, 0, 0, 0, 252], form: 0, ability: :VOLTABSORB, double_moves: [:PLASMAFISTS, :CLOSECOMBAT, :KNOCKOFF, :PROTECT], role: "Schneller Volt-Pivot", theme: :electric },
  ]
end

module REDiscoGegnerPool
  # One third of regular tower opponents use a curated roster. Round-five
  # bosses, rental formats, rewards, IV scaling and trainer identities stay native.
  CHANCE_DENOMINATOR = 3 unless const_defined?(:CHANCE_DENOMINATOR, false)
  DATA_FIELDS = [:species, :item, :nature, :moves, :ev, :form, :ability].freeze unless const_defined?(:DATA_FIELDS, false)

  def self.enabled?(challenge, cache)
    defined?(REDiscoOhneBans) && StandardRestriction.ancestors.include?(REDiscoOhneBans) &&
      ['towersingle', 'towerdouble'].include?(challenge.currentChallenge) &&
      challenge.battleNumber != 5 && !cache.instance_variable_get(:@re_disco_pool_active)
  end

  def self.roll
    rand(CHANCE_DENOMINATOR)
  end

  def self.battle_rows(double)
    REDiscoPoolData::SETS.map do |row|
      double && row[:double_moves] ? row.merge(moves: row[:double_moves]) : row
    end
  end

  def self.weather_lead(rows)
    if rows.first[:theme] == :rain
      rows.find { |r| r[:ability] == :DRIZZLE }
    elsif rows.first[:theme] == :sun
      rows.find { |r| [:ORICHALCUMPULSE, :DROUGHT].include?(r[:ability]) }
    elsif rows.first[:theme] == :electric
      rows.find { |r| [:HADRONENGINE, :ELECTRICSURGE].include?(r[:ability]) }
    end
  end

  def self.fill_team(group, locked, number)
    return nil if locked.size > number
    return locked if locked.size == number
    species = locked.map { |r| r[:species] }
    items = locked.map { |r| r[:item] }.compact
    candidates = group.reject { |r| species.include?(r[:species]) || (r[:item] && items.include?(r[:item])) }
    # Species-to-item matching avoids enumerating millions of combinations.
    # An augmenting path can move an earlier species to another legal item.
    by_species = candidates.group_by { |r| r[:species] }
    by_species.each_value(&:shuffle!)
    matched = {}
    assign = lambda do |mon, seen|
      by_species.fetch(mon).each do |row|
        key = row[:item] || [:no_item, mon]
        next if seen[key]
        seen[key] = true
        if !matched[key] || assign.call(matched[key][:species], seen)
          matched[key] = row
          return true
        end
      end
      false
    end
    by_species.keys.shuffle.each do |mon|
      assign.call(mon, {})
      return locked + matched.values.shuffle if locked.size + matched.size == number
    end
    nil
  end

  def self.select_team(number, double = false, anchor_id = nil)
    # Every species can anchor a team; several variants do not give a species
    # extra weight. A forced anchor is also used by the reachability checks.
    groups = battle_rows(double).group_by { |r| r[:theme] }.values.shuffle
    groups.each do |group|
      anchors = if anchor_id
        group.select { |r| r[:id] == anchor_id }
      else
        group.group_by { |r| r[:species] }.values.shuffle.flat_map(&:shuffle)
      end
      anchors.each do |anchor|
        # Environment setters that can disable allies' plans must be the
        # anchor, so compatibility is decided before item/species matching.
        disruptive = [:PRIMORDIALSEA, :MISTYSURGE, :PSYCHICSURGE, :GRASSYSURGE]
        eligible = group.reject do |r|
          next true if disruptive.include?(r[:ability]) && r[:ability] != anchor[:ability]
          case anchor[:ability]
          when :PRIMORDIALSEA
            r[:moves].any? { |m| $cache.moves[m].type == :FIRE }
          when :MISTYSURGE
            (r[:moves] & [:REST, :SPORE, :SLEEPPOWDER, :HYPNOSIS, :TOXIC, :WILLOWISP, :THUNDERWAVE, :YAWN]).any?
          when :PSYCHICSURGE
            (r[:moves] & [:EXTREMESPEED, :FAKEOUT, :SUCKERPUNCH, :THUNDERCLAP, :SHADOWSNEAK, :MACHPUNCH, :BULLETPUNCH, :AQUAJET, :GRASSYGLIDE, :FIRSTIMPRESSION, :ICESHARD]).any?
          when :GRASSYSURGE
            (r[:moves] & [:EARTHQUAKE, :MAGNITUDE, :BULLDOZE]).any?
          else
            false
          end
        end
        needs_lead = [:sun, :rain, :electric].include?(anchor[:theme])
        leaders = needs_lead ? eligible.select { |r| weather_lead([r]) }.shuffle : [nil]
        leaders = [anchor] if needs_lead && weather_lead([anchor])
        leaders.each do |lead|
          locked = [lead, anchor].compact.uniq
          next if locked.map { |r| r[:species] }.uniq.size != locked.size
          held = locked.map { |r| r[:item] }.compact
          next if held.uniq.size != held.size
          team = fill_team(eligible, locked, number)
          return team if team
        end
      end
    end
    nil
  end

  def self.with_team(cache, trainerid, rows)
    original_mons = cache.btmons
    original_trainers = cache.bttrainers
    expanded_mons = original_mons.dup
    trainer = original_trainers.fetch(trainerid).dup
    trainer.monIndexes = rows.map do |row|
      key = "RE_DISCO_#{row.fetch(:id)}".to_sym
      raise "RE-Disco: Pool-Schlüssel bereits vorhanden: #{key}" if expanded_mons.key?(key)
      expanded_mons[key] = [BTPokemon.new(row.select { |field, _| DATA_FIELDS.include?(field) })]
      key
    end
    expanded_trainers = original_trainers.dup
    expanded_trainers[trainerid] = trainer
    begin
      cache.instance_variable_set(:@re_disco_pool_active, true)
      cache.instance_variable_set(:@btmons, expanded_mons)
      cache.instance_variable_set(:@bttrainers, expanded_trainers)
      yield
    ensure
      cache.instance_variable_set(:@btmons, original_mons)
      cache.instance_variable_set(:@bttrainers, original_trainers)
      cache.instance_variable_set(:@re_disco_pool_active, false)
    end
  end

  module Generation
    def pbGenerateBattleTrainer(trainerid, rule)
      challenge = pbBattleChallenge
      return super unless REDiscoGegnerPool.enabled?(challenge, $cache)
      return super unless REDiscoGegnerPool.roll == 0

      number = rule.ruleset.suggestedNumber
      rows = REDiscoGegnerPool.select_team(number, challenge.currentChallenge == 'towerdouble')
      raise "RE-Disco: Kein gültiges Team für #{number} Pokémon hinterlegt." unless rows
      opponent = REDiscoGegnerPool.with_team($cache, trainerid, rows) { super }
      if (lead = REDiscoGegnerPool.weather_lead(rows))
        index = opponent.party.index { |pokemon| pokemon.species == lead[:species] }
        opponent.party.unshift(opponent.party.delete_at(index)) if index
      end
      opponent
    end
  end
end

Object.prepend(REDiscoGegnerPool::Generation) unless Object.ancestors.include?(REDiscoGegnerPool::Generation)
