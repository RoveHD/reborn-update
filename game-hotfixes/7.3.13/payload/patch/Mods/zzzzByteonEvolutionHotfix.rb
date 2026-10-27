# Additive fix for Eevee evolving into Byteon in the AllGen 0.9.9 glitch areas.
# Loaded after the base scripts; preserve any existing randomized evolution behavior.

alias __byteon_hotfix_original_pbGetEvolvedFormData pbGetEvolvedFormData
def pbGetEvolvedFormData(species, pokemon = nil)
  evolutions = __byteon_hotfix_original_pbGetEvolvedFormData(species, pokemon)
  return evolutions if species != :EEVEE || evolutions.nil?
  if $game_switches && $game_switches[:Randomized_Challenge] &&
      $Randomizer && $Randomizer.randomEvolutions
    return evolutions
  end
  return evolutions if evolutions.any? { |evo| evo[:species] == :BYTEON }

  evolutions + [{ :species => :BYTEON, :method => :Location, :parameter => 443 }]
end
