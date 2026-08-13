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

    public SocketPlacement PlacementFor(WeaponHoldState hold)
        => hold == WeaponHoldState.Holstered ? holstered : drawn;
}
