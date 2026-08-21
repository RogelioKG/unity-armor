using System;
using UnityEngine;

// Serialized by ordinal into every WeaponData asset: append, never reorder.
public enum WeaponSlot { MainHand, OffHand }
public enum WeaponHoldState { Drawn, Holstered }

// Which socket the weapon hangs from, plus a per-weapon correction on top.
// The socket carries the rig-side transform; the offsets absorb only what is specific
// to this weapon. Large values mean the mesh pivot or the socket itself needs fixing.
[Serializable]
public struct SocketPlacement
{
    public SocketId socket;
    public Vector3 positionOffset;
    public Vector3 rotationEulerOffset;
}

// Unlike armor and appearance this is a static mesh on a socket, not a skinned part —
// but it is still identity + visuals + a slot, so it shares the same base.
[CreateAssetMenu(menuName = "Game/Weapon Data")]
public class WeaponData : EquipmentData<WeaponSlot>
{
    [Header("Attachment")]
    public SocketPlacement drawn;
    public SocketPlacement holstered;

    [Header("Combat")]
    public float damage = 20f;
    public DamageType damageType = DamageType.Slash;
    public float staminaCost = 10f;
    public float knockback = 2f;

    [Header("Swing Timing")]
    [Tooltip("Swing animation for this weapon. Empty keeps the Animator's own attack clip.")]
    public AnimationClip attackClip;
    [Tooltip("Normalized time in the attack clip where the hitbox opens.")]
    [Range(0f, 1f)] public float hitboxOpen = 0.25f;
    [Tooltip("Normalized time where the hitbox closes. Must be greater than hitboxOpen.")]
    [Range(0f, 1f)] public float hitboxClose = 0.55f;
    [Tooltip("Animator speed multiplier for this weapon's swing.")]
    public float attackSpeed = 1f;

    [Header("Hitbox")]
    [Tooltip("Swing volume in the drawn socket's space: it rides the hand, so shape it around the blade.")]
    public Vector3 hitboxCenter = new(0f, 0f, 0.5f);
    public Vector3 hitboxSize = new(0.3f, 0.3f, 1.2f);

    [Header("Defense")]
    [Tooltip("How much of a hit a raised guard stops with this in hand. The off hand wins; the main hand only stands in when the off hand is empty. Zero guards nothing and costs no stamina either.")]
    public float blockPower = 20f;

    public SocketPlacement PlacementFor(WeaponHoldState hold)
        => hold == WeaponHoldState.Holstered ? holstered : drawn;

    void OnValidate()
    {
        hitboxClose = Mathf.Max(hitboxClose, hitboxOpen);
    }
}
