# AllGen 0.9.9 removed SHUCKLITE, but older saves can still own it.
# Keep its identity in the item cache so bag drawing, descriptions and sorting
# remain valid. This does not restore Shuckle's removed Mega form or edit saves.
module RetiredMegaStoneCompatibility
  def self.restore(items)
    return if items.key?(:SHUCKLITE)

    items[:SHUCKLITE] = ItemData.new(:SHUCKLITE, {
      name: "Pottrottnit",
      desc: "Alter Pottrott-Megastein. In dieser Version ohne Wirkung.",
      price: 999,
      crystal: true,
      noUse: true,
      noUseInBattle: true,
      image: "item000"
    })
  end

  def cacheItems
    super
    RetiredMegaStoneCompatibility.restore(@items)
  end
end

Cache_Game.prepend(RetiredMegaStoneCompatibility)
RetiredMegaStoneCompatibility.restore($cache.items) if $cache
