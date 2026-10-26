# encoding: UTF-8
require 'json'
require 'digest'
game, baseline_path, approval_path, report_path = ARGV.map { |p| File.expand_path(p) }
inventory = JSON.parse(File.read(baseline_path))
approval = JSON.parse(File.read(approval_path))
approval.fetch('files').each { |f| inventory[f['path']] = f }
source_path = File.join(game, 'patch/Mods/M44Followers.rb')
source = File.read(source_path)
raise 'Resolver differs from published game' unless Digest::SHA256.file(source_path).hexdigest == inventory.fetch('patch/Mods/M44Followers.rb').fetch('sha256')
FOLLOWER_FILENAME_FORMAT = 'Followers/%04d'
FOLLOWER_FALLBACK = 'Followers/000'
source_methods = source[/^def pbFollowerDexNumber\(pkmn\).*?(?=^def pbFollowerEventObjectByName)/m]
raise 'Missing real follower methods' unless source_methods
eval(source_methods, TOPLEVEL_BINDING, source_path)
$available = inventory.keys.each_with_object({}) { |path, result| result[path] = true }
# The engine mounts patch/ over Graphics; exercise the real candidate selector
# and file-existence helper against the reviewed package inventory.
module Kernel
  def self.pbResolveBitmap(path); end
  def pbResolveBitmap(path)
    ['patch/' + path, path, 'patch/' + path + '.png', path + '.png'].find { |p| $available[p] }
  end
end
def pbFollowerIsShiny(pkmn); pkmn.shiny; end
PokemonFixture = Struct.new(:dexnum, :form, :shiny, :item)
cases = [[722, 0, false, 'Followers/722'], [887, 0, true, 'Followers/887s'],
         [1037, 0, false, 'Followers/1037'], [1037, 0, true, 'Followers/1037s'],
         [1038, 0, false, 'Followers/1038'], [1038, 0, true, 'Followers/1038s'],
         [612, 0, false, 'Followers/612'], [612, 0, true, 'Followers/612s'],
         [612, 1, false, 'Followers/612_1']]
checks = cases.map do |dex, form, shiny, expected|
  actual = pbFollowerCharsetName(PokemonFixture.new(dex, form, shiny, nil))
  raise "Wrong follower for #{dex}/#{form}/#{shiny}: #{actual}" unless actual == expected
  {dex: dex, form: form, shiny: shiny, charset: actual,
   effective_path: pbResolveBitmap('Graphics/Characters/' + actual + '.png')}
end
raise 'Abysara shadowed by old base sprite' unless checks[2][:effective_path] == 'patch/Graphics/Characters/Followers/1037.png'
raise 'Divinara mapping lost' unless checks[4][:effective_path] == 'patch/Graphics/Characters/Followers/1038.png'
result = {success: true, resolver_sha256: Digest::SHA256.file(source_path).hexdigest,
          checks: checks.length + 2, cases: checks, game_started: false,
          scope: 'Real follower selection methods with inventory-backed engine bitmap resolution'}
File.write(report_path, JSON.pretty_generate(result) + "\n")
puts JSON.generate(result)
