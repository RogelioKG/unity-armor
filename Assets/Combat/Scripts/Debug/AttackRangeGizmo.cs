#if UNITY_EDITOR
using UnityEngine;

// Where the next swing starts: the red MeleeHitboxGizmo only lasts the few frames the
// window is open, so the drawn weapon's box is previewed here while idle.
[RequireComponent(typeof(PlayerAttack), typeof(WeaponState), typeof(CharacterRig))]
public class AttackRangeGizmo : MonoBehaviour
{
    [SerializeField] private Color color = new(1f, 0.9f, 0.2f, 0.5f);

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (!TryGetComponent(out PlayerAttack attack) || attack.IsAttacking) return;
        if (!TryGetComponent(out WeaponState weaponState) || !TryGetComponent(out CharacterRig rig)) return;

        WeaponData weapon = weaponState.Get(WeaponSlot.MainHand);
        if (weapon == null || weaponState.GetHold(WeaponSlot.MainHand) == WeaponHoldState.Holstered) return;

        // Rotation-only offset, matching MeleeHitbox: socket transforms carry the rig's scale.
        Transform origin = rig.TryGetSocket(weapon.drawn.socket, out Transform socket) ? socket : transform;

        Gizmos.color = color;
        Gizmos.matrix = Matrix4x4.TRS(origin.position + origin.rotation * weapon.hitboxCenter, origin.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, weapon.hitboxSize);
    }
}
#endif
