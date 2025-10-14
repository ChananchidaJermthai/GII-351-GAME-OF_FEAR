using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public class PlayerController3D : MonoBehaviour
{
    // ------------------- References -------------------
    [Header("References")]
    [Tooltip("หัวกล้อง/ตำแหน่งสายตา (ใส่ CameraHolder)")]
    public Transform cameraHolder;
    [Tooltip("จุดเช็คพื้น (ไม่ใส่จะคำนวณอัตโนมัติจาก CharacterController)")]
    public Transform groundCheck;
    public LayerMask groundMask = ~0;

    [Header("Animator (optional)")]
    public Animator animator; // Speed(float) / IsGrounded(bool) / IsRun(bool) / IsCrouch(bool) / yVelocity(float)

    // ------------------- Movement -------------------
    [Header("Move Speeds (m/s)")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.5f;
    public float crouchSpeed = 2.0f;
    public float acceleration = 12f;
    public float deceleration = 14f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;  // meters
    public float gravity = -20f;
    public float groundedRememberTime = 0.12f; // coyote time

    [Header("Crouch (ตัวละคร + กล้องย่อตาม)")]
    public bool holdToCrouch = true;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float heightLerpSpeed = 12f;
    public float standCameraY = 1.6f;
    public float crouchCameraY = 1.0f;
    public float cameraLerpSpeed = 10f;
    public float headClearCheckRadius = 0.2f;

    [Header("FPS Look")]
    public float mouseSensitivity = 200f; // deg/sec
    public float pitchClamp = 85f;

    // ------------------- Stamina / Sprint -------------------
    [Header("Stamina")]
    public float staminaMax = 100f;
    public float staminaDrainRunPerSec = 20f;
    public float staminaDrainJump = 12f;
    public float staminaRegenPerSec = 18f;
    public float staminaRegenDelay = 0.75f;
    public float minStaminaToStartSprint = 15f;

    // ------------------- Footstep -------------------
    [Header("Footstep")]
    public AudioSource footstepSource;
    public AudioClip[] walkSteps;
    public AudioClip[] runSteps;
    public AudioClip[] crouchSteps;
    [Tooltip("เวลาขั้นต่ำระหว่างเสียง (กันติดสแปม)")]
    public float footstepMinInterval = 0.1f;
    [Tooltip("ระยะก้าว (หน่วยเมตร) – เดินจะยิงเมื่อเคลื่อนที่เกินค่านี้")]
    public float stepDistanceWalk = 1.8f;
    public float stepDistanceRun = 2.2f;
    public float stepDistanceCrouch = 1.6f;
    public Vector2 stepPitchRange = new Vector2(0.95f, 1.05f);
    public Vector2 stepVolRange = new Vector2(0.65f, 0.95f);

    // ------------------- Use Item -------------------
    [Header("Use Item")]
    public UnityEvent onUseItem;               // ผูกกับ Inventory/ไอเทมภายนอกใน Inspector
    [Tooltip("ถ้าเปิด จะ Raycast หาของที่เล็งไว้และเรียก IUsable.Use()")]
    public bool raycastUse = true;
    public float useDistance = 3.0f;
    public LayerMask useLayerMask = ~0;

    // ------------------- Internals -------------------
    CharacterController controller;
    PlayerInput playerInput;

    // Input actions
    InputAction moveAction, lookAction, jumpAction, runAction, crouchAction, useItemAction;

    // move state
    Vector2 moveInput;
    Vector2 lookInput;
    bool runHeld;
    bool crouchHeldOrToggled;
    bool jumpPressed;
    bool isGrounded;
    float groundedTimer;
    bool isRunning;
    bool isCrouching;
    float currentSpeed;
    float desiredHeight;
    float originalCenterY;
    Vector3 velocity; // includes gravity

    // camera look
    float yaw;
    float pitch;

    // stamina
    float stamina;
    float lastSprintOrJumpTime;

    // footsteps
    float stepAccumulator; // accumulate distance moved
    float lastFootTime;

    const float groundCheckRadius = 0.25f;

    // ------------------- Unity -------------------
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (standHeight <= 0f) standHeight = controller.height;
        if (crouchHeight <= 0f || crouchHeight >= standHeight)
            crouchHeight = Mathf.Max(0.6f, standHeight * 0.55f);

        desiredHeight = controller.height;
        originalCenterY = controller.center.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraHolder)
        {
            Vector3 e = cameraHolder.localEulerAngles;
            pitch = e.x;
        }
        yaw = transform.eulerAngles.y;

        stamina = staminaMax; // start full
    }

    void OnEnable()
    {
        var a = playerInput.actions;
        moveAction = a["Move"];
        lookAction = a["Look"];
        jumpAction = a["Jump"];
        runAction = a["Run"];
        crouchAction = a["Crouch"];
        useItemAction = a.FindAction("UseItem", false); // optional

        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        runAction?.Enable();
        crouchAction?.Enable();
        useItemAction?.Enable();

        if (jumpAction != null) jumpAction.started += OnJumpStarted;
        if (runAction != null) { runAction.performed += OnRunPerformed; runAction.canceled += OnRunCanceled; }
        if (crouchAction != null) { crouchAction.performed += OnCrouchPerformed; crouchAction.canceled += OnCrouchCanceled; }
        if (useItemAction != null) useItemAction.performed += OnUseItem;
    }

    void OnDisable()
    {
        if (jumpAction != null) jumpAction.started -= OnJumpStarted;
        if (runAction != null) { runAction.performed -= OnRunPerformed; runAction.canceled -= OnRunCanceled; }
        if (crouchAction != null) { crouchAction.performed -= OnCrouchPerformed; crouchAction.canceled -= OnCrouchCanceled; }
        if (useItemAction != null) useItemAction.performed -= OnUseItem;

        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        runAction?.Disable();
        crouchAction?.Disable();
        useItemAction?.Disable();
    }

    void Update()
    {
        ReadInputs();
        HandleLookFPS();
        HandleGroundCheck();
        HandleCrouchState();
        HandleStaminaAndSprintGate();
        HandleMove();
        HandleJumpAndGravity();
        ApplyHeightAndCamera();
        ApplyMovement();
        UpdateFootstepLoop();
        UpdateAnimator();
    }

    // ------------------- Input -------------------
    void ReadInputs()
    {
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    }

    void OnJumpStarted(InputAction.CallbackContext _) => jumpPressed = true;
    void OnRunPerformed(InputAction.CallbackContext ctx) => runHeld = ctx.ReadValueAsButton();
    void OnRunCanceled(InputAction.CallbackContext _) => runHeld = false;

    void OnCrouchPerformed(InputAction.CallbackContext _)
    {
        if (holdToCrouch) crouchHeldOrToggled = true;
        else crouchHeldOrToggled = !crouchHeldOrToggled;
    }
    void OnCrouchCanceled(InputAction.CallbackContext _)
    {
        if (holdToCrouch) crouchHeldOrToggled = false;
    }

    void OnUseItem(InputAction.CallbackContext _)
    {
        // 1) UnityEvent (เผื่อผูกกับ Inventory/Item Manager ภายนอก)
        onUseItem?.Invoke();

        // 2) (ทางเลือก) Raycast หาของที่เล็งแล้วเรียก IUsable.Use()
        if (raycastUse && cameraHolder)
        {
            if (Physics.Raycast(cameraHolder.position, cameraHolder.forward, out var hit, useDistance, useLayerMask, QueryTriggerInteraction.Ignore))
            {
                var usable = hit.collider.GetComponentInParent<IUsable>();
                usable?.Use();
            }
        }
    }

    // ------------------- Systems -------------------
    void HandleLookFPS()
    {
        if (!cameraHolder) return;

        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -pitchClamp, pitchClamp);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
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

    void HandleStaminaAndSprintGate()
    {
        // ถ้าไม่ได้กดวิ่ง → Regen หลังดีเลย์
        bool canRegen = !runHeld && isGrounded && (Time.time - lastSprintOrJumpTime) >= staminaRegenDelay;
        if (canRegen) stamina = Mathf.MoveTowards(stamina, staminaMax, staminaRegenPerSec * Time.deltaTime);

        // เปิด/ปิดวิ่งตาม Stamina
        bool allowSprint = stamina >= minStaminaToStartSprint && !isCrouching;
        isRunning = runHeld && allowSprint && (moveInput.sqrMagnitude > 0.001f);

        // Drain stamina เมื่อกำลังวิ่ง
        if (isRunning)
        {
            stamina = Mathf.Max(0f, stamina - staminaDrainRunPerSec * Time.deltaTime);
            lastSprintOrJumpTime = Time.time;
            if (stamina <= 0.01f) isRunning = false; // หมดแรงหยุดวิ่ง
        }
    }

    void HandleMove()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x);
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);

        float maxSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);
        float target = maxSpeed * moveDir.magnitude;
        float rate = (target > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);

        Vector3 horiz = moveDir * currentSpeed;
        velocity.x = horiz.x;
        velocity.z = horiz.z;
    }

    void HandleJumpAndGravity()
    {
        if (jumpPressed && groundedTimer > 0f && !isCrouching)
        {
            if (stamina >= staminaDrainJump || staminaMax <= 0f) // เผื่อไม่มี stamina system
            {
                velocity.y = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
                stamina = Mathf.Max(0f, stamina - staminaDrainJump);
                lastSprintOrJumpTime = Time.time;
                groundedTimer = 0f;
                isGrounded = false;
            }
        }
        jumpPressed = false; // consume
        velocity.y += gravity * Time.deltaTime;
    }

    void ApplyHeightAndCamera()
    {
        controller.height = Mathf.Lerp(controller.height, desiredHeight, Time.deltaTime * heightLerpSpeed);
        Vector3 c = controller.center;
        float targetCenterY = originalCenterY - (standHeight - controller.height) * 0.5f;
        c.y = Mathf.Lerp(c.y, targetCenterY, Time.deltaTime * heightLerpSpeed);
        controller.center = c;

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
        if (controller.isGrounded && velocity.y < 0f) velocity.y = -2f;
    }

    // ------------------- Footsteps -------------------
    void UpdateFootstepLoop()
    {
        if (footstepSource == null) return;

        // เล่นเฉพาะตอนติดพื้น + มีการเคลื่อนที่
        Vector3 horizVel = new Vector3(velocity.x, 0f, velocity.z);
        float planarSpeed = horizVel.magnitude;
        if (!isGrounded || planarSpeed < 0.2f) { stepAccumulator = 0f; return; }

        // สะสมระยะตามกริดตลอด (ใช้ความเร็ว x เวลา)
        stepAccumulator += planarSpeed * Time.deltaTime;

        float stepDist = isCrouching ? stepDistanceCrouch
                       : (isRunning ? stepDistanceRun : stepDistanceWalk);

        if (Time.time - lastFootTime < footstepMinInterval) return;
        if (stepAccumulator >= stepDist)
        {
            // เลือกคลิป
            AudioClip[] bank = isCrouching ? crouchSteps : (isRunning ? runSteps : walkSteps);
            if (bank != null && bank.Length > 0)
            {
                var clip = bank[Random.Range(0, bank.Length)];
                footstepSource.pitch = Random.Range(stepPitchRange.x, stepPitchRange.y);
                footstepSource.volume = Random.Range(stepVolRange.x, stepVolRange.y);
                footstepSource.PlayOneShot(clip);
            }
            lastFootTime = Time.time;
            stepAccumulator = 0f;
        }
    }

    // ------------------- Animator -------------------
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

    // ------------------- API / Interfaces -------------------
    public float Stamina01 => Mathf.Clamp01(stamina / Mathf.Max(0.0001f, staminaMax));

    // ใช้กับ onUseItem (Raycast)
    public interface IUsable { void Use(); }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (cameraHolder && raycastUse)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cameraHolder.position, cameraHolder.forward * useDistance);
        }
    }
}
