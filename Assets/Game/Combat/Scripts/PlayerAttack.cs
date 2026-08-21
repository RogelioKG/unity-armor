using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Turns the Attack input into a swing: plays the action layer attack state, then opens and
/// closes the MeleeHitbox at the normalized times the equipped weapon asks for. One state
/// serves every weapon — the clip inside it is retargeted on equip.
/// </summary>
[RequireComponent(typeof(Stamina), typeof(WeaponState), typeof(MeleeHitbox))]
[RequireComponent(typeof(Animator), typeof(CharacterRig))]
public class PlayerAttack : MonoBehaviour, IMovementOverride, IEquipLock, IActionLock
{
    [Header("Targets")]
    [Tooltip("What a swing can hit. Enemy layer by default.")]
    [SerializeField] private LayerMask targetLayers = 1 << 9;

    [Header("Movement")]
    [Tooltip("Movement speed while swinging. Low values make attacks commit.")]
    [Range(0f, 1f)]
    [SerializeField] private float attackMoveMultiplier = 0.1f;

    [Header("Animator")]
    [Tooltip("Layer the swing plays on. Must match PlayerBlock's.")]
    [SerializeField] private string actionLayer = "Action";
    [SerializeField] private string attackState = "AttackSlash";
    [Tooltip("Seconds spent blending into the swing. Short values make attacks snappy.")]
    [SerializeField] private float attackBlend = 0.25f;
    [Tooltip("The clip the attack state already plays. Weapons carrying their own swing replace this one; leave it empty to keep every weapon on the Animator's clip.")]
    [SerializeField] private AnimationClip defaultAttackClip;

    private readonly List<IActionLock> actionLocks = new();

    private Animator animator;
    private WeaponState weaponState;
    private MeleeHitbox hitbox;
    private CharacterRig rig;
    private Stamina stamina;
    private InputAction attackAction;
    private AnimatorOverrideController overrides;
    private Coroutine swing;
    private int actionLayerIndex;
    private int attackStateHash;

    private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");

    /// <summary>True from the press until the swing animation ends, recovery included.</summary>
    public bool IsAttacking { get; private set; }

    /// <summary>Raised when a swing is accepted.</summary>
    public event Action<WeaponData> SwingStarted;

    // Explicit: the controller asks these, nothing else has business calling them.
    int IMovementOverride.Priority => 10;

    bool IMovementOverride.IsActive => IsAttacking;

    // Scale, not drive: a swing slows the character rather than steering it.
    MovementIntent IMovementOverride.GetMovement() => MovementIntent.Scale(attackMoveMultiplier);

    bool IEquipLock.BlocksEquip => IsAttacking;

    // Recovery included: a dodge out of a swing would need the swing to be interruptible.
    bool IActionLock.BlocksActions => IsAttacking;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        weaponState = GetComponent<WeaponState>();
        hitbox = GetComponent<MeleeHitbox>();
        rig = GetComponent<CharacterRig>();
        stamina = GetComponent<Stamina>();
        GetComponents(actionLocks);

        var actions = InputSystem.actions;
        attackAction = actions.FindAction("Attack");

        actionLayerIndex = animator.GetLayerIndex(actionLayer);
        attackStateHash = animator.ResolveState(actionLayerIndex, attackState);

        // CrossFade to a missing state fails silently and the swing would hang waiting for it,
        // so a zero hash disables attacks outright — where a missing pose only costs PlayerBlock the pose.
        if (attackStateHash == 0 || attackAction == null)
        {
            Debug.LogError($"{name}: needs a '{actionLayer}' Animator layer holding '{attackState}' and an Attack action; disabling attacks.", this);
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

        // Overrides are keyed by the clip a state holds, not by the state's name.
        if (!Array.Exists(animator.runtimeAnimatorController.animationClips, clip => clip == defaultAttackClip))
        {
            Debug.LogError($"{name}: '{defaultAttackClip.name}' is not a clip this Animator plays; per-weapon attack animations are off.", this);
            return;
        }

        overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrides;
    }

    private void OnEnable()
    {
        attackAction.Enable();
        weaponState.Changed += OnWeaponChanged;

        ApplyAttackClip(weaponState.Get(WeaponSlot.MainHand));   // Catch up on what is already equipped.
    }

    private void OnDisable()
    {
        attackAction.Disable();
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

        if (overrides[defaultAttackClip] != clip) overrides[defaultAttackClip] = clip;
    }

    private void Update()
    {
        if (attackAction.WasPressedThisFrame()) TryAttack();
    }

    private void TryAttack()
    {
        if (IsAttacking) return;   // No combo buffering this stage.

        // Mid-dodge or staggered: the body is spoken for.
        if (ActionLock.AnyBlocking(actionLocks, this)) return;

        var weapon = weaponState.Get(WeaponSlot.MainHand);
        if (weapon == null) return;
        if (weaponState.GetHold(WeaponSlot.MainHand) == WeaponHoldState.Holstered) return;

        // Last of the gates: a swing refused above must not have paid for itself.
        if (!stamina.TryCommit(weapon.staminaCost)) return;

        // Before the CrossFade, or the opening frames play at the previous weapon's speed.
        animator.SetFloat(AttackSpeedHash, Mathf.Max(weapon.attackSpeed, 0.01f));
        animator.CrossFadeInFixedTime(attackStateHash, attackBlend, actionLayerIndex);

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
        var info = animator.GetCurrentAnimatorStateInfo(actionLayerIndex);

        if (animator.IsInTransition(actionLayerIndex))
        {
            var next = animator.GetNextAnimatorStateInfo(actionLayerIndex);
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
}
