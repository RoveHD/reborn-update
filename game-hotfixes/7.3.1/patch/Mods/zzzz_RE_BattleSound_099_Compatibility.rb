# AllGen 0.9.9: compatibility with Battle_Sound_Effects_v1.1.rb.
# That legacy method takes three arguments; the new caller also supplies hitnum.
# Keep the installed sound mod and its behavior intact. A newer implementation
# receives the original arguments unchanged, including the hit index.
module REBattleSound099Compatibility
  def pbReduceHPDamage(*args, &block)
    downstream = REBattleSound099Compatibility.instance_method(:pbReduceHPDamage).
      bind(self).super_method
    source = downstream && downstream.source_location
    legacy_sound = source && File.basename(source[0].tr('\\', '/')) == 'Battle_Sound_Effects_v1.1.rb'
    if legacy_sound && args.length == 4 && downstream.arity == 3
      super(*args.first(3), &block)
    else
      super(*args, &block)
    end
  end
end

if defined?(PokeBattle_Move) && !PokeBattle_Move.ancestors.include?(REBattleSound099Compatibility)
  PokeBattle_Move.prepend(REBattleSound099Compatibility)
end
