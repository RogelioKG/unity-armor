using System;
using UnityEngine;

// Everything the character can have on: armor, appearance, weapons alike.
// Split in two on purpose — editor tooling needs to touch the shared fields without
// knowing which slot enum a given asset uses.
public abstract class EquipmentData : ScriptableObject
{
    [Header("Identity")]
    public string displayName;

    [Header("Visuals")]
    public Mesh mesh;
    public Material[] materials;
}

// The slot travels with the data, so an EquipmentState or EquipmentRenderer cannot be
// paired with the wrong enum — that is now a compile error rather than a bad cast.
// Declared last so the field order in existing .asset files does not shift.
public abstract class EquipmentData<TSlot> : EquipmentData
    where TSlot : struct, Enum
{
    [Header("Slot")]
    public TSlot slot;
}
