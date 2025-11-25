using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("กล้องผู้เล่น ต้องแท็ก MainCamera ถ้าให้สคริปต์หาด้วยตัวเอง")]
    public Transform cameraTransform;
    public Light flashlight;
    public AudioSource audioSource;

    [Header("Follow Camera")]
    [Tooltip("แนบไฟฉายเป็นลูกของกล้อง (แนะนำ เสถียรสุด)")]
    public bool attachAsChild = true;
    public bool followCamera = true; // ใช้กับโหมดคัดลอก pose
    public Vector3 localPositionOffset = new Vector3(0f, -0.05f, 0.1f);
    public Vector3 localEulerOffset = Vector3.zero;

    [Header("Toggle Input")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Input Action สำหรับ Toggle (ปล่อยว่าง = ใช้ปุ่ม fallback ข้างล่าง)")]
    public InputActionReference toggleAction;
    public Key fallbackKeyIS = Key.F;
#else
    public KeyCode fallbackKey = KeyCode.F;
#endif
    public bool fallbackKeyEnabled = true;

    [Header("Battery Settings")]
    public bool useBattery = true;
    public float maxEnergy = 100f;
    public float startEnergy = 100f;
    public float drainPerSecond = 6f;
    public float rechargePerItem = 50f;
    public bool autoTurnOffWhenEmpty = true;

    [Header("Inventory")]
    public InventoryLite playerInventory;
    public string batteryKeyId = "Battery";
    public bool autoUseBatteryItem = false;

    [Header("Light Shaping")]
    public float baseIntensity = 1.8f;
    [Range(0f, 1f)] public float minIntensityFactorAtLow = 0.4f;
    public float spotAngle = 45f;
    public float range = 20f;

    [Header("Low Energy Flicker")]
    public bool enableFlicker = true;
    public float lowEnergyThreshold = 20f;
    [Range(0f, 0.6f)] public float flickerAmplitude = 0.2f;
    public float flickerSpeed = 12f;

    [Header("UI (Optional)")]
    public Slider energySlider;
    public TMP_Text energyText;

    [Header("SFX (Optional)")]
    public AudioClip sfxToggleOn, sfxToggleOff, sfxNoEnergy, sfxUseBattery;

    // -------- Proximity Dimming (with smoothing) --------
    [Header("Proximity Dimming")]
    public bool proximityDimming = true;
    public float dimStartDistance = 1.5f;
    public float dimEndDistance = 0.15f;
    public float spherecastRadius = 0.05f;

    [Tooltip("อย่ารวมเลเยอร์ Player/Ignore Raycast เพื่อกันค่ากระพริก")]
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers & ~(1 << 2); // ตัด IgnoreRaycast(2) ออก

    [Tooltip("x=0 ชิดมาก → y=คูณความสว่าง, x=1 ไกล → y≈1")]
    public AnimationCurve proximityIntensityCurve = AnimationCurve.EaseInOut(0, 0.25f, 1, 1f);

    public bool useSpotAngleNarrowWhenClose = true;
    public float closeSpotAngle = 25f;

    public bool useShorterRangeWhenClose = true;
    public float closeRange = 8f;

    [Tooltip("เวลาในการไหลลื่น (วินาที) สำหรับค่าหรี่/มุม/ระยะ")]
    public float proximitySmoothing = 0.08f;
    // -----------------------------------------------------

    // Runtime
    float _energy;
    bool _isOn = false;
    float _baseIntensityCached;
    float _flickerSeed;

    // smoothing states
    float _proxFactorSmooth, _proxFactorVel;
    float _lerpCloseSmooth, _lerpCloseVel;

    void Reset()
    {
        if (!flashlight)
        {
            flashlight = GetComponentInChildren<Light>();
            if (!flashlight)
            {
                var go = new GameObject("Flashlight_Spot", typeof(Light));
                go.transform.SetParent(transform, false);
                flashlight = go.GetComponent<Light>();
            }
        }
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        flashlight.type = LightType.Spot;
        flashlight.spotAngle = spotAngle;
        flashlight.range = range;
        flashlight.intensity = baseIntensity;

        _baseIntensityCached = baseIntensity;
    }

    void Awake()
    {
        // หา MainCamera อัตโนมัติ
        if (!cameraTransform)
        {
            var cam = Camera.main;
            if (cam) cameraTransform = cam.transform;
        }
        if (!flashlight)
            flashlight = GetComponentInChildren<Light>();

        if (flashlight)
        {
            flashlight.type = LightType.Spot;
            flashlight.spotAngle = spotAngle;
            flashlight.range = range;
            flashlight.intensity = baseIntensity;
            flashlight.enabled = false;
        }

        _energy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
        _baseIntensityCached = baseIntensity;
        _flickerSeed = Random.value * 1000f;

        // แนบเป็นลูกของกล้อง (โหมดเสถียรสุด)
        TryAttachToCamera();

        if (!playerInventory)
        {
#if UNITY_2023_1_OR_NEWER
            playerInventory = FindFirstObjectByType<InventoryLite>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            playerInventory = FindObjectOfType<InventoryLite>(true);
#pragma warning restore 618
#endif
        }
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed += OnTogglePerformed;
            toggleAction.action.Enable();
        }
#endif
        UpdateUI();
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnTogglePerformed;
            toggleAction.action.Disable();
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    void OnTogglePerformed(InputAction.CallbackContext ctx) => ToggleFlashlight();
#endif

    void Update()
    {
        // อ่านปุ่ม (เฉพาะ Input System)
#if ENABLE_INPUT_SYSTEM
        if (fallbackKeyEnabled && (toggleAction == null || toggleAction.action == null) && Keyboard.current != null)
        {
            if (Keyboard.current[fallbackKeyIS].wasPressedThisFrame) ToggleFlashlight();
        }
#else
        if (fallbackKeyEnabled && Input.GetKeyDown(fallbackKey)) ToggleFlashlight();
#endif

        // Drain แบต
        if (_isOn && useBattery)
        {
            _energy -= drainPerSecond * Time.deltaTime;
            if (_energy <= 0f)
            {
                _energy = 0f;
                if (autoTurnOffWhenEmpty) SetFlashlight(false, playSfx: true);
                else ApplyLight(0f);

                if (autoUseBatteryItem) TryUseBatteryItem();
            }
        }

        // คำนวณพลังงาน/กระพริบ
        if (_isOn && flashlight)
        {
            float tEnergy = (_energy <= 0f) ? 0f : Mathf.Clamp01(_energy / maxEnergy);
            float energyFactor = Mathf.Lerp(minIntensityFactorAtLow, 1f, tEnergy);

            float flicker = 1f;
            if (enableFlicker && _energy > 0f && _energy <= lowEnergyThreshold)
            {
                float n = Mathf.PerlinNoise(_flickerSeed, Time.time * flickerSpeed);
                flicker = Mathf.Max(0f, 1f + (n - 0.5f) * 2f * flickerAmplitude);
            }

            // Proximity (มี smoothing)
            float proxFactorTarget = 1f;
            float lerpCloseTarget = 0f; // 0=ไกล, 1=ชิด
            if (proximityDimming && cameraTransform)
            {
                float hitDist = ProbeFrontHitDistance();
                if (hitDist >= 0f)
                {
                    float t = Mathf.InverseLerp(dimEndDistance, dimStartDistance, hitDist); // 0=ชิด,1=ไกล
                    proxFactorTarget = Mathf.Clamp01(proximityIntensityCurve.Evaluate(t));
                    lerpCloseTarget = 1f - t;
                }
            }
            // smoothDamp
            _proxFactorSmooth = Mathf.SmoothDamp(_proxFactorSmooth == 0f ? proxFactorTarget : _proxFactorSmooth, proxFactorTarget, ref _proxFactorVel, proximitySmoothing);
            _lerpCloseSmooth = Mathf.SmoothDamp(_lerpCloseSmooth, lerpCloseTarget, ref _lerpCloseVel, proximitySmoothing);

            float intensity = _baseIntensityCached * energyFactor * flicker * Mathf.Clamp01(_proxFactorSmooth);
            flashlight.intensity = intensity;

            flashlight.spotAngle = useSpotAngleNarrowWhenClose
                ? Mathf.Lerp(spotAngle, closeSpotAngle, Mathf.Clamp01(_lerpCloseSmooth))
                : spotAngle;

            flashlight.range = useShorterRangeWhenClose
                ? Mathf.Lerp(range, closeRange, Mathf.Clamp01(_lerpCloseSmooth))
                : range;
        }

        UpdateUI();
    }

    void LateUpdate()
    {
        // โหมดคัดลอก pose (ถ้าไม่ได้แนบเป็นลูก)
        if (!attachAsChild && followCamera && cameraTransform && flashlight)
        {
            var targetPos = cameraTransform.TransformPoint(localPositionOffset);
            var targetRot = cameraTransform.rotation * Quaternion.Euler(localEulerOffset);
            flashlight.transform.SetPositionAndRotation(targetPos, targetRot);
        }
    }

    // ---------- Public API ----------
    public void ToggleFlashlight()
    {
        if (useBattery && _energy <= 0f && autoUseBatteryItem)
            TryUseBatteryItem();

        if (useBattery && _energy <= 0f)
        {
            PlaySfx(sfxNoEnergy);
            SetFlashlight(false, playSfx: false);
            return;
        }
        SetFlashlight(!_isOn, playSfx: true);
    }

    public void SetFlashlight(bool on, bool playSfx)
    {
        _isOn = on;
        if (flashlight) flashlight.enabled = on;
        if (playSfx)
        {
            if (_isOn) PlaySfx(sfxToggleOn);
            else PlaySfx(sfxToggleOff);
        }
    }

    public bool TryUseBatteryItem()
    {
        if (!useBattery || playerInventory == null || string.IsNullOrEmpty(batteryKeyId)) return false;

        int cnt = SafeGetCount(playerInventory, batteryKeyId);
        if (cnt <= 0) return false;
        if (!TryConsume(playerInventory, batteryKeyId, 1)) return false;

        _energy = Mathf.Clamp(_energy + rechargePerItem, 0f, maxEnergy);
        PlaySfx(sfxUseBattery);
        UpdateUI();
        return true;
    }

    // ---------- Helpers ----------
    void TryAttachToCamera()
    {
        if (!cameraTransform || !flashlight) return;

        if (attachAsChild)
        {
            flashlight.transform.SetParent(cameraTransform, true);
            flashlight.transform.localPosition = localPositionOffset;
            flashlight.transform.localRotation = Quaternion.Euler(localEulerOffset);
        }
        else
        {
            // ถ้าเคยเป็นลูกอยู่ ให้ปลดก่อน
            if (flashlight.transform.parent == cameraTransform)
                flashlight.transform.SetParent(null, true);
        }
    }

    float ProbeFrontHitDistance()
    {
        if (cameraTransform == null) return -1f;
        var origin = cameraTransform.position;
        var dir = cameraTransform.forward;

        RaycastHit hit;
        bool hasHit = Physics.SphereCast(origin, Mathf.Max(0.001f, spherecastRadius), dir,
                                         out hit, Mathf.Max(dimStartDistance, 0.01f),
                                         obstacleMask, QueryTriggerInteraction.Ignore);
        if (hasHit) return hit.distance;
        return -1f;
    }

    void ApplyLight(float intensity)
    {
        if (flashlight)
        {
            flashlight.enabled = intensity > 0f;
            flashlight.intensity = intensity;
        }
    }

    void UpdateUI()
    {
        if (energySlider)
        {
            energySlider.gameObject.SetActive(useBattery);
            energySlider.minValue = 0f;
            energySlider.maxValue = maxEnergy;
            energySlider.value = _energy;
        }
        if (energyText)
        {
            if (useBattery)
            {
                float pct = (maxEnergy <= 0f) ? 0f : (_energy / maxEnergy * 100f);
                energyText.text = $"{Mathf.RoundToInt(pct)}%";
            }
            else energyText.text = "∞";
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // UI sfx เป็น 2D
        }
        audioSource.PlayOneShot(clip);
    }

    int SafeGetCount(InventoryLite inv, string key)
    {
        var mi = inv.GetType().GetMethod("GetCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        mi = inv.GetType().GetMethod("CountOf", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        return 0;
    }

    bool TryConsume(InventoryLite inv, string key, int amount)
    {
        if (inv == null || string.IsNullOrEmpty(key) || amount <= 0) return false;

        var miB = inv.GetType().GetMethod("Consume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miB != null && miB.ReturnType == typeof(bool)) return (bool)miB.Invoke(inv, new object[] { key, amount });

        miB = inv.GetType().GetMethod("Remove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miB != null && miB.ReturnType == typeof(bool)) return (bool)miB.Invoke(inv, new object[] { key, amount });

        var miV = inv.GetType().GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miV != null && miV.ReturnType == typeof(void))
        {
            miV.Invoke(inv, new object[] { key, -amount });
            return true;
        }
        return false;
    }

    void OnValidate()
    {
        if (flashlight)
        {
            flashlight.type = LightType.Spot;
            flashlight.spotAngle = spotAngle;
            flashlight.range = range;
        }
        baseIntensity = Mathf.Max(0f, baseIntensity);
        maxEnergy = Mathf.Max(1f, maxEnergy);
        startEnergy = Mathf.Clamp(startEnergy, 0f, maxEnergy);
        drainPerSecond = Mathf.Max(0f, drainPerSecond);
        rechargePerItem = Mathf.Max(0f, rechargePerItem);

        dimStartDistance = Mathf.Max(0.05f, dimStartDistance);
        dimEndDistance = Mathf.Clamp(dimEndDistance, 0.01f, Mathf.Max(0.02f, dimStartDistance - 0.01f));
        spherecastRadius = Mathf.Max(0.0f, spherecastRadius);
        closeSpotAngle = Mathf.Clamp(closeSpotAngle, 1f, Mathf.Max(1f, spotAngle));
        closeRange = Mathf.Clamp(closeRange, 0.5f, Mathf.Max(0.5f, range));
    }

    void OnDrawGizmosSelected()
    {
        if (cameraTransform && proximityDimming)
        {
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.35f);
            var o = cameraTransform.position;
            var d = cameraTransform.forward;
            Gizmos.DrawLine(o, o + d * dimStartDistance);
            Gizmos.DrawWireSphere(o + d * dimEndDistance, spherecastRadius);
        }

        if (cameraTransform && (attachAsChild || followCamera) && flashlight)
        {
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
            Vector3 p = attachAsChild
                ? (cameraTransform.TransformPoint(localPositionOffset))
                : (flashlight.transform.position);
            Gizmos.DrawLine(cameraTransform.position, p);
            Gizmos.DrawWireSphere(p, 0.03f);
        }
    }
}
