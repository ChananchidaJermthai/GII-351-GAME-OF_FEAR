using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController3D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("วาง Empty ชื่อ CameraHolder สูงระดับสายตา แล้วใส่ Main Camera เป็นลูกของมัน")]
    public Transform cameraHolder;        // จุดหมุนกล้อง (หัว)
    [Tooltip("วางใกล้ๆ เท้า ใช้เช็คพื้น (ถ้าเว้นว่างจะคำนวณจาก CharacterController)")]
    public Transform groundCheck;
    public LayerMask groundMask = ~0;     // เลเยอร์ที่ถือว่าเป็นพื้น/สิ่งกีดขวาง

    [Header("Move Speeds (m/s)")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.5f;
    public float crouchSpeed = 2.0f;
    public float acceleration = 12f;
    public float deceleration = 14f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;       // เมตร
    public float gravity = -20f;          // ติดลบ
    public float groundedRememberTime = 0.12f; // coyote time

    [Header("Crouch (ตัวละคร + กล้องลงตาม)")]
    [Tooltip("true = กดค้างเพื่อย่อ, false = กดสลับโหมดย่อ/ยืน")]
    public bool holdToCrouch = true;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float heightLerpSpeed = 12f;
    [Tooltip("ตำแหน่งสายตาตอนยืน (local Y ของ CameraHolder)")]
    public float standCameraY = 1.6f;
    [Tooltip("ตำแหน่งสายตาตอนย่อ (local Y ของ CameraHolder)")]
    public float crouchCameraY = 1.0f;
    public float cameraLerpSpeed = 10f;
    public float headClearCheckRadius = 0.2f;   // เช็คหัวชนเพดานตอนจะลุก

    [Header("Mouse Look (FPS)")]
    public float mouseSensitivity = 200f; // องศา/วินาที
    public float pitchClamp = 85f;        // จำกัดก้ม/เงย

    [Header("Animator (optional)")]
    public Animator animator;             // Speed(float), IsGrounded(bool), IsRun(bool), IsCrouch(bool), yVelocity(float)

    // -------- internal state --------
    CharacterController controller;
    PlayerInput playerInput;

    // InputActions (อ่านตามชื่อ)
    InputAction moveAction, lookAction, jumpAction, runAction, crouchAction;

    Vector2 moveInput;        // จาก Move
    Vector2 lookInput;        // จาก Look (Mouse Delta / Right Stick)
    bool runHeld;             // จาก Run (hold)
    bool crouchHeldOrToggled; // จาก Crouch (hold/toggle)
    bool jumpPressed;         // single-frame consume

    Vector3 velocity;         // x/z แนวนอน, y แรงโน้มถ่วง/กระโดด
    bool isGrounded;
    float groundedTimer;

    bool isRunning;
    bool isCrouching;
    float currentSpeed;
    float desiredHeight;
    float originalCenterY;

    float yaw;    // หมุนแกน Y ของตัวละคร (ซ้าย/ขวา)
    float pitch;  // หมุนแกน X ของกล้อง (ก้ม/เงย)

    const float groundCheckRadius = 0.25f;

    // ------------- Unity -------------
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (!cameraHolder)
        {
            Debug.LogWarning("[CombinedFPSController] ไม่ได้ตั้ง cameraHolder — กรุณาลาก Transform ของ CameraHolder มาวางใน Inspector");
        }

        if (standHeight <= 0f) standHeight = controller.height;
        if (crouchHeight <= 0f || crouchHeight >= standHeight)
            crouchHeight = Mathf.Max(0.6f, standHeight * 0.55f);

        desiredHeight = controller.height;
        originalCenterY = controller.center.y;

        // ล็อกเมาส์แบบ FPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // เริ่มต้นมุมกล้องตามค่าเริ่มต้น
        if (cameraHolder)
        {
            Vector3 e = cameraHolder.localEulerAngles;
            pitch = e.x;
        }
        yaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        var actions = playerInput.actions;
        moveAction = actions["Move"];
        lookAction = actions["Look"];
        jumpAction = actions["Jump"];
        runAction = actions["Run"];
        crouchAction = actions["Crouch"];

        // enable & subscribe (บางแพทเทิร์นไม่จำเป็นต้อง subscribe ถ้าอ่านใน Update)
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        runAction?.Enable();
        crouchAction?.Enable();

        if (jumpAction != null) jumpAction.started += OnJumpStarted;
        if (runAction != null) { runAction.performed += OnRunPerformed; runAction.canceled += OnRunCanceled; }
        if (crouchAction != null) { crouchAction.performed += OnCrouchPerformed; crouchAction.canceled += OnCrouchCanceled; }
    }

    void OnDisable()
    {
        if (jumpAction != null) jumpAction.started -= OnJumpStarted;
        if (runAction != null) { runAction.performed -= OnRunPerformed; runAction.canceled -= OnRunCanceled; }
        if (crouchAction != null) { crouchAction.performed -= OnCrouchPerformed; crouchAction.canceled -= OnCrouchCanceled; }

        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        runAction?.Disable();
        crouchAction?.Disable();
    }

    void Update()
    {
        ReadInputs();
        HandleLookFPS();
        HandleGroundCheck();
        HandleCrouchState();
        HandleMoveRun();
        HandleJumpGravity();
        ApplyHeightChangeAndCameraOffset();
        ApplyMovement();
        UpdateAnimator();
    }

    // ------------- Input -------------
    void ReadInputs()
    {
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        // runHeld, crouchHeldOrToggled และ jumpPressed จัดการใน callbacks
    }

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

    // ------------- Systems -------------
    void HandleLookFPS()
    {
        if (!cameraHolder) return;

        // lookInput ควรเป็น Mouse Delta (หรือ Right Stick) หน่วย "หน่วย/เฟรม"
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

        // หมุนตัวละครซ้าย/ขวา
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        // หมุนหัว (ก้ม/เงย)
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleGroundCheck()
    {
        Vector3 checkPos = groundCheck
            ? groundCheck.position
            : (transform.position + Vector3.down * (controller.height * 0.5f - controller.radius + 0.05f));

        isGrounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded)
        {
            groundedTimer = groundedRememberTime;
            if (velocity.y < 0f) velocity.y = -2f; // ติดพื้น
        }
        else
        {
            groundedTimer -= Time.deltaTime;
        }
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

    void HandleMoveRun()
    {
        // ใน FPS ใช้ทิศของตัวละคร (transform.forward/right)
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x);
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        isRunning = runHeld && !isCrouching && (moveDir.sqrMagnitude > 0.001f);

        float maxSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        float target = maxSpeed * moveDir.magnitude;
        float rate = (target > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        Vector3 horiz = moveDir * currentSpeed;
        velocity.x = horiz.x;
        velocity.z = horiz.z;
    }

    void HandleJumpGravity()
    {
        if (jumpPressed && groundedTimer > 0f && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            groundedTimer = 0f;
            isGrounded = false;
        }
        jumpPressed = false; // consume

        velocity.y += gravity * Time.deltaTime;
    }

    void ApplyHeightChangeAndCameraOffset()
    {
        // ปรับความสูง CharacterController ให้เนียน
        controller.height = Mathf.Lerp(controller.height, desiredHeight, Time.deltaTime * heightLerpSpeed);
        Vector3 c = controller.center;
        float targetCenterY = originalCenterY - (standHeight - controller.height) * 0.5f;
        c.y = Mathf.Lerp(c.y, targetCenterY, Time.deltaTime * heightLerpSpeed);
        controller.center = c;

        // กล้องย่อตาม
        if (cameraHolder)
        {
            float targetY = isCrouching ? crouchCameraY : standCameraY;
            Vector3 camLocal = cameraHolder.localPosition;
            camLocal.y = Mathf.Lerp(camLocal.y, targetY, Time.deltaTime * cameraLerpSpeed);
            cameraHolder.localPosition = camLocal;
        }
    }

    void ApplyMovement()
    {
        controller.Move(velocity * Time.deltaTime);

        // เอาไว้กันสั่นเมื่อแตะพื้นหลัง Move
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
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

    // ดีบักดูตำแหน่งเช็คพื้น
    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
