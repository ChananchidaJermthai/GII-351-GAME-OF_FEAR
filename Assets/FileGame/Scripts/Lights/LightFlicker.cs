using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class LightFlicker : MonoBehaviour
{
    [Header("Target Light")]
    public Light targetLight;
    public float onIntensity = 1f;
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
    public AudioSource audioSource;
    public AudioClip flickerClip;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
    public bool playOnlyOnTurnOn = true;

    [Header("Optional: Emission")]
    public Renderer emissiveRenderer;
    public Color emissionOn = Color.white;
    public Color emissionOff = Color.black;
    [Range(0f, 5f)] public float emissionIntensity = 1.2f;

    float _originalIntensity;
    bool _running;
    Coroutine _co;

    // 🔹 cache materials เพื่อไม่ให้สร้างใหม่เรื่อย ๆ
    Material[] _emissionMats;

    void Awake()
    {
        if (!targetLight) targetLight = GetComponent<Light>();
        if (targetLight)
            _originalIntensity = targetLight.intensity;

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

        // ดึง materials แค่ครั้งเดียว
        if (emissiveRenderer)
        {
            _emissionMats = emissiveRenderer.materials;
            SetupEmission(emissionOff);
        }
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
        SetupEmission(emissionOff);
    }

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

        if (targetLight) targetLight.intensity = onIntensity;
        SetupEmission(emissionOn);
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
            // ON
            yield return ToggleLight(true);
            yield return new WaitForSeconds(Random.Range(minOnTime, maxOnTime));

            // OFF
            yield return ToggleLight(false);
            yield return new WaitForSeconds(Random.Range(minOffTime, maxOffTime));
        }
    }

    IEnumerator ToggleLight(bool turnOn)
    {
        if (flickerClip && audioSource && (!playOnlyOnTurnOn || turnOn))
        {
            audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            audioSource.PlayOneShot(flickerClip, sfxVolume);
        }

        SetupEmission(turnOn ? emissionOn : emissionOff);

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
            k = k * k * (3f - 2f * k);
            targetLight.intensity = Mathf.Lerp(start, end, k);
            yield return null;
        }
        targetLight.intensity = end;
    }

    void SetupEmission(Color c)
    {
        if (_emissionMats == null) return;
        foreach (var mat in _emissionMats)
        {
            if (!mat) continue;
            mat.EnableKeyword("_EMISSION");
            Color hdr = c * Mathf.LinearToGammaSpace(emissionIntensity);
            mat.SetColor("_EmissionColor", hdr);
        }
    }
}
