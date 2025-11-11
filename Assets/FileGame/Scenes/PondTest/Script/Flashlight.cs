using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
public class Flashlight : MonoBehaviour
{
    [Header("References")]
    public Light spot;
    public AudioSource audioSrc;
    public AudioClip sfxToggleOn;
    public AudioClip sfxToggleOff;
    public AudioClip sfxReload;
    public AudioClip sfxSputter;

    [Header("Inventory (Optional)")]
    public InventoryLite playerInventory;
    public string batteryItemId = "Battery";

    [Header("Input Fallback (If no PlayerInput)")]
    public Key toggleKey = Key.F;
    public Key reloadKey = Key.R;
    public Key brightUpKey = Key.Equals;
    public Key brightDownKey = Key.Minus;

    private PlayerInput playerInput;
    private InputAction aToggle, aFocus, aReload, aBrightUp, aBrightDown;

    [Header("Base Light")]
    public float baseIntensity = 3500f;
    public float baseRange = 24f;
    public float baseSpotAngle = 60f;
    public Color color = new Color(1.0f, 0.956f, 0.84f);
    public float lumenToUnity = 0.04f;

    [Header("Focus Hold")]
    public float focusIntensityMul = 1.35f;
    public float focusRangeMul = 1.2f;
    public float focusSpotAngle = 22f;
    public float focusTransition = 12f;

    [Header("Brightness Control")]
    public float userBrightnessMin = 0.4f;
    public float userBrightnessMax = 2.0f;
    public float userBrightnessStep = 0.1f;
    [Range(0.4f, 2.0f)] public float userBrightness = 1.2f;

    [Header("Battery (Time-Based)")]
    public float batteryDurationSeconds = 60f;
    public bool requireItemForReload = false;
    [Range(0f, 1f)] public float lowBatteryThreshold = 0.2f;

    [Header("Flicker")]
    public bool enableFlicker = true;
    public float perlinAmplitude = 0.07f;
    public float perlinSpeed = 4f;

    [Header("Smoothing")]
    public float onOffLerpSpeed = 10f;

    [Header("Auto-Setup")]
    public bool autoFindSpotFromChildren = true;

    [Header("FarFill (No Shadows)")]
    public bool ff_enable = true;
    [Range(0f, 1f)] public float ff_intensityFactor = 0.35f;
    [Min(0.1f)] public float ff_range = 35f;
    [Range(-10f, 40f)] public float ff_angleOffset = 5f;
    public bool ff_copyCookie = true;
    public bool ff_flicker = false;
    [Range(0f, 0.5f)] public float ff_flickerAmp = 0.10f;
    [Min(0.1f)] public float ff_flickerSpeed = 10f;
    [Min(0.1f)] public float ff_lerpSpeed = 10f;

    [Header("UI Hint")]
    public TMP_Text reloadHintText;
    public string reloadHintMessage = "Press R to reload battery";
    public float reloadHintDuration = 2f;

    [Header("Reload Cooldown")]
    public float reloadCooldownSec = 0.6f;

    [Header("Close Object Dimming")]
    public bool enableProximityDimming = true;
    public float dimCheckDistance = 3f;
    [Range(0f, 1f)] public float minDimIntensityFactor = 0.5f; // ⬅️ ทำเป็นสไลเดอร์ปรับได้ใน Inspector
    public LayerMask dimLayerMask = Physics.DefaultRaycastLayers;

    [Header("Flicker Sound")]
    public AudioClip sfxFlicker;
    public float flickerSoundThreshold = 0.15f;
    public float flickerSoundCooldown = 1.5f;
    private float nextFlickerSoundTime = 0f;
    private float lastIntensity = 0f;

    private bool isOn = true;
    private bool wantFocus = false; // ใช้งานได้เฉพาะผ่าน Input Action (ตัดคลิกขวาแล้ว)
    private float timeLeft;
    private float prevTimeLeft;
    private float nextReloadAllowedAt = 0f;
    private float perlinT = 0f;
    private float hintHideAt = -1f;
    private Light _ff;

    void Reset()
    {
        if (!spot && autoFindSpotFromChildren)
            spot = GetComponentInChildren<Light>();
    }

    void Awake()
    {
        if (!spot && autoFindSpotFromChildren)
            spot = GetComponentInChildren<Light>();

        if (spot)
        {
            spot.type = LightType.Spot;
            spot.color = color;
            spot.intensity = 0f;
            spot.range = baseRange;
            spot.spotAngle = baseSpotAngle;
        }

        playerInput = GetComponent<PlayerInput>();
        if (playerInput && playerInput.actions)
        {
            var actions = playerInput.actions;
            aToggle = actions.FindAction("FlashlightToggle", false);
            aFocus = actions.FindAction("FlashlightFocus", false);
            aReload = actions.FindAction("FlashlightReload", false);
            aBrightUp = actions.FindAction("FlashlightBrightUp", false);
            aBrightDown = actions.FindAction("FlashlightBrightDown", false);

            if (aToggle != null) aToggle.performed += _ => Toggle();
            if (aFocus != null) { aFocus.performed += _ => wantFocus = true; aFocus.canceled += _ => wantFocus = false; }
            if (aReload != null) aReload.performed += _ => TryReload();
            if (aBrightUp != null) aBrightUp.performed += _ => AdjustBrightness(+userBrightnessStep);
            if (aBrightDown != null) aBrightDown.performed += _ => AdjustBrightness(-userBrightnessStep);
        }

        timeLeft = Mathf.Max(0f, batteryDurationSeconds);
        prevTimeLeft = timeLeft;

        FF_Ensure(spot);
        if (_ff) FF_ApplyImmediate(spot);
        SetReloadHint(false);
    }

    void Update()
    {
        HandleInputsFallback();
        float dt = Time.deltaTime;

        if (isOn && timeLeft > 0f)
        {
            float focusMul = wantFocus ? 1.2f : 1f;
            float brightMul = Mathf.Lerp(1f, 1.45f, Mathf.InverseLerp(userBrightnessMin, userBrightnessMax, userBrightness));
            timeLeft = Mathf.Max(0f, timeLeft - dt * focusMul * brightMul);
        }

        if (timeLeft <= 0f) isOn = false;

        if (prevTimeLeft > 0f && timeLeft <= 0f)
        {
            ShowReloadHint();
            if (audioSrc && sfxSputter) audioSrc.PlayOneShot(sfxSputter);
        }
        prevTimeLeft = timeLeft;

        if (hintHideAt > 0f && Time.unscaledTime >= hintHideAt)
        {
            SetReloadHint(false);
            hintHideAt = -1f;
        }

        perlinT += dt * perlinSpeed;

        UpdateAndApplyLight(dt);
        FF_Update(spot, isOn);
    }

    void HandleInputsFallback()
    {
        if (playerInput) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame) Toggle();
        if (kb[reloadKey].wasPressedThisFrame) TryReload();
        if (kb[brightUpKey].wasPressedThisFrame) AdjustBrightness(+userBrightnessStep);
        if (kb[brightDownKey].wasPressedThisFrame) AdjustBrightness(-userBrightnessStep);
        // ❌ ตัด logic คลิกขวาเพื่อ Focus ออกแล้ว (ไม่ตั้งค่า wantFocus จาก Mouse อีก)
    }

    void Toggle()
    {
        if (IsBatteryEmpty()) return;
        isOn = !isOn;
        if (audioSrc) audioSrc.PlayOneShot(isOn ? sfxToggleOn : sfxToggleOff);
    }

    public bool TryReload()
    {
        if (!IsBatteryEmpty()) return false;
        if (Time.unscaledTime < nextReloadAllowedAt) return false;
        nextReloadAllowedAt = Time.unscaledTime + reloadCooldownSec;

        if (requireItemForReload)
        {
            if (!playerInventory || string.IsNullOrEmpty(batteryItemId)) return false;
            if (!playerInventory.Consume(batteryItemId, 1)) return false;
        }

        timeLeft = batteryDurationSeconds;
        isOn = true;

        if (audioSrc && sfxReload) audioSrc.PlayOneShot(sfxReload);
        SetReloadHint(false);
        return true;
    }

    void AdjustBrightness(float d)
    {
        userBrightness = Mathf.Clamp(userBrightness + d, userBrightnessMin, userBrightnessMax);
    }

    bool IsBatteryEmpty() => timeLeft <= 0.001f;
    float GetBatteryPct() => Mathf.Clamp01(batteryDurationSeconds <= 0f ? 0f : timeLeft / batteryDurationSeconds);

    void UpdateAndApplyLight(float dt)
    {
        if (!spot) return;
        float pct = GetBatteryPct();

        // Flicker แบบ A — แบตยิ่งน้อย ยิ่งกระพริบแรง
        float n = Mathf.PerlinNoise(perlinT, 0.123f);
        float amp = Mathf.Lerp(perlinAmplitude * 2f, perlinAmplitude, pct);
        float flicker = enableFlicker ? (1f + (n - 0.5f) * 2f * amp) : 1f;

        float i = baseIntensity * userBrightness * flicker * lumenToUnity;
        if (wantFocus) i *= focusIntensityMul;
        i *= Mathf.Lerp(0.6f, 1.0f, pct);

        // ลดความสว่างเมื่อเข้าใกล้วัตถุ
        float proximityFactor = 1f;
        if (enableProximityDimming && spot)
        {
            Ray ray = new Ray(spot.transform.position, spot.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, dimCheckDistance, dimLayerMask))
            {
                float t = Mathf.InverseLerp(0f, dimCheckDistance, hit.distance);
                proximityFactor = Mathf.Lerp(minDimIntensityFactor, 1f, t);
            }
        }
        i *= proximityFactor;

        float targetIntensity = isOn ? i : 0f;
        float targetRange = (wantFocus ? baseRange * focusRangeMul : baseRange);
        float targetAngle = wantFocus ? focusSpotAngle : baseSpotAngle;

        spot.color = color;
        spot.intensity = Mathf.Lerp(spot.intensity, targetIntensity, dt * onOffLerpSpeed);
        spot.range = Mathf.Lerp(spot.range, targetRange, dt * onOffLerpSpeed);
        spot.spotAngle = Mathf.Lerp(spot.spotAngle, targetAngle, dt * focusTransition);
        spot.enabled = (spot.intensity > 0.02f && isOn);

        // เล่นเสียง Flicker เมื่อกระพริบแรง (กันสแปมด้วย Cooldown)
        if (audioSrc && sfxFlicker && Time.time >= nextFlickerSoundTime)
        {
            float diff = Mathf.Abs(spot.intensity - lastIntensity);
            if (diff > flickerSoundThreshold)
            {
                audioSrc.PlayOneShot(sfxFlicker);
                nextFlickerSoundTime = Time.time + flickerSoundCooldown;
            }
        }
        lastIntensity = spot.intensity;
    }

    void ShowReloadHint()
    {
        if (!reloadHintText) return;
        reloadHintText.text = reloadHintMessage;
        reloadHintText.gameObject.SetActive(true);
        hintHideAt = Time.unscaledTime + reloadHintDuration;
    }

    void SetReloadHint(bool on)
    {
        if (reloadHintText)
            reloadHintText.gameObject.SetActive(on);
    }

    void FF_Ensure(Light main)
    {
        if (!ff_enable || !main) return;
        if (_ff) return;

        var tf = main.transform.Find("FarFill");
        if (tf) _ff = tf.GetComponent<Light>();
        if (!_ff)
        {
            var go = new GameObject("FarFill");
            go.transform.SetParent(main.transform, false);
            _ff = go.AddComponent<Light>();
            _ff.type = LightType.Spot;
            _ff.shadows = LightShadows.None;
            _ff.intensity = 0f;
        }
    }

    void FF_ApplyImmediate(Light main)
    {
        if (!_ff || !main) return;
        _ff.color = main.color;
        _ff.range = ff_range;
        _ff.spotAngle = Mathf.Clamp(main.spotAngle + ff_angleOffset, 1f, 85f);
        if (ff_copyCookie) _ff.cookie = main.cookie;
        float baseI = (isOn && main.enabled) ? main.intensity * ff_intensityFactor : 0f;
        _ff.intensity = baseI;
        _ff.enabled = _ff.intensity > 0.02f;
    }

    void FF_Update(Light main, bool on)
    {
        if (!ff_enable) { if (_ff) _ff.enabled = false; return; }
        if (!_ff) FF_Ensure(main);
        if (!_ff || !main) return;

        _ff.color = main.color;
        _ff.range = ff_range;
        _ff.spotAngle = Mathf.Clamp(main.spotAngle + ff_angleOffset, 1f, 85f);
        if (ff_copyCookie) _ff.cookie = main.cookie;
        float baseI = (on && main.enabled) ? main.intensity * ff_intensityFactor : 0f;
        if (ff_flicker && baseI > 0f)
            baseI *= 1f + Mathf.Sin(Time.time * ff_flickerSpeed) * ff_flickerAmp;
        _ff.intensity = Mathf.MoveTowards(_ff.intensity, baseI, ff_lerpSpeed * Time.deltaTime);
        _ff.enabled = _ff.intensity > 0.02f;
    }
}
