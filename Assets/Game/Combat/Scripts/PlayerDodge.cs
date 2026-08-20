using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The dodge: a committed burst of movement that owns the body, safe through its middle only.
/// Distance comes from the speed it started at, so a sprinting dodge covers more ground than a
/// walking one.
/// </summary>
[RequireComponent(typeof(Animator), typeof(Stamina), typeof(Health))]
[RequireComponent(typeof(PlayerController))]
public class PlayerDodge : MonoBehaviour, IDamageModifier, IMovementOverride, IActionLock, IEquipLock
{
    [Header("Dodge")]
    [SerializeField] private float dodgeStaminaCost = 25f;
    [Tooltip("Dodge speed from a standstill, where there is no locomotion speed to carry into it.")]
    [SerializeField] private float dodgeDefaultSpeed = 2f;
    [Tooltip("How long the dodge owns the body.")]
    [SerializeField] private float dodgeDuration = 0.8f;

    [Header("I-Frames")]
    [Tooltip("When the i-frames open, in seconds from the start of the dodge.")]
    [SerializeField] private float invulnerableStart = 0.1f;
    [Tooltip("When they close. The dodge is only safe through its middle, never on the recovery.")]
    [SerializeField] private float invulnerableEnd = 0.45f;

    [Header("Animator")]
    [Tooltip("Base layer state holding the dodge. A missing state only costs the pose: the dodge still moves and still has i-frames.")]
    [SerializeField] private string dodgeState = "Dodge";
    [Tooltip("Base layer state to hand movement back to when the dodge ends.")]
    [SerializeField] private string locomotionState = "Locomotion";
    [Tooltip("Crossfade time. The dodge turns the body over it too, so the turn stays hidden under the blend.")]
    [SerializeField] private float blend = 0.15f;

    private readonly List<IActionLock> actionLocks = new();

    private Animator animator;
    private Stamina stamina;
    private Health health;
    private PlayerController controller;
    private InputAction dodgeAction;

    private int dodgeStateHash, locomotionStateHash;

    private Vector3 dodgeDirection;
    private Quaternion turnFrom, turnTo;
    private float dodgeSpeed;
    private float dodgeStartTime;   // Only read while IsDodging, so no sentinel needed.

    /// <summary>First in the pipeline: a dodged hit never reaches the guard or the armor curve.</summary>
    public int Order => -10;

    public bool IsDodging { get; private set; }

    /// <summary>Open through the middle of the dodge only, so a panic dodge into an already-landing
    /// attack still gets hit.</summary>
    public bool IsInvulnerable
    {
        get
        {
            if (!IsDodging) return false;

            float elapsed = Time.time - dodgeStartTime;
            return elapsed >= invulnerableStart && elapsed <= invulnerableEnd;
        }
    }

    int IMovementOverride.Priority => 0;

    bool IMovementOverride.IsActive => IsDodging;

    // Drive, not scale: the dodge steers the body rather than slowing it.
    MovementIntent IMovementOverride.GetMovement() => MovementIntent.Drive(dodgeDirection * dodgeSpeed);

    bool IActionLock.BlocksActions => IsDodging;

    bool IEquipLock.BlocksEquip => IsDodging;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        stamina = GetComponent<Stamina>();
        health = GetComponent<Health>();
        controller = GetComponent<PlayerController>();
        GetComponents(actionLocks);

        dodgeAction = InputSystem.actions.FindAction("Dodge");

        if (dodgeAction == null)
        {
            Debug.LogError($"{name}: the Player action map needs a 'Dodge' action; disabling dodges.", this);
            enabled = false;
            return;
        }

        // A missing pose is a warning rather than a shutdown: the numbers work without it.
        dodgeStateHash = animator.ResolveState(0, dodgeState, this);
        locomotionStateHash = animator.ResolveState(0, locomotionState, this);
    }

    private void OnEnable()
    {
        dodgeAction.Enable();

        health.AddModifier(this);
    }

    private void OnDisable()
    {
        dodgeAction.Disable();

        health.RemoveModifier(this);

        // Polled, so a stale dodge would keep driving movement with nothing left to end it.
        IsDodging = false;
    }

    private void Update()
    {
        if (dodgeAction.WasPressedThisFrame()) TryDodge();

        TickDodge();
    }

    private void TryDodge()
    {
        if (IsDodging) return;

        // Mid-swing or staggered: the body is spoken for.
        if (ActionLock.AnyBlocking(actionLocks, this)) return;

        if (!stamina.TryCommit(dodgeStaminaCost)) return;

        dodgeDirection = DodgeDirection();

        dodgeSpeed = controller.CurrentSpeed > 0f ? controller.CurrentSpeed : dodgeDefaultSpeed;

        dodgeStartTime = Time.time;
        IsDodging = true;

        turnFrom = transform.rotation;
        turnTo = Quaternion.LookRotation(dodgeDirection);

        animator.PlayState(dodgeStateHash, blend, 0);
    }

    /// <summary>Where the player is asking to go, falling back to straight ahead. Read off the
    /// controller so the camera flattening stays in one place.</summary>
    private Vector3 DodgeDirection()
    {
        Vector3 steer = controller.SteerDirection;

        return steer.sqrMagnitude > 0.01f ? steer.normalized : transform.forward;
    }

    private void TickDodge()
    {
        if (!IsDodging) return;

        float elapsed = Time.time - dodgeStartTime;

        TurnIntoDodge(elapsed);

        if (elapsed < dodgeDuration) return;

        IsDodging = false;
        animator.PlayState(locomotionStateHash, blend, 0);
    }

    /// <summary>Turns under cover of the crossfade, then stops writing rotation at all: nothing
    /// else steers facing while the dodge drives movement.</summary>
    private void TurnIntoDodge(float elapsed)
    {
        if (elapsed > blend) return;

        transform.rotation = blend > 0f
            ? Quaternion.Slerp(turnFrom, turnTo, elapsed / blend)
            : turnTo;
    }

    /// <summary>I-frames answer everything, hits from behind included.</summary>
    public float Modify(float amount, in DamageInfo info) => IsInvulnerable ? 0f : amount;
}
