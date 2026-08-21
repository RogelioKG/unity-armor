using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The guard: hold it up to eat part of an incoming hit, or catch the hit in its first instants
/// for a parry. Absorbing costs stamina, and running the pool dry breaks the guard into a stagger.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Stamina), typeof(Health))]
[RequireComponent(typeof(WeaponState), typeof(PlayerStagger))]
public class PlayerBlock : MonoBehaviour, IDamageModifier, IMovementOverride
{
    [Header("Block")]
    [Tooltip("Stamina paid per point of damage absorbed. Raising the guard itself is free.")]
    [SerializeField] private float blockStaminaPerDamage = 0.5f;
    [Tooltip("Half-angle from forward that the guard covers. Hits from behind land in full.")]
    [Range(0f, 180f)]
    [SerializeField] private float blockAngle = 100f;
    [Range(0f, 1f)]
    [SerializeField] private float blockMoveMultiplier = 0.1f;

    [Header("Parry")]
    [Tooltip("Window after the guard goes up where a hit is negated outright.")]
    [SerializeField] private float parryWindow = 0.2f;

    [Header("Animator")]
    [Tooltip("Layer the guard pose plays on. Must match PlayerAttack's and PlayerStagger's.")]
    [SerializeField] private string actionLayer = "Action";
    [SerializeField] private string blockState = "Block";
    [Tooltip("State to drop back to when the guard comes down.")]
    [SerializeField] private string emptyState = "Empty";
    [SerializeField] private float blend = 0.15f;

    private readonly List<IActionLock> actionLocks = new();

    private Animator animator;
    private Stamina stamina;
    private WeaponState weaponState;
    private Health health;
    private PlayerStagger stagger;
    private InputAction blockAction;

    private int actionLayerIndex;
    private int blockStateHash, emptyStateHash;

    private float guardRaisedTime;   // Only read while IsBlocking, so no sentinel needed.
    private int posePlaying;         // Last pose this component put up; zero while a lock owns the layer.

    /// <summary>Armor resolves after this, a dodge's i-frames before it.</summary>
    public int Order => 0;

    /// <summary>True while the guard is actually up — not overruled by exhaustion, a stagger, a dodge or a swing.</summary>
    public bool IsBlocking { get; private set; }

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

    /// <summary>Raised the moment a blocked hit empties the pool, just before the stagger starts.</summary>
    public event Action GuardBroken;

    int IMovementOverride.Priority => 20;

    bool IMovementOverride.IsActive => IsBlocking;

    // Scale, not drive: a guard slows the character rather than steering it.
    MovementIntent IMovementOverride.GetMovement() => MovementIntent.Scale(blockMoveMultiplier);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        stamina = GetComponent<Stamina>();
        weaponState = GetComponent<WeaponState>();
        health = GetComponent<Health>();
        stagger = GetComponent<PlayerStagger>();
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

        IsBlocking = false;
        posePlaying = 0;
    }

    /// <summary>Resolves whether the guard is up, then puts the action layer where that answer says.</summary>
    private void Update()
    {
        bool locked = ActionLock.AnyBlocking(actionLocks, null);
        bool wasBlocking = IsBlocking;

        IsBlocking = blockAction.IsPressed() && !stamina.IsExhausted && !locked;

        if (IsBlocking && !wasBlocking) guardRaisedTime = Time.time;

        if (locked)
        {
            posePlaying = 0;
            return;
        }

        int pose = IsBlocking ? blockStateHash : emptyStateHash;
        if (pose == posePlaying) return;

        posePlaying = pose;
        animator.PlayState(pose, blend, actionLayerIndex);
    }

    public float Modify(float amount, in DamageInfo info)
    {
        if (amount <= 0f || !IsBlocking || !IsFrontal(info)) return amount;

        if (Time.time - guardRaisedTime <= parryWindow)
        {
            Parried?.Invoke(info);
            return 0f;
        }

        float absorbed = Mathf.Min(amount, BlockPower);
        if (absorbed <= 0f) return amount;

        stamina.Spend(absorbed * blockStaminaPerDamage);
        if (stamina.IsExhausted) BreakGuard();

        return amount - absorbed;
    }

    // How long the punishment lasts and what it locks is not the guard's call.
    private void BreakGuard()
    {
        IsBlocking = false;

        GuardBroken?.Invoke();
        stagger.Trigger();
    }

    /// <summary>DamageInfo.Direction runs attacker to target, so flip it. Flattened: a hit from
    /// above is still a hit from the front.</summary>
    private bool IsFrontal(in DamageInfo info)
    {
        Vector3 incoming = -info.Direction;
        incoming.y = 0f;

        if (incoming.sqrMagnitude < 0.0001f) return true;

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
