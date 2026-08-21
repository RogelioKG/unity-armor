using UnityEngine;

// Bridges the armor equipment state into Health's ordered mitigation pipeline.
[RequireComponent(typeof(ArmorState), typeof(Health))]
public class ArmorMitigation : MonoBehaviour, IDamageModifier
{
    private ArmorState armorState;
    private Health health;

    /// <summary>Armor resolves after blocking and other early-out defenses.</summary>
    public int Order => 10;

    /// <summary>Cached from ArmorState and refreshed only when equipment changes.</summary>
    public int Rating { get; private set; }

    private void Awake()
    {
        armorState = GetComponent<ArmorState>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (armorState == null) armorState = GetComponent<ArmorState>();
        if (health == null) health = GetComponent<Health>();

        if (armorState == null || health == null)
        {
            Debug.LogError($"{name}: ArmorMitigation requires ArmorState and Health; disabling.", this);
            enabled = false;
            return;
        }

        armorState.Changed += OnArmorChanged;
        Rating = armorState.TotalArmor;
        health.AddModifier(this);
    }

    private void OnDisable()
    {
        if (armorState != null) armorState.Changed -= OnArmorChanged;
        if (health != null) health.RemoveModifier(this);
    }

    /// <summary>Applies the two-part curve: linear reduction below the soft cap, then
    /// diminishing returns once armor is at least half the incoming damage.</summary>
    public float Modify(float amount, in DamageInfo info)
    {
        if (amount <= 0f || Rating <= 0) return amount;

        float armor = Rating;
        return armor < amount * 0.5f
            ? amount - armor * 0.5f
            : amount * amount / (armor * 2f);
    }

    private void OnArmorChanged(ArmorSlot slot, ArmorData item)
        => Rating = armorState.TotalArmor;
}