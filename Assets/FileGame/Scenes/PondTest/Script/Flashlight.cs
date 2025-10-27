using UnityEngine;
using UnityEngine.InputSystem; // ใช้ New Input System เท่านั้น

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
            if (aReload != null) aReload.performed += _ => TryReload();
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
    }

    void Update()
    {
        HandleInputsFallback();
        SimulateBatteryAndFlicker(Time.deltaTime);
        UpdateTargets();
        ApplyLight(Time.deltaTime);
    }

    void HandleInputsFallback()
    {
        if (playerInput) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame) Toggle();
        if (kb[reloadKey].wasPressedThisFrame) TryReload();
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
        // ถ้าเต็มอยู่แล้ว ไม่ต้องใช้ของ
        if (batteryRemain >= batteryCapacity - 0.001f)
        {
            // Debug.Log("[Flashlight] Battery already full.");
            return false;
        }

        if (useInventoryBatteries)
            return ReloadFromInventory();
        else
            return ReloadFromInternalCells();
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

        // ถ้ายังไม่ต้อง consume แต่อยากตรวจว่ามีของไหม สามารถเช็คด้วย Consume แบบ dry-run ได้ถ้ามีเมธอดนับ
        // ที่นี่เราใช้แนวทางง่าย: พยายาม Consume 1 ชิ้นเลย (ตามสเป็กคุณ: ใช้แล้วของหาย)
        if (reloadConsumesItem)
        {
            bool ok = playerInventory.Consume(batteryItemId, 1);
            if (!ok)
            {
                // ไม่มีแบตในกระเป๋า
                if (audioSrc && sfxSputter) audioSrc.PlayOneShot(sfxSputter);
                return false;
            }
        }
        // ถ้าไม่ต้อง consume ก็จะไม่หาย — แต่ตาม logic ที่ให้มา “ใช้แล้ว Item หาย” จึงควรเปิด reloadConsumesItem = true

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
}
