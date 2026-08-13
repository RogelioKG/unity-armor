using UnityEngine;

// Serialized by ordinal into every ArmorData asset: append, never reorder.
public enum ArmorSlot { Head, Chest, Arms, Belt, Legs, Feet }

[CreateAssetMenu(menuName = "Game/Armor Data")]
public class ArmorData : EquipmentData<ArmorSlot>
{
    [Header("Stats")]
    public int armor;
}
