using UnityEngine;

[DisallowMultipleComponent]
public class HeartbeatAudioController : MonoBehaviour
{
    [Header("References")]
    public PlayerController3D player; // drag ผู้เล่นที่มี Sanity01
    [Range(0f, 1f)] public float lowAnchor = 0.20f;
    [Range(0f, 1f)] public float midAnchor = 0.50f;
    [Range(0f, 1f)] public float hiAnchor = 1.00f;

    [Header("Audio Sources")]
    public AudioSource lowSrc;
    public AudioSource midSrc;
    public AudioSource hiSrc;
    public AudioSource recoverySrc;

    [Header("Volumes")]
    [Range(0f,1f)] public float lowVolMax = 0.9f;
    [Range(0f,1f)] public float midVolMax = 0.8f;
    [Range(0f,1f)] public float hiVolMax = 0.7f;
    [Range(0f,1f)] public float recVolMax = 0.35f;

    [Header("Pitch")]
    public float lowPitch = 1.15f;
    public float midPitch = 1.00f;
    public float highPitch = 0.85f;

    [Header("Smoothing")]
    public float volumeResponse = 6f;
    public float pitchResponse = 6f;
    public float recoveryGain = 1.8f;
    public float recoverySmooth = 6f;

    float _prevSanity = -1f;
    float _recLevel;

    void Awake()
    {
        if (!player) player = FindFirstObjectByType<PlayerController3D>();
        StartSource(lowSrc); StartSource(midSrc); StartSource(hiSrc); StartSource(recoverySrc);
        MuteAll();
    }

    void Update()
    {
        if (!player) return;
        float s = Mathf.Clamp01(player.Sanity01);
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        // --- Blend weights ---
        float wLow = 0f, wMid = 0f, wHi = 0f;
        if (s <= midAnchor)
        {
            float t = Mathf.InverseLerp(lowAnchor, midAnchor, s);
            wLow = (s < lowAnchor) ? 1f : 1f - t;
            wMid = (s < lowAnchor) ? 0f : t;
        }
        else
        {
            float t = Mathf.InverseLerp(midAnchor, hiAnchor, s);
            wMid = 1f - t;
            wHi = t;
        }

        // --- Target volumes ---
        float vLow = wLow * lowVolMax;
        float vMid = wMid * midVolMax;
        float vHi = wHi * hiVolMax;

        // --- Recovery accent ---
        if (_prevSanity < 0f) _prevSanity = s;
        float ds = (s - _prevSanity) / dt;
        _prevSanity = s;
        float add = Mathf.Max(0f, ds) * recoveryGain;
        _recLevel += (Mathf.Clamp01(add) - _recLevel) * (1f - Mathf.Exp(-recoverySmooth * dt));
        float vRec = _recLevel * recVolMax;

        // --- Smooth volume & pitch ---
        SmoothAudio(lowSrc, vLow, Mathf.Lerp(lowPitch, highPitch, s), dt);
        SmoothAudio(midSrc, vMid, Mathf.Lerp(lowPitch, highPitch, s), dt);
        SmoothAudio(hiSrc, vHi, Mathf.Lerp(lowPitch, highPitch, s), dt);
        SmoothAudio(recoverySrc, vRec, lowPitch, dt); // pitch recoverySrc fixed or can be parameterized

        // --- 3D setup (once ideally) ---
        Setup3D(lowSrc); Setup3D(midSrc); Setup3D(hiSrc); Setup3D(recoverySrc);
    }

    void StartSource(AudioSource a)
    {
        if (a && a.clip && !a.isPlaying)
        {
            a.loop = true;
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.volume = 0f;
            a.Play();
        }
    }

    void SmoothAudio(AudioSource a, float targetVol, float targetPitch, float dt)
    {
        if (!a) return;
        float expVol = 1f - Mathf.Exp(-volumeResponse * dt);
        float expPitch = 1f - Mathf.Exp(-pitchResponse * dt);
        a.volume += (targetVol - a.volume) * expVol;
        a.pitch += (targetPitch - a.pitch) * expPitch;
    }

    void MuteAll()
    {
        if (lowSrc) lowSrc.volume = 0f;
        if (midSrc) midSrc.volume = 0f;
        if (hiSrc) hiSrc.volume = 0f;
        if (recoverySrc) recoverySrc.volume = 0f;
    }

    void Setup3D(AudioSource a)
    {
        if (!a) return;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 1.5f;
        a.maxDistance = 18f;
    }
}
