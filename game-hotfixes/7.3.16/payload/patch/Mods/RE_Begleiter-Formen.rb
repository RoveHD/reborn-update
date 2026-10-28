#===============================================================================
# Pokemon Form-Changing Items Database
#===============================================================================
# This module defines which items trigger form changes for specific Pokemon.
# Used by the follower system to display the correct form sprite.
#===============================================================================

module PokemonFormItems
  # Returns the form number if the Pokemon is holding a form-changing item or knows a form-changing move
  # Returns nil if no form-changing item/move is found
  def self.getFormFromItem(pkmn)
    return nil if !pkmn
    
    dex = pbFollowerDexNumber(pkmn) rescue nil
    return nil if !dex
    
    # First check for move-based forms
    move_form = getFormFromMove(pkmn)
    return move_form if move_form
    
    # Then check for item-based forms
    return nil if !pkmn.respond_to?(:item)
    item = pkmn.item
    return nil if !item || item == 0
    
    item_name = getItemName(item)
    return nil if !item_name
    
    # Check the form-changing items database
    form_data = FORM_ITEMS[dex]
    return nil if !form_data
    
    # Iterate through the form items for this species
    # Support formats:
    #  - form_num => "KEYWORD" or ["KEYWORD1","KEYWORD2"] (backwards compatible)
    #  - form_num => { keywords: "KEYWORD" or [..], required_form: <int> }
    #    If :required_form is provided, the Pokemon's current form must match.
    form_data.each do |form_num, item_keywords|
      required_form = nil
      keywords = nil
      if item_keywords.is_a?(Hash)
        keywords = item_keywords[:keywords]
        required_form = item_keywords[:required_form]
      else
        keywords = item_keywords
      end
      keywords = keywords.is_a?(Array) ? keywords : [keywords]
      keywords.each do |keyword|
        next if !keyword
        if item_name.include?(keyword.upcase)
          pkmn_form = (pkmn.respond_to?(:form) ? pkmn.form : (pkmn.form rescue 0)) || 0
          return form_num if required_form.nil? || required_form == pkmn_form
        end
      end
    end
    
    return nil
  end
  
  # Returns the form number if the Pokemon knows a form-changing move
  # Returns nil if no form-changing move is found
  def self.getFormFromMove(pkmn)
    return nil if !pkmn
    
    dex = pbFollowerDexNumber(pkmn) rescue nil
    return nil if !dex
    
    # Check the move-based forms database
    form_data = FORM_MOVES[dex]
    return nil if !form_data
    
    # Check if the Pokemon knows any of the form-changing moves
    # Try multiple ways to access moves
    moves = nil
    if pkmn.respond_to?(:moves)
      moves = pkmn.moves
    elsif pkmn.respond_to?(:getMoveList)
      moves = pkmn.getMoveList rescue nil
    end
    
    return nil if !moves || moves.empty?
    
    form_data.each do |form_num, move_keywords|
      keywords = move_keywords.is_a?(Array) ? move_keywords : [move_keywords]
      moves.each do |move|
        next if !move
        move_name = nil
        
        # Get move name - try multiple approaches
        if move.respond_to?(:name)
          move_name = move.name.to_s.upcase
        elsif move.respond_to?(:id)
          move_id = move.id
          if defined?(PBMoves)
            move_name = PBMoves.getName(move_id).to_s.upcase rescue nil
          end
          if !move_name && defined?(GameData) && GameData.respond_to?(:Move)
            move_data = GameData::Move.try_get(move_id)
            move_name = move_data.name.to_s.upcase if move_data && move_data.respond_to?(:name)
          end
        elsif move.is_a?(Integer) || move.is_a?(Symbol)
          # Move stored as ID directly
          if defined?(PBMoves)
            move_name = PBMoves.getName(move).to_s.upcase rescue nil
          end
        end
        
        next if !move_name
        
        # Remove spaces from move name for comparison (handles "SECRET SWORD" vs "SECRETSWORD")
        move_name_no_space = move_name.gsub(" ", "")
        
        # Check if ALL keywords are present in the move name
        all_match = keywords.all? { |keyword| move_name_no_space.include?(keyword.upcase) }
        return form_num if all_match
      end
    end
    
    return nil
  end
  
  # Helper method to get item name
  def self.getItemName(item)
    item_name = nil
    begin
      if item.is_a?(Symbol)
        item_name = item.to_s.upcase
      elsif item.respond_to?(:name)
        item_name = item.name.to_s.upcase
      elsif defined?(PBItems) && item.is_a?(Integer)
        item_name = PBItems.getName(item).to_s.upcase rescue nil
      elsif defined?(GameData) && GameData.respond_to?(:Item)
        item_data = GameData::Item.try_get(item)
        item_name = item_data.name.to_s.upcase if item_data && item_data.respond_to?(:name)
      end
    rescue
    end
    return item_name
  end
  
  #=============================================================================
  # Form-Changing Items Database
  #=============================================================================
  # Format: Pokedex_Number => { form_number => "ITEM_KEYWORD" or ["KEYWORD1", "KEYWORD2"] }
  #
  # The item_keyword should be a unique part of the item name (case-insensitive)
  # Multiple keywords can be provided as an array if needed
  #=============================================================================
  
  FORM_ITEMS = {
    # Venusaur - Mega Forms
    3 => {
      2 => ["VENUSAURITE G", "VENUSAURITEG"],  # Mega Venusaur G (check G version first)
      1 => ["VENUSAURITE"]                       # Mega Venusaur
    },
    
    # Charizard - Mega Forms
    6 => {
      1 => ["CHARIZARDITEX", "CHARIZARDITE X"],  # Mega Charizard X
      2 => ["CHARIZARDITEY", "CHARIZARDITE Y"],   # Mega Charizard Y
      3 => ["CHARIZARDITE G", "CHARIZARDITEG"]    # Mega Charizard G
    },
    
    # Blastoise - Mega Forms
    9 => {
      2 => ["BLASTOISINITE G", "BLASTOISINITEG"],  # Mega Blastoise G (check G version first)
      1 => ["BLASTOISINITE"]                        # Mega Blastoise
    },
    
    # Slowbro - Mega Form (form 2 because Alolan is form 1)
    80 => {
      2 => ["SLOWBRONITE", "SLOWBROITE"]  # Mega Slowbro (uses form 2 to accommodate Alolan form)
    },
    445 => {
      2 => ["GARCHOMPITEZ"]  # Mega Slowbro (uses form 2 to accommodate Alolan form)
    },   
    # Gengar - Mega Forms
    94 => {
      2 => ["GENGARITE G", "GENGARITEG"],  # Mega Gengar G (check G version first)
      1 => ["GENGARITE"]                     # Mega Gengar
    },
	149 => {
      1 => ["Dragoninite"]   
    },	  
	655 => {
      1 => ["Delphoxite"]                     
    },    
    # Mewtwo - Mega Forms
    150 => {
      1 => ["MEWTWONITEX", "MEWTWONITE X"],    # Mega Mewtwo X
      2 => ["MEWTWONITEY", "MEWTWONITE Y"]     # Mega Mewtwo Y
    },
    
    # Groudon - Primal Form (Red Orb)
    382 => {
      1 => ["RED", "ORB"]
    },
    
    # Kyogre - Primal Form (Blue Orb)
    383 => {
      1 => ["BLUE", "ORB"]
    },
    
    # Rayquaza - Mega Form (Green Orb)
    384 => {
      1 => ["GREEN", "ORB"]
    },
    
    # Dialga - Primal Form (Adamant Orb)
    483 => {
      1 => ["ADAMANT", "ORB"]  # Requires both "ADAMANT" and "ORB" in item name
    },
    
    # Palkia - Primal Form (Lustrous Orb)
    484 => {
      1 => ["LUSTROUS", "ORB"]
    },
    
    # Giratina - Origin Form (Griseous Orb)
    487 => {
      1 => ["GRISEOUS", "ORB"]
    },
    
    # Arceus - Plate Forms
    493 => {
      1  => "FIST PLATE",      # Fighting
      2  => "SKY PLATE",       # Flying
      3  => "TOXIC PLATE",     # Poison
      4  => "EARTH PLATE",     # Ground
      5  => "STONE PLATE",     # Rock
      6  => "INSECT PLATE",    # Bug
      7  => "SPOOKY PLATE",    # Ghost
      8  => "IRON PLATE",      # Steel
      9  => "FLAME PLATE",     # Fire
      10 => "SPLASH PLATE",    # Water
      11 => "MEADOW PLATE",    # Grass
      12 => "ZAP PLATE",       # Electric
      13 => "MIND PLATE",      # Psychic
      14 => "ICICLE PLATE",    # Ice
      15 => "DRACO PLATE",     # Dragon
      16 => "DREAD PLATE",     # Dark
      17 => "PIXIE PLATE"      # Fairy
    },
    
    # Rotom - Appliance Forms
    479 => {
      1 => "HEAT",      # Heat Rotom (Microwave)
      2 => "WASH",      # Wash Rotom (Washing Machine)
      3 => "FROST",     # Frost Rotom (Refrigerator)
      4 => "FAN",       # Fan Rotom (Electric Fan)
      5 => "MOW"        # Mow Rotom (Lawnmower)
    },
	26 => {
      2 => ["RAICHUNITEX", "RAICHUNITE X"],
      3 => ["RAICHUNITEY", "RAICHUNITE Y"]
    },
    68 => {
      1 => "MACHAMPITE"
    },
    # Genesect - Drive Forms
    649 => {
      1 => "DOUSE DRIVE",   # Douse Drive (Water)
      2 => "SHOCK DRIVE",   # Shock Drive (Electric)
      3 => "BURN DRIVE",    # Burn Drive (Fire)
      4 => "CHILL DRIVE"    # Chill Drive (Ice)
    },
    
    # Silvally - Memory Forms
    773 => {
      1  => "FIGHTING MEMORY",
      2  => "FLYING MEMORY",
      3  => "POISON MEMORY",
      4  => "GROUND MEMORY",
      5  => "ROCK MEMORY",
      6  => "BUG MEMORY",
      7  => "GHOST MEMORY",
      8  => "STEEL MEMORY",
      9  => "FIRE MEMORY",
      10 => "WATER MEMORY",
      11 => "GRASS MEMORY",
      12 => "ELECTRIC MEMORY",
      13 => "PSYCHIC MEMORY",
      14 => "ICE MEMORY",
      15 => "DRAGON MEMORY",
      16 => "DARK MEMORY",
      17 => "FAIRY MEMORY"
    },
    
    # Tornadus - Therian Form (Reveal Glass)
    641 => {
      1 => "REVEAL GLASS"
    },
    
    # Thundurus - Therian Form (Reveal Glass)
    642 => {
      1 => "REVEAL GLASS"
    },
    
    # Landorus - Therian Form (Reveal Glass)
    645 => {
      1 => "REVEAL GLASS"
    },
    
    # Hoopa - Unbound Form (Prison Bottle)
    720 => {
      1 => "PRISON BOTTLE"
    },
    
    # Toxtricity - Mega Form (Mega shown as form 3 to accommodate Low Key form)
    849 => {
      3 => "TOXTRICITITE"  # Mega Toxtricity (uses form 3 to accommodate Low Key form)
    },
	
    323 => {
      1 => "CAMERUPTITE"  # Mega Toxtricity (uses form 3 to accommodate Low Key form)
    },
    # Lapras - Mega Form (if holding Mega Stone)
    131 => {
      2 => "LAPRASITE"  # Mega Lapras
    },

    # Ampharos - Mega Form (if holding Mega Stone)
    181 => {
      2 => "AMPHAROSITE"  # Mega Ampharos
    },

    # Gyarados - Mega Form (if holding Mega Stone)
    130 => {
      2 => "GYARADOSITE"  # Mega Gyarados
    },

    # Glalie - Mega Form (if holding Mega Stone)
    362 => {
      2 => ["GLALITITE", "GLALIEITE", "GLALIE-TITE"]  # common naming variants
    },
    
    # Zacian - Crowned Sword Form (Rusted Sword)
    888 => {
      1 => "RUSTED SWORD"
    },
    
    # Zamazenta - Crowned Shield Form (Rusted Shield)
    889 => {
      1 => "RUSTED SHIELD"
    },
    
    # Enamorus - Therian Form (Reveal Glass)
    905 => { 1 => "REVEAL GLASS" },
    # Urshifu - Urshifite transforms depending on Urshifu's current form
    # If Urshifu is form 0 (single-strike), Urshifite should show form 2
    # If Urshifu is form 1 (rapid-strike), Urshifite should show form 3
    892 => {
      2 => { :keywords => "URSHIFITE", :required_form => 0 },
      3 => { :keywords => "URSHIFITE", :required_form => 1 }
    },
    # Alcremie - show form 63 when holding its form item
    869 => {
      63 => ["ALCREMIEITE", "ALCREMIE"]
    },
    # Add more Pokemon with form-changing items here as needed
    # Format: dex_number => { form_number => "ITEM_KEYWORD" }
  }
  
  #=============================================================================
  # Move-Based Forms Database
  #=============================================================================
  # Format: Pokedex_Number => { form_number => "MOVE_KEYWORD" or ["KEYWORD1", "KEYWORD2"] }
  #
  # The move_keyword should be a unique part of the move name (case-insensitive)
  #=============================================================================
  
  FORM_MOVES = {
    # Keldeo - Resolute Form (knows Secret Sword)
    647 => {
      1 => ["SECRET", "SWORD"]  # Matches both "SECRET SWORD" and "SECRETSWORD"
    }
    # Add more Pokemon with move-based forms here as needed
    # Format: dex_number => { form_number => "MOVE_NAME" }
  }
end
