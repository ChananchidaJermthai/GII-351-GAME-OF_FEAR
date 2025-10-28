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
    public Transform cameraHolder;
    public Camera playerCamera;
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
    public float standCamY = 1.6f;
    public float crouchCamY = 1.0f;
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

    // ===== Footstep by Surface =====
    [Header("Footstep (Surface)")]
    public bool useSurfaceFootsteps = true;

    public enum SurfaceSource { Tag, PhysicMaterialName }
    [Tooltip("เลือกจะอ่านจาก Tag ของ Collider หรือจากชื่อ PhysicMaterial")]
    public SurfaceSource surfaceFrom = SurfaceSource.PhysicMaterialName;

    [Tooltip("เลเยอร์ที่ใช้ตรวจพื้นสำหรับเสียง")]
    public LayerMask surfaceRaycastMask = ~0;

    [Tooltip("ระยะยิง Ray ลงพื้นเพื่อหา Collider พื้น")]
    public float surfaceRaycastDistance = 1.2f;

    [System.Serializable]
    public class SurfaceSet
    {
        [Tooltip("ชื่อพื้นผิว: ถ้าใช้ Tag ให้ใส่ชื่อตรงกับ Tag; ถ้าใช้ PhysicMaterialName ให้ใส่ชื่อ material.name")]
        public string id = "Default";

        [Tooltip("คลิปฝีเท้าเมื่อเดินบนพื้นผิวนี้ (ไม่ตั้ง = fallback ไปใช้ walkLoop ปกติ)")]
        public AudioClip walk;

        [Tooltip("คลิปฝีเท้าเมื่อสปรินต์บนพื้นผิวนี้ (ไม่ตั้ง = fallback ไปใช้ sprintLoop ปกติ)")]
        public AudioClip sprint;

        [Tooltip("คลิปฝีเท้าเมื่อย่อง/ก้มบนพื้นผิวนี้ (ไม่ตั้ง = fallback ไปใช้ crouchLoop ปกติ)")]
        public AudioClip crouch;

        [Range(0f, 1f)]
        [Tooltip("ตัวคูณความดังเพิ่มเติมสำหรับพื้นผิวนี้")]
        public float volumeScale = 1f;
    }

    [Tooltip("แม็ปพื้นผิว -> ชุดคลิปเสียง")]
    public SurfaceSet[] surfaceSets;


    // ===== Ground / Controller =====
    [Header("Ground Fix / Controller")]
    public LayerMask groundMask = ~0;
    public bool liftFromGroundOnStart = true;
    public float liftClearance = 0.05f;
    public float groundProbeExtra = 0.1f;

    [Tooltip("เคารพค่า CharacterController จาก Inspector (ไม่เขียนทับใน Awake)")]
    public bool respectControllerFromInspector = true;

    // ===== Runtime =====
    CharacterController cc;
    float yaw, pitch;
    Vector3 velocity;
    float currentSpeed;

    bool isSprinting, isCrouching, controlLocked;
    float stamina, lastSprintTime;
    float sanity;
    float feedbackTimer = -1f;

    float capsuleBottomLocalY;
    Vector3 camLocalPos0;

    // ---- external look override / follow target ----
    float extYaw, extPitch, extBlendT, extBlendDur, extHoldT;
    bool lookOverride;
    Transform followTarget;     // <— เป้าหมายที่ให้กล้องตาม
    float followRotSpeed = 8f;  // ค่าความไวในการหันตาม

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
        if (cameraHolder) camLocalPos0 = cameraHolder.localPosition;

        if (!inventory) inventory = GetComponentInParent<InventoryLite>();

        if (!respectControllerFromInspector)
        {
            cc.stepOffset = Mathf.Max(cc.stepOffset, 0.25f);
            cc.skinWidth = Mathf.Max(cc.skinWidth, 0.02f);
            float minH = Mathf.Max(cc.radius * 2f + 0.01f, 1.2f);
            cc.height = Mathf.Max(cc.height, minH);
            var c0 = cc.center; c0.x = 0f; c0.z = 0f; c0.y = cc.height * 0.5f; cc.center = c0;
        }

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
        // ===== External look override / follow (กันดีด) =====
        bool usingOverride = ApplyExternalLookIfAny();

        // ===== Inputs =====
        Vector2 move = controlLocked ? Vector2.zero : (moveA != null ? moveA.ReadValue<Vector2>() : Vector2.zero);
        Vector2 look = (!usingOverride && !controlLocked && lookA != null) ? lookA.ReadValue<Vector2>() : Vector2.zero;
        bool wantSprint = !controlLocked && runA != null && runA.IsPressed();
        bool crouchInput = false;
        if (!controlLocked && crouchA != null)
            crouchInput = crouchToggle ? crouchA.WasPressedThisFrame() : crouchA.IsPressed();
        bool useItemPressed = !controlLocked && useItemA != null && useItemA.WasPressedThisFrame();

        // ===== Accumulate yaw/pitch — ใช้ที่เดียวเสมอ =====
        yaw += look.x * sensX;
        pitch = Mathf.Clamp(pitch - look.y * sensY, minPitch, maxPitch);

        // ===== Apply rotation =====
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraHolder) cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        else if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // ===== Crouch =====
        if (crouchToggle) { if (crouchInput) isCrouching = !isCrouching; }
        else { isCrouching = crouchInput; }
        if (isCrouching) isSprinting = false;

        // ===== Sprint & Stamina =====
        bool canMove = move.sqrMagnitude > 0.001f;
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
        Vector3 wish = transform.right * move.x + transform.forward * move.y;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float targetSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        float target = targetSpeed * wish.magnitude;
        float rate = (target > currentSpeed) ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.deltaTime);
        velocity.x = wish.x * currentSpeed;
        velocity.z = wish.z * currentSpeed;

        // ===== Ground / Gravity =====
        bool grounded = IsGroundedSphere();
        velocity.y = grounded ? Mathf.Min(velocity.y, stickToGroundForce) : velocity.y + gravity * Time.deltaTime;

        // ===== Height Lerp & keep bottom =====
        float wantHeight = Mathf.Max(isCrouching ? crouchHeight : standHeight, cc.radius * 2f + 0.01f);
        cc.height = Mathf.Lerp(cc.height, wantHeight, Time.deltaTime * heightLerpSpeed);
        var c = cc.center;
        c.y = capsuleBottomLocalY + cc.height * 0.5f;
        cc.center = c;

        // ===== Camera crouch offset =====
        if (cameraHolder)
        {
            float targetCamY = isCrouching ? crouchCamY : standCamY;
            var lp = cameraHolder.localPosition;
            lp.y = Mathf.Lerp(lp.y, targetCamY, Time.deltaTime * camLerpSpeed);
            lp.x = camLocalPos0.x; lp.z = camLocalPos0.z;
            cameraHolder.localPosition = lp;
        }

        // ===== Apply Move =====
        cc.Move(velocity * Time.deltaTime);

        // ===== Footstep =====
        UpdateFootstep();

        // ===== Sanity & Use Item =====
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

    // ===== External control for cutscene / trigger =====
    public void LockControl(bool locked)
    {
        controlLocked = locked;
        var pi = GetComponent<PlayerInput>();
        if (pi) pi.enabled = !locked;
        if (locked && TryGetComponent<CharacterController>(out var ch)) ch.Move(Vector3.zero);
    }

    // เริ่มโหมด "กล้องตามเป้า"
    public void StartLookFollow(Transform target, float rotateSpeed = 8f, bool lockControl = true)
    {
        followTarget = target;
        followRotSpeed = Mathf.Max(0.1f, rotateSpeed);
        if (lockControl) LockControl(true);
        lookOverride = true; // ให้ ApplyExternalLookIfAny ทำงาน
    }

    // หยุดตามเป้า
    public void StopLookFollow(bool unlockControl = true)
    {
        followTarget = null;
        lookOverride = false;
        if (unlockControl) LockControl(false);
    }

    // Look-at once (ยังคงมีอยู่ ใช้ได้เหมือนเดิม)
    public void LookAtWorld(Vector3 worldPos, float rotateSeconds = 0.35f, float holdSeconds = 0.6f)
    {
        Vector3 flat = worldPos - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude < 1e-4f) return;

        extYaw = Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;

        if (cameraHolder)
        {
            Vector3 camTo = worldPos - cameraHolder.position;
            Vector3 f = camTo.normalized;
            extPitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            extPitch = Mathf.Clamp(extPitch, minPitch, maxPitch);
        }
        else extPitch = 0f;

        extBlendT = 0f;
        extBlendDur = Mathf.Max(0.01f, rotateSeconds);
        extHoldT = Mathf.Max(0f, holdSeconds);
        lookOverride = true;
    }

    // อัปเดตมุมเมื่ออยู่ในโหมด override / follow
    bool ApplyExternalLookIfAny()
    {
        // โหมด "ตามเป้า": คำนวณ yaw/pitch เป้าหมาย "ทุกเฟรม"
        if (followTarget != null)
        {
            Vector3 targetPos = followTarget.position;
            // yaw
            Vector3 flat = targetPos - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
            {
                float targetYaw = Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
                float k = 1f - Mathf.Exp(-followRotSpeed * Time.deltaTime); // smooth lerp factor
                yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            }
            // pitch
            if (cameraHolder)
            {
                Vector3 camTo = targetPos - cameraHolder.position;
                Vector3 f = camTo.normalized;
                float targetPitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
                float k = 1f - Mathf.Exp(-followRotSpeed * Time.deltaTime);
                pitch = Mathf.Lerp(pitch, targetPitch, k);
            }
            return true; // ข้ามการอ่านเมาส์
        }

        // โหมด "หันไปรอบเดียว + hold"
        if (!lookOverride) return false;

        extBlendT += Time.deltaTime / Mathf.Max(0.01f, extBlendDur);
        float k2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(extBlendT));
        yaw = Mathf.LerpAngle(yaw, extYaw, k2);
        pitch = Mathf.Lerp(pitch, extPitch, k2);

        if (extBlendT >= 1f)
        {
            extHoldT -= Time.deltaTime;
            if (extHoldT <= 0f) lookOverride = false;
        }
        return true;
    }

    // ===== Helpers =====
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

    string GetSurfaceId()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, surfaceRaycastDistance, surfaceRaycastMask, QueryTriggerInteraction.Ignore))
        {
            if (surfaceFrom == SurfaceSource.Tag)
            {
                return hit.collider.tag; // อ่านจาก Tag
            }
            else
            {
                var pm = hit.collider.sharedMaterial;
                if (pm) return pm.name;   // อ่านจากชื่อ PhysicMaterial
            }
        }
        return "Default"; // ไม่เจออะไรให้ตกไป Default
    }

    bool TryGetClipsForSurface(string id, out AudioClip w, out AudioClip s, out AudioClip c, out float vScale)
    {
        if (surfaceSets != null)
        {
            for (int i = 0; i < surfaceSets.Length; i++)
            {
                var set = surfaceSets[i];
                if (!string.IsNullOrEmpty(set.id) && set.id == id)
                {
                    w = set.walk; s = set.sprint; c = set.crouch; vScale = set.volumeScale;
                    return true;
                }
            }
        }
        w = s = c = null; vScale = 1f;
        return false;
    }


    void UpdateFootstep()
    {
        if (!footstepEnable || footstepSource == null) return;

        Vector3 v = cc.velocity; v.y = 0f;
        float speed = v.magnitude;
        bool moving = IsGroundedSphere() && speed >= minSpeedForSound;

        if (!moving)
        {
            if (footstepFade <= 0f)
            {
                if (footstepSource.isPlaying) footstepSource.Stop();
                footstepSource.volume = 0f;
            }
            else
            {
                if (footstepSource.volume <= 0.001f && footstepSource.isPlaying) footstepSource.Stop();
                footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, 0f, Time.deltaTime / Mathf.Max(0.001f, footstepFade));
            }
            return;
        }

        // เดิม: เลือกคลิป/วอลุ่มตามสถานะการเดิน-วิ่ง-ก้ม
        AudioClip baseWant = isCrouching ? (crouchLoop ? crouchLoop : walkLoop)
                            : (isSprinting ? (sprintLoop ? sprintLoop : walkLoop) : walkLoop);
        float baseVol = isCrouching ? crouchVol : (isSprinting ? sprintVol : walkVol);

        AudioClip want = baseWant;
        float vol = baseVol;

        // ใหม่: Override ตามพื้นผิว (ถ้าเปิดใช้งาน)
        if (useSurfaceFootsteps)
        {
            string sid = GetSurfaceId();
            if (TryGetClipsForSurface(sid, out var wClip, out var sClip, out var cClip, out var vScale))
            {
                // เลือกคลิปตามโหมด แต่ถ้าไม่ได้เซ็ตคลิปไว้ ให้ fallback ไปที่ baseWant
                AudioClip overrideClip = isCrouching ? (cClip ? cClip : wClip)
                                       : (isSprinting ? (sClip ? sClip : wClip) : wClip);
                if (overrideClip) want = overrideClip;
                vol *= vScale; // เพิ่ม/ลดความดังตามพื้นผิว
            }
        }

        // เล่น/สลับคลิปตามปกติ (คง logic เดิม)
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

    public void AddSanity(float amount)
    {
        sanity = Mathf.Clamp(sanity + amount, 0f, sanityMax);
        UpdateSanityUI();
    }

    void ShowFeedback(string msg)
    {
        if (useItemFeedbackText) { useItemFeedbackText.text = msg; feedbackTimer = feedbackHideDelay; }
        else Debug.Log(msg);
    }

    void UpdateSanityUI()
    {
        if (sanitySlider) sanitySlider.value = Mathf.Clamp01(sanity / sanityMax);
        if (sanityText) sanityText.text = $" {Mathf.RoundToInt(sanity)}";
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
