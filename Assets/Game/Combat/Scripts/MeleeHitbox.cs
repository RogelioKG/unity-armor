using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hit detection for one swing: an oriented box riding the attacker's weapon socket, swept
/// while the damage window is open and resolving each target once. OverlapBox rather than a
/// collider on the weapon mesh, so WeaponRenderer stays pure view.
/// </summary>
public class MeleeHitbox : MonoBehaviour
{
    private const int MaxOverlaps = 16;   // Fixed buffer: a swing into a crowd must not allocate.
    private const int MaxSubsteps = 8;

    private readonly Collider[] overlaps = new Collider[MaxOverlaps];

    // Cleared per swing: the window spans frames, so a standing target would otherwise be
    // billed once per frame.
    private readonly HashSet<IDamageable> alreadyHit = new();

    private Transform origin;
    private DamageInfo template;
    private Vector3 center, size;
    private LayerMask targets;
    private Vector3 lastCenter;
    private Quaternion lastRotation;

    /// <summary>Once per target per swing. Amount is the damage actually dealt — zero means
    /// fully absorbed, which is still a hit.</summary>
    public event Action<IDamageable, DamageInfo> Hit;

    public bool IsActive { get; private set; }

    /// <summary>Opens the window. `center` / `size` are in `origin`'s space, in metres.</summary>
    public void Begin(Transform origin, in DamageInfo template, Vector3 center, Vector3 size, LayerMask targets)
    {
        if (origin == null)
        {
            Debug.LogError($"{name}: MeleeHitbox.Begin needs an origin; ignoring swing.", this);
            return;
        }

        this.origin = origin;
        this.template = template;
        this.center = center;
        this.size = size;
        this.targets = targets;

        alreadyHit.Clear();
        IsActive = true;
        lastCenter = WorldCenter();   // Or the first sweep drags in from the last swing's end.
        lastRotation = origin.rotation;

        Sweep();   // A window shorter than one frame must still get one query.
    }

    public void End() => IsActive = false;

    // LateUpdate, not FixedUpdate: the bone only holds this frame's pose once the Animator
    // has written it.
    private void LateUpdate()
    {
        if (IsActive) Sweep();
    }

    private void OnDisable() => End();   // Never carry an open window across a re-enable.

    // Rotation only, never TransformPoint: rig bones carry the FBX's scale of 100.
    private Vector3 WorldCenter() => origin.position + origin.rotation * center;

    private void Sweep()
    {
        Vector3 worldCenter = WorldCenter();
        Quaternion rotation = origin.rotation;

        // The blade tip crosses most of a metre between frames, so walk there by the box's
        // thinnest side rather than querying only the new pose and stepping over thin targets.
        float step = Mathf.Max(Mathf.Min(size.x, Mathf.Min(size.y, size.z)), 0.05f);
        int steps = Mathf.Clamp(
            Mathf.CeilToInt(Vector3.Distance(lastCenter, worldCenter) / step), 1, MaxSubsteps);

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Query(Vector3.Lerp(lastCenter, worldCenter, t), Quaternion.Slerp(lastRotation, rotation, t));
        }

        lastCenter = worldCenter;
        lastRotation = rotation;
    }

    private void Query(Vector3 worldCenter, Quaternion rotation)
    {
        int count = Physics.OverlapBoxNonAlloc(
            worldCenter, size * 0.5f, overlaps, rotation, targets, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            // In parent, not on the collider: hurtboxes hang off the rig while Health sits on the root.
            var damageable = overlaps[i].GetComponentInParent<IDamageable>();
            if (damageable == null || !alreadyHit.Add(damageable)) continue;

            Vector3 point = overlaps[i].ClosestPoint(worldCenter);
            var info = new DamageInfo(template.Amount, template.Type, point, DirectionTo(point), template.Source);

            // Never inline this into the Invoke below: `?.` skips its arguments when the event
            // has no subscribers, and the hit would silently stop landing.
            float dealt = damageable.TakeDamage(info);
            Hit?.Invoke(damageable, info.WithAmount(dealt));
        }
    }

    // Attacker-to-target, flattened: knockback wants a horizontal direction from the body, not
    // the angle the blade happened to be at.
    private Vector3 DirectionTo(Vector3 point)
    {
        Transform attacker = template.Source != null ? template.Source.transform : origin;

        Vector3 direction = point - attacker.position;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : attacker.forward;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!IsActive) return;

        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(WorldCenter(), origin.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
#endif
}
