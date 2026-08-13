using System;

// A named group of parts to put on together. Armor calls these sets, weapons call them
// loadouts, but the shape is the same, so there is one type.
[Serializable]
public class EquipmentSet<TData>
{
    public string setName;      // "Type 1", "Type 2", ...
    public TData[] pieces;      // one per slot in use; order does not matter
}
