using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person movement with a turn threshold: the body normally faces the camera (so A/D
/// strafe), but once input points far enough away it turns to face the movement direction and
/// runs forward. Backpedal clips become unnecessary.
///
/// Needs Animator parameters MoveX / MoveZ (Float) and Jump (Trigger) driving a 2D Freeform
/// Directional Blend Tree.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Animator), typeof(Stamina))]
public class PlayerController : MonoBehaviour
{
    private const float Never = float.NegativeInfinity;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float sprintSpeed = 5f;
    [SerializeField] private float rotateSpeed = 12f;

    [Header("Facing")]
    [Tooltip("Angle from the camera's forward beyond which the body turns to the movement direction instead of strafing.")]
    [Range(45f, 180f)]
    [SerializeField] private float turnThreshold = 100f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Grace period after leaving the ground where a jump is still allowed; absorbs isGrounded flicker.")]
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float terminalVelocity = -30f;

    [Header("Stamina")]
    [Tooltip("Drained for as long as the character is actually sprinting somewhere.")]
    [SerializeField] private float sprintStaminaPerSecond = 12f;
    [Tooltip("Paid on takeoff, into overdraft if the pool is short. Only an empty pool refuses.")]
    [SerializeField] private float jumpStaminaCost = 15f;
    [Tooltip("Stamina the pool has to climb back to before an interrupted sprint re-engages.")]
    [SerializeField] private float sprintResumeStamina = 10f;

    [Header("Animation")]
    [SerializeField] private float animDamping = 0.1f;
    [Tooltip("Must match the Blend Tree's inner ring thresholds.")]
    [SerializeField] private float walkRing = 1f;
    [Tooltip("Must match the Blend Tree's outer ring thresholds.")]
    [SerializeField] private float sprintRing = 2f;

    private Animator animator;
    private CharacterController controller;
    private Stamina stamina;
    private Transform cameraTransform;
    private InputAction moveAction, jumpAction, sprintAction;

    // Collected once: this controller knows nothing about attacking, blocking or dodging.
    private readonly List<IMovementOverride> movementOverrides = new();

    // A stagger has to stop the jump too, not just the ground speed.
    private readonly List<IActionLock> actionLocks = new();

    private Vector3 steerDirection;      // World space. What the player is asking for.
    private Vector3 cameraForward;       // Camera's horizontal facing.
    private Vector3 horizontalVelocity;  // What Move actually gets, m/s. Resolved once per frame.
    private float currentSpeed;
    private float verticalVelocity;
    private float lastGroundedTime = Never;
    private bool isSprinting;
    private bool movementDriven;         // An override is steering, not just slowing us down.

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    public Vector3 SteerDirection => steerDirection;

    /// <summary>Zero when the character is not asking to move, so callers that need a speed from a
    /// standstill have to supply their own.</summary>
    public float CurrentSpeed => currentSpeed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        stamina = GetComponent<Stamina>();
        GetComponents(movementOverrides);
        GetComponents(actionLocks);

        var actions = InputSystem.actions;
        moveAction = actions.FindAction("Move");
        jumpAction = actions.FindAction("Jump");
        sprintAction = actions.FindAction("Sprint");

        var mainCamera = Camera.main;
        if (mainCamera != null) cameraTransform = mainCamera.transform;

        if (cameraTransform == null || moveAction == null || jumpAction == null)
        {
            Debug.LogError($"{name}: missing camera or input actions; disabling controller.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
    }

    private void Update()
    {
        ReadInput();
        ResolveHorizontalVelocity();
        DrainSprintStamina();
        UpdateFacing();
        ApplyGravity();
        TryJump();
        ApplyMovement();
        UpdateAnimator();
    }

    private void ReadInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);

        steerDirection = Vector3.ClampMagnitude(cameraForward * input.y + cameraRight * input.x, 1f);

        bool moving = input.sqrMagnitude > 0.01f;
        bool sprintHeld = sprintAction != null && sprintAction.IsPressed();

        isSprinting = moving && sprintHeld && (isSprinting ? !stamina.IsExhausted : stamina.Current >= sprintResumeStamina);
        currentSpeed = moving ? (isSprinting ? sprintSpeed : walkSpeed) : 0f;
    }

    /// <summary>Billed for ground actually covered, so a sprint held through a swing that pins
    /// the character in place costs nothing.</summary>
    private void DrainSprintStamina()
    {
        if (isSprinting && horizontalVelocity.sqrMagnitude > 0.01f)
            stamina.Drain(sprintStaminaPerSecond);
    }

    /// <summary>The one place horizontal motion is decided. A single winner drives it, so
    /// overrides never stack their multipliers.</summary>
    private void ResolveHorizontalVelocity()
    {
        IMovementOverride winner = MovementOverride.SelectActive(movementOverrides);
        if (winner == null)
        {
            movementDriven = false;
            horizontalVelocity = steerDirection * currentSpeed;
            return;
        }

        MovementIntent intent = winner.GetMovement();
        movementDriven = intent.DrivesVelocity;
        horizontalVelocity = movementDriven
            ? intent.Velocity
            : steerDirection * (currentSpeed * intent.SpeedMultiplier);
    }

    /// <summary>steerDirection derives from the camera rather than the transform, so the
    /// threshold test has no feedback loop and cannot oscillate mid-turn.</summary>
    private void UpdateFacing()
    {
        // An override that steers the body owns its facing too. Tested against the resolved
        // velocity, so an override that scales movement to nothing pins the facing with it.
        if (movementDriven || horizontalVelocity.sqrMagnitude <= 0.0001f) return;

        Vector3 facing = Vector3.Angle(cameraForward, steerDirection) > turnThreshold
            ? steerDirection
            : cameraForward;

        float t = 1f - Mathf.Exp(-rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), t);
    }

    private void ApplyGravity()
    {
        if (!controller.isGrounded)
        {
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, terminalVelocity);
            return;
        }

        lastGroundedTime = Time.time;
        if (verticalVelocity < 0f) verticalVelocity = -2f;   // Keep pressing down so isGrounded stays stable.
    }

    private void TryJump()
    {
        if (!jumpAction.WasPressedThisFrame()) return;
        if (ActionLock.AnyBlocking(actionLocks, null)) return;
        if (Time.time - lastGroundedTime > coyoteTime) return;

        // Last of the gates: a jump refused above must not have paid for itself.
        if (!stamina.TryCommit(jumpStaminaCost)) return;

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Mathf.Min(gravity, -0.01f));
        lastGroundedTime = Never;

        animator.ResetTrigger(JumpHash);
        animator.SetTrigger(JumpHash);
    }

    /// <summary>Call Move once per frame. Two calls make the horizontal one report isGrounded as false.</summary>
    private void ApplyMovement()
        => controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

    /// <summary>Local space so the Blend Tree knows whether to strafe or run forward. Reads the
    /// velocity actually applied, not raw input, so overrides show up here too.</summary>
    private void UpdateAnimator()
    {
        // Ring thresholds are expressed per walk / sprint speed, so normalize by that speed.
        float reference = Mathf.Max(isSprinting ? sprintSpeed : walkSpeed, 0.01f);
        float scale = (isSprinting ? sprintRing : walkRing) / reference;
        Vector3 local = transform.InverseTransformDirection(horizontalVelocity);

        animator.SetFloat(MoveXHash, local.x * scale, animDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, local.z * scale, animDamping, Time.deltaTime);
    }
}
