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
    public Transform cameraTransform;
    public Light flashlight;
    public AudioSource audioSource;

    [Header("Follow Camera")]
    public bool attachAsChild = true;
    public bool followCamera = true;
    public Vector3 localPositionOffset = new Vector3(0f, -0.05f, 0.1f);
    public Vector3 localEulerOffset = Vector3.zero;

    [Header("Toggle Input")]
#if ENABLE_INPUT_SYSTEM
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
    [Range(0f,1f)] public float minIntensityFactorAtLow = 0.4f;
    public float spotAngle = 45f;
    public float range = 20f;

    [Header("Low Energy Flicker")]
    public bool enableFlicker = true;
    public float lowEnergyThreshold = 20f;
    [Range(0f,0.6f)] public float flickerAmplitude = 0.2f;
    public float flickerSpeed = 12f;

    [Header("UI (Optional)")]
    public Slider energySlider;
    public TMP_Text energyText;

    [Header("SFX (Optional)")]
    public AudioClip sfxToggleOn, sfxToggleOff, sfxNoEnergy, sfxUseBattery;

    [Header("Proximity Dimming")]
    public bool proximityDimming = true;
    public float dimStartDistance = 1.5f;
    public float dimEndDistance = 0.15f;
    public float spherecastRadius = 0.05f;
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers & ~(1 << 2);
    public AnimationCurve proximityIntensityCurve = AnimationCurve.EaseInOut(0, 0.25f, 1, 1f);
    public bool useSpotAngleNarrowWhenClose = true;
    public float closeSpotAngle = 25f;
    public bool useShorterRangeWhenClose = true;
    public float closeRange = 8f;
    public float proximitySmoothing = 0.08f;

    // Runtime
    private float _energy;
    private bool _isOn = false;
    private float _baseIntensityCached;
    private float _flickerSeed;
    private float _proxFactorSmooth, _proxFactorVel;
    private float _lerpCloseSmooth, _lerpCloseVel;

    private Ray _ray;
    private RaycastHit _hit;

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
        if (!cameraTransform) cameraTransform = Camera.main?.transform;
        if (!flashlight) flashlight = GetComponentInChildren<Light>();

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
        if (toggleAction?.action != null)
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
        if (toggleAction?.action != null)
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
        HandleInput();
        UpdateBattery();
        UpdateLight();
        UpdateUI();
    }

    void LateUpdate()
    {
        if (!attachAsChild && followCamera && cameraTransform && flashlight)
        {
            var targetPos = cameraTransform.TransformPoint(localPositionOffset);
            var targetRot = cameraTransform.rotation * Quaternion.Euler(localEulerOffset);
            flashlight.transform.SetPositionAndRotation(targetPos, targetRot);
        }
    }

    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (fallbackKeyEnabled && (toggleAction == null || toggleAction.action == null) && Keyboard.current != null)
        {
            if (Keyboard.current[fallbackKeyIS].wasPressedThisFrame) ToggleFlashlight();
        }
#else
        if (fallbackKeyEnabled && Input.GetKeyDown(fallbackKey)) ToggleFlashlight();
#endif
    }

    private void UpdateBattery()
    {
        if (!_isOn || !useBattery) return;

        _energy -= drainPerSecond * Time.deltaTime;
        if (_energy <= 0f)
        {
            _energy = 0f;
            if (autoTurnOffWhenEmpty) SetFlashlight(false, playSfx: true);
            else ApplyLight(0f);
            if (autoUseBatteryItem) TryUseBatteryItem();
        }
    }

    private void UpdateLight()
    {
        if (!_isOn || !flashlight) return;

        float tEnergy = Mathf.Clamp01(_energy / maxEnergy);
        float energyFactor = Mathf.Lerp(minIntensityFactorAtLow, 1f, tEnergy);

        // Flicker
        float flicker = 1f;
        if (enableFlicker && _energy > 0f && _energy <= lowEnergyThreshold)
        {
            float n = Mathf.PerlinNoise(_flickerSeed, Time.time * flickerSpeed);
            flicker = Mathf.Max(0f, 1f + (n - 0.5f) * 2f * flickerAmplitude);
        }

        // Proximity dimming
        float proxFactorTarget = 1f;
        float lerpCloseTarget = 0f;
        if (proximityDimming && cameraTransform)
        {
            float hitDist = ProbeFrontHitDistance();
            if (hitDist >= 0f)
            {
                float t = Mathf.InverseLerp(dimEndDistance, dimStartDistance, hitDist);
                proxFactorTarget = Mathf.Clamp01(proximityIntensityCurve.Evaluate(t));
                lerpCloseTarget = 1f - t;
            }
        }

        _proxFactorSmooth = Mathf.SmoothDamp(_proxFactorSmooth, proxFactorTarget, ref _proxFactorVel, proximitySmoothing);
        _lerpCloseSmooth = Mathf.SmoothDamp(_lerpCloseSmooth, lerpCloseTarget, ref _lerpCloseVel, proximitySmoothing);

        flashlight.intensity = _baseIntensityCached * energyFactor * flicker * Mathf.Clamp01(_proxFactorSmooth);
        flashlight.spotAngle = useSpotAngleNarrowWhenClose
            ? Mathf.Lerp(spotAngle, closeSpotAngle, Mathf.Clamp01(_lerpCloseSmooth))
            : spotAngle;
        flashlight.range = useShorterRangeWhenClose
            ? Mathf.Lerp(range, closeRange, Mathf.Clamp01(_lerpCloseSmooth))
            : range;
    }

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
        if (!playSfx) return;

        PlaySfx(_isOn ? sfxToggleOn : sfxToggleOff);
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

    private void TryAttachToCamera()
    {
        if (!cameraTransform || !flashlight) return;

        if (attachAsChild)
        {
            flashlight.transform.SetParent(cameraTransform, true);
            flashlight.transform.localPosition = localPositionOffset;
            flashlight.transform.localRotation = Quaternion.Euler(localEulerOffset);
        }
        else if (flashlight.transform.parent == cameraTransform)
        {
            flashlight.transform.SetParent(null, true);
        }
    }

    private float ProbeFrontHitDistance()
    {
        if (cameraTransform == null) return -1f;
        bool hasHit = Physics.SphereCast(cameraTransform.position, Mathf.Max(0.001f, spherecastRadius),
                                         cameraTransform.forward, out _hit,
                                         Mathf.Max(dimStartDistance, 0.01f),
                                         obstacleMask, QueryTriggerInteraction.Ignore);
        return hasHit ? _hit.distance : -1f;
    }

    private void ApplyLight(float intensity)
    {
        if (!flashlight) return;
        flashlight.enabled = intensity > 0f;
        flashlight.intensity = intensity;
    }

    private void UpdateUI()
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
            energyText.text = useBattery ? $"{Mathf.RoundToInt(_energy / maxEnergy * 100f)}%" : "∞";
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(clip);
    }

    private int SafeGetCount(InventoryLite inv, string key)
    {
        var mi = inv.GetType().GetMethod("GetCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        mi = inv.GetType().GetMethod("CountOf", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        return 0;
    }

    private bool TryConsume(InventoryLite inv, string key, int amount)
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
            Gizmos.DrawLine(cameraTransform.position, cameraTransform.position + cameraTransform.forward * dimStartDistance);
            Gizmos.DrawWireSphere(cameraTransform.position + cameraTransform.forward * dimEndDistance, spherecastRadius);
        }

        if (cameraTransform && (attachAsChild || followCamera) && flashlight)
        {
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
            Vector3 p = attachAsChild ? cameraTransform.TransformPoint(localPositionOffset) : flashlight.transform.position;
            Gizmos.DrawLine(cameraTransform.position, p);
            Gizmos.DrawWireSphere(p, 0.03f);
        }
    }
}
