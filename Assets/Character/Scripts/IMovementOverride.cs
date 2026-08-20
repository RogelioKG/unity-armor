using UnityEngine;

/// <summary>
/// Implemented by anything that needs to slow the character down or drive it outright —
/// attacking, blocking, dodging. PlayerController polls these instead of them writing its
/// speed, so several components cannot race over one field in Awake order.
/// </summary>
public interface IMovementOverride
{
    /// <summary>Lower wins when several are active. Dodge 0, attack 10, block 20.</summary>
    int Priority { get; }

    bool IsActive { get; }

    /// <summary>Return true to take movement over completely with `velocity` (world space,
    /// units per second). Return false to keep the controller's own movement, scaled by
    /// `speedMultiplier`.</summary>
    bool TryGetMovement(out Vector3 velocity, out float speedMultiplier);
}
