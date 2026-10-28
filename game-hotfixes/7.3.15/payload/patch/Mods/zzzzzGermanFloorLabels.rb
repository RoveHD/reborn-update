# encoding: UTF-8
# Reviewed floor labels and lift menus; display-only overrides.
module GermanFloorLabels
  REPAIRS = {
    [1, 'This is the fifth floor: Mid-tier General Needs and Vitamins.'] => ['This is the fifth floor: Das ist die fünfte Etage: Mittleres Sortiment – allgemeiner Bedarf und Vitamine.', 'Hier ist die 5. Etage – Erweiterter Bedarf; Vitamine.'],
    [2, 'This is the sixth floor: Type Berries.'] => ['This is the sixth floor: Dies ist das sechste Stockwerk: Typ-Beeren.', 'Hier ist die 6. Etage – Typ-Beeren.'],
    [3, 'This is the seventh floor: Type Gems.'] => ['This is the seventh floor: Dies ist die siebte Etage: Typ-Edelsteine.', 'Hier ist die 7. Etage – Typ-Juwelen.'],
    [4, 'This is the Eighth floor: Battle Items and Niche Items II.'] => ['This is the Eighth floor: Dies ist die achte Etage: Kampf-Items und Nischen-Items II.', 'Hier ist die 8. Etage – Kampf-Items; Spezial-Items II.'],
    [5, 'This is the ninth floor: Evolution Stones and Special II.'] => ['This is the ninth floor: Das ist der neunte Stock: Evolutionssteine und Spezial II.', 'Hier ist die 9. Etage – Entwicklungssteine; Extras II.'],
    [6, 'This is the eleventh floor: Utility Items and Evolution Items.'] => ['This is the eleventh floor: Dies ist die elfte Etage: Hilfsgegenstände und Entwicklungsgegenstände.', 'Hier ist die 11. Etage – Hilfs- und Entwicklungs-Items.'],
    [7, 'This is the tenth floor: Top-tier General Needs and Rare Berries.'] => ['This is the tenth floor: Das ist die zehnte Etage: Hochwertige Bedarfsartikel und seltene Beeren.', 'Hier ist die 10. Etage – Premiumbedarf; seltene Beeren.'],
    [71, 'Ground Floor-- Now arrived.'] => ['Boden Boden – Jetzt eingetroffen.', 'Erdgeschoss erreicht.'],
    [71, 'FERN: That\'s the floor we\'re already on, numbnuts.'] => ['FERN: Das ist der Boden, auf dem wir uns bereits befinden, Idioten.', 'FERN: Auf der Etage sind wir doch schon, du Idiot.'],
    [71, '2F-- Now arrived.'] => ['2F-- Jetzt angekommen.', '2F erreicht.'],
    [71, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [89, 'Currently on: 1F'] => ['Derzeit am: 1F', 'Aktuelle Etage: 1F'],
    [89, 'Currently on: 2F'] => ['Derzeit am: 2F', 'Aktuelle Etage: 2F'],
    [89, 'Currently on: B1F'] => ['Derzeit auf: B1F', 'Aktuelle Etage: B1F'],
    [89, 'B1F-- Now arrived.'] => ['B1F-- Jetzt angekommen.', 'B1F erreicht.'],
    [89, '1F-- Now arrived.'] => ['1F-- Jetzt angekommen.', '1F erreicht.'],
    [89, '2F-- Now arrived.'] => ['2F-- Jetzt angekommen.', '2F erreicht.'],
    [112, 'First Floor- Low-tier General Needs; Medicine'] => ['Erster Stock – untere Etage mit allgemeinem Bedarf; Medizin', '1. Etage – Grundbedarf; Medizin'],
    [112, 'Second Floor- Status and Friendship Berries'] => ['Zweiter Stock – Status- und Freundschaftsbeeren', '2. Etage – Status- und Freundschaftsbeeren'],
    [112, 'Third Floor- Niche Items I; Prolonging Items'] => ['Dritter Stock – Nische Items I; Verlängerung Items', '3. Etage – Spezial-Items I; Wirkungsverlängerer'],
    [112, 'Fourth Floor- Consumable Items; Special I'] => ['Vierte Etage – Verbrauchsmaterial Items; Besonderes I', '4. Etage – Verbrauchs-Items; Extras I'],
    [112, 'Fifth Floor- Mid-tier General Needs; Vitamins'] => ['Fünfte Etage – Allgemeine Bedürfnisse der Mittelklasse; Vitamine', '5. Etage – Erweiterter Bedarf; Vitamine'],
    [112, 'Sixth Floor- Type Berries'] => ['Sechster Stock – Typ Beeren', '6. Etage – Typ-Beeren'],
    [112, 'Seventh Floor- Type Gems'] => ['Siebter Stock – Typ Gems', '7. Etage – Typ-Juwelen'],
    [112, 'Eighth Floor- Battle Items; Niche Items II'] => ['Achter Stock – Battle Items; Nische Items II', '8. Etage – Kampf-Items; Spezial-Items II'],
    [112, 'Ninth Floor- Evolution Stones; Special II'] => ['Neunte Etage – Evolutionssteine; Spezial II', '9. Etage – Entwicklungssteine; Extras II'],
    [112, 'Tenth Floor- Top-tier General Needs; Rare Berries'] => ['Zehnter Stock – Erstklassiger allgemeiner Bedarf; Seltene Beeren', '10. Etage – Premiumbedarf; seltene Beeren'],
    [112, 'Eleventh Floor- Utility Items; Evolution Items'] => ['Elfte Etage – Versorgungswirtschaft Items; Evolution Items', '11. Etage – Hilfs- und Entwicklungs-Items'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems, 8F - Battle Items; Niche Items II, 9F - Evolution Stones; Special II, 10F - Top-Tier General; Rare Berries, 11F - Utility and Evolution Items, Penthouse]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine, 8F – Kampf Items; Nische Items II, 9F – Evolution Stones; Spezial II, 10F – General Beste Stufe; Seltene Beeren, 11F – Utility und Evolution Items, Penthouse]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen, 8F – Kampf-Items; Spezial-Items II, 9F – Entwicklungssteine; Extras II, 10F – Premiumbedarf; seltene Beeren, 11F – Hilfs- und Entwicklungs-Items, Penthouse]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems, 8F - Battle Items; Niche Items II, 9F - Evolution Stones; Special II, 10F - Top-Tier General; Rare Berries, 11F - Utility and Evolution Items]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine, 8F – Kampf Items; Nische Items II, 9F – Evolution Stones; Spezial II, 10F – General Beste Stufe; Seltene Beeren, 11F – Utility und Evolution Items]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen, 8F – Kampf-Items; Spezial-Items II, 9F – Entwicklungssteine; Extras II, 10F – Premiumbedarf; seltene Beeren, 11F – Hilfs- und Entwicklungs-Items]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems, 8F - Battle Items; Niche Items II, 9F - Evolution Stones; Special II, 10F - Top-Tier General; Rare Berries]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine, 8F – Kampf Items; Nische Items II, 9F – Evolution Stones; Spezial II, 10F – General Beste Stufe; Seltene Beeren]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen, 8F – Kampf-Items; Spezial-Items II, 9F – Entwicklungssteine; Extras II, 10F – Premiumbedarf; seltene Beeren]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems, 8F - Battle Items; Niche Items II, 9F - Evolution Stones; Special II]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine, 8F – Kampf Items; Nische Items II, 9F – Evolution Stones; Spezial II]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen, 8F – Kampf-Items; Spezial-Items II, 9F – Entwicklungssteine; Extras II]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems, 8F - Battle Items; Niche Items II]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine, 8F – Kampf Items; Nische Items II]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen, 8F – Kampf-Items; Spezial-Items II]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries, 7F - Type Gems]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren, 7F – Typ Edelsteine]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren, 7F – Typ-Juwelen]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins, 6F - Type Berries]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine, 6F – Typ Beeren]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine, 6F – Typ-Beeren]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I, 5F - Mid-Tier General; Vitamins]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I, 5F – Mittelklasse-General; Vitamine]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I, 5F – Erweiterter Bedarf; Vitamine]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items, 4F - Consumable Items; Special I]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items, 4F - Verbrauchsmaterial Items; Spezial I]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer, 4F – Verbrauchs-Items; Extras I]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries, 3F - Niche Items I; Prolonging Items]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Nische Items I; Verlängerung Items]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren, 3F – Spezial-Items I; Wirkungsverlängerer]'],
    [113, 'To which floor would you like to go?\\ch[68,-1, 1F - Low-Tier General; Medicine, 2F - Status and Friendship Berries]'] => ['In welche Etage möchten Sie gehen? \\ch[68,-1, 1F – General der unteren Stufe; Medizin, 2F – Status- und Freundschaftsbeeren]', 'Zu welcher Etage möchten Sie fahren?\\ch[68,-1, 1F – Grundbedarf; Medizin, 2F – Status- und Freundschaftsbeeren]'],
    [113, 'First floor- Low-Tier General Needs; Medicine'] => ['Erster Stock – untere Ebene mit allgemeinem Bedarf; Medizin', '1. Etage – Grundbedarf; Medizin'],
    [113, 'Second floor- Status and Friendship Berries'] => ['Zweiter Stock – Status- und Freundschaftsbeeren', '2. Etage – Status- und Freundschaftsbeeren'],
    [113, 'Third Floor- Niche Items I; Prolonging Items'] => ['Dritter Stock – Nische Items I; Verlängerung Items', '3. Etage – Spezial-Items I; Wirkungsverlängerer'],
    [113, 'Fourth Floor- Consumable Items; Special I'] => ['Vierte Etage – Verbrauchsmaterial Items; Besonderes I', '4. Etage – Verbrauchs-Items; Extras I'],
    [113, 'Fifth Floor- Mid-Tier General Needs; Vitamins'] => ['Fünfter Stock – Allgemeine Bedürfnisse der Mittelschicht; Vitamine', '5. Etage – Erweiterter Bedarf; Vitamine'],
    [113, 'Sixth Floor- Type Berries'] => ['Sechster Stock – Typ Beeren', '6. Etage – Typ-Beeren'],
    [113, 'Seventh Floor- Type Gems'] => ['Siebter Stock – Typ Gems', '7. Etage – Typ-Juwelen'],
    [113, 'Eighth Floor- Battle Items; Niche Items II'] => ['Achter Stock – Battle Items; Nische Items II', '8. Etage – Kampf-Items; Spezial-Items II'],
    [113, 'Ninth Floor- Evolution Stones; Special II'] => ['Neunte Etage – Evolutionssteine; Spezial II', '9. Etage – Entwicklungssteine; Extras II'],
    [113, 'Tenth Floor- Top-Tier General Needs; Rare Berries'] => ['Zehnter Stock – Erstklassiger allgemeiner Bedarf; Seltene Beeren', '10. Etage – Premiumbedarf; seltene Beeren'],
    [113, 'Eleventh Floor- Utility Items; Evolution Items'] => ['Elfte Etage – Versorgungswirtschaft Items; Evolution Items', '11. Etage – Hilfs- und Entwicklungs-Items'],
    [114, 'This is the second floor: Status and Friendship Berries.'] => ['Dies ist die zweite Etage: Status- und Freundschaftsbeeren.', 'Hier ist die 2. Etage – Status- und Freundschaftsbeeren.'],
    [129, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [139, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [139, 'Fifth Floor'] => ['Fünfter Stock', '5. Etage'],
    [141, 'This is the third floor: Niche Items I and Prolonging Items.'] => ['Dies ist die dritte Etage: Nische Items I und Verlängerung Items.', 'Hier ist die 3. Etage – Spezial-Items I; Wirkungsverlängerer.'],
    [172, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [183, 'This is the fourth floor: Consumable Items and Special I.'] => ['Dies ist die vierte Etage: Verbrauchsmaterial Items und Spezial I.', 'Hier ist die 4. Etage – Verbrauchs-Items; Extras I.'],
    [457, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [460, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [460, 'Floor 2'] => ['Etage 2', '2. Etage'],
    [733, 'Currently on: B2F'] => ['Derzeit auf: B2F', 'Aktuelle Etage: B2F'],
    [733, 'Currently on: B1F'] => ['Derzeit auf: B1F', 'Aktuelle Etage: B1F'],
    [733, 'Currently on: Ground Floor'] => ['Derzeit auf: Boden Boden', 'Aktuelle Etage: Erdgeschoss'],
    [733, 'Ground Floor-- Now arrived.'] => ['Boden Boden – Jetzt eingetroffen.', 'Erdgeschoss erreicht.'],
    [733, 'B1F-- Now arrived.'] => ['B1F-- Jetzt angekommen.', 'B1F erreicht.'],
    [733, 'B2F-- Now arrived.'] => ['B2F-- Jetzt angekommen.', 'B2F erreicht.'],
    [733, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [737, 'GROUND FLOOR ENTRANCE UNLOCKED.'] => ['Boden BODENEINGANG ENTSPERRT.', 'EINGANG ZUM ERDGESCHOSS ENTRIEGELT.'],
    [753, 'Ground Floor'] => ['Boden Boden', 'Erdgeschoss'],
    [753, 'Basement Floor'] => ['Kellergeschoss', 'Untergeschoss'],
  }.freeze
  def self.repair(map, source, value)
    return value unless source.is_a?(String) && value.is_a?(String)
    pair = REPAIRS[[map, Messages.stringToKey(source)]]
    pair && Messages.stringToKey(value) == Messages.stringToKey(pair[0]) ? pair[1] : value
  end
  module Lookup
    def loadMessageFile(filename)
      @german_floor_labels_enabled = false
      result = super
      @german_floor_labels_enabled = !!result && File.basename(filename.to_s.tr('\\', '/')).downcase == 'german.dat'
      result
    end
    def getFromMapHash(map, source)
      value = super
      return value unless @german_floor_labels_enabled
      GermanFloorLabels.repair(map, source, value)
    end
  end
end
Messages.prepend(GermanFloorLabels::Lookup) unless Messages.ancestors.include?(GermanFloorLabels::Lookup)
