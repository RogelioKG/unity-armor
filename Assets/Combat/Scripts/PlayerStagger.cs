using System;
using UnityEngine;

/// <summary>
/// The punishment state: a stretch where the body answers to nobody — no attacking, no dodging,
/// no guard, no equipping, barely any movement, and everything that lands hurts more. Whatever
/// earns one calls Trigger and says how long; this component neither knows nor cares which.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Health))]
public class PlayerStagger : MonoBehaviour, IDamageModifier, IMovementOverride, IActionLock, IEquipLock
{
    private const float Never = float.NegativeInfinity;

    [Header("Stagger")]
    [Tooltip("Seconds a stagger lasts when the source has no opinion. Callers with one pass their own.")]
    [SerializeField] private float defaultDuration = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float moveMultiplier = 0f;
    [Tooltip("Multiplies anything that lands while staggered. One turns the vulnerability off.")]
    [SerializeField] private float damageMultiplier = 2f;

    [Header("Animator")]
    [Tooltip("Layer the stagger plays on. Must match PlayerBlock's and PlayerAttack's.")]
    [SerializeField] private string actionLayer = "Action";
    [Tooltip("Missing, the lockout still runs — only the pose is skipped.")]
    [SerializeField] private string staggerState = "Stagger";
    [SerializeField] private float blend = 0.15f;

    private Animator animator;
    private Health health;
    private int actionLayerIndex;
    private int staggerStateHash;
    private float staggerUntil = Never;
    private float staggerStartTime;   // Only read while IsStaggered, so no sentinel needed.

    /// <summary>Everything else reads this through the lock interfaces, not directly: the point
    /// of the component is that nobody has to know staggering exists to respect it.</summary>
    public bool IsStaggered => Time.time < staggerUntil;

    /// <summary>Raised on every Trigger, extensions included. For UI, audio, camera shake.</summary>
    public event Action Staggered;

    /// <summary>Last in the pipeline, so it doubles what the guard and the armor curve left rather
    /// than the attacker's raw swing.</summary>
    public int Order => 20;

    /// <summary>Amplifies what lands during a stagger, never the hit that started it: a same-frame
    /// hit is the cause, not the consequence.</summary>
    public float Modify(float amount, in DamageInfo info)
        => IsStaggered && Time.time > staggerStartTime ? amount * damageMultiplier : amount;

    // Below attack and block, above nothing that drives: if a stagger ever lands mid-dodge,
    // the dodge that is already steering the body keeps it.
    int IMovementOverride.Priority => 5;

    bool IMovementOverride.IsActive => IsStaggered;

    MovementIntent IMovementOverride.GetMovement() => MovementIntent.Scale(moveMultiplier);

    bool IActionLock.BlocksActions => IsStaggered;

    bool IEquipLock.BlocksEquip => IsStaggered;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();
        actionLayerIndex = animator.GetLayerIndex(actionLayer);

        // A missing pose is a warning rather than a shutdown: the lockout works without it.
        staggerStateHash = animator.ResolveState(actionLayerIndex, staggerState, this);
    }

    private void OnEnable() => health.AddModifier(this);

    private void OnDisable()
    {
        health.RemoveModifier(this);

        // Polled, so a stale timer would keep the body locked with nothing left to release it.
        staggerUntil = Never;
    }

    /// <summary>Starts a stagger of this component's own length.</summary>
    public void Trigger() => Trigger(defaultDuration);

    /// <summary>Starts a stagger of the given length. The pose is a loop, so a twitch and a long
    /// collapse share one clip. Triggering during a stagger extends it rather than restarting it.</summary>
    public void Trigger(float seconds)
    {
        bool wasStaggered = IsStaggered;
        staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);

        // Crossfading again would pin the pose at the start of its blend, and the loop is already
        // running. Keeping the start time also stops an extension re-opening the same-frame window.
        if (!wasStaggered)
        {
            staggerStartTime = Time.time;
            animator.PlayState(staggerStateHash, blend, actionLayerIndex);
        }

        Staggered?.Invoke();
    }
}
