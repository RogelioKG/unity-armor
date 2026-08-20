using System;
using System.Collections.Generic;

// Weapons are slotted like everything else, plus they are either drawn or holstered.
// That is a second, independent property, so it gets its own event rather than being
// folded into Changed — moving a sword to your back is not equipping a different sword.
public class WeaponState : EquipmentState<WeaponData, WeaponSlot>
{
    readonly Dictionary<WeaponSlot, WeaponHoldState> holds = new();

    /// <summary>Raised when a weapon that stays equipped moves between drawn and holstered.</summary>
    public event Action<WeaponSlot, WeaponHoldState> HoldChanged;

    /// <summary>Weapons come out drawn until something says otherwise.</summary>
    public WeaponHoldState GetHold(WeaponSlot slot)
        => holds.TryGetValue(slot, out var hold) ? hold : WeaponHoldState.Drawn;

    public void SetHold(WeaponSlot slot, WeaponHoldState hold)
    {
        if (Get(slot) == null || GetHold(slot) == hold) return;

        if (IsLocked) return;   // Sheathing mid-swing would strand the swing on an empty hand.

        holds[slot] = hold;
        HoldChanged?.Invoke(slot, hold);
    }

    public void ToggleHold(WeaponSlot slot)
        => SetHold(slot, GetHold(slot) == WeaponHoldState.Drawn
            ? WeaponHoldState.Holstered
            : WeaponHoldState.Drawn);

    public void ToggleAll()
    {
        foreach (var slot in AllSlots) ToggleHold(slot);
    }

    protected override void OnCleared(WeaponSlot slot) => holds.Remove(slot);
}
