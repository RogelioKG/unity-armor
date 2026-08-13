using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person movement with a turn threshold: the body normally faces the camera
/// (so A/D strafe), but once input points far enough away it turns to face the
/// movement direction and runs forward. Backpedal clips become unnecessary.
///
/// Requires Animator parameters MoveX / MoveZ (Float) and Jump (Trigger) driving a
/// 2D Freeform Directional Blend Tree.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    private const float Never = -999f;

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

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    [Header("Animation")]
    [SerializeField] private float animDamping = 0.1f;
    [Tooltip("Must match the Blend Tree's inner ring thresholds.")]
    [SerializeField] private float walkRing = 1f;
    [Tooltip("Must match the Blend Tree's outer ring thresholds.")]
    [SerializeField] private float sprintRing = 2f;

    private Animator animator;
    private CharacterController controller;
    private InputAction moveAction, jumpAction, sprintAction;

    private Vector3 moveDirection;   // World space
    private Vector3 cameraForward;   // Camera's horizontal facing
    private float currentSpeed;
    private float verticalVelocity;
    private float lastGroundedTime = Never;
    private bool isSprinting;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();

        var actions = InputSystem.actions;
        moveAction = actions.FindAction("Move");
        jumpAction = actions.FindAction("Jump");
        sprintAction = actions.FindAction("Sprint");

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

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
        UpdateFacing();
        HandleJumpAndGravity();
        ApplyMovement();
        UpdateAnimator();
    }

    private void ReadInput()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Flatten Y, or a downward-looking camera makes the character walk into the ground.
        cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        moveDirection = cameraForward * input.y + cameraRight * input.x;
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        bool moving = input.sqrMagnitude > 0.01f;
        bool sprintHeld = sprintAction != null && sprintAction.IsPressed();

        isSprinting = moving && sprintHeld;
        currentSpeed = moving ? (isSprinting ? sprintSpeed : walkSpeed) : 0f;
    }

    /// <summary>moveDirection derives from the camera rather than the transform, so the
    /// threshold test has no feedback loop and cannot oscillate mid-turn.</summary>
    private void UpdateFacing()
    {
        if (currentSpeed <= 0f) return;

        Vector3 facing = Vector3.Angle(cameraForward, moveDirection) > turnThreshold
            ? moveDirection
            : cameraForward;

        float t = 1f - Mathf.Exp(-rotateSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(facing), t);
    }

    private void HandleJumpAndGravity()
    {
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;   // Keep pressing down so isGrounded stays stable.
        }
        else
        {
            verticalVelocity = Mathf.Max(
                verticalVelocity + gravity * Time.deltaTime, terminalVelocity);
        }

        if (jumpAction.WasPressedThisFrame() && Time.time - lastGroundedTime <= coyoteTime)
        {
            // Clamp gravity negative so a mis-set positive value can't produce NaN.
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Mathf.Min(gravity, -0.01f));
            lastGroundedTime = Never;   // Consume the window to prevent mid-air double jumps.

            animator.ResetTrigger(JumpHash);
            animator.SetTrigger(JumpHash);
        }
    }

    /// <summary>Call Move once per frame. Two calls make the horizontal one report isGrounded as false.</summary>
    private void ApplyMovement()
    {
        Vector3 velocity = moveDirection * currentSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>Convert to local space so the Blend Tree knows whether to strafe or run forward.</summary>
    private void UpdateAnimator()
    {
        Vector3 local = transform.InverseTransformDirection(moveDirection);
        float ring = isSprinting ? sprintRing : walkRing;

        animator.SetFloat(MoveXHash, local.x * ring, animDamping, Time.deltaTime);
        animator.SetFloat(MoveZHash, local.z * ring, animDamping, Time.deltaTime);
    }
}