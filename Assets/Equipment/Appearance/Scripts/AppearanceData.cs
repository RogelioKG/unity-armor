using UnityEngine;

// Serialized by ordinal into every AppearanceData asset: append, never reorder.
public enum AppearanceSlot { Hair, FacialHair, Eyebrows, Eyes, Nose, Ears }

[CreateAssetMenu(menuName = "Game/Appearance Data")]
public class AppearanceData : EquipmentData<AppearanceSlot> { }
