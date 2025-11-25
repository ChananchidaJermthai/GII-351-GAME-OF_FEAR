using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LightFlicker : MonoBehaviour
{
    [Header("Target Light")]
    public Light targetLight;                     // ถ้าเว้นไว้จะดึงจากตัวเอง
    [Tooltip("ความสว่างตอน 'ติด'")]
    public float onIntensity = 1f;
    [Tooltip("ความสว่างตอน 'ดับ' (0 = มืด)")]
    public float offIntensity = 0f;

    [Header("Timing (random per cycle)")]
    [Min(0.01f)] public float minOnTime = 0.07f;
    [Min(0.01f)] public float maxOnTime = 0.25f;
    [Min(0.01f)] public float minOffTime = 0.04f;
    [Min(0.01f)] public float maxOffTime = 0.18f;

    [Header("Fade (optional)")]
    public bool smoothFade = true;
    [Range(0f, 0.3f)] public float fadeSeconds = 0.05f;

    [Header("Auto Start")]
    public bool playOnStart = true;

    [Header("Sound on flicker")]
    public AudioSource audioSource;               // ถ้าเว้นไว้จะสร้างให้อัตโนมัติ
    public AudioClip flickerClip;               // เสียงสั้น ๆ
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("สุ่ม pitch เล็กน้อยให้ไม่ซ้ำเดิม")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
    [Tooltip("เล่นเสียงเฉพาะตอนไฟ 'ติด' (ถ้าปิด = เล่นทั้งตอนติดและดับ)")]
    public bool playOnlyOnTurnOn = true;

    [Header("Optional: Emission (for meshes near the lamp)")]
    public Renderer emissiveRenderer;             // เช่น หัวหลอดไฟ
    public Color emissionOn = Color.white;
    public Color emissionOff = Color.black;
    [Range(0f, 5f)] public float emissionIntensity = 1.2f;

    // runtime
    float _originalIntensity;
    bool _running;
    Coroutine _co;

    void Reset()
    {
        minOnTime = 0.07f; maxOnTime = 0.25f;
        minOffTime = 0.04f; maxOffTime = 0.18f;
        smoothFade = true; fadeSeconds = 0.05f;
        sfxVolume = 0.8f; pitchJitter = 0.05f; playOnlyOnTurnOn = true;
        emissionIntensity = 1.2f;
    }

    void Awake()
    {
        if (!targetLight) targetLight = GetComponent<Light>();
        if (targetLight)
        {
            _originalIntensity = targetLight.intensity;
        }

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f; // 3D (ปรับเป็น 0f ถ้าอยาก 2D)
        }

        SetupEmission(emissiveRenderer, emissionOff);
    }

    void OnEnable()
    {
        if (playOnStart) StartFlicker();
    }

    void OnDisable()
    {
        StopFlicker();
        RestoreDefaults();
    }

    void RestoreDefaults()
    {
        if (targetLight) targetLight.intensity = _originalIntensity;
        SetupEmission(emissiveRenderer, emissionOff);
    }

    // -------- Public Controls --------
    [ContextMenu("Start Flicker")]
    public void StartFlicker()
    {
        if (_running) return;
        _running = true;
        _co = StartCoroutine(CoFlicker());
    }

    [ContextMenu("Stop Flicker")]
    public void StopFlicker()
    {
        _running = false;
        if (_co != null) StopCoroutine(_co);
        _co = null;
        if (targetLight) targetLight.intensity = onIntensity; // ค้างที่ติดไว้
        SetupEmission(emissiveRenderer, emissionOn);
    }

    IEnumerator CoFlicker()
    {
        if (!targetLight)
        {
            Debug.LogWarning("[LightFlicker] ไม่มี Light ให้ควบคุม");
            yield break;
        }

        while (_running)
        {
            // ----- ON -----
            yield return ToggleLight(true);
            yield return new WaitForSeconds(Random.Range(minOnTime, maxOnTime));

            // ----- OFF -----
            yield return ToggleLight(false);
            yield return new WaitForSeconds(Random.Range(minOffTime, maxOffTime));
        }
    }

    IEnumerator ToggleLight(bool turnOn)
    {
        // Sound
        if (flickerClip && audioSource && (!playOnlyOnTurnOn || turnOn))
        {
            audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            audioSource.PlayOneShot(flickerClip, sfxVolume);
        }

        // Emission
        SetupEmission(emissiveRenderer, turnOn ? emissionOn : emissionOff);

        // Intensity
        if (!smoothFade || fadeSeconds <= 0f)
        {
            targetLight.intensity = turnOn ? onIntensity : offIntensity;
            yield break;
        }

        float start = targetLight.intensity;
        float end = turnOn ? onIntensity : offIntensity;
        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeSeconds);
            // easeInOut
            k = k * k * (3f - 2f * k);
            targetLight.intensity = Mathf.Lerp(start, end, k);
            yield return null;
        }
        targetLight.intensity = end;
    }

    void SetupEmission(Renderer r, Color c)
    {
        if (!r) return;
        foreach (var mat in r.materials)
        {
            if (!mat) continue;
            mat.EnableKeyword("_EMISSION");
            // HDR emission: คูณความสว่าง
            Color hdr = c * Mathf.LinearToGammaSpace(emissionIntensity);
            mat.SetColor("_EmissionColor", hdr);
        }
    }
}
