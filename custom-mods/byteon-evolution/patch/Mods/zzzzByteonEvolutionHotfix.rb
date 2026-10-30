# Additive fix for Eevee evolving into Byteon in the AllGen 0.9.9 glitch areas.
# Location evolution must precede friendship evolutions only in those areas.

alias __byteon_hotfix_original_pbGetEvolvedFormData pbGetEvolvedFormData

def pbGetEvolvedFormData(species, pokemon = nil)
  evolutions = __byteon_hotfix_original_pbGetEvolvedFormData(species, pokemon)
  return evolutions if species != :EEVEE || evolutions.nil?
  if $game_switches && $game_switches[:Randomized_Challenge] &&
      $Randomizer && $Randomizer.randomEvolutions
    return evolutions
  end
  unless evolutions.any? { |evo| evo[:species] == :BYTEON }
    evolutions = evolutions + [{ :species => :BYTEON, :method => :Location, :parameter => 443 }]
  end
  if defined?(Byteon) && $game_map && Byteon.include?($game_map.map_id)
    glitch, others = evolutions.partition { |evo| evo[:species] == :BYTEON && evo[:method] == :Location }
    return glitch + others
  end
  evolutions
end
