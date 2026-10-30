# encoding: UTF-8
# English street names in German text; exact display-only overrides.
module EnglishStreetNames
  REPAIRS = {
    [29, '<>- Grandview Avenue\\n^ - Opal Bridge'] => ['<>- Grandview-Allee\\n^ - Opalbrücke', '<>- Grandview Avenue\\n^ - Opalbrücke'],
    [37, '<>- Grandview Avenue\\n^ - 4th Street'] => ['<>- Grandview-Allee\\n^ - 4th Street', '<>- Grandview Avenue\\n^ - 4th Street'],
    [37, '<>- Grandview Avenue\\n^ - 3rd Street'] => ['<>- Grandview-Allee\\n^ - 3rd Street', '<>- Grandview Avenue\\n^ - 3rd Street'],
    [37, '<>- Neopath Road\\n^v - 2nd Street'] => ['<>- Neopath-Straße\\n^v - 2nd Street', '<>- Neopath Road\\n^v - 2nd Street'],
    [80, '<- Peony Circle\\n^v -4th Street'] => ['<- Peony-Platz\\n^v - 4th Street', '<- Peony Circle\\n^v - 4th Street'],
    [80, '>- Crocus Road\\n^v -4th Street'] => ['>- Crocus-Straße\\n^v - 4th Street', '>- Crocus Road\\n^v - 4th Street'],
    [80, '<- Crocus Road\\n^ -5th Street'] => ['<- Crocus-Straße\\n^ - 5th Street', '<- Crocus Road\\n^ - 5th Street'],
    [130, '<>- Willowreed Road\\n^ - Crystal Street'] => ['<>- Willowreed-Straße\\n^ - Crystal Street', '<>- Willowreed Road\\n^ - Crystal Street'],
    [130, '<>- Heatherhill Lane \\nv - Platinum Street'] => ['<>- Heatherhill-Gasse\\nv - Platinum Street', '<>- Heatherhill Lane\\nv - Platinum Street'],
    [130, '<- Willowreed Road\\n^ - Platinum Street'] => ['<- Willowreed-Straße\\n^ - Platinum Street', '<- Willowreed Road\\n^ - Platinum Street'],
    [142, '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl Bridge'] => ['<- Stoker-Straße\\n^ - Platinum Street\\n>- Beryl-Brücke', '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl-Brücke'],
    [143, '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl Bridge'] => ['<- Stoker-Straße\\n^ - Platinum Street\\n>- Beryl-Brücke', '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl-Brücke'],
    [149, '<>- Stoker Road\\n^ -Crystal Street'] => ['<>- Stoker-Straße\\n^ - Crystal Street', '<>- Stoker Road\\n^ - Crystal Street'],
    [149, '<>- Stoker Road\\n^ -Yellow Street'] => ['<>- Stoker-Straße\\n^ - Yellow Street', '<>- Stoker Road\\n^ - Yellow Street'],
    [149, '<>- Little Poe Street\\n^v -Crystal Street'] => ['<>- Little-Poe Street\\n^v - Crystal Street', '<>- Little Poe Street\\n^v - Crystal Street'],
    [149, '<>- Little Poe Street\\n^v -Yellow Street'] => ['<>- Little-Poe Street\\n^v - Yellow Street', '<>- Little Poe Street\\n^v - Yellow Street'],
    [353, '<>- Grandview Avenue\\n^ - 4th Street'] => ['<>- Grandview-Allee\\n^ - 4th Street', '<>- Grandview Avenue\\n^ - 4th Street'],
    [353, '<>- Grandview Avenue\\n^ - 3rd Street'] => ['<>- Grandview-Allee\\n^ - 3rd Street', '<>- Grandview Avenue\\n^ - 3rd Street'],
    [447, '^ - Opal Bridge\\n<>- Grandbooty Avenue'] => ['^ - Opalbrücke\\n<>- Riesenarsch-Allee', '^ - Opalbrücke\\n<>- Grandbooty Avenue'],
    [519, '<>- Grandview Avenue\\n^ - Opal Bridge'] => ['<>- Grandview-Allee\\n^ - Opalbrücke', '<>- Grandview Avenue\\n^ - Opalbrücke'],
    [523, '<>- Willowreed Road\\n^ - Crystal Street'] => ['<>- Willowreed-Straße\\n^ - Crystal Street', '<>- Willowreed Road\\n^ - Crystal Street'],
    [523, '<>- Heatherhill Lane \\nv - Platinum Street'] => ['<>- Heatherhill-Gasse\\nv - Platinum Street', '<>- Heatherhill Lane\\nv - Platinum Street'],
    [523, '<- Willowreed Road\\n^ - Platinum Street'] => ['<- Willowreed-Straße\\n^ - Platinum Street', '<- Willowreed Road\\n^ - Platinum Street'],
    [524, '<- Peony Circle\\n^v -4th Street'] => ['<- Peony-Platz\\n^v - 4th Street', '<- Peony Circle\\n^v - 4th Street'],
    [524, '>- Crocus Road\\n^v -4th Street'] => ['>- Crocus-Straße\\n^v - 4th Street', '>- Crocus Road\\n^v - 4th Street'],
    [524, '<- Crocus Road\\n^ -5th Street'] => ['<- Crocus-Straße\\n^ - 5th Street', '<- Crocus Road\\n^ - 5th Street'],
    [525, '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl Bridge'] => ['<- Stoker-Straße\\n^ - Platinum Street\\n>- Beryl-Brücke', '<- Stoker Road\\n^ - Platinum Street\\n>- Beryl-Brücke'],
    [530, '<>- Grandview Avenue\\n^ - 4th Street'] => ['<>- Grandview-Allee\\n^ - 4th Street', '<>- Grandview Avenue\\n^ - 4th Street'],
    [530, '<>- Grandview Avenue\\n^ - 3rd Street'] => ['<>- Grandview-Allee\\n^ - 3rd Street', '<>- Grandview Avenue\\n^ - 3rd Street'],
    [530, '<>- Neopath Road\\n^v - 2nd Street'] => ['<>- Neopath-Straße\\n^v - 2nd Street', '<>- Neopath Road\\n^v - 2nd Street'],
    [544, '<>- Stoker Road\\n^ -Crystal Street'] => ['<>- Stoker-Straße\\n^ - Crystal Street', '<>- Stoker Road\\n^ - Crystal Street'],
    [544, '<>- Stoker Road\\n^ -Yellow Street'] => ['<>- Stoker-Straße\\n^ - Yellow Street', '<>- Stoker Road\\n^ - Yellow Street'],
    [544, '<>- Little Poe Street\\n^v -Yellow Street'] => ['<>- Little-Poe Street\\n^v - Yellow Street', '<>- Little Poe Street\\n^v - Yellow Street'],
  }.freeze

  def self.repair(map, source, value)
    return value unless source.is_a?(String) && value.is_a?(String)
    pair = REPAIRS[[map, Messages.stringToKey(source)]]
    pair && Messages.stringToKey(value) == Messages.stringToKey(pair[0]) ? pair[1] : value
  end
  module Lookup
    def loadMessageFile(filename)
      @english_street_names_german = false
      result = super
      @english_street_names_german = !!result && File.basename(filename.to_s.tr('\\', '/')).downcase == 'german.dat'
      result
    end
    def getFromMapHash(map, source)
      value = super
      return value unless @english_street_names_german
      EnglishStreetNames.repair(map, source, value)
    end
  end
end
Messages.prepend(EnglishStreetNames::Lookup) unless Messages.ancestors.include?(EnglishStreetNames::Lookup)
