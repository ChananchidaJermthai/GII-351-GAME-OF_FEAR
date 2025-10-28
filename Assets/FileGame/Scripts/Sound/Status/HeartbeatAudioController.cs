using UnityEngine;

[DisallowMultipleComponent]
public class HeartbeatAudioController : MonoBehaviour
{
    [Header("References")]
    public PlayerController3D player;   // drag ผู้เล่นที่มี Sanity01
    [Tooltip("0.0–1.0 (เช่น 0.2 = 20%)")]
    [Range(0f, 1f)] public float lowAnchor = 0.20f;   // 20%
    [Range(0f, 1f)] public float midAnchor = 0.50f;   // 50%
    [Range(0f, 1f)] public float hiAnchor = 1.00f;   // 100%

    [Header("Audio Sources (loop)")]
    public AudioSource lowSrc;    // heartbeat (low)
    public AudioSource midSrc;    // heartbeat (mid)
    public AudioSource hiSrc;     // heartbeat (high)
    public AudioSource recoverySrc; // airy recovery bed (optional)

    [Header("Volumes")]
    [Range(0f, 1f)] public float lowVolMax = 0.9f;
    [Range(0f, 1f)] public float midVolMax = 0.8f;
    [Range(0f, 1f)] public float hiVolMax = 0.7f;
    [Range(0f, 1f)] public float recVolMax = 0.35f;

    [Header("Pitch (by sanity)")]
    public float lowPitch = 1.15f;   // very stressed
    public float midPitch = 1.00f;
    public float highPitch = 0.85f;   // calm

    [Header("Smoothing")]
    [Tooltip("ค่ามาก = ตอบสนองไว")]
    public float volumeResponse = 6f;  // exp lerp factor
    public float pitchResponse = 6f;
    [Tooltip("สเกลความดังของ Recovery ตามอัตราฟื้นตัว ds/dt")]
    public float recoveryGain = 1.8f;
    public float recoverySmooth = 6f;

    float _prevSanity = -1f;
    float _recLevel; // 0..1

    void Awake()
    {
        if (!player) player = FindFirstObjectByType<PlayerController3D>();
        StartIf(lowSrc); StartIf(midSrc); StartIf(hiSrc); StartIf(recoverySrc);
        MuteAllInstant();
    }

    void Update()
    {
        if (!player) return;
        float s = Mathf.Clamp01(player.Sanity01);     // 0..1
        float dt = Mathf.Max(0.0001f, Time.deltaTime);

        // ----- Blend weights: (low<->mid) then (mid<->hi)
        float wLow = 0, wMid = 0, wHi = 0;

        if (s <= midAnchor)
        {
            // map [lowAnchor..midAnchor] -> low to mid
            float t = Mathf.InverseLerp(lowAnchor, midAnchor, s);
            wLow = 1f - t; wMid = t; wHi = 0f;
            if (s < lowAnchor) { wLow = 1f; wMid = 0f; }
        }
        else
        {
            // map [midAnchor..hiAnchor] -> mid to hi
            float t = Mathf.InverseLerp(midAnchor, hiAnchor, s);
            wMid = 1f - t; wHi = t; wLow = 0f;
        }

        // ----- Target volumes
        float vLow = wLow * lowVolMax;
        float vMid = wMid * midVolMax;
        float vHi = wHi * hiVolMax;

        // ----- Recovery level from positive ds/dt
        if (_prevSanity < 0f) _prevSanity = s;
        float ds = (s - _prevSanity) / dt;        // rate
        _prevSanity = s;
        float add = Mathf.Max(0f, ds) * recoveryGain;   // only when recovering
        _recLevel = Mathf.Lerp(_recLevel, Mathf.Clamp01(add), 1f - Mathf.Exp(-recoverySmooth * dt));
        float vRec = _recLevel * recVolMax;

        // ----- Apply smooth volumes
        SmoothVolume(lowSrc, vLow, volumeResponse, dt);
        SmoothVolume(midSrc, vMid, volumeResponse, dt);
        SmoothVolume(hiSrc, vHi, volumeResponse, dt);
        SmoothVolume(recoverySrc, vRec, volumeResponse, dt);

        // ----- Pitch by sanity (low sanity -> faster)
        float targetPitch = Mathf.Lerp(lowPitch, highPitch, s); // s=0 => lowPitch, s=1 => highPitch
        SmoothPitch(lowSrc, targetPitch, pitchResponse, dt);
        SmoothPitch(midSrc, targetPitch, pitchResponse, dt);
        SmoothPitch(hiSrc, targetPitch, pitchResponse, dt);

        // keep 3D settings friendly
        Setup3D(lowSrc); Setup3D(midSrc); Setup3D(hiSrc); Setup3D(recoverySrc);
    }

    // ===== helpers =====
    void StartIf(AudioSource a) { if (a && !a.isPlaying && a.clip) { a.loop = true; a.playOnAwake = false; a.spatialBlend = 1f; a.volume = 0f; a.Play(); } }
    void SmoothVolume(AudioSource a, float target, float resp, float dt) { if (!a) return; a.volume = Mathf.Lerp(a.volume, target, 1f - Mathf.Exp(-resp * dt)); }
    void SmoothPitch(AudioSource a, float target, float resp, float dt) { if (!a) return; a.pitch = Mathf.Lerp(a.pitch, target, 1f - Mathf.Exp(-resp * dt)); }
    void MuteAllInstant() { if (lowSrc) lowSrc.volume = 0; if (midSrc) midSrc.volume = 0; if (hiSrc) hiSrc.volume = 0; if (recoverySrc) recoverySrc.volume = 0; }
    void Setup3D(AudioSource a) { if (!a) return; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 1.5f; a.maxDistance = 18f; }
}
