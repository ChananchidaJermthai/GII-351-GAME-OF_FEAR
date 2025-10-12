using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController3D : MonoBehaviour
{
    [Header("Refs")]
    public Transform cameraTransform;
    public Transform groundCheck;
    public LayerMask groundMask = ~0;

    [Header("Move Speeds")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.5f;
    public float crouchSpeed = 2f;
    public float acceleration = 12f;
    public float deceleration = 14f;
    public float turnSpeed = 12f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float groundedRememberTime = 0.12f;

    [Header("Crouch")]
    public bool holdToCrouch = true;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float heightLerpSpeed = 12f;
    public float headClearCheckRadius = 0.2f;

    [Header("Animator (optional)")]
    public Animator animator;

    // internal
    private CharacterController controller;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction runAction;
    private InputAction crouchAction;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool runHeld;
    private bool crouchHeldOrToggled;

    private Vector3 velocity;
    private bool isGrounded;
    private float groundedTimer;

    private bool isRunning;
    private bool isCrouching;
    private float currentSpeed;
    private float desiredHeight;
    private float originalCenterY;

    const float groundCheckRadius = 0.25f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (standHeight <= 0f) standHeight = controller.height;
        if (crouchHeight <= 0f || crouchHeight >= standHeight)
            crouchHeight = Mathf.Max(0.6f, standHeight * 0.55f);

        desiredHeight = controller.height;
        originalCenterY = controller.center.y;
    }

    void OnEnable()
    {
        // ดึง actions ตามชื่อจาก PlayerInput
        var actions = playerInput.actions;
        moveAction = actions["Move"];
        jumpAction = actions["Jump"];
        runAction = actions["Run"];
        crouchAction = actions["Crouch"];

        // สมัคร callback
        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;

        jumpAction.started += OnJumpStarted;

        runAction.performed += OnRunPerformed;
        runAction.canceled += OnRunCanceled;

        crouchAction.performed += OnCrouchPerformed;
        crouchAction.canceled += OnCrouchCanceled;
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
        }
        if (jumpAction != null)
            jumpAction.started -= OnJumpStarted;

        if (runAction != null)
        {
            runAction.performed -= OnRunPerformed;
            runAction.canceled -= OnRunCanceled;
        }
        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouchPerformed;
            crouchAction.canceled -= OnCrouchCanceled;
        }
    }

    // -------- Input handlers --------
    void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    void OnJumpStarted(InputAction.CallbackContext ctx) => jumpPressed = true;
    void OnRunPerformed(InputAction.CallbackContext ctx) => runHeld = ctx.ReadValueAsButton();
    void OnRunCanceled(InputAction.CallbackContext ctx) => runHeld = false;

    void OnCrouchPerformed(InputAction.CallbackContext ctx)
    {
        if (holdToCrouch) crouchHeldOrToggled = true;
        else crouchHeldOrToggled = !crouchHeldOrToggled;
    }
    void OnCrouchCanceled(InputAction.CallbackContext ctx)
    {
        if (holdToCrouch) crouchHeldOrToggled = false;
    }

    void Update()
    {
        HandleGroundCheck();
        HandleCrouchState();
        HandleMoveAndRun();
        HandleJumpAndGravity();
        ApplyHeightChange();
        ApplyMovement();
        UpdateAnimator();
    }

    // ----- systems -----
    void HandleGroundCheck()
    {
        Vector3 checkPos = groundCheck
            ? groundCheck.position
            : (transform.position + Vector3.down * (controller.height * 0.5f - controller.radius + 0.05f));

        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            groundedTimer = groundedRememberTime;
            if (velocity.y < 0f) velocity.y = -2f;
        }
        else groundedTimer -= Time.deltaTime;
    }

    void HandleCrouchState()
    {
        bool wantCrouch = crouchHeldOrToggled;
        if (!wantCrouch && !CanStandUp()) wantCrouch = true;

        isCrouching = wantCrouch;
        desiredHeight = isCrouching ? crouchHeight : standHeight;
    }

    bool CanStandUp()
    {
        float castDist = standHeight - controller.height + 0.1f;
        if (castDist <= 0f) return true;

        Vector3 origin = transform.position + Vector3.up * (controller.radius + 0.05f);
        return !Physics.SphereCast(origin, headClearCheckRadius, Vector3.up, out _, castDist, groundMask, QueryTriggerInteraction.Ignore);
    }

    void HandleMoveAndRun()
    {
        Vector3 moveDir;
        if (cameraTransform)
        {
            Vector3 f = cameraTransform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cameraTransform.right; r.y = 0f; r.Normalize();
            moveDir = (f * moveInput.y + r * moveInput.x);
            moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        }
        else
        {
            moveDir = new Vector3(moveInput.x, 0f, moveInput.y);
            moveDir = Vector3.ClampMagnitude(moveDir, 1f);
        }

        isRunning = runHeld && !isCrouching && moveDir.sqrMagnitude > 0.001f;

        float maxSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        float target = maxSpeed * moveDir.magnitude;
        float rate = (target > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion rot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * turnSpeed);
        }

        Vector3 horiz = moveDir * currentSpeed;
        velocity.x = horiz.x;
        velocity.z = horiz.z;
    }

    void HandleJumpAndGravity()
    {
        if (jumpPressed && groundedTimer > 0f && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            groundedTimer = 0f;
            isGrounded = false;
        }
        jumpPressed = false;

        velocity.y += gravity * Time.deltaTime;
    }

    void ApplyHeightChange()
    {
        controller.height = Mathf.Lerp(controller.height, desiredHeight, Time.deltaTime * heightLerpSpeed);
        Vector3 c = controller.center;
        float targetCenterY = originalCenterY - (standHeight - controller.height) * 0.5f;
        c.y = Mathf.Lerp(c.y, targetCenterY, Time.deltaTime * heightLerpSpeed);
        controller.center = c;
    }

    void ApplyMovement()
    {
        controller.Move(velocity * Time.deltaTime);
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
    }

    void UpdateAnimator()
    {
        if (!animator) return;
        float planar = new Vector2(velocity.x, velocity.z).magnitude;
        animator.SetFloat("Speed", planar);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsRun", isRunning);
        animator.SetBool("IsCrouch", isCrouching);
        animator.SetFloat("yVelocity", velocity.y);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
