using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerControllerTest : MonoBehaviour
{
    // ---------- INPUT ACTIONS ----------
    [Header("Input Actions (.inputactions)")]
    public InputActionReference moveAction;    // Vector2
    public InputActionReference lookAction;    // Vector2
    public InputActionReference sprintAction;  // Button
    public InputActionReference crouchAction;  // Button (toggle/hold)
    public InputActionReference useItemAction; // Button

    // ---------- REFS ----------
    [Header("Refs")]
    public Camera playerCamera;
    public InventoryLite inventory;

    // ---------- MOVE ----------
    [Header("Move Speeds (m/s)")]
    [Min(0f)] public float walkSpeed = 3.5f;
    [Min(0f)] public float sprintSpeed = 6.5f;
    [Min(0f)] public float crouchSpeed = 2.0f;

    [Header("Crouch")]
    public bool crouchToggle = true;
    [Min(0.8f)] public float standHeight = 1.8f;
    [Min(0.4f)] public float crouchHeight = 1.2f;
    [Min(1f)] public float heightLerpSpeed = 12f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float stickToGroundForce = -2f;

    [Header("Mouse Look")]
    public float mouseSensitivityX = 1.2f;
    public float mouseSensitivityY = 1.2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    // ---------- STAMINA ----------
    [Header("Stamina (for Sprint)")]
    public TMP_Text staminaText;
    [Min(0.1f)] public float staminaMax = 100f;
    [Min(0.1f)] public float staminaDrainPerSec = 22f;
    [Min(0.1f)] public float staminaRegenPerSec = 14f;
    [Min(0f)] public float regenDelay = 0.6f;
    [Min(0f)] public float minSprintToStart = 10f;

    // ---------- SANITY ----------
    [Header("Sanity (auto regen)")]
    public Slider sanitySlider; // Max=1
    public TMP_Text sanityText;
    [Min(0.1f)] public float sanityMax = 100f;
    [Min(0f)] public float sanityStart = 0f;
    [Min(0f)] public float sanityRegenPerSec = 5f;

    // ---------- USE ITEM ----------
    [Header("Use Item Settings")]
    public string useItemKeyId = "Key";
    public float sanityCostPerUse = 10f;
    public TMP_Text useItemFeedbackText;
    public float feedbackHideDelay = 1.5f;

    // ---------- FALLBACK KEYS (Input System only) ----------
    [Header("Fallback Keys (Input System)")]
    public Key keyUseItemIS = Key.E;
    Keyboard kb => Keyboard.current;
    Mouse ms => Mouse.current;

    // ---------- FOOTSTEPS : SIMPLE LOOP ----------
    [Header("Footstep (Simple Loop)")]
    public bool footstepEnable = true;
    public AudioSource footstepSource;
    public AudioClip walkLoop;
    public AudioClip sprintLoop;
    public AudioClip crouchLoop;
    public float minSpeedForSound = 0.15f;
    [Range(0f, 1f)] public float walkVolume = 0.8f;
    [Range(0f, 1f)] public float sprintVolume = 1.0f;
    [Range(0f, 1f)] public float crouchVolume = 0.55f;
    [Range(0f, 0.3f)] public float fadeTime = 0.08f;

    // ---------- RUNTIME ----------
    CharacterController _cc;
    float _pitch, _verticalVel;
    bool _isCrouching, _isSprinting;
    float _stamina, _lastSprintTime;
    float _sanity;
    float _feedbackTimer = -1f;

    // footstep fade
    float _currentTargetVol = 0f;
    float _fadeVel = 0f;

    // ---------- PUBLIC PROPERTIES (ให้สคริปต์อื่นเรียกใช้) ----------
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeedXZ => new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
    public float Stamina01 => Mathf.Clamp01(_stamina / staminaMax);
    public float Sanity01 => Mathf.Clamp01(_sanity / sanityMax);

    void OnEnable()
    {
        moveAction?.action.Enable();
        lookAction?.action.Enable();
        sprintAction?.action.Enable();
        crouchAction?.action.Enable();
        useItemAction?.action.Enable();
    }
    void OnDisable()
    {
        moveAction?.action.Disable();
        lookAction?.action.Disable();
        sprintAction?.action.Disable();
        crouchAction?.action.Disable();
        useItemAction?.action.Disable();
    }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        if (!inventory) inventory = GetComponentInParent<InventoryLite>();

        _stamina = staminaMax;
        _sanity = Mathf.Clamp(sanityStart, 0f, sanityMax);

        _cc.height = standHeight;
        var c = _cc.center; c.y = _cc.height * 0.5f; _cc.center = c;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (useItemFeedbackText) useItemFeedbackText.text = "";

        // Footstep AudioSource (loop)
        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.spatialBlend = 1f;
        footstepSource.rolloffMode = AudioRolloffMode.Logarithmic;
        footstepSource.minDistance = 1.5f;
        footstepSource.maxDistance = 18f;
        footstepSource.volume = 0f; // start silent

        UpdateSanityUI();
        UpdateStaminaUI();
    }

    void Update()
    {
        UpdateStaminaUI();
        UpdateFeedbackTimer();

        // Inputs
        Vector2 move = ReadMoveIA();
        Vector2 look = ReadLookIA();
        bool wantSprint = ReadSprintIA();
        bool crouchPress = ReadCrouchIA();
        bool useItem = ReadUseItemIA();

        HandleCrouch(crouchPress);
        HandleSprintAndStamina(move, wantSprint);
        HandleLook(look);

        // Move
        float targetSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
        Vector3 wishDir = (transform.right * move.x + transform.forward * move.y);
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
        Vector3 horizontalVel = wishDir * targetSpeed;

        _verticalVel = _cc.isGrounded ? stickToGroundForce : _verticalVel + gravity * Time.deltaTime;
        Vector3 motion = horizontalVel + Vector3.up * _verticalVel;

        float targetHeight = _isCrouching ? crouchHeight : standHeight;
        _cc.height = Mathf.Lerp(_cc.height, targetHeight, Time.deltaTime * heightLerpSpeed);
        var center = _cc.center; center.y = _cc.height * 0.5f; _cc.center = center;

        _cc.Move(motion * Time.deltaTime);

        // Footstep loop
        UpdateFootstepLoop();

        // Sanity regen + Use item
        RegenerateSanity();
        if (useItem) TryUseConfiguredItem();
    }

    // ===================== FOOTSTEP (LOOP) =====================
    void UpdateFootstepLoop()
    {
        if (!footstepEnable || footstepSource == null) return;

        Vector3 vel = _cc.velocity; vel.y = 0f;
        float speed = vel.magnitude;
        bool isMoving = _cc.isGrounded && speed >= minSpeedForSound;

        if (!isMoving)
        {
            _currentTargetVol = 0f;
            if (fadeTime <= 0f)
            {
                if (footstepSource.isPlaying) footstepSource.Stop();
                footstepSource.volume = 0f;
            }
            else
            {
                if (footstepSource.volume <= 0.001f && footstepSource.isPlaying)
                    footstepSource.Stop();
                footstepSource.volume = Mathf.SmoothDamp(footstepSource.volume, 0f, ref _fadeVel, fadeTime);
            }
            return;
        }

        AudioClip wantClip = _isCrouching ? (crouchLoop ? crouchLoop : walkLoop)
                          : (_isSprinting ? (sprintLoop ? sprintLoop : walkLoop)
                                          : walkLoop);

        float wantVol = _isCrouching ? crouchVolume
                      : (_isSprinting ? sprintVolume : walkVolume);

        if (footstepSource.clip != wantClip)
        {
            footstepSource.clip = wantClip;
            if (wantClip)
            {
                if (!footstepSource.isPlaying) footstepSource.Play();
            }
            else
            {
                footstepSource.Stop();
                return;
            }
        }
        else
        {
            if (wantClip && !footstepSource.isPlaying) footstepSource.Play();
        }

        _currentTargetVol = Mathf.Clamp01(wantVol);
        if (fadeTime > 0f)
            footstepSource.volume = Mathf.SmoothDamp(footstepSource.volume, _currentTargetVol, ref _fadeVel, fadeTime);
        else
            footstepSource.volume = _currentTargetVol;
    }

    // ===================== USE ITEM =====================
    void TryUseConfiguredItem()
    {
        if (string.IsNullOrEmpty(useItemKeyId)) { ShowFeedback("ไม่ได้ตั้ง KeyID ของไอเท็ม"); return; }
        if (!inventory) { ShowFeedback("ไม่พบ InventoryLite บนผู้เล่น"); return; }

        bool ok = inventory.Consume(useItemKeyId, 1);
        if (ok)
        {
            if (sanityCostPerUse > 0f)
            {
                _sanity = Mathf.Max(0f, _sanity - sanityCostPerUse);
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
        if (useItemFeedbackText)
        {
            useItemFeedbackText.text = msg;
            _feedbackTimer = feedbackHideDelay > 0f ? feedbackHideDelay : -1f;
        }
        else
        {
            Debug.Log(msg);
        }
    }
    void UpdateFeedbackTimer()
    {
        if (_feedbackTimer < 0f) return;
        _feedbackTimer -= Time.deltaTime;
        if (_feedbackTimer <= 0f && useItemFeedbackText)
        {
            useItemFeedbackText.text = "";
            _feedbackTimer = -1f;
        }
    }

    // ===================== INPUT READERS =====================
    Vector2 ReadMoveIA()
    {
        if (moveAction && moveAction.action.enabled) return moveAction.action.ReadValue<Vector2>();
        float x = 0f, y = 0f;
        if (kb != null)
        {
            x += kb.dKey.isPressed ? 1f : 0f; x -= kb.aKey.isPressed ? 1f : 0f;
            y += kb.wKey.isPressed ? 1f : 0f; y -= kb.sKey.isPressed ? 1f : 0f;
        }
        var v = new Vector2(x, y); if (v.sqrMagnitude > 1f) v.Normalize(); return v;
    }
    Vector2 ReadLookIA()
    {
        if (lookAction && lookAction.action.enabled) return lookAction.action.ReadValue<Vector2>();
        var d = ms != null ? ms.delta.ReadValue() * 0.1f : Vector2.zero;
        return new Vector2(d.x, d.y);
    }
    bool ReadSprintIA()
    {
        if (sprintAction && sprintAction.action.enabled) return sprintAction.action.IsPressed();
        return kb != null && kb.leftShiftKey.isPressed;
    }
    bool ReadCrouchIA()
    {
        if (crouchAction && crouchAction.action.enabled)
            return crouchToggle ? crouchAction.action.WasPressedThisFrame()
                                : crouchAction.action.IsPressed();
        if (kb == null) return false;
        return crouchToggle ? kb.leftCtrlKey.wasPressedThisFrame : kb.leftCtrlKey.isPressed;
    }
    bool ReadUseItemIA()
    {
        if (useItemAction && useItemAction.action.enabled) return useItemAction.action.WasPressedThisFrame();
        return kb != null && kb[keyUseItemIS].wasPressedThisFrame;
    }

    // ===================== LOOK / CROUCH / SPRINT =====================
    void HandleLook(Vector2 look)
    {
        float yawDelta = look.x * mouseSensitivityX;
        float pitchDelta = -look.y * mouseSensitivityY;

        transform.Rotate(0f, yawDelta, 0f);
        _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
        if (playerCamera) playerCamera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
    }

    void HandleCrouch(bool inputCrouch)
    {
        if (crouchToggle) { if (inputCrouch) _isCrouching = !_isCrouching; }
        else { _isCrouching = inputCrouch; }
        if (_isCrouching) _isSprinting = false;
    }

    void HandleSprintAndStamina(Vector2 move, bool wantSprint)
    {
        bool canMove = move.sqrMagnitude > 0.001f;
        bool canStartSprint = !_isCrouching && canMove && _stamina >= minSprintToStart;

        if (wantSprint && canStartSprint) _isSprinting = true;
        if (!wantSprint || !canMove || _stamina <= 0f) _isSprinting = false;

        if (_isSprinting)
        {
            _stamina = Mathf.Max(0f, _stamina - staminaDrainPerSec * Time.deltaTime);
            _lastSprintTime = Time.time;
        }
        else if (Time.time - _lastSprintTime >= regenDelay)
        {
            _stamina = Mathf.Min(staminaMax, _stamina + staminaRegenPerSec * Time.deltaTime);
        }
    }

    // ===================== SANITY =====================
    void RegenerateSanity()
    {
        if (sanityRegenPerSec > 0f && _sanity < sanityMax)
        {
            _sanity = Mathf.Min(sanityMax, _sanity + sanityRegenPerSec * Time.deltaTime);
            UpdateSanityUI();
        }
    }
    void UpdateSanityUI()
    {
        if (sanitySlider) sanitySlider.value = Mathf.Clamp01(_sanity / sanityMax);
        if (sanityText) sanityText.text = $"Sanity: {Mathf.RoundToInt(_sanity)}/{Mathf.RoundToInt(sanityMax)}";
    }
    void UpdateStaminaUI()
    {
        if (staminaText) staminaText.text = $"Stamina: {Mathf.RoundToInt(_stamina)}/{Mathf.RoundToInt(staminaMax)}";
    }
}
