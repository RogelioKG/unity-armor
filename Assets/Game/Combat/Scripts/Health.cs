using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hit points, plus the mitigation pipeline that guards them. The player and every enemy share
/// this one component; what differs between them is which IDamageModifiers register, not the
/// class. An enemy with no armor and no shield simply has an empty pipeline.
///
/// Nothing here touches visuals, animation or physics. Reactions subscribe to Damaged and Died,
/// the same way equipment renderers subscribe to EquipmentState.Changed.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Health to start with. Zero or less means start at full.")]
    [SerializeField] private float startingHealth = 0f;

    [Header("Debug")]
    [Tooltip("Ignores all incoming damage. For testing; leave off on anything that ships.")]
    [SerializeField] private bool invulnerable;

    // Kept sorted on insert rather than sorted per hit: equipping is rare, being hit is not.
    private readonly List<IDamageModifier> modifiers = new();

    /// <summary>Raised on every change to Current, heals included. Arguments are current, max.</summary>
    public event Action<float, float> Changed;

    /// <summary>Raised only when a hit actually removed health. Drives stagger, hit flash, audio.
    /// The DamageInfo carries the post-mitigation amount, not what the attacker swung for.</summary>
    public event Action<DamageInfo> Damaged;

    /// <summary>Raised once, on the hit that emptied the bar. Later hits are ignored outright,
    /// so this can never fire twice.</summary>
    public event Action<DamageInfo> Died;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsAlive => Current > 0f;

    /// <summary>Current over max, clamped to 0..1. Health bars want this, not the raw figures.</summary>
    public float Normalized => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

    private void Awake()
    {
        Current = startingHealth > 0f ? Mathf.Min(startingHealth, maxHealth) : maxHealth;
    }

    private void Start()
    {
        // Deferred out of Awake so listeners that subscribe in their own Awake still catch it.
        // Anything enabled later must read Current itself, the way EquipmentRenderer replays Worn.
        Changed?.Invoke(Current, maxHealth);
    }

    public float TakeDamage(in DamageInfo info)
    {
        if (!IsAlive || invulnerable) return 0f;

        float amount = info.Amount;
        for (int i = 0; i < modifiers.Count; i++)
        {
            amount = modifiers[i].Modify(amount, info);

            // Fully absorbed. Stop here so a dodge's i-frames cannot be undone by a later
            // modifier, and so the armor curve never runs on a hit that already dealt nothing.
            if (amount <= 0f) return 0f;
        }

        amount = Mathf.Min(amount, Current);   // Report health actually lost, not the overkill.
        Current -= amount;

        var applied = info.WithAmount(amount);
        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(applied);

        if (!IsAlive) Died?.Invoke(applied);

        return amount;
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;   // Reviving the dead is a separate concern.

        float healed = Mathf.Min(amount, maxHealth - Current);
        if (healed <= 0f) return;               // Already full; stay quiet rather than spam Changed.

        Current += healed;
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>Registers a mitigation source, ordered by IDamageModifier.Order. Duplicates are
    /// ignored so a mismatched OnEnable/OnDisable pair cannot stack the same modifier twice.</summary>
    public void AddModifier(IDamageModifier modifier)
    {
        if (modifier == null || modifiers.Contains(modifier)) return;

        // Insertion sort.
        int index = modifiers.Count;
        while (index > 0 && modifiers[index - 1].Order > modifier.Order)
            index--;

        modifiers.Insert(index, modifier);
    }

    public void RemoveModifier(IDamageModifier modifier) => modifiers.Remove(modifier);
}
