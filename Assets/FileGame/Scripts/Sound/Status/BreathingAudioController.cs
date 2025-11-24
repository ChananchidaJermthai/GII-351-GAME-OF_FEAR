using UnityEngine;

[DisallowMultipleComponent]
public class BreathingAudioController : MonoBehaviour
{
    [Header("References")]
    public PlayerController3D player;   // drag ผู้เล่นที่มี Stamina01
    public AudioSource breathSrc;       // loop clip: heavy breathing

    [Header("Volume by Stamina")]
    [Range(0f, 1f)] public float volAtFull = 0.05f;
    [Range(0f, 1f)] public float volAtZero = 0.9f;

    [Header("Pitch by Stamina")]
    public float pitchAtFull = 1.0f;
    public float pitchAtZero = 1.25f;

    [Header("Recovery Accent")]
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
            breathSrc.loop = true;
            breathSrc.playOnAwake = false;
            breathSrc.spatialBlend = 1f;
            breathSrc.volume = 0f;
            breathSrc.Play();

            // 3D audio setup once
            breathSrc.rolloffMode = AudioRolloffMode.Logarithmic;
            breathSrc.minDistance = 1.5f;
            breathSrc.maxDistance = 18f;
        }
    }

    void Update()
    {
        if (!player || !breathSrc) return;

        float dt = Time.deltaTime;
        float s01 = Mathf.Clamp01(player.Stamina01); // 0..1 (1 = ดี, 0 = เหนื่อย)
        float t = 1f - s01;

        // base mapping: stamina ต่ำ -> ดัง/ถี่
        float vTarget = Mathf.Lerp(volAtFull, volAtZero, t);
        float pTarget = Mathf.Lerp(pitchAtFull, pitchAtZero, t);

        // recovery accent
        if (_prevStam < 0f) _prevStam = s01;
        float ds = (s01 - _prevStam) / Mathf.Max(dt, 0.0001f);
        _prevStam = s01;

        float add = Mathf.Max(0f, ds) * recoverBoost;
        _recoverLvl += (Mathf.Clamp01(add) - _recoverLvl) * (1f - Mathf.Exp(-recoverSmooth * dt));
        vTarget = Mathf.Clamp01(vTarget + _recoverLvl);

        // smooth volume & pitch
        breathSrc.volume += (vTarget - breathSrc.volume) * (1f - Mathf.Exp(-volumeResponse * dt));
        breathSrc.pitch += (pTarget - breathSrc.pitch) * (1f - Mathf.Exp(-pitchResponse * dt));
    }
}
