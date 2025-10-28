using UnityEngine;
using UnityEngine.InputSystem; // ใช้ New Input System เท่านั้น
using TMPro;                   // << เพิ่มสำหรับข้อความ UI

[DisallowMultipleComponent]
public class Flashlight : MonoBehaviour
{
    [Header("References")]
    public Light spot;
    public AudioSource audioSrc;
    public AudioClip sfxToggleOn, sfxToggleOff, sfxReload, sfxSputter;

    [Header("Inventory (Battery Options)")]
    [Tooltip("เปิดเพื่อให้ไฟฉายใช้แบตเตอรี่จาก InventoryLite (ต้องมี item ถึงจะ Reload ได้)")]
    public bool useInventoryBatteries = true;
    [Tooltip("อ้างอิง InventoryLite ของผู้เล่น (จะหาอัตโนมัติจากพาเรนต์ถ้าเว้นว่าง)")]
    public InventoryLite playerInventory;
    [Tooltip("KeyID ของแบตเตอรี่ใน InventoryLite")]
    public string batteryItemId = "Battery";
    [Tooltip("ถ้าเปิด: ทุกครั้งที่ Reload จะ Consume แบตในกระเป๋าทันที (1 ชิ้น)")]
    public bool reloadConsumesItem = true;

    [Header("Fallback Keys (ใช้เมื่อไม่มี PlayerInput actions)")]
    public Key toggleKey = Key.F;
    public Key reloadKey = Key.R;
    public Key brightUpKey = Key.Equals;    // (=) หรือ Key.NumpadPlus
    public Key brightDownKey = Key.Minus;   // (-) หรือ Key.NumpadMinus

    [Header("Base Light")]
    public float baseIntensity = 3500f;   // หน่วยลูเมนจำลอง
    public float baseRange = 24f;
    public float baseSpotAngle = 60f;
    public Color color = new Color(1.0f, 0.956f, 0.84f);

    [Header("Light Scale")]
    public float lumenToUnity = 0.04f;
    public float masterBoost = 1.0f; // เผื่อคูณเพิ่มอีกชั้น (ยังไม่ใช้ในสูตรข้างล่าง แต่เผื่อไว้จูน)

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

    [Header("Battery (Internal Store Mode)")]
    [Tooltip("ความจุสูงสุดของไฟฉายในครั้งหนึ่ง")]
    public float batteryCapacity = 120f;
    [Tooltip("อัตรากินแบตต่อวินาที (คูณด้วยความสว่าง/โฟกัส)")]
    public float drainPerSecond = 1.0f;
    [Tooltip("พลังที่ได้ต่อการ Reload 1 ครั้ง")]
    public float reloadAmount = 60f;

    [Tooltip("ใช้เฉพาะเมื่อ useInventoryBatteries=false")]
    public int maxCells = 3;
    [Tooltip("ใช้เฉพาะเมื่อ useInventoryBatteries=false")]
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

    // ===== FarFill (No-Shadow) Add-on (เพิ่มเข้าไป โดยไม่ตัด logic เดิม) =====
    [Header("FarFill (No Shadows)")]
    public bool ff_enable = true;                            // เปิด/ปิดไฟส่องไกล
    [Range(0f, 1f)] public float ff_intensityFactor = 0.35f; // ความสว่าง FarFill = main.intensity * factor
    [Min(0.1f)] public float ff_range = 35f;                 // ระยะไกล
    [Range(-10f, 40f)] public float ff_angleOffset = 5f;     // มุมของ Far = main.spotAngle + offset
    public bool ff_copyCookie = true;                        // คัดลอก cookie จากไฟหลัก

    [Header("Flicker (FarFill เท่านั้น)")]
    public bool ff_flicker = false;                          // เปิด flicker ที่ Far เพื่อไม่ให้เงาสั่น
    [Range(0f, 0.5f)] public float ff_flickerAmp = 0.10f;
    [Min(0.1f)] public float ff_flickerSpeed = 10f;

    [Header("FarFill Smoothing")]
    [Min(0.1f)] public float ff_lerpSpeed = 10f;

    // ===== UI Hint: Reload Message =====
    [Header("UI Hint")]
    [Tooltip("TMP_Text ที่จะแสดงข้อความ “กดปุ่ม R เพื่อ reload battery”")]
    public TMP_Text reloadHintText;
    [Tooltip("ข้อความเมื่อแบตหมด")]
    public string reloadHintMessage = "กดปุ่ม R เพื่อ reload battery";
    [Tooltip("เวลาที่แสดงข้อความเป็นวินาที")]
    public float reloadHintDuration = 2f;

    // ===== Anti-spam (R) =====
    [Header("Reload Cooldown")]
    [Tooltip("กันสแปมปุ่ม R: เวลาคูลดาวน์ต่อการกดรีโหลด (วินาที)")]
    public float reloadCooldownSec = 0.6f;

    // ===== Runtime =====
    private PlayerInput playerInput;
    private InputAction aToggle, aFocus, aReload, aBrightUp, aBrightDown;

    private bool isOn = true;
    private bool wantFocus = false;
    private float batteryRemain;
    private float desiredIntensity, desiredRange, desiredSpotAngle;
    private bool inBurst = false;
    private float burstEndTime = 0f, nextFlicker = 0f;
    private float perlinT = 0f;

    private Light _ff; // FarFill light (ไม่สร้างเงา)

    // state สำหรับ hint และคูลดาวน์
    private bool wasBatteryEmpty = false;
    private float hintHideAt = -1f;
    private float nextReloadAllowedAt = 0f;

    void Reset()
    {
        if (!spot && autoFindSpotFromChildren) spot = GetComponentInChildren<Light>();
    }

    void Awake()
    {
        if (!spot && autoFindSpotFromChildren) spot = GetComponentInChildren<Light>();
        if (!spot) { Debug.LogWarning("[Flashlight] Assign a Spot Light to 'spot'."); return; }

        // Auto find inventory บนผู้เล่นถ้าเว้นว่าง
        if (!playerInventory) playerInventory = GetComponentInParent<InventoryLite>();

        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = 0f;
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
            if (aReload != null) aReload.performed += _ => TryReload(); // คูลดาวน์จะเช็คข้างใน
            if (aBrightUp != null) aBrightUp.performed += _ => AdjustBrightness(+userBrightnessStep);
            if (aBrightDown != null) aBrightDown.performed += _ => AdjustBrightness(-userBrightnessStep);

            aToggle?.Enable(); aFocus?.Enable(); aReload?.Enable(); aBrightUp?.Enable(); aBrightDown?.Enable();
        }

        // เริ่มต้น: ถ้าเปิดโหมด inventory ไม่สนค่า currentCells
        if (useInventoryBatteries)
        {
            batteryRemain = Mathf.Min(batteryCapacity, reloadAmount); // เติมให้พอเปิดใช้งานครั้งแรก
        }
        else
        {
            batteryRemain = Mathf.Min(batteryCapacity, reloadAmount * currentCells);
        }

        desiredIntensity = CalcTargetIntensity();
        desiredRange = CalcTargetRange();
        desiredSpotAngle = baseSpotAngle;

        // === สร้าง FarFill (ถ้าเปิดใช้) และตั้งค่าเริ่มต้น ===
        FF_Ensure(spot);
        if (_ff) FF_ApplyImmediate(spot);

        // ปิด hint เริ่มต้น
        SetReloadHint(false);
    }

    void Update()
    {
        HandleInputsFallback();
        SimulateBatteryAndFlicker(Time.deltaTime);

        // แสดง Hint เมื่อแบตหมด (edge trigger)
        if (batteryRemain <= 0f && !wasBatteryEmpty)
        {
            wasBatteryEmpty = true;
            ShowReloadHint();
        }
        else if (batteryRemain > 0f)
        {
            wasBatteryEmpty = false;
        }

        // ซ่อน hint เมื่อครบเวลา
        if (hintHideAt > 0f && Time.unscaledTime >= hintHideAt)
        {
            SetReloadHint(false);
            hintHideAt = -1f;
        }

        UpdateTargets();
        ApplyLight(Time.deltaTime);

        // อัปเดต FarFill ทุกเฟรม (แยกจากไฟหลัก เพื่อไม่ให้เงาเพี้ยน)
        FF_Update(spot, isOn);
    }

    void HandleInputsFallback()
    {
        if (playerInput) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame) Toggle();
        if (kb[reloadKey].wasPressedThisFrame) TryReload(); // คูลดาวน์จะเช็คข้างใน
        if (kb[brightUpKey].wasPressedThisFrame) AdjustBrightness(+userBrightnessStep);
        if (kb[brightDownKey].wasPressedThisFrame) AdjustBrightness(-userBrightnessStep);
        wantFocus = mouse != null && mouse.rightButton.isPressed;
    }

    void Toggle()
    {
        isOn = !isOn;
        if (audioSrc) audioSrc.PlayOneShot(isOn ? sfxToggleOn : sfxToggleOff);
    }

    void AdjustBrightness(float d) =>
        userBrightness = Mathf.Clamp(userBrightness + d, userBrightnessMin, userBrightnessMax);

    // === จุดเดียวในการ Reload (รองรับสองโหมด) ===
    public bool TryReload()
    {
        // กันสแปมปุ่ม R
        if (Time.unscaledTime < nextReloadAllowedAt)
            return false;

        nextReloadAllowedAt = Time.unscaledTime + reloadCooldownSec;

        // ถ้าเต็มอยู่แล้ว ไม่ต้องใช้ของ
        if (batteryRemain >= batteryCapacity - 0.001f)
        {
            return false;
        }

        bool result = false;
        if (useInventoryBatteries)
            result = ReloadFromInventory();
        else
            result = ReloadFromInternalCells();

        // ถ้ารีโหลดสำเร็จ ปิด hint ทันที
        if (result) SetReloadHint(false);

        return result;
    }

    bool ReloadFromInventory()
    {
        // ต้องมี InventoryLite + Battery ID
        if (!playerInventory)
        {
            Debug.LogWarning("[Flashlight] Missing InventoryLite reference.");
            return false;
        }
        if (string.IsNullOrEmpty(batteryItemId))
        {
            Debug.LogWarning("[Flashlight] Battery item id is empty.");
            return false;
        }

        if (reloadConsumesItem)
        {
            bool ok = playerInventory.Consume(batteryItemId, 1);
            if (!ok)
            {
                // ไม่มีแบตในกระเป๋า
                if (audioSrc && sfxSputter) audioSrc.PlayOneShot(sfxSputter);
                // ถ้ายังไม่มีแบต ให้โชว์ hint สั้น ๆ ก็ได้ (ใช้ข้อความเดิม)
                ShowReloadHint();
                return false;
            }
        }

        // เติมพลังงาน
        float before = batteryRemain;
        batteryRemain = Mathf.Min(batteryRemain + reloadAmount, batteryCapacity);

        // เสียงรีโหลด
        if (audioSrc && sfxReload) audioSrc.PlayOneShot(sfxReload);

        return batteryRemain > before + 0.001f;
    }

    bool ReloadFromInternalCells()
    {
        if (currentCells <= 0)
        {
            if (audioSrc && sfxSputter) audioSrc.PlayOneShot(sfxSputter);
            ShowReloadHint();
            return false;
        }

        float before = batteryRemain;
        batteryRemain = Mathf.Min(batteryRemain + reloadAmount, batteryCapacity);
        currentCells = Mathf.Clamp(currentCells - 1, 0, maxCells);

        if (audioSrc && sfxReload) audioSrc.PlayOneShot(sfxReload);
        return batteryRemain > before + 0.001f;
    }

    void SimulateBatteryAndFlicker(float dt)
    {
        if (!spot) return;
        float mul = userBrightness * (wantFocus ? 1.2f : 1f) * (isOn ? 1f : 0f);
        batteryRemain = Mathf.Max(0f, batteryRemain - drainPerSecond * mul * dt);

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
            i *= 1f + (n - 0.5f) * 2f * perlinAmplitude;
        }

        if (wantFocus) i *= focusIntensityMul;

        float batteryPct = batteryCapacity <= 0 ? 0 : batteryRemain / batteryCapacity;
        i *= Mathf.Lerp(0.6f, 1.0f, batteryPct);

        return Mathf.Clamp(i * lumenToUnity, 0f, 200f);
    }

    float CalcTargetRange()
    {
        float r = baseRange;
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
        spot.enabled = (spot.intensity > 0.02f && isOn);
    }

    // ==================== Hint Helpers ====================
    private void ShowReloadHint()
    {
        if (!reloadHintText) return;
        reloadHintText.text = reloadHintMessage;
        SetReloadHint(true);
        hintHideAt = Time.unscaledTime + reloadHintDuration;
    }

    private void SetReloadHint(bool on)
    {
        if (!reloadHintText) return;
        reloadHintText.gameObject.SetActive(on);
    }

    // ==================== FarFill Helpers ====================
    private void FF_Ensure(Light main)
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
            _ff.shadows = LightShadows.None; // สำคัญ: ไม่สร้างเงา
            _ff.intensity = 0f;               // เริ่มนิ่ม ๆ
        }
    }

    private void FF_ApplyImmediate(Light main)
    {
        if (!_ff || !main) return;
        _ff.color = main.color;
        _ff.range = ff_range;
        _ff.spotAngle = Mathf.Clamp(main.spotAngle + ff_angleOffset, 1f, 80f);
        if (ff_copyCookie) _ff.cookie = main.cookie;

        float baseI = (isOn && main.enabled) ? main.intensity * ff_intensityFactor : 0f;
        _ff.intensity = baseI;
        _ff.enabled = _ff.intensity > 0.02f;
    }

    private void FF_Update(Light main, bool on)
    {
        if (!ff_enable) { if (_ff) _ff.enabled = false; return; }
        if (!_ff) FF_Ensure(main);
        if (!_ff || !main) return;

        // ซิงก์พื้นฐานทุกเฟรม
        _ff.color = main.color;
        _ff.range = ff_range;
        _ff.spotAngle = Mathf.Clamp(main.spotAngle + ff_angleOffset, 1f, 80f);
        if (ff_copyCookie) _ff.cookie = main.cookie;

        float baseI = (on && main.enabled) ? main.intensity * ff_intensityFactor : 0f;
        if (ff_flicker && baseI > 0f)
            baseI *= 1f + Mathf.Sin(Time.time * ff_flickerSpeed) * ff_flickerAmp;

        _ff.intensity = Mathf.MoveTowards(_ff.intensity, baseI, ff_lerpSpeed * Time.deltaTime);
        _ff.enabled = _ff.intensity > 0.02f;
    }
}
