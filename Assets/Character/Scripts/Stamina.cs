using System;
using UnityEngine;

/// <summary>
/// The pool every committed action spends from: sprinting drains it, a swing or a dodge takes a
/// lump sum, a raised guard pays for what it absorbs.
///
/// Any stamina at all is enough to commit an action, and the cost is paid even when it drives the
/// pool below zero. That debt is the lockout — nothing starts until it is paid off — so the price
/// of overreaching scales with how far you overreached, and the bar reads empty for exactly as
/// long as the player cannot act.
/// </summary>
public class Stamina : MonoBehaviour
{
    private const float Never = float.NegativeInfinity;

    [Header("Pool")]
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("How far below zero a commit may push the pool, as a fraction of max. Deeper overdraft means a longer lockout.")]
    [Range(0f, 1f)]
    [SerializeField] private float overdraftLimit = 0.5f;

    [Header("Regeneration")]
    [SerializeField] private float regenPerSecond = 20f;
    [Tooltip("Quiet period after the last spend before regen resumes.")]
    [SerializeField] private float regenDelay = 1f;

    private float lastSpendTime = Never;

    /// <summary>Raised on every change to Current, regen included. Arguments are current, max.</summary>
    public event Action<float, float> Changed;

    /// <summary>Raised when the pool empties and again when it climbs back into the black.</summary>
    public event Action<bool> ExhaustedChanged;

    /// <summary>Negative while in debt. Bars want Normalized; only the pool reads this raw.</summary>
    public float Current { get; private set; }

    public float Max => maxStamina;

    /// <summary>Current over max, clamped to 0..1. Debt reads as an empty bar, not a missing one.</summary>
    public float Normalized => maxStamina > 0f ? Mathf.Clamp01(Current / maxStamina) : 0f;

    /// <summary>Empty or in debt. Every action refuses to start while it holds.</summary>
    public bool IsExhausted => Current <= 0f;

    /// <summary>Hard floor on Current, and so the deepest a single commit can dig.</summary>
    private float Floor => -maxStamina * overdraftLimit;

    private bool IsRecovering => Current < maxStamina && Time.time - lastSpendTime >= regenDelay;

    private void Awake() => Current = maxStamina;

    // Deferred out of Awake so listeners that subscribe in their own Awake still catch it.
    private void Start() => Changed?.Invoke(Current, maxStamina);

    private void Update()
    {
        if (IsRecovering) SetCurrent(Current + regenPerSecond * Time.deltaTime);
    }

    /// <summary>Commits a discrete action, into debt if the cost outruns the pool. Refuses only on
    /// empty, and a refusal costs nothing, so it does not push the regen delay out either.</summary>
    public bool TryCommit(float cost)
    {
        if (IsExhausted) return false;

        Spend(cost);
        return true;
    }

    /// <summary>Takes a cost with no say in the matter, returning what was actually taken — short
    /// of the ask only at the floor. Guarding spends this way: the hit is absorbed whether or not
    /// there is stamina for it, and the debt left behind is what breaks the guard.</summary>
    public float Spend(float amount)
    {
        if (amount <= 0f) return 0f;

        float before = Current;
        lastSpendTime = Time.time;
        SetCurrent(Current - amount);

        return before - Current;
    }

    /// <summary>Continuous drain for sprinting, stopping at empty rather than running into debt:
    /// you overdraw by committing to something, not by jogging. Safe to call every frame.</summary>
    public void Drain(float amountPerSecond)
    {
        if (IsExhausted) return;

        Spend(Mathf.Min(amountPerSecond * Time.deltaTime, Current));
    }

    /// <summary>Back to full, debt cleared, regen delay dropped.</summary>
    public void Refill()
    {
        lastSpendTime = Never;
        SetCurrent(maxStamina);
    }

    private void SetCurrent(float value)
    {
        value = Mathf.Clamp(value, Floor, maxStamina);
        if (value == Current) return;

        bool wasExhausted = IsExhausted;
        Current = value;
        Changed?.Invoke(Current, maxStamina);

        if (IsExhausted != wasExhausted) ExhaustedChanged?.Invoke(IsExhausted);
    }
}
