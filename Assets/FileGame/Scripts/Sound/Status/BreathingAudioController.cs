using UnityEngine;

[DisallowMultipleComponent]
public class BreathingAudioController : MonoBehaviour
{
    [Header("References")]
    public PlayerController3D player;   // drag ผู้เล่นที่มี Stamina01
    public AudioSource breathSrc;       // loop clip: heavy breathing

    [Header("Volume by Stamina")]
    [Tooltip("ความดังเมื่อสุขภาพดี (Stamina สูง)")]
    [Range(0f, 1f)] public float volAtFull = 0.05f;
    [Tooltip("ความดังเมื่อ Stamina ต่ำมาก")]
    [Range(0f, 1f)] public float volAtZero = 0.9f;

    [Header("Pitch by Stamina")]
    public float pitchAtFull = 1.0f;
    public float pitchAtZero = 1.25f;

    [Header("Recovery Accent")]
    [Tooltip("เพิ่มความดังชั่วคราวตอนกำลังฟื้นตัวจาก 0 → สูง")]
    public float recoverBoost = 0.25f;
    public float recoverSmooth = 6f;

    [Header("Smoothing")]
    public float volumeResponse = 6f;
    public float pitchResponse = 6f;

    float _prevStam = -1f;
    float _recoverLvl;

    void Awake()
    {
        if (!player) player = FindFirstObjectByType<PlayerController3D>();
        if (breathSrc && breathSrc.clip)
        {
            breathSrc.loop = true; breathSrc.playOnAwake = false; breathSrc.spatialBlend = 1f;
            breathSrc.volume = 0f; breathSrc.Play();
        }
    }

    void Update()
    {
        if (!player || !breathSrc) return;
        float s01 = Mathf.Clamp01(player.Stamina01); // 0..1 (1 = ดี, 0 = เหนื่อย)

        // base mapping: stamina ต่ำ -> ดัง/ถี่
        float t = 1f - s01; // 0=calm, 1=exhausted
        float vTarget = Mathf.Lerp(volAtFull, volAtZero, t);
        float pTarget = Mathf.Lerp(pitchAtFull, pitchAtZero, t);

        // recovery accent: ถ้า ds/dt > 0 ให้มีบูสต์นิดๆ แล้วค่อยๆจาง
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        if (_prevStam < 0f) _prevStam = s01;
        float ds = (s01 - _prevStam) / dt; _prevStam = s01;

        float add = Mathf.Max(0f, ds) * recoverBoost;
        _recoverLvl = Mathf.Lerp(_recoverLvl, Mathf.Clamp01(add), 1f - Mathf.Exp(-recoverSmooth * dt));
        vTarget = Mathf.Clamp01(vTarget + _recoverLvl);

        // apply smooth
        breathSrc.volume = Mathf.Lerp(breathSrc.volume, vTarget, 1f - Mathf.Exp(-volumeResponse * dt));
        breathSrc.pitch = Mathf.Lerp(breathSrc.pitch, pTarget, 1f - Mathf.Exp(-pitchResponse * dt));

        // 3D friendly
        breathSrc.rolloffMode = AudioRolloffMode.Logarithmic;
        breathSrc.minDistance = 1.5f; breathSrc.maxDistance = 18f;
    }
}
