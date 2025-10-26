using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerController3D : MonoBehaviour
{
    // ===== Input =====
    [Header("Input (Input System)")]
    public PlayerInput playerInput;
    InputAction moveA, lookA, runA, crouchA, useItemA;

    // ===== Refs =====
    [Header("References")]
    public Transform cameraHolder;        // ยึดกล้องเพื่อหมุน/เลื่อน Y ตอน Crouch
    public Camera playerCamera;         // ถ้าไม่ได้ใช้ cameraHolder ให้กล้องจริงไว้ดู pitch
    public InventoryLite inventory;

    // ===== Movement =====
    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.5f;
    public float crouchSpeed = 2.0f;
    public float acceleration = 12f;
    public float deceleration = 14f;

    [Header("Crouch")]
    public bool crouchToggle = true;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.1f;
    public float heightLerpSpeed = 12f;

    [Header("Camera Crouch Offset")]
    public float standCamY = 1.6f;  // local Y ของ cameraHolder ตอนยืน
    public float crouchCamY = 1.0f;  // local Y ของ cameraHolder ตอนย่อ
    public float camLerpSpeed = 10f;

    [Header("Gravity (No Jump)")]
    public float gravity = -20f;
    public float stickToGroundForce = -2f;

    [Header("Mouse Look")]
    public float sensX = 1.2f, sensY = 1.2f;
    public float minPitch = -80f, maxPitch = 80f;

    // ===== Stamina =====
    [Header("Stamina")]
    public float staminaMax = 100f;
    public float staminaDrainPerSec = 22f;
    public float staminaRegenPerSec = 14f;
    public float regenDelay = 0.6f;
    public float minSprintToStart = 10f;
    public TMP_Text staminaText;

    // ===== Sanity =====
    [Header("Sanity")]
    public float sanityMax = 100f;
    public float sanityStart = 0f;
    public float sanityRegenPerSec = 5f;
    public Slider sanitySlider;
    public TMP_Text sanityText;

    // ===== Use Item =====
    [Header("Use Item Settings")]
    public string useItemKeyId = "Key";
    public float sanityCostPerUse = 10f;
    public TMP_Text useItemFeedbackText;
    public float feedbackHideDelay = 1.2f;

    // ===== Footstep =====
    [Header("Footstep (Loop)")]
    public bool footstepEnable = true;
    public AudioSource footstepSource;
    public AudioClip walkLoop, sprintLoop, crouchLoop;
    public float minSpeedForSound = 0.15f;
    [Range(0f, 1f)] public float walkVol = 0.8f, sprintVol = 1.0f, crouchVol = 0.55f;
    [Range(0f, 0.3f)] public float footstepFade = 0.08f;

    // ===== Ground / Controller =====
    [Header("Ground Fix / Controller")]
    public LayerMask groundMask = ~0;
    public bool liftFromGroundOnStart = true;
    public float liftClearance = 0.05f;
    public float groundProbeExtra = 0.1f;

    [Tooltip("เปิดไว้เพื่อ 'เคารพ' ค่าของ CharacterController จาก Inspector (height/center/radius) โดยไม่เขียนทับใน Awake)")]
    public bool respectControllerFromInspector = true;

    // ===== Runtime =====
    CharacterController cc;
    float yaw, pitch;
    Vector3 velocity;
    float currentSpeed;

    bool isSprinting, isCrouching;
    float stamina, lastSprintTime;
    float sanity;
    float feedbackTimer = -1f;

    // ค่าก้นแคปซูล (local) เพื่อยึดไว้ขณะปรับความสูง
    float capsuleBottomLocalY;

    // เก็บค่าเริ่มต้นของตำแหน่งกล้อง (X/Z คงไว้ เปลี่ยนเฉพาะ Y)
    Vector3 camLocalPos0;

    public bool IsSprinting => isSprinting;
    public bool IsCrouching => isCrouching;
    public float Stamina01 => Mathf.Clamp01(stamina / staminaMax);
    public float Sanity01 => Mathf.Clamp01(sanity / sanityMax);

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        if (playerInput && playerInput.actions)
        {
            moveA = playerInput.actions["Move"];
            lookA = playerInput.actions["Look"];
            runA = playerInput.actions["Run"];
            crouchA = playerInput.actions["Crouch"];
            useItemA = playerInput.actions["UseItem"];
        }

        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        if (!cameraHolder && playerCamera) cameraHolder = playerCamera.transform;
        if (cameraHolder) camLocalPos0 = cameraHolder ? cameraHolder.localPosition : Vector3.zero;

        if (!inventory) inventory = GetComponentInParent<InventoryLite>();

        // ไม่แตะต้องค่า height/center/radius ถ้า respectControllerFromInspector = true
        if (!respectControllerFromInspector)
        {
            // ตั้งค่าพื้นฐานให้ปลอดภัย ถ้าอยากให้สคริปต์เป็นคนกำหนด
            cc.stepOffset = Mathf.Max(cc.stepOffset, 0.25f);
            cc.skinWidth = Mathf.Max(cc.skinWidth, 0.02f);
            float minH = Mathf.Max(cc.radius * 2f + 0.01f, 1.2f);
            cc.height = Mathf.Max(cc.height, minH);
            var c0 = cc.center; c0.x = 0f; c0.z = 0f; c0.y = cc.height * 0.5f; cc.center = c0;
        }

        // คำนวณตำแหน่งก้นแคปซูลจากค่าปัจจุบันใน Inspector
        capsuleBottomLocalY = cc.center.y - cc.height * 0.5f;

        stamina = staminaMax;
        sanity = Mathf.Clamp(sanityStart, 0f, sanityMax);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!footstepSource)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = true;
            footstepSource.spatialBlend = 1f;
            footstepSource.rolloffMode = AudioRolloffMode.Logarithmic;
            footstepSource.minDistance = 1.5f;
            footstepSource.maxDistance = 18f;
            footstepSource.volume = 0f;
        }

        if (useItemFeedbackText) useItemFeedbackText.text = "";
        UpdateSanityUI();
        UpdateStaminaUI();
    }

    void Start()
    {
        if (liftFromGroundOnStart) LiftClearOfGround();
    }

    void OnEnable()
    {
        moveA?.Enable(); lookA?.Enable(); runA?.Enable(); crouchA?.Enable(); useItemA?.Enable();
    }
    void OnDisable()
    {
        moveA?.Disable(); lookA?.Disable(); runA?.Disable(); crouchA?.Disable(); useItemA?.Disable();
    }

    void Update()
    {
        // ===== Read Input =====
        Vector2 m = moveA != null ? moveA.ReadValue<Vector2>() : Vector2.zero;
        Vector2 l = lookA != null ? lookA.ReadValue<Vector2>() : Vector2.zero;
        bool wantSprint = runA != null && runA.IsPressed();
        bool crouchInput = false;
        if (crouchA != null) crouchInput = crouchToggle ? crouchA.WasPressedThisFrame() : crouchA.IsPressed();
        bool useItemPressed = useItemA != null && useItemA.WasPressedThisFrame();

        // ===== Look =====
        float yawDelta = l.x * sensX;
        float pitchDelta = -l.y * sensY;
        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraHolder) cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        else if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // ===== Crouch =====
        if (crouchToggle) { if (crouchInput) isCrouching = !isCrouching; }
        else { isCrouching = crouchInput; }
        if (isCrouching) isSprinting = false;

        // ===== Sprint & Stamina =====
        bool canMove = m.sqrMagnitude > 0.001f;
        bool canStartSprint = !isCrouching && canMove && stamina >= minSprintToStart;
        if (wantSprint && canStartSprint) isSprinting = true;
        if (!wantSprint || !canMove || stamina <= 0f) isSprinting = false;

        if (isSprinting)
        {
            stamina = Mathf.Max(0f, stamina - staminaDrainPerSec * Time.deltaTime);
            lastSprintTime = Time.time;
        }
        else if (Time.time - lastSprintTime >= regenDelay)
        {
            stamina = Mathf.Min(staminaMax, stamina + staminaRegenPerSec * Time.deltaTime);
        }
        UpdateStaminaUI();

        // ===== Move =====
        Vector3 wish = transform.right * m.x + transform.forward * m.y;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        float target = targetSpeed * wish.magnitude;
        float rate = (target > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);
        velocity.x = wish.x * currentSpeed;
        velocity.z = wish.z * currentSpeed;

        // ===== Grounded / Gravity =====
        bool grounded = IsGroundedSphere();
        velocity.y = grounded ? Mathf.Min(velocity.y, stickToGroundForce) : velocity.y + gravity * Time.deltaTime;

        // ===== Height Lerp & keep bottom =====
        float wantHeight = Mathf.Max(isCrouching ? crouchHeight : standHeight, cc.radius * 2f + 0.01f);
        cc.height = Mathf.Lerp(cc.height, wantHeight, Time.deltaTime * heightLerpSpeed);
        var c = cc.center;
        c.y = capsuleBottomLocalY + cc.height * 0.5f;   // ยึดก้นไว้เท่าเดิม
        cc.center = c;

        // ===== Camera Y Lerp (นี่แหละที่ทำให้กล้องย่อตาม) =====
        if (cameraHolder)
        {
            float targetCamY = isCrouching ? crouchCamY : standCamY;
            var local = cameraHolder.localPosition;
            local.y = Mathf.Lerp(local.y, targetCamY, Time.deltaTime * camLerpSpeed);
            // คง x/z เริ่มต้น
            local.x = camLocalPos0.x;
            local.z = camLocalPos0.z;
            cameraHolder.localPosition = local;
        }

        // ===== Apply Move =====
        cc.Move(velocity * Time.deltaTime);

        // ===== Footstep =====
        UpdateFootstep();

        // ===== Sanity & UseItem =====
        if (sanityRegenPerSec > 0f && sanity < sanityMax)
        {
            sanity = Mathf.Min(sanityMax, sanity + sanityRegenPerSec * Time.deltaTime);
            UpdateSanityUI();
        }
        if (useItemPressed) TryUseConfiguredItem();

        if (feedbackTimer >= 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f && useItemFeedbackText) { useItemFeedbackText.text = ""; feedbackTimer = -1f; }
        }
    }

    // ===== Ground helpers =====
    bool IsGroundedSphere()
    {
        Vector3 bottom = transform.TransformPoint(new Vector3(0f, capsuleBottomLocalY + cc.skinWidth + groundProbeExtra, 0f));
        float radius = cc.radius * 0.95f;
        return Physics.CheckSphere(bottom, radius, groundMask, QueryTriggerInteraction.Ignore);
    }

    void LiftClearOfGround()
    {
        float up = cc.skinWidth + Mathf.Max(0.01f, liftClearance);
        transform.position += Vector3.up * up;
    }

    // ===== Footstep =====
    void UpdateFootstep()
    {
        if (!footstepEnable || footstepSource == null) return;
        Vector3 v = cc.velocity; v.y = 0f; float speed = v.magnitude;
        bool moving = IsGroundedSphere() && speed >= minSpeedForSound;

        if (!moving)
        {
            if (footstepFade <= 0f) { if (footstepSource.isPlaying) footstepSource.Stop(); footstepSource.volume = 0f; }
            else
            {
                if (footstepSource.volume <= 0.001f && footstepSource.isPlaying) footstepSource.Stop();
                footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, 0f, Time.deltaTime / Mathf.Max(0.001f, footstepFade));
            }
            return;
        }

        AudioClip want = isCrouching ? (crouchLoop ? crouchLoop : walkLoop)
                        : (isSprinting ? (sprintLoop ? sprintLoop : walkLoop) : walkLoop);
        float vol = isCrouching ? crouchVol : (isSprinting ? sprintVol : walkVol);

        if (footstepSource.clip != want)
        {
            footstepSource.clip = want;
            if (want) footstepSource.Play();
        }
        else
        {
            if (want && !footstepSource.isPlaying) footstepSource.Play();
        }

        if (footstepFade > 0f)
            footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, vol, Time.deltaTime / Mathf.Max(0.001f, footstepFade));
        else
            footstepSource.volume = vol;
    }

    // ===== Use Item =====
    public void TryUseConfiguredItem()
    {
        if (string.IsNullOrEmpty(useItemKeyId)) { ShowFeedback("UseItem KeyID not set."); return; }
        if (!inventory) { ShowFeedback("No InventoryLite on player."); return; }

        bool ok = inventory.Consume(useItemKeyId, 1);
        if (ok)
        {
            if (sanityCostPerUse > 0f)
            {
                sanity = Mathf.Max(0f, sanity - sanityCostPerUse);
                UpdateSanityUI();
            }
            ShowFeedback($"Use {useItemKeyId} -1");
        }
        else
        {
            ShowFeedback($"Missing {useItemKeyId} !!");
        }
    }

    void ShowFeedback(string msg)
    {
        if (useItemFeedbackText) { useItemFeedbackText.text = msg; feedbackTimer = feedbackHideDelay; }
        else Debug.Log(msg);
    }

    // ===== UI =====
    void UpdateSanityUI()
    {
        if (sanitySlider) sanitySlider.value = Mathf.Clamp01(sanity / sanityMax);
        if (sanityText) sanityText.text = $"Sanity: {Mathf.RoundToInt(sanity)}/{Mathf.RoundToInt(sanityMax)}";
    }
    void UpdateStaminaUI()
    {
        if (staminaText) staminaText.text = $"Stamina: {Mathf.RoundToInt(stamina)}/{Mathf.RoundToInt(staminaMax)}";
    }

    void OnDrawGizmosSelected()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        Vector3 bottom = Application.isPlaying
            ? transform.TransformPoint(new Vector3(0f, capsuleBottomLocalY + cc.skinWidth + groundProbeExtra, 0f))
            : transform.position + Vector3.up * (cc.center.y - cc.height * 0.5f + cc.skinWidth + groundProbeExtra);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(bottom, cc ? cc.radius * 0.95f : 0.2f);
    }
}
