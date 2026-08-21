#if UNITY_EDITOR
using UnityEngine;

// A thing to hit while there is nothing to fight yet: put it on a cube with a Health and a
// collider, on the Enemy layer. Logs every hit, so a swing that lands twice shows up in the
// Console. Editor-only like the equipment testers; real death handling is Stage 5's business.
[RequireComponent(typeof(Health))]
public class HitDummy : MonoBehaviour
{
    [Tooltip("Seconds between the killing blow and the object leaving the scene.")]
    [SerializeField] private float despawnDelay = 0.5f;

    private Health health;

    private void Awake() => health = GetComponent<Health>();

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void OnDamaged(DamageInfo info)
        => Debug.Log($"{name} took {info} → {health.Current:0.#}/{health.Max:0.#}", this);

    private void OnDied(DamageInfo info)
    {
        Debug.Log($"{name} died to {info}", this);

        // Health has no revive, by design — set a large maxHealth to keep swinging at one target.
        Destroy(gameObject, despawnDelay);
    }
}
#endif
