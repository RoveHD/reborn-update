# encoding: UTF-8
# Remove the facility's balance bans for both player and opponent Pokemon.
# Existing opponent pools and separate team/level/battle rules stay unchanged.
module REDiscoOhneBans
  def isValid?(pokemon)
    return false if !pokemon || pokemon.isEgg?
    return false if $game_switches[:Nuzlocke_Mode] == true && pokemon.hp == 0

    true
  end
end

# Prepend survives base/AllGen class redefinitions during F12 reloads.
StandardRestriction.prepend(REDiscoOhneBans) unless StandardRestriction.ancestors.include?(REDiscoOhneBans)
