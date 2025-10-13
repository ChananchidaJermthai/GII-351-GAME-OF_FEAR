using UnityEngine;
using UnityEngine.InputSystem; // New Input System only

[DisallowMultipleComponent]
public class Flashlight : MonoBehaviour
{
    [Header("References")]
    public Light spot;
    public AudioSource audioSrc;
    public AudioClip sfxToggleOn, sfxToggleOff, sfxReload, sfxSputter;

    [Header("Fallback Keys (ใช้เมื่อไม่มี PlayerInput actions)")]
    public Key toggleKey = Key.F;
    public Key reloadKey = Key.R;
    public Key brightUpKey = Key.Equals;    // (=) หรือ Key.NumpadPlus
    public Key brightDownKey = Key.Minus;   // (-) หรือ Key.NumpadMinus
    // โฟกัส: ถ้าไม่มี action จะใช้ Mouse ปุ่มขวาเสมอ (Mouse.current.rightButton)

    [Header("Base Light")]
    public float baseIntensity = 800f;  // “ลูเมนจำลอง” -> map ไป Light.intensity ด้านล่าง
    public float baseRange = 18f;
    public float baseSpotAngle = 60f;
    public Color color = new Color(1.0f, 0.956f, 0.84f);

    [Header("Focus Hold")]
    public float focusIntensityMul = 1.25f;
    public float focusRangeMul = 1.2f;
    public float focusSpotAngle = 30f;
    public float focusTransition = 12f;

    [Header("Brightness Control")]
    public float userBrightnessMin = 0.4f;
    public float userBrightnessMax = 1.4f;
    public float userBrightnessStep = 0.1f;
    [Range(0.4f, 1.4f)] public float userBrightness = 1.0f;

    [Header("Battery")]
    public float batteryCapacity = 120f;
    public float drainPerSecond = 1.0f;
    public float reloadAmount = 60f;
    public int maxCells = 3;
    public int currentCells = 1;
    [Range(0f, 1f)] public float lowBatteryThreshold = 0.2f;

    [Header("Flicker")]
    public bool enableFlicker = true;
    public float lowBatteryFlickerChance = 0.15f;
    public Vector2 flickerBurstDuration = new Vector2(0.15f, 0.45f);
    public Vector2 flickerGap = new Vector2(0.02f, 0.08f);
    public float perlinAmplitude = 0.07f;
    public float perlinSpeed = 4f;

    [Header("Smooth On/Off")]
    public float onOffLerpSpeed = 10f;

    [Header("Auto-Setup")]
    public bool autoFindSpotFromChildren = true;

    // Optional: PlayerInput + actions (ถ้ามี)
    private PlayerInput playerInput;
    private InputAction aToggle, aFocus, aReload, aBrightUp, aBrightDown;

    // State
    private bool isOn = true;
    private bool wantFocus = false;
    private float batteryRemain;
    private float desiredIntensity, desiredRange, desiredSpotAngle;
    private bool inBurst = false;
    private float burstEndTime = 0f, nextFlicker = 0f;
    private float perlinT = 0f;

    void Reset()
    {
        if (!spot && autoFindSpotFromChildren) spot = GetComponentInChildren<Light>();
    }

    void Awake()
    {
        if (!spot && autoFindSpotFromChildren) spot = GetComponentInChildren<Light>();
        if (!spot) { Debug.LogWarning("[Flashlight] Assign a Spot Light to 'spot'."); return; }

        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = 0f;        // จะ lerp ไปค่าเป้าหมาย
        spot.range = baseRange;
        spot.spotAngle = baseSpotAngle;

        playerInput = GetComponent<PlayerInput>();
        if (playerInput)
        {
            var actions = playerInput.actions;
            aToggle = actions.FindAction("FlashlightToggle", false);
            aFocus = actions.FindAction("FlashlightFocus", false);
            aReload = actions.FindAction("FlashlightReload", false);
            aBrightUp = actions.FindAction("FlashlightBrightUp", false);
            aBrightDown = actions.FindAction("FlashlightBrightDown", false);

            if (aToggle != null) aToggle.performed += _ => Toggle();
            if (aFocus != null) { aFocus.performed += _ => wantFocus = true; aFocus.canceled += _ => wantFocus = false; }
            if (aReload != null) aReload.performed += _ => ReloadOneCell();
            if (aBrightUp != null) aBrightUp.performed += _ => AdjustBrightness(+userBrightnessStep);
            if (aBrightDown != null) aBrightDown.performed += _ => AdjustBrightness(-userBrightnessStep);

            aToggle?.Enable(); aFocus?.Enable(); aReload?.Enable(); aBrightUp?.Enable(); aBrightDown?.Enable();
        }

        batteryRemain = Mathf.Min(batteryCapacity, reloadAmount * currentCells);
        desiredIntensity = CalcTargetIntensity();
        desiredRange = CalcTargetRange();
        desiredSpotAngle = baseSpotAngle;
    }

    void OnDisable()
    {
        aToggle?.Disable(); aFocus?.Disable(); aReload?.Disable(); aBrightUp?.Disable(); aBrightDown?.Disable();
        if (aToggle != null) aToggle.performed -= _ => Toggle();
        if (aFocus != null) { aFocus.performed -= _ => wantFocus = true; aFocus.canceled -= _ => wantFocus = false; }
        if (aReload != null) aReload.performed -= _ => ReloadOneCell();
        if (aBrightUp != null) aBrightUp.performed -= _ => AdjustBrightness(+userBrightnessStep);
        if (aBrightDown != null) aBrightDown.performed -= _ => AdjustBrightness(-userBrightnessStep);
    }

    void Update()
    {
        HandleInputsFallback();         // ไม่มี PlayerInput ก็อ่านจาก Keyboard.current/Mouse.current
        SimulateBatteryAndFlicker(Time.deltaTime);
        UpdateTargets();
        ApplyLight(Time.deltaTime);
    }

    // ---------- Fallback (New Input System ล้วน ๆ) ----------
    void HandleInputsFallback()
    {
        if (playerInput) return; // มี actions แล้วไม่ต้อง fallback

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame) Toggle();
        if (kb[reloadKey].wasPressedThisFrame) ReloadOneCell();
        if (kb[brightUpKey].wasPressedThisFrame) AdjustBrightness(+userBrightnessStep);
        if (kb[brightDownKey].wasPressedThisFrame) AdjustBrightness(-userBrightnessStep);

        // Focus = ปุ่มขวาค้าง
        wantFocus = mouse != null && mouse.rightButton.isPressed;
    }

    void Toggle()
    {
        isOn = !isOn;
        if (audioSrc) audioSrc.PlayOneShot(isOn ? sfxToggleOn : sfxToggleOff);
    }

    void AdjustBrightness(float d)
    {
        userBrightness = Mathf.Clamp(userBrightness + d, userBrightnessMin, userBrightnessMax);
    }

    bool ReloadOneCell()
    {
        if (currentCells <= 0) return false;
        batteryRemain = Mathf.Min(batteryRemain + reloadAmount, batteryCapacity);
        currentCells = Mathf.Clamp(currentCells - 1, 0, maxCells);
        if (audioSrc && sfxReload) audioSrc.PlayOneShot(sfxReload);
        return true;
    }

    // ---------- Sim / Targets / Apply ----------
    void SimulateBatteryAndFlicker(float dt)
    {
        if (!spot) return;

        float brightnessMul = userBrightness;
        float focusMul = wantFocus ? 1.2f : 1.0f;
        float onMul = isOn ? 1f : 0f;
        batteryRemain = Mathf.Max(0f, batteryRemain - (drainPerSecond * brightnessMul * focusMul * onMul) * dt);

        float batteryPct = batteryCapacity <= 0 ? 0 : batteryRemain / batteryCapacity;
        bool low = batteryPct <= lowBatteryThreshold;

        if (enableFlicker && low && isOn && !inBurst)
        {
            if (Random.value < lowBatteryFlickerChance * dt)
            {
                inBurst = true;
                burstEndTime = Time.time + Random.Range(flickerBurstDuration.x, flickerBurstDuration.y);
                nextFlicker = 0f;
                if (audioSrc && sfxSputter) audioSrc.PlayOneShot(sfxSputter);
            }
        }
        if (inBurst && Time.time >= burstEndTime) inBurst = false;

        if (batteryRemain <= 0f) isOn = false;

        perlinT += dt * perlinSpeed;
    }

    float CalcTargetIntensity()
    {
        if (!isOn || batteryRemain <= 0f) return 0f;

        float i = baseIntensity * userBrightness;

        if (enableFlicker)
        {
            float n = Mathf.PerlinNoise(perlinT, 0.123f);
            float jitter = 1f + (n - 0.5f) * 2f * perlinAmplitude;
            i *= jitter;

            if (inBurst)
            {
                if (Time.time >= nextFlicker) nextFlicker = Time.time + Random.Range(flickerGap.x, flickerGap.y);
                float phase = Mathf.PingPong(Time.time * 50f, 1f);
                if (phase > 0.5f) i *= 0.25f;
            }
        }

        if (wantFocus) i *= focusIntensityMul;

        float batteryPct = batteryCapacity <= 0 ? 0 : batteryRemain / batteryCapacity;
        float cap = Mathf.Lerp(0.4f, 1.0f, batteryPct); // แบตต่ำลดเพดานสว่าง
        i *= cap;

        // map “ลูเมนจำลอง” -> Unity Light.intensity (ประมาณการ)
        return Mathf.Clamp(i * 0.003f, 0f, 10f);
    }

    float CalcTargetRange()
    {
        float r = baseRange * Mathf.Lerp(0.6f, 1.0f, batteryRemain / Mathf.Max(1f, batteryCapacity));
        if (wantFocus) r *= focusRangeMul;
        r *= Mathf.Lerp(0.7f, 1.0f, userBrightness);
        return r;
    }

    float CalcTargetSpotAngle() => wantFocus ? focusSpotAngle : baseSpotAngle;

    void UpdateTargets()
    {
        desiredIntensity = CalcTargetIntensity();
        desiredRange = CalcTargetRange();
        desiredSpotAngle = CalcTargetSpotAngle();
        if (spot) spot.color = color;
    }

    void ApplyLight(float dt)
    {
        if (!spot) return;
        float lerp = onOffLerpSpeed * dt;

        spot.intensity = Mathf.Lerp(spot.intensity, desiredIntensity, lerp);
        spot.range = Mathf.Lerp(spot.range, desiredRange, lerp);
        spot.spotAngle = Mathf.Lerp(spot.spotAngle, desiredSpotAngle, focusTransition * dt);

        bool shouldEnable = spot.intensity > 0.02f && isOn;
        if (spot.enabled != shouldEnable) spot.enabled = shouldEnable;
    }

    // API เสริม
    public float BatteryPercent => batteryCapacity <= 0 ? 0 : Mathf.Clamp01(batteryRemain / batteryCapacity);
    public bool IsOn => isOn;
}
