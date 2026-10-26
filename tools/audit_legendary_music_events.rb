# Read only: inspect the effective map overlays for wild-battle music overrides.
require 'json'
game = File.expand_path(ARGV.fetch(0))
module RPG; end
%w[Map Event Event::Page Event::Page::Condition Event::Page::Graphic EventCommand MoveRoute MoveCommand AudioFile BGM BGS ME SE].each do |path|
  path.split('::').inject(RPG) { |parent, name| parent.const_defined?(name, false) ? parent.const_get(name) : parent.const_set(name, Class.new) }
end
%w[Table Color Tone Rect].each do |name|
  klass = Class.new
  def klass._load(data); new; end
  Object.const_set(name, klass)
end
maps = {}
%w[Data patch/Data].each do |folder|
  Dir[File.join(game, folder, 'Map[0-9]*.rxdata')].each { |path| maps[File.basename(path)] = path }
end
rows = []
maps.each do |name, path|
  map = Marshal.load(File.binread(path))
  (map.instance_variable_get(:@events) || {}).each do |id, event|
    (event.instance_variable_get(:@pages) || []).each_with_index do |page, page_index|
      commands = page.instance_variable_get(:@list) || []
      relevant = []
      commands.each_with_index do |command, index|
        code = command.instance_variable_get(:@code)
        params = command.instance_variable_get(:@parameters) || []
        if code == 132
          relevant << {index: index, next_bgm: params[0]&.instance_variable_get(:@name)}
        elsif [355, 655, 111].include?(code)
          params.grep(String).each do |script|
            relevant << {index: index, script: script} if script.match?(/pb(?:Double)?WildBattle|nextBattleBGM|beginDynamicMusic/)
          end
        end
      end
      rows << {map: name, event: id, page: page_index, commands: relevant} if relevant.any? { |r| r[:script]&.match?(/pb(?:Double)?WildBattle/) }
    end
  end
end
File.write(ARGV.fetch(1), JSON.pretty_generate({maps: maps.size, wild_pages: rows}) + "\n")
focus = rows.select { |row| row[:commands].any? { |r| r[:script]&.match?(/DIALGA|GROUDON|GIRATINA|ARCEUS|ZACIAN|KORAIDON/) } }
puts JSON.pretty_generate({maps: maps.size, focus: focus})
