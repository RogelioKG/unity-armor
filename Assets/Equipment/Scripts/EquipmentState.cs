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

    /// <summary>Raised once the slot has already changed. A null item means it was cleared.</summary>
    public event Action<TSlot, TData> Changed;

    public IReadOnlyDictionary<TSlot, TData> Worn => worn;

    public TData Get(TSlot slot) => worn.TryGetValue(slot, out var item) ? item : null;

    public void Equip(TData item)
    {
        if (item == null) return;

        if (ReferenceEquals(Get(item.slot), item)) return;   // Already on, no need to respawn it.

        worn[item.slot] = item;
        Changed?.Invoke(item.slot, item);
    }

    public void Unequip(TSlot slot)
    {
        if (!worn.Remove(slot)) return;   // Stay quiet when the slot was empty.

        OnCleared(slot);
        Changed?.Invoke(slot, null);
    }

    public void Clear()
    {
        foreach (var slot in AllSlots) Unequip(slot);
    }

    /// <summary>Lets a subclass drop whatever it tracks alongside the item. Runs before Changed.</summary>
    protected virtual void OnCleared(TSlot slot) { }
}
