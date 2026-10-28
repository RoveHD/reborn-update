# encoding: UTF-8
# Pokédex-Vorderseite: Mit der Bestätigungstaste (Input::C) Geschlecht, Form
# und Shiny-Anzeige wählen. Es werden nur bekannte Dex-Ansichten angeboten.
# Deinstallation: Diese Datei entfernen und das Spiel neu starten.
# Updates: Diese separate Datei behalten; keine Originalskripte überschreiben.
# Nach Änderungen der Pokédex-API erneut prüfen. Inkompatibilität wird gemeldet.
module PokedexFrontMenu
  class Incompatible < StandardError; end

  def self.warn_once(reason)
    return if @warned
    @warned = true
    warn("[Pokédex-Vorderseiten-Mod] Deaktiviert: #{reason}")
  end

  def self.entry(species)
    unless $Trainer && $Trainer.respond_to?(:pokedex) &&
           $Trainer.pokedex.respond_to?(:dexList)
      raise Incompatible, "Pokédex-Spielstand-API fehlt."
    end
    list = $Trainer.pokedex.dexList
    value = list[species] if list.respond_to?(:[])
    unless value.is_a?(Hash) && value[:lastSeen].is_a?(Hash) &&
           value[:forms].is_a?(Hash) && value[:gender].is_a?(Hash) &&
           [:gender, :form, :shiny].all? { |key| value[:lastSeen].key?(key) }
      raise Incompatible, "Unbekanntes Pokédex-Datenformat."
    end
    value
  end

  def self.available(species, entry)
    unless $cache && $cache.respond_to?(:pkmn) && $cache.pkmn.respond_to?(:[])
      raise Incompatible, "Pokémon-Formdaten fehlen."
    end
    data = $cache.pkmn[species]
    unless data && data.respond_to?(:forms) && data.forms.is_a?(Hash)
      raise Incompatible, "Unbekanntes Pokémon-Formformat."
    end
    # Gleicher Filter wie PokedexFormScene#pbGetAvailable, ohne Hilfsszene.
    # Formnamen bleiben unveränderte interne Schlüssel, auch bei Custom-Formen.
    names = data.forms.empty? ? ["Normal"] : data.forms.values
    forms = names.select do |name|
      next false unless entry[:forms][name]
      next true if name == names[0]
      formdata = $cache.pkmn[species, name]
      unless formdata && formdata.respond_to?(:checkFlag?)
        raise Incompatible, "ExcludeDex-Formprüfung fehlt."
      end
      !formdata.checkFlag?(:ExcludeDex)
    end
    genders = entry[:gender].keys.select { |gender| entry[:gender][gender] }
    [forms, genders]
  end

  def self.choose(species, ask)
    value = entry(species)
    return false unless value[:seen?]
    forms, genders = available(species, value)
    receiver = Object.new
    unless receiver.respond_to?(:cancelledgenders, true)
      raise Incompatible, "Form-/Geschlechts-Ausnahmeregel fehlt."
    end
    excluded = receiver.send(:cancelledgenders)
    unless excluded.respond_to?(:include?)
      raise Incompatible, "Unbekanntes Format der Form-Ausnahmeregel."
    end
    actions = []
    actions << :gender if value[:gender].length > 1 && !genders.empty?
    actions << :form if value[:forms].length > 1 && !forms.empty? && !excluded.include?(species)
    actions << :shiny if value[:shinySeen?]
    return false if actions.empty?
    labels = { :gender => "Geschlecht", :form => "Form", :shiny => "Shiny umschalten" }
    choice = ask.call("Welche Ansicht möchtest du anzeigen?", actions.map { |action| labels[action] })
    return false unless choice.is_a?(Integer) && choice >= 0 && choice < actions.length
    action = actions[choice]
    if action == :shiny
      value[:lastSeen][:shiny] = !value[:lastSeen][:shiny]
    else
      options = action == :gender ? genders : forms
      shown = options.map do |option|
        action == :gender ? { "Male" => "Männlich", "Female" => "Weiblich", "Any" => "Beliebig" }.fetch(option, option) : option
      end
      prompt = action == :gender ? "Welches Geschlecht möchtest du anzeigen?" : "Welche Form möchtest du anzeigen?"
      selection = ask.call(prompt, shown)
      return false unless selection.is_a?(Integer) && selection >= 0 && selection < options.length
      value[:lastSeen][action] = options[selection]
    end
    true
  end

  module Scene
    def pbDexEntry(index)
      @pokedex_front_menu_active = true
      @pokedex_front_menu_front = false
      super
    ensure
      @pokedex_front_menu_active = false
      @pokedex_front_menu_front = false
      @pokedex_front_menu_species = nil
    end

    def pbChangeToDexEntry(species)
      result = super
      @pokedex_front_menu_species = species.is_a?(Integer) ? $cache.pkmn.keys[species - 1] : species
      @pokedex_front_menu_front = true
      result
    end

    def pbUpdate
      result = super
      return result unless @pokedex_front_menu_active && @pokedex_front_menu_front
      return result if @pokedex_front_menu_busy || @pokedex_front_menu_disabled
      # Der Original-Loop verarbeitet Navigation nach pbUpdate. Beim Verlassen
      # der Vorderseite bis zum nächsten pbChangeToDexEntry kein Menü öffnen.
      return result if [Input::B, Input::UP, Input::DOWN].any? { |key| Input.trigger?(key) }
      if Input.trigger?(Input::LEFT) || Input.trigger?(Input::RIGHT)
        @pokedex_front_menu_front = false
        return result
      end
      return result unless Input.trigger?(Input::C)
      @pokedex_front_menu_busy = true
      begin
        ask = lambda { |prompt, options| Kernel.pbMessage(prompt, options, -1) }
        changed = PokedexFrontMenu.choose(@pokedex_front_menu_species, ask)
        pbChangeToDexEntry(@pokedex_front_menu_species) if changed
      rescue Incompatible => error
        @pokedex_front_menu_disabled = true
        PokedexFrontMenu.warn_once(error.message)
        Kernel.pbMessage("Die Pokédex-Vorderseiten-Mod ist mit dieser Spielversion nicht kompatibel.\n#{error.message}")
      ensure
        # B/C aus dem Untermenü dürfen den äußeren Dex-Loop nicht bedienen.
        Input.update
        @pokedex_front_menu_busy = false
      end
      result
    end
  end

  def self.install
    unless defined?(PokemonPokedexScene) && defined?(Input) &&
           [:pbDexEntry, :pbChangeToDexEntry, :pbUpdate].all? { |name| PokemonPokedexScene.method_defined?(name) } &&
           Input.respond_to?(:trigger?) && Input.respond_to?(:update) &&
           [:B, :C, :UP, :DOWN, :LEFT, :RIGHT].all? { |name| Input.const_defined?(name) } &&
           Kernel.respond_to?(:pbMessage)
      warn_once("Benötigte Pokédex-/Input-/Nachrichten-API fehlt.")
      return false
    end
    expected = { :pbDexEntry => 1, :pbChangeToDexEntry => 1, :pbUpdate => 0 }
    unless expected.all? { |name, arity| PokemonPokedexScene.instance_method(name).arity == arity }
      warn_once("Signatur der Pokédex-Methoden wurde geändert.")
      return false
    end
    PokemonPokedexScene.prepend(Scene) unless PokemonPokedexScene.ancestors.include?(Scene)
    true
  end
end

PokedexFrontMenu.install
