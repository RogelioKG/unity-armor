#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Play-mode harness for the damage pipeline, so it can be verified before there is anything
// to fight. Also registers a dummy modifier on request, which is how the ordering guarantee
// in Health.AddModifier gets exercised ahead of real armor and blocking.
//
// Editor-only, like the equipment testers: a component left on a shipped prefab deserializes
// as a missing script once the class is gone.
public class DamageTester : MonoBehaviour
{
    [Tooltip("Left empty, falls back to a Health on this GameObject.")]
    [SerializeField] private Health target;

    [Header("Test Hit")]
    [SerializeField] private float amount = 25f;
    [SerializeField] private DamageType type = DamageType.Slash;

    [Header("Dummy Modifier")]
    [Tooltip("Registers a flat reduction to prove the pipeline runs. Zero registers nothing.")]
    [SerializeField] private float flatReduction = 0f;
    [SerializeField] private int modifierOrder = 10;

    [Header("Keys")]
    [SerializeField] private Key damageKey = Key.Digit5;
    [SerializeField] private Key healKey = Key.Digit6;

    private FlatReduction dummy;

    private void OnEnable()
    {
        if (target == null) target = GetComponent<Health>();

        if (target == null)
        {
            Debug.LogError($"{name}: no Health to test against; disabling tester.", this);
            enabled = false;
            return;
        }

        target.Changed += OnChanged;
        target.Died += OnDied;

        if (flatReduction > 0f)
        {
            dummy = new FlatReduction(flatReduction, modifierOrder);
            target.AddModifier(dummy);
        }
    }

    private void OnDisable()
    {
        if (target == null) return;

        target.Changed -= OnChanged;
        target.Died -= OnDied;

        if (dummy != null) target.RemoveModifier(dummy);
        dummy = null;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[damageKey].wasPressedThisFrame) Hit();
        if (keyboard[healKey].wasPressedThisFrame) target.Heal(amount);
    }

    private void Hit()
    {
        // Land it on the target's face: Direction is attacker-to-target, so a frontal hit
        // travels along -forward. Blocking reads this later, so get it right now.
        var t = target.transform;
        var info = new DamageInfo(amount, type, t.position + Vector3.up, -t.forward, gameObject);

        float dealt = target.TakeDamage(info);
        Debug.Log($"swung for {amount:0.#} {type} → {dealt:0.#} dealt", target);
    }

    private void OnChanged(float current, float max)
        => Debug.Log($"health {current:0.#}/{max:0.#}", target);

    private void OnDied(DamageInfo info)
        => Debug.Log($"died to {info}", target);

    // Stand-in for ArmorMitigation until Stage 1 lands.
    private sealed class FlatReduction : IDamageModifier
    {
        private readonly float reduction;

        public FlatReduction(float reduction, int order)
        {
            this.reduction = reduction;
            Order = order;
        }

        public int Order { get; }

        public float Modify(float amount, in DamageInfo info) => amount - reduction;
    }
}
#endif
