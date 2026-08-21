using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What an override wants done with horizontal motion: either drive it outright, or leave the
/// controller's own steering alone and scale it. Exactly one of the two fields is meaningful,
/// which is why the constructor is private and the two factories name the modes.
/// </summary>
public readonly struct MovementIntent
{
    public readonly bool DrivesVelocity;
    public readonly Vector3 Velocity;
    public readonly float SpeedMultiplier;

    private MovementIntent(bool drivesVelocity, Vector3 velocity, float speedMultiplier)
    {
        DrivesVelocity = drivesVelocity;
        Velocity = velocity;
        SpeedMultiplier = speedMultiplier;
    }

    /// <summary>Take movement over completely with `velocity` (world space, units per second).</summary>
    public static MovementIntent Drive(Vector3 velocity) => new(true, velocity, 0f);

    /// <summary>Keep the controller's own movement, scaled by `speedMultiplier`.</summary>
    public static MovementIntent Scale(float speedMultiplier) => new(false, Vector3.zero, speedMultiplier);
}

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

    MovementIntent GetMovement();
}

public static class MovementOverride
{
    /// <summary>The active override with the lowest Priority, or null when none is active.
    /// Ties go to the earlier entry, i.e. component order on the GameObject. Pass the list
    /// collected in Awake: overrides added at runtime are not seen.</summary>
    public static IMovementOverride SelectActive(List<IMovementOverride> overrides)
    {
        IMovementOverride winner = null;
        foreach (var candidate in overrides)
            if (candidate.IsActive && (winner == null || candidate.Priority < winner.Priority))
                winner = candidate;

        return winner;
    }
}
