using System;
using System.Collections.Generic;
using UnityEngine;

// What the character has on, one entry per slot. This is the truth; renderers, stats,
// UI and saving all read it and listen to Changed rather than each other.
// e.g. ArmorState : EquipmentState<ArmorData, ArmorSlot>
public abstract class EquipmentState<TData, TSlot> : MonoBehaviour
    where TData : EquipmentData<TSlot>
    where TSlot : struct, Enum
{
    /// <summary>Every slot in the enum. Cached because Enum.GetValues allocates and boxes.</summary>
    public static readonly TSlot[] AllSlots = (TSlot[])Enum.GetValues(typeof(TSlot));

    readonly Dictionary<TSlot, TData> worn = new();

    readonly List<IEquipLock> locks = new();

    /// <summary>Raised once the slot has already changed. A null item means it was cleared.</summary>
    public event Action<TSlot, TData> Changed;

    public IReadOnlyDictionary<TSlot, TData> Worn => worn;

    /// <summary>True while something on this character is mid-action and gear must not change.
    /// UI reads it to grey a slot out before the player presses anything.</summary>
    public bool IsLocked
    {
        get
        {
            // Rebuilt per query, not cached: equipping is a cold path, and a lock added after
            // Awake still counts.
            GetComponents(locks);

            foreach (var candidate in locks)
                if (candidate.BlocksEquip) return true;

            return false;
        }
    }

    public TData Get(TSlot slot) => worn.TryGetValue(slot, out var item) ? item : null;

    /// <summary>Returns whether the slot now holds `item` — false means a lock refused it.</summary>
    public bool Equip(TData item)
    {
        if (item == null) return false;

        if (ReferenceEquals(Get(item.slot), item)) return true;   // Already on, no need to respawn it.

        if (IsLocked) return false;

        worn[item.slot] = item;
        Changed?.Invoke(item.slot, item);

        return true;
    }

    /// <summary>Returns whether the slot ended up empty — false means a lock refused it.</summary>
    public bool Unequip(TSlot slot)
    {
        if (IsLocked) return false;

        if (!worn.Remove(slot)) return true;   // Stay quiet when the slot was empty.

        OnCleared(slot);
        Changed?.Invoke(slot, null);

        return true;
    }

    public void Clear()
    {
        foreach (var slot in AllSlots) Unequip(slot);
    }

    /// <summary>Lets a subclass drop whatever it tracks alongside the item. Runs before Changed.</summary>
    protected virtual void OnCleared(TSlot slot) { }
}
