# =========================
# Pickup beim Laufen + Step Counter (SAFE)
# =========================

PICKUP_STEP_INTERVAL = 100     # Test: alle 10 Schritte
PICKUP_CHANCE = 3       # Test: 100% Chance

$pickup_steps ||= 0
$pickup_messages ||= []

# =========================
# Step Event
# =========================

Events.onStepTaken += proc {
  $pickup_steps += 1

  if $pickup_steps >= PICKUP_STEP_INTERVAL
  $pickup_steps = 0
  pbPickupOverworld
end
}

# =========================
# Pickup Logik
# =========================

def pbPickupOverworld
  return if !$Trainer || !$Trainer.party

  messages = []

  $Trainer.party.each do |pkmn|
    next if pkmn.egg?
    next unless pkmn.ability == :PICKUP

    if rand(100) < PICKUP_CHANCE
      item = pbPickupItem(pkmn.level)
      next if !item

      $PokemonBag.pbStoreItem(item)


      # 👉 Nachricht sammeln
      messages.push(_INTL("{1} hat {2} gefunden.", pkmn.name, getItemName(item)))
    end
  end

  # 👉 EINMAL anzeigen (wie dein Shop)
  if messages.length > 0
    Kernel.pbMessage(messages.join("\n"))
  end
end

def pbPickupItem(level)
  table = case level
  when 1..10
    [
      [:ORANBERRY,30], [:GREATBALL,10], [:SUPERREPEL,10], [:POKESNAX,10],
      [:CHOCOICECREAM,10], [:BLASTPOWDER,10], [:DUSKBALL,10],
      [:ULTRAPOTION,4], [:MAXREPEL,4], [:NUGGET,1], [:STRAWBERRYICECREAM,1]
    ]
  when 11..20
    [
      [:GREATBALL,30], [:SUPERREPEL,10], [:POKESNAX,10], [:CHOCOICECREAM,10],
      [:BLASTPOWDER,10], [:DUSKBALL,10], [:ULTRAPOTION,10],
      [:MAXREPEL,4], [:FULLRESTORE,4], [:STRAWBERRYICECREAM,1], [:NUGGET,1]
    ]
  when 21..30
    [
      [:SUPERREPEL,30], [:POKESNAX,10], [:CHOCOICECREAM,10], [:BLASTPOWDER,10],
      [:DUSKBALL,10], [:ULTRAPOTION,10], [:MAXREPEL,10],
      [:FULLRESTORE,4], [:REVIVE,4], [:NUGGET,1], [:RARECANDY,1]
    ]
  when 31..40
    [
      [:POKESNAX,30], [:CHOCOICECREAM,10], [:BLASTPOWDER,10], [:DUSKBALL,10],
      [:ULTRAPOTION,10], [:MAXREPEL,10], [:FULLRESTORE,10],
      [:REVIVE,4], [:ETHER,4], [:RARECANDY,1], [:BLUEMOONICECREAM,1]
    ]
  when 41..50
    [
      [:CHOCOICECREAM,30], [:BLASTPOWDER,10], [:DUSKBALL,10], [:ULTRAPOTION,10],
      [:MAXREPEL,10], [:FULLRESTORE,10], [:REVIVE,10],
      [:ETHER,4], [:PPUP,4], [:BLUEMOONICECREAM,1], [:RARECANDY,1]
    ]
  when 51..60
    [
      [:BLASTPOWDER,30], [:DUSKBALL,10], [:ULTRAPOTION,10], [:MAXREPEL,10],
      [:FULLRESTORE,10], [:REVIVE,10], [:ETHER,10],
      [:PPUP,4], [:HEARTSCALE,4], [:RARECANDY,1], [:BLUEMOONICECREAM,1]
    ]
  when 61..70
    [
      [:DUSKBALL,30], [:ULTRAPOTION,10], [:MAXREPEL,10], [:FULLRESTORE,10],
      [:REVIVE,10], [:ETHER,10], [:PPUP,10],
      [:HEARTSCALE,4], [:ABILITYCAPSULE,4], [:BLUEMOONICECREAM,1], [:BIGNUGGET,1]
    ]
  when 71..80
    [
      [:ULTRAPOTION,30], [:MAXREPEL,10], [:FULLRESTORE,10], [:REVIVE,10],
      [:ETHER,10], [:PPUP,10], [:HEARTSCALE,10],
      [:ABILITYCAPSULE,4], [:HEARTSCALE,4], [:BIGNUGGET,1], [:LEFTOVERS,1]
    ]
  when 81..90
    [
      [:MAXREPEL,30], [:FULLRESTORE,10], [:REVIVE,10], [:ETHER,10],
      [:PPUP,10], [:HEARTSCALE,10], [:ABILITYCAPSULE,10],
      [:HEARTSCALE,4], [:BIGNUGGET,4], [:LEFTOVERS,1], [:LUCKYEGG,1]
    ]
  else
    [
      [:FULLRESTORE,30], [:REVIVE,10], [:ETHER,10], [:PPUP,10],
      [:HEARTSCALE,10], [:ABILITYCAPSULE,10], [:HEARTSCALE,10],
      [:BIGNUGGET,4], [:SACREDASH,4], [:LUCKYEGG,1], [:LEFTOVERS,1]
    ]
  end

  roll = rand(100)
  sum = 0

  table.each do |item, chance|
    sum += chance
    return item if roll < sum
  end

  return table[0][0]
end