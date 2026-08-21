#if UNITY_EDITOR
using UnityEngine;

// The swept damage box, visible only for the few frames the window is open.
[RequireComponent(typeof(MeleeHitbox))]
public class MeleeHitboxGizmo : MonoBehaviour
{
    [SerializeField] private Color color = Color.red;

    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out MeleeHitbox hitbox)) return;
        if (!hitbox.TryGetWindow(out Vector3 center, out Quaternion rotation, out Vector3 size)) return;

        Gizmos.color = color;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
#endif
