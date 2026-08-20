using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the Attack input into a swing: plays the upper body attack state, then opens and
/// closes the MeleeHitbox at the normalized times the equipped weapon asks for. One state
/// serves every weapon — the clip inside it is retargeted on equip.
/// </summary>
[RequireComponent(typeof(WeaponState), typeof(Animator), typeof(MeleeHitbox))]
public class PlayerAttack : MonoBehaviour, IMovementOverride, IEquipLock
{
    [Header("Targets")]
    [Tooltip("What a swing can hit. Enemy layer by default.")]
    [SerializeField] private LayerMask targetLayers = 1 << 9;

    [Header("Movement")]
    [Tooltip("Movement speed while swinging. Low values make attacks commit.")]
    [Range(0f, 1f)]
    [SerializeField] private float attackMoveMultiplier = 0f;

    [Header("Animator")]
    [SerializeField] private string upperBodyLayer = "UpperBody";
    [SerializeField] private string attackState = "Attack_01";
    [Tooltip("Seconds spent blending into the swing. Short values make attacks snappy.")]
    [SerializeField] private float attackBlend = 0.25f;
    [Tooltip("The clip the attack state already plays. Weapons carrying their own swing replace this one; leave it empty to keep every weapon on the Animator's clip.")]
    [SerializeField] private AnimationClip defaultAttackClip;

    private Animator animator;
    private WeaponState weaponState;
    private MeleeHitbox hitbox;
    private CharacterRig rig;
    private InputAction attackAction;
    private AnimatorOverrideController overrides;
    private Coroutine swing;
    private int upperBodyIndex;
    private int attackStateHash;

    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

    /// <summary>True from the press until the swing animation ends, recovery included.</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Raised when a swing is accepted.</summary>
    public event Action<WeaponData> SwingStarted;

    // Explicit: the controller asks these, nothing else has business calling them.
    int IMovementOverride.Priority => 10;

    bool IMovementOverride.IsActive => IsAttacking;

    // False: a swing slows the character rather than steering it.
    bool IMovementOverride.TryGetMovement(out Vector3 velocity, out float speedMultiplier)
    {
        velocity = Vector3.zero;
        speedMultiplier = attackMoveMultiplier;
        return false;
    }

    bool IEquipLock.BlocksEquip => IsAttacking;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponState = GetComponent<WeaponState>();
        hitbox = GetComponent<MeleeHitbox>();
        rig = GetComponent<CharacterRig>();

        upperBodyIndex = animator.GetLayerIndex(upperBodyLayer);
        attackStateHash = Animator.StringToHash(attackState);
        attackAction = InputSystem.actions.FindAction("Attack");

        // CrossFade to a missing state fails silently and the swing would hang waiting for it.
        bool hasState = upperBodyIndex >= 0 && animator.HasState(upperBodyIndex, attackStateHash);

        if (!hasState || attackAction == null || rig == null)
        {
            Debug.LogError($"{name}: needs a '{upperBodyLayer}' Animator layer holding '{attackState}', an Attack action and a CharacterRig; disabling attacks.", this);
            enabled = false;
            return;
        }

        SetUpClipOverrides();
    }

    // One override controller for the character's lifetime: assigning a RuntimeAnimatorController
    // rebinds and resets every layer.
    private void SetUpClipOverrides()
    {
        if (defaultAttackClip == null) return;

        overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrides;

        var keys = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.overridesCount);
        overrides.GetOverrides(keys);

        // Overrides are keyed by the clip a state holds, not by the state's name.
        if (keys.Exists(pair => pair.Key == defaultAttackClip)) return;

        Debug.LogError($"{name}: '{defaultAttackClip.name}' is not a clip this Animator plays; per-weapon attack animations are off.", this);
        overrides = null;
    }

    private void OnEnable()
    {
        attackAction?.Enable();
        weaponState.Changed += OnWeaponChanged;

        ApplyAttackClip(weaponState.Get(WeaponSlot.MainHand));   // Catch up on what is already equipped.
    }

    private void OnDisable()
    {
        attackAction?.Disable();
        weaponState.Changed -= OnWeaponChanged;

        // Disabling a component does not stop its coroutines; only deactivating the GameObject does.
        if (swing != null) StopCoroutine(swing);
        Finish();
    }

    private void OnWeaponChanged(WeaponSlot slot, WeaponData weapon)
    {
        if (slot == WeaponSlot.MainHand) ApplyAttackClip(weapon);
    }

    /// <summary>Points the attack state at this weapon's swing, or back at the Animator's own
    /// clip when it hasn't got one.</summary>
    private void ApplyAttackClip(WeaponData weapon)
    {
        if (overrides == null) return;

        var clip = weapon != null && weapon.attackClip != null ? weapon.attackClip : defaultAttackClip;

        // Retargeting rebinds the controller, so only pay for it when the clip actually changes.
        if (overrides[defaultAttackClip] != clip) overrides[defaultAttackClip] = clip;
    }

    private void Update()
    {
        if (attackAction.WasPressedThisFrame()) TryAttack();
    }

    private void TryAttack()
    {
        if (IsAttacking) return;   // No combo buffering this stage.

        var weapon = weaponState.Get(WeaponSlot.MainHand);
        if (weapon == null) return;
        if (weaponState.GetHold(WeaponSlot.MainHand) == WeaponHoldState.Holstered) return;

        // Stage 3 gates here: `if (!stamina.TryConsume(weapon.staminaCost)) return;`

        // Before the CrossFade, or the opening frames play at the previous weapon's speed.
        animator.SetFloat(AttackSpeedHash, Mathf.Max(weapon.attackSpeed, 0.01f));
        animator.CrossFadeInFixedTime(attackStateHash, attackBlend, upperBodyIndex);

        IsAttacking = true;
        swing = StartCoroutine(Swing(weapon));

        SwingStarted?.Invoke(weapon);
    }

    private IEnumerator Swing(WeaponData weapon)
    {
        yield return null;   // The CrossFade is only registered by the Animator's next update.

        float open = weapon.hitboxOpen;
        float close = Mathf.Max(open, weapon.hitboxClose);
        bool opened = false;

        // Loop condition doubles as interruption handling: state gone → window closed.
        while (TryGetSwingTime(out float progress))
        {
            // Separate tests, not else-if: a window narrower than one frame still has to open and close.
            if (!opened && progress >= open)
            {
                OpenHitbox(weapon);
                opened = true;
            }

            if (progress >= close) hitbox.End();   // Idempotent, and close >= open, so the window already ran.

            if (progress >= 1f) break;

            yield return null;
        }

        Finish();
    }

    private void OpenHitbox(WeaponData weapon)
    {
        // Point and direction are recomputed per target by the sweep.
        var template = new DamageInfo(weapon.damage, weapon.damageType, transform.position, transform.forward, gameObject);
        hitbox.Begin(Socket(weapon), template, weapon.hitboxCenter, weapon.hitboxSize, targetLayers);
    }

    // The socket WeaponRenderer parents the mesh to, so the box follows the blade.
    private Transform Socket(WeaponData weapon) => rig.TryGetSocket(weapon.drawn.socket, out var socket) ? socket : transform;

    /// <summary>Progress through the attack state, 0..1, or false when it isn't playing. A
    /// transition *into* the attack state wins over the current one: a re-swing would otherwise
    /// read the previous swing blending out and report a progress of 1 straight away.</summary>
    private bool TryGetSwingTime(out float progress)
    {
        var info = animator.GetCurrentAnimatorStateInfo(upperBodyIndex);

        if (animator.IsInTransition(upperBodyIndex))
        {
            var next = animator.GetNextAnimatorStateInfo(upperBodyIndex);
            if (next.shortNameHash == attackStateHash) info = next;
        }

        progress = info.normalizedTime;
        return info.shortNameHash == attackStateHash;
    }

    private void Finish()
    {
        hitbox.End();
        IsAttacking = false;
        swing = null;
    }

#if UNITY_EDITOR
    // Where the next swing starts: the red gizmo only lasts the few frames the window is open.
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || weaponState == null || IsAttacking) return;

        var weapon = weaponState.Get(WeaponSlot.MainHand);
        if (weapon == null || weaponState.GetHold(WeaponSlot.MainHand) == WeaponHoldState.Holstered) return;

        // Rotation-only offset, matching MeleeHitbox: socket transforms carry the rig's scale.
        var origin = Socket(weapon);

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(
            origin.position + origin.rotation * weapon.hitboxCenter, origin.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, weapon.hitboxSize);
    }
#endif
}
