using System.Collections.Generic;

/// <summary>
/// Implemented by the character actions that commit the whole body — a swing, a dodge, a stagger.
/// Each one polls the others before it starts, so neither has to know the other's type and
/// neither can clear a window it did not open. IEquipLock guards what you are wearing; this
/// guards what you are doing.
/// </summary>
public interface IActionLock
{
    /// <summary>True while this action owns the character and no other may start.</summary>
    bool BlocksActions { get; }
}

public static class ActionLock
{
    /// <summary>True when any lock other than `self` is running. Pass the list collected in Awake:
    /// locks added at runtime are not seen, the same trade-off IMovementOverride makes.</summary>
    public static bool AnyBlocking(List<IActionLock> locks, IActionLock self)
    {
        foreach (var other in locks)
            if (!ReferenceEquals(other, self) && other.BlocksActions) return true;

        return false;
    }
}
