using UnityEngine;

// Serialized by ordinal into weapon and enemy assets: append, never reorder.
public enum DamageType { Blunt, Slash, Pierce }

// One hit, fully described. Everything downstream of a swing — mitigation, knockback,
// aggro, impact VFX — reads this rather than reaching back to the attacker, so a hit stays
// resolvable even if whatever produced it is destroyed before the hit is handled.
//
// Passed by `in` everywhere: it is well past the width where copying is free, and it crosses
// every damage call on the hot path.
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly DamageType Type;

    /// <summary>Where the hit landed, in world space. Positions impact VFX and audio.</summary>
    public readonly Vector3 Point;

    /// <summary>Attacker to target, normalized. Drives knockback and the blocking facing test.</summary>
    public readonly Vector3 Direction;

    /// <summary>Who swung. Null is legal — environmental damage has no attacker.</summary>
    public readonly GameObject Source;

    public DamageInfo(float amount, DamageType type, Vector3 point, Vector3 direction, GameObject source)
    {
        Amount = amount;
        Type = type;
        Point = point;
        Direction = direction;
        Source = source;
    }

    /// <summary>The same hit at a different amount. Damage modifiers rebuild the struct this way
    /// instead of mutating, so a modifier cannot quietly rewrite the hit's origin or attacker.</summary>
    public DamageInfo WithAmount(float amount)
        => new(amount, Type, Point, Direction, Source);

    public override string ToString()
        => $"{Amount:0.#} {Type} from {(Source == null ? "<none>" : Source.name)}";
}
