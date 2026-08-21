using UnityEngine;

// Attachment points authored on the character prefab, under the bone they follow.
// WeaponData serializes these values, so append new entries and never renumber old ones.
public enum SocketId
{
    MainHandGrip = 0,
    OffHandGrip = 1,
    HipSheathL = 2,
    HipSheathR = 3,
    BackMount = 4,
}

// Put this on an empty child of a bone and position it in the Scene view; SocketGizmo on the
// character root draws its axes. CharacterRig collects them at Awake, so there is nothing to wire up.
public class CharacterSocket : MonoBehaviour
{
    [SerializeField] SocketId id;

    public SocketId Id => id;
}
