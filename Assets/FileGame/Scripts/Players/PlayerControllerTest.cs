using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class PlayerControllerOptimized : MonoBehaviour
{
    #region Inspector
    [Header("Input Actions (.inputactions)")]
    public InputActionReference moveAction;
    public InputActionReference lookAction;
    public InputActionReference sprintAction;
    public InputActionReference crouchAction;
    public InputActionReference useItemAction;

    [Header("Refs")]
    public Camera playerCamera;
    public InventoryLite inventory;

    [Header("Move Speeds (m/s)")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.5f;
    public float crouchSpeed = 2.0f;

    [Header("Crouch")]
    public bool crouchToggle = true;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.2f;
    public float heightLerpSpeed = 12f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float stickToGroundForce = -2f;

    [Header("Mouse Look")]
    public float mouseSensitivityX = 1.2f;
    public float mouseSensitivityY = 1.2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Stamina")]
    public TMP_Text staminaText;
    public float staminaMax = 100f;
    public float staminaDrainPerSec = 22f;
    public float staminaRegenPerSec = 14f;
    public float regenDelay = 0.6f;
    public float minSprintToStart = 10f;

    [Header("Sanity")]
    public Slider sanitySlider;
    public TMP_Text sanityText;
    public float sanityMax = 100f;
    public float sanityStart = 0f;
    public float sanityRegenPerSec = 5f;

    [Header("Use Item")]
    public string useItemKeyId = "Key";
    public float sanityCostPerUse = 10f;
    public TMP_Text useItemFeedbackText;
    public float feedbackHideDelay = 1.5f;

    [Header("Fallback Key (InputSystem only)")]
    public Key keyUseItemIS = Key.E;
    Keyboard kb => Keyboard.current;
    Mouse ms => Mouse.current;

    [Header("Footsteps (Loop)")]
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
    #endregion

    #region Runtime State
    CharacterController _cc;
    float _pitch, _verticalVel;
    bool _isCrouching, _isSprinting;

    float _stamina, _lastSprintTime;
    float _sanity;
    float _feedbackTimer = -1f;

    float _currentTargetVol = 0f;
    float _capsuleBottomLocalY;

    float _lastSanity01 = -1f;
    #endregion

    #region Properties
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public float CurrentSpeedXZ => new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
    public float Stamina01 => Mathf.Clamp01(_stamina / staminaMax);
    public float Sanity01 => Mathf.Clamp01(_sanity / sanityMax);
    #endregion

    #region Unity Lifecycle
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

        _capsuleBottomLocalY = _cc.center.y - (_cc.height * 0.5f);

        _stamina = staminaMax;
        _sanity = Mathf.Clamp(sanityStart, 0f, sanityMax);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (useItemFeedbackText) useItemFeedbackText.text = "";

        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.spatialBlend = 1f;
        footstepSource.rolloffMode = AudioRolloffMode.Logarithmic;
        footstepSource.minDistance = 1.5f;
        footstepSource.maxDistance = 18f;
        footstepSource.volume = 0f;

        UpdateSanityUI(_sanity / sanityMax);
        UpdateStaminaUI();
    }

    void Update()
    {
        UpdateStaminaUI();
        UpdateFeedbackTimer();

        Vector2 move = ReadMoveIA();
        Vector2 look = ReadLookIA();
        bool wantSprint = ReadSprintIA();
        bool crouchPress = ReadCrouchIA();
        bool useItem = ReadUseItemIA();

        HandleCrouch(crouchPress);
        HandleSprintAndStamina(move, wantSprint);
        HandleLook(look);

        MoveCharacter(move);
        UpdateFootstepLoop();

        RegenerateSanity();
        if (useItem) TryUseConfiguredItem();
    }
    #endregion

    #region Character Motion
    void MoveCharacter(Vector2 move)
    {
        float targetSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? sprintSpeed : walkSpeed);
        Vector3 wishDir = transform.right * move.x + transform.forward * move.y;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
        Vector3 horizontalVel = wishDir * targetSpeed;

        _verticalVel = _cc.isGrounded ? stickToGroundForce : _verticalVel + gravity * Time.deltaTime;
        Vector3 motion = horizontalVel + Vector3.up * _verticalVel;

        float targetHeight = Mathf.Max(_cc.radius * 2f + 0.01f, _isCrouching ? crouchHeight : standHeight);
        _cc.height = Mathf.Lerp(_cc.height, targetHeight, Time.deltaTime * heightLerpSpeed);
        var c = _cc.center;
        c.y = _capsuleBottomLocalY + (_cc.height * 0.5f);
        _cc.center = c;

        _cc.Move(motion * Time.deltaTime);
    }
    #endregion

    #region Footsteps
    void UpdateFootstepLoop()
    {
        if (!footstepEnable || footstepSource == null) return;

        Vector3 vel = _cc.velocity; vel.y = 0f;
        bool isMoving = _cc.isGrounded && vel.sqrMagnitude >= minSpeedForSound * minSpeedForSound;

        AudioClip wantClip = _isCrouching ? crouchLoop : (_isSprinting ? sprintLoop : walkLoop);
        float wantVol = isMoving ? (_isCrouching ? crouchVolume : (_isSprinting ? sprintVolume : walkVolume)) : 0f;

        // Volume fade
        if (fadeTime > 0f)
            footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, wantVol, Time.deltaTime / fadeTime);
        else
            footstepSource.volume = wantVol;

        if (footstepSource.clip != wantClip)
        {
            footstepSource.clip = wantClip;
            if (wantClip && wantVol > 0f) footstepSource.Play();
        }
        else
        {
            if (wantVol > 0f && !footstepSource.isPlaying) footstepSource.Play();
            if (wantVol <= 0f && footstepSource.isPlaying) footstepSource.Stop();
        }
    }
    #endregion

    #region Sanity & Stamina
    void RegenerateSanity()
    {
        if (sanityRegenPerSec <= 0f || _sanity >= sanityMax) return;
        _sanity = Mathf.Min(sanityMax, _sanity + sanityRegenPerSec * Time.deltaTime);
        float sanity01 = _sanity / sanityMax;
        if (Mathf.Abs(sanity01 - _lastSanity01) > 0.001f) UpdateSanityUI(sanity01);
    }

    void UpdateSanityUI(float sanity01)
    {
        _lastSanity01 = sanity01;
        if (sanitySlider) sanitySlider.value = sanity01;
        if (sanityText) sanityText.text = $"Sanity: {(int)_sanity}/{(int)sanityMax}";
    }

    void UpdateStaminaUI()
    {
        if (staminaText) staminaText.text = $"Stamina: {(int)_stamina}/{(int)staminaMax}";
    }
    #endregion

    #region Use Item
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
                UpdateSanityUI(_sanity / sanityMax);
            }
            ShowFeedback($"Use {useItemKeyId} -1");
        }
        else ShowFeedback($"Missing {useItemKeyId} !!");
    }

    void ShowFeedback(string msg)
    {
        if (useItemFeedbackText)
        {
            useItemFeedbackText.text = msg;
            _feedbackTimer = feedbackHideDelay > 0f ? feedbackHideDelay : -1f;
        }
        else Debug.Log(msg);
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
    #endregion

    #region Input
    Vector2 ReadMoveIA()
    {
        if (moveAction?.action.enabled == true) return moveAction.action.ReadValue<Vector2>();
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
        if (lookAction?.action.enabled == true) return lookAction.action.ReadValue<Vector2>();
        var d = ms != null ? ms.delta.ReadValue() * 0.1f : Vector2.zero;
        return new Vector2(d.x, d.y);
    }

    bool ReadSprintIA() => sprintAction?.action.enabled == true ? sprintAction.action.IsPressed() : kb != null && kb.leftShiftKey.isPressed;

    bool ReadCrouchIA() => crouchAction?.action.enabled == true
        ? crouchToggle ? crouchAction.action.WasPressedThisFrame() : crouchAction.action.IsPressed()
        : kb != null && (crouchToggle ? kb.leftCtrlKey.wasPressedThisFrame : kb.leftCtrlKey.isPressed);

    bool ReadUseItemIA() => useItemAction?.action.enabled == true ? useItemAction.action.WasPressedThisFrame() : kb != null && kb[keyUseItemIS].wasPressedThisFrame;
    #endregion

    #region Core Handlers
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
        if (crouchToggle) if (inputCrouch) _isCrouching = !_isCrouching;
        else _isCrouching = inputCrouch;
        if (_isCrouching) _isSprinting = false;
    }

    void HandleSprintAndStamina(Vector2 move, bool wantSprint)
    {
        bool canMove = move.sqrMagnitude > 0.001f;
        bool canStartSprint = !_isCrouching && canMove && _stamina >= minSprintToStart;

        _isSprinting = wantSprint && canStartSprint;
        if (!_isSprinting || !canMove || _stamina <= 0f) _isSprinting = false;

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
    #endregion
}
