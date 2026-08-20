using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The guard: hold it up to eat part of an incoming hit, or catch the hit in its first instants
/// for a parry. Absorbing costs stamina, and running the pool dry breaks the guard into a stagger.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Stamina), typeof(Health))]
[RequireComponent(typeof(WeaponState))]
public class PlayerBlock : MonoBehaviour, IDamageModifier, IMovementOverride, IActionLock, IEquipLock
{
    private const float Never = float.NegativeInfinity;

    [Header("Block")]
    [Tooltip("Stamina paid per point of damage the guard absorbs. Raising the guard itself is free.")]
    [SerializeField] private float blockStaminaPerDamage = 0.5f;
    [Tooltip("Half-angle from the character's forward that the guard covers. Hits from behind land in full.")]
    [Range(0f, 180f)]
    [SerializeField] private float blockAngle = 100f;
    [Range(0f, 1f)]
    [SerializeField] private float blockMoveMultiplier = 0.1f;

    [Header("Parry")]
    [Tooltip("Window after the guard goes up where a hit is negated outright.")]
    [SerializeField] private float parryWindow = 0.2f;

    [Header("Guard Break")]
    [Tooltip("Seconds of stagger once a blocked hit empties the pool. No attacking or dodging out of it.")]
    [SerializeField] private float guardBreakStun = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float guardBreakMoveMultiplier = 0f;

    [Header("Animator")]
    [Tooltip("Layer the guard pose plays on. Must match PlayerAttack's.")]
    [SerializeField] private string actionLayer = "Action";
    [Tooltip("Action layer state holding the guard pose.")]
    [SerializeField] private string blockState = "Block";
    [Tooltip("Action layer state to drop back to when the guard comes down.")]
    [SerializeField] private string emptyState = "Empty";
    [SerializeField] private float blend = 0.15f;

    private readonly List<IActionLock> actionLocks = new();

    private Animator animator;
    private Stamina stamina;
    private WeaponState weaponState;
    private Health health;
    private InputAction blockAction;

    private int actionLayerIndex;
    private int blockStateHash, emptyStateHash;

    private float guardRaisedTime = Never;
    private float stunUntil = Never;
    private bool blockPosePlaying;

    /// <summary>Armor resolves after this, a dodge's i-frames before it.</summary>
    public int Order => 0;

    /// <summary>True while the guard is actually up — not overruled by exhaustion, a stagger, a dodge or a swing.</summary>
    public bool IsBlocking { get; private set; }

    /// <summary>Staggered by a broken guard: no attacking, no dodging, barely any movement.</summary>
    public bool IsStunned => Time.time < stunUntil;

    /// <summary>The off hand's shield, or the main hand weapon when the off hand is empty. A
    /// holstered item guards nothing.</summary>
    public float BlockPower
    {
        get
        {
            float offHand = PowerOf(WeaponSlot.OffHand);
            return offHand > 0f ? offHand : PowerOf(WeaponSlot.MainHand);
        }
    }

    /// <summary>Raised on a perfect block, carrying the hit that was negated.</summary>
    public event Action<DamageInfo> Parried;

    /// <summary>Raised the moment a blocked hit empties the pool.</summary>
    public event Action GuardBroken;

    int IMovementOverride.Priority => 20;

    bool IMovementOverride.IsActive => IsBlocking || IsStunned;

    // Scale, not drive: a guard slows the character rather than steering it.
    MovementIntent IMovementOverride.GetMovement() =>
        MovementIntent.Scale(IsStunned ? guardBreakMoveMultiplier : blockMoveMultiplier);

    // A guard can be dropped on any frame, so only the stagger locks.
    bool IActionLock.BlocksActions => IsStunned;

    bool IEquipLock.BlocksEquip => IsStunned;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        stamina = GetComponent<Stamina>();
        weaponState = GetComponent<WeaponState>();
        health = GetComponent<Health>();
        GetComponents(actionLocks);

        blockAction = InputSystem.actions.FindAction("Block");

        if (blockAction == null)
        {
            Debug.LogError($"{name}: the Player action map needs a 'Block' action; disabling the guard.", this);
            enabled = false;
            return;
        }

        actionLayerIndex = animator.GetLayerIndex(actionLayer);

        // A missing pose is a warning rather than a shutdown: the numbers work without it.
        blockStateHash = animator.ResolveState(actionLayerIndex, blockState, this);
        emptyStateHash = animator.ResolveState(actionLayerIndex, emptyState, this);
    }

    private void OnEnable()
    {
        blockAction.Enable();

        health.AddModifier(this);
    }

    private void OnDisable()
    {
        blockAction.Disable();

        health.RemoveModifier(this);

        // Polled, so a stale guard would keep scaling movement with nothing left to drop it.
        IsBlocking = false;
        blockPosePlaying = false;
    }

    /// <summary>Resolves whether the guard is up, then puts the pose where that answer says.</summary>
    private void Update()
    {
        bool locked = ActionLock.AnyBlocking(actionLocks, this);
        bool wasBlocking = IsBlocking;

        IsBlocking = blockAction.IsPressed() && !IsStunned && !stamina.IsExhausted && !locked;

        // Timed from the guard coming up, not the key press: one raised during a swing gets its
        // window when the swing lets go of the body.
        if (IsBlocking && !wasBlocking) guardRaisedTime = Time.time;

        // A swing owns the action layer and has already crossfaded over the guard pose. Drop the
        // bookkeeping instead of fighting it, and raise the guard again when the layer comes back.
        if (locked)
        {
            blockPosePlaying = false;
            return;
        }

        if (IsBlocking == blockPosePlaying) return;

        blockPosePlaying = IsBlocking;
        animator.PlayState(IsBlocking ? blockStateHash : emptyStateHash, blend, actionLayerIndex);
    }

    public float Modify(float amount, in DamageInfo info)
    {
        if (amount <= 0f) return amount;

        if (!IsBlocking || !IsFrontal(info)) return amount;

        if (Time.time - guardRaisedTime <= parryWindow)
        {
            Parried?.Invoke(info);
            return 0f;
        }

        float absorbed = Mathf.Min(amount, BlockPower);
        if (absorbed <= 0f) return amount;   // Nothing in hand worth guarding with, so no cost either.

        // Spend goes into debt rather than refusing: the price for overreaching is the stagger,
        // not a hit that half connected.
        stamina.Spend(absorbed * blockStaminaPerDamage);
        if (stamina.IsExhausted) BreakGuard();

        return amount - absorbed;
    }

    private void BreakGuard()
    {
        stunUntil = Time.time + guardBreakStun;
        IsBlocking = false;

        GuardBroken?.Invoke();
    }

    /// <summary>DamageInfo.Direction runs attacker to target, so flip it. Flattened: a hit from
    /// above is still a hit from the front.</summary>
    private bool IsFrontal(in DamageInfo info)
    {
        Vector3 incoming = -info.Direction;
        incoming.y = 0f;

        if (incoming.sqrMagnitude < 0.0001f) return true;   // Directionless damage: give it to the player.

        return Vector3.Angle(transform.forward, incoming) <= blockAngle;
    }

    private float PowerOf(WeaponSlot slot)
    {
        var weapon = weaponState.Get(slot);

        return weapon != null && weaponState.GetHold(slot) == WeaponHoldState.Drawn
            ? weapon.blockPower
            : 0f;
    }
}
