using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ChromaticSanityManager : MonoBehaviour
{
    [Header("Player Sanity Reference")]
    public PlayerController3D player;   // ลากจาก Inspector หรือหาอัตโนมัติ

    [Header("Volume")]
    public Volume volume;               // Volume ของฉาก
    private ChromaticAberration chroma; // ตัวเอฟเฟกต์จริง

    [Header("Curve Settings")]
    [Range(0f, 1f)] public float minIntensity = 0f;
    [Range(0f, 1f)] public float maxIntensity = 1f;

    [Header("Volume Influence")]
    public AudioSource volumeSource;
    [Range(0f, 2f)] public float volumeMultiplier = 1f;

    [Header("Smooth")]
    public float lerpSpeed = 5f;

    private void Awake()
    {
        if (!player)
            player = FindAnyObjectByType<PlayerController3D>();

        if (volume && volume.profile.TryGet(out chroma))
        {
            chroma.intensity.overrideState = true;
        }

        if (!chroma)
            Debug.LogWarning("ChromaticSanityManager: Volume ไม่มี Chromatic Aberration");
    }

    private void Update()
    {
        if (!player || !chroma) return;

        // 1) เอา sanity 0 → 1
        float s01 = player.Sanity01;  // มีอยู่แล้วใน PlayerController3D

        // 2) base intensity จาก sanity
        float target = Mathf.Lerp(minIntensity, maxIntensity, s01);

        // 3) คูณด้วย volume ของเสียง (optional)
        if (volumeSource)
        {
            target *= Mathf.Clamp01(volumeSource.volume * volumeMultiplier);
        }

        // 4) ลื่นไหล
        float current = chroma.intensity.value;
        float smooth = Mathf.Lerp(current, target, Time.deltaTime * lerpSpeed);

        // 5) ใส่ค่ากลับไปที่ Volume
        chroma.intensity.value = smooth;
    }
}
