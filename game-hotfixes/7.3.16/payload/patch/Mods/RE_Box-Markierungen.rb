# Font-independent PC markings. The four saved bit flags keep their original order.
module RebornMarkingSymbols
  SHAPES = [
    ["00011111000", "00111111100", "01111111110", "11111111111", "11111111111", "11111111111", "11111111111", "11111111111", "01111111110", "00111111100", "00011111000"],
    Array.new(11, "11111111111"),
    ["00000100000", "00000100000", "00001110000", "00001110000", "00011111000", "00011111000", "00111111100", "00111111100", "01111111110", "01111111110", "11111111111"],
    ["00000100000", "00001110000", "00011111000", "00111111100", "01111111110", "11111111111", "01111111110", "00111111100", "00011111000", "00001110000", "00000100000"]
  ].map { |rows| rows.map(&:freeze).freeze }.freeze

  def drawMarkings(bitmap, x, y, width, height, markings)
    # Four evenly spaced 11-pixel shapes fit the existing slot beside the level.
    left = (x + (width - 62) / 2.0).round
    top = (y + (height - 11) / 2.0).round
    SHAPES.each_with_index do |shape, index|
      marked = (markings & (1 << index)) != 0
      color = marked ? Color.new(112, 216, 192) : Color.new(152, 152, 144)
      shape.each_with_index do |row, py|
        row.each_char.with_index do |pixel, px|
          next unless pixel == '1'
          edge = px == 0 || py == 0 || px == 10 || py == 10 ||
            shape[py - 1][px] == '0' || shape[py + 1][px] == '0' || row[px - 1] == '0' || row[px + 1] == '0'
          next unless marked || edge
          bitmap.fill_rect(left + index * 17 + px, top + py, 1, 1, color)
        end
      end
    end
  end

end

module RebornBoxMarkings
  def getMarkingCommands(markings)
    german = defined?(LocalGermanVisible) && LocalGermanVisible.enabled?
    labels = german ? ['Kreis', 'Quadrat', 'Dreieck', 'Raute'] : ['Circle', 'Square', 'Triangle', 'Diamond']
    commands = labels.each_with_index.map { |label, index| "#{(markings & (1 << index)) != 0 ? '[x]' : '[ ]'} #{label}" } +
      [_INTL('OK'), _INTL('Cancel')]
    commands
  end

  def pbMark(selected, heldpoke)
    ret = 0
    msgwindow = Window_UnformattedTextPokemon.newWithSize("", 180, 0, Graphics.width - 180, 32)
    msgwindow.viewport = @viewport
    msgwindow.visible = true
    msgwindow.letterbyletter = false
    msgwindow.resizeHeightToFit(_INTL("Mark your Pokemon."), Graphics.width - 180)
    msgwindow.text = _INTL("Mark your Pokemon.")
    pokemon = heldpoke
    if heldpoke
      pokemon = heldpoke
    elsif selected[0] == -1
      pokemon = @storage.party[selected[1]]
    else
      pokemon = @storage.boxes[selected[0]][selected[1]]
    end
    pbBottomRight(msgwindow)
    selectedtag = "<c=505050>"
    deselectedtag = "<c=D0C8B8>"
    commands = getMarkingCommands(pokemon.markings)
    cmdwindow = Window_AdvancedCommandPokemon.new(commands)
    cmdwindow.viewport = @viewport
    cmdwindow.visible = true
    cmdwindow.resizeToFit(getMarkingCommands(15))
    cmdwindow.commands = commands
    # Keep the official system font; size the window to the translated labels.
    cmdwindow.height = Graphics.height - msgwindow.height if cmdwindow.height > Graphics.height - msgwindow.height
    cmdwindow.update
    pbBottomRight(cmdwindow)
    markings = pokemon.markings
    cmdwindow.y -= msgwindow.height
    loop do
      Graphics.update
      Input.update
      if Input.trigger?(Input::B)
        break # cancel
      end

      if Input.trigger?(Input::C)
        if cmdwindow.index == commands.length - 1
          break # cancel
        elsif cmdwindow.index == commands.length - 2
          pokemon.markings = markings # OK
          break
        elsif cmdwindow.index >= 0
          mask = (1 << cmdwindow.index)
          if (markings & mask) == 0
            markings |= mask
          else
            markings &= ~mask
          end
          commands = getMarkingCommands(markings)
          cmdwindow.commands = commands
        end
      end
      pbUpdateSpriteHash(@sprites)
      msgwindow.update
      cmdwindow.update
    end
    msgwindow.dispose
    cmdwindow.dispose
    Input.update
  end

end

PokemonStorageScene.prepend(RebornBoxMarkings) unless PokemonStorageScene.ancestors.include?(RebornBoxMarkings)

[PokemonStorageScene, PokemonSummaryScene].each do |scene|
  scene.prepend(RebornMarkingSymbols) unless scene.ancestors.include?(RebornMarkingSymbols)
end
