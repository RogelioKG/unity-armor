#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

// Play-mode harness for the stamina pool and the guard break hanging off it: hold 5 to bleed the
// bar, tap 6 to take a hit from straight ahead, 7 to refill.
//
// Draining alone never staggers. The guard only breaks on a blocked hit whose absorb cost empties
// the pool, so a stagger takes both keys — bleed the bar low, hold block, then 6. An empty bar on
// its own just drops the guard, which is why the exhaustion and break logs are separate lines.
//
// The hit runs that real pipeline, so Health's `invulnerable` debug flag short-circuits it before
// the guard is ever consulted: with that ticked nothing drains and nothing breaks.
//
// Editor-only like the equipment testers: park it on a debug object in the test scene and drag the
// player's Stamina in. Health and PlayerBlock are read off that same GameObject.
public class StaminaTester : MonoBehaviour
{
    [SerializeField] private Stamina stamina;

    [Header("Keys")]
    [SerializeField] private Key drainKey = Key.Digit5;
    [SerializeField] private Key hitKey = Key.Digit6;
    [SerializeField] private Key refillKey = Key.Digit7;

    [Header("Drain")]
    [Tooltip("Stamina per second while the drain key is held. Floors at zero, the way sprinting does.")]
    [SerializeField] private float drainPerSecond = 40f;

    [Header("Fake Hit")]
    [SerializeField] private float hitDamage = 20f;
    [SerializeField] private DamageType hitType = DamageType.Slash;
    [Tooltip("Hits land in front by default. From behind they fall outside the guard's angle and are never blocked.")]
    [SerializeField] private bool hitFromBehind;

    private Health health;
    private PlayerBlock block;

    private void Awake()
    {
        if (stamina == null)
        {
            Debug.LogError($"{name}: drag the player's Stamina in; disabling the tester.", this);
            enabled = false;
            return;
        }

        health = stamina.GetComponent<Health>();
        block = stamina.GetComponent<PlayerBlock>();
    }

    private void OnEnable()
    {
        stamina.ExhaustedChanged += OnExhaustedChanged;

        if (block == null) return;

        block.GuardBroken += OnGuardBroken;
        block.Parried += OnParried;
    }

    private void OnDisable()
    {
        stamina.ExhaustedChanged -= OnExhaustedChanged;

        if (block == null) return;

        block.GuardBroken -= OnGuardBroken;
        block.Parried -= OnParried;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[drainKey].isPressed) stamina.Drain(drainPerSecond);
        if (keyboard[hitKey].wasPressedThisFrame) Hit();
        if (keyboard[refillKey].wasPressedThisFrame) Restore();
    }

    /// <summary>Sent through the damage pipeline rather than poked straight into Health, so the
    /// i-frames, the guard and the armor curve all get their say — that is the point of the key.</summary>
    private void Hit()
    {
        Transform target = stamina.transform;

        // Direction runs attacker to target, so a hit from the front points along -forward.
        Vector3 direction = hitFromBehind ? target.forward : -target.forward;

        var info = new DamageInfo(hitDamage, hitType, target.position, direction, gameObject);

        // Read across the call rather than off the bar afterwards: what the guard charged for the
        // hit is the thing under test, so the log states it instead of leaving it to be inferred.
        float before = stamina.Current;
        float dealt = health.TakeDamage(info);
        float spent = before - stamina.Current;

        // Whatever the guard did not absorb lands on health, so a long run of hits kills the player
        // and every hit after that returns zero without a word. There is no revive by design.
        if (!health.IsAlive)
            Debug.LogWarning($"{target.name} is dead — hits do nothing from here; restart play mode.", this);
    }

    /// <summary>Both bars. Blocked damage still comes through the guard, so a stamina-only refill
    /// would leave a test run dying of the hits it needs to make.</summary>
    private void Restore()
    {
        stamina.Refill();
        health.Heal(health.Max);
    }

    private void OnExhaustedChanged(bool exhausted)
        => Debug.Log(exhausted ? "stamina empty — guard drops, no stagger" : "stamina back in the black", this);

    private void OnGuardBroken()
        => Debug.Log($"guard broken — staggered, stamina {stamina.Current:0.#}", this);

    private void OnParried(DamageInfo info) => Debug.Log($"parried {info}", this);
}
#endif
