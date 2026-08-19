// Anything a hit can land on. Hitboxes look for this and never for a concrete type, so a
// destructible prop can take damage without dragging in everything Health carries.
public interface IDamageable
{
    /// <summary>Applies the hit and returns the health actually lost after mitigation.
    /// Zero is a valid outcome — a fully absorbed hit, not a miss.</summary>
    float TakeDamage(in DamageInfo info);
}

// One source of damage reduction: worn armor, a raised shield, a dodge's i-frames, a resistance
// buff. Implementors register themselves with Health rather than Health knowing about any of
// them, which is what lets the player and a mindless enemy share the same Health component.
public interface IDamageModifier
{
    /// <summary>Lower runs first. Blocking (0) resolves before armor (10) so a fully absorbed
    /// hit never reaches the armor curve at all. Equal values keep registration order.</summary>
    int Order { get; }

    /// <summary>Returns what is left of the damage after this modifier. `amount` is the running
    /// total from earlier modifiers; `info` still carries the original hit, so read it for
    /// direction or type but never for the current amount.</summary>
    float Modify(float amount, in DamageInfo info);
}
