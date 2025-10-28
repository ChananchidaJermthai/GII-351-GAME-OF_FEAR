using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public class SoundGameBackground : MonoBehaviour
{
    [Header("Background Sound Settings")]
    [Tooltip("ใส่เสียงที่ต้องการให้สุ่มเล่น")]
    public AudioClip[] backgroundSounds; // ✅ Array ของเสียงที่สุ่มเล่น

    [Tooltip("เสียงจะสุ่มเล่นทุกช่วง 15-20 วินาที")]
    [SerializeField] private float minDelay = 15f;
    [SerializeField] private float maxDelay = 20f;

    private AudioSource audioSource;
    private float nextPlayTime;

    void Awake()
    {
        // ดึง AudioSource จาก GameObject นี้
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogWarning("⚠️ ไม่มี AudioSource ใน GameObject ที่มี SoundGameBackground! จะเพิ่มให้อัตโนมัติ");
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Start()
    {
        // ตั้งเวลาการเล่นครั้งแรกแบบสุ่ม
        ScheduleNextSound();
    }

    void Update()
    {
        // ถ้าถึงเวลาให้เล่นเสียง
        if (Time.time >= nextPlayTime)
        {
            PlayRandomSound();
            ScheduleNextSound();
        }
    }


    public float removeSanity = 2.5f;
   private void PlayRandomSound()
    {
        if (backgroundSounds == null || backgroundSounds.Length == 0)
        {
            Debug.LogWarning("⚠️ ไม่มีเสียงใน Array backgroundSounds!");
            return;
        }

        // ✅ สุ่มเลือกเสียงจาก Array
        AudioClip clip = backgroundSounds[Random.Range(0, backgroundSounds.Length)];

        // เล่นแบบ OneShot (ไม่ขัดเสียงอื่น)
        audioSource.PlayOneShot(clip);

        // ✅ เรียก AddSanity ของ PlayerController3D
        GameObject player = GameObject.FindGameObjectWithTag("Player"); // หรือ reference ที่มีอยู่
        if (player != null)
        {
            PlayerController3D pc = player.GetComponent<PlayerController3D>();
            if (pc != null)
            {
                pc.AddSanity(removeSanity); // เรียกเมธอดของผู้เล่นถูกต้อง
            }
        }

        Debug.Log($"🎵 เล่นเสียงสุ่ม: {clip.name}");
    }


    private void ScheduleNextSound()
    {
        // ✅ สุ่มระยะเวลารอครั้งต่อไป (15–20 วินาที)
        float delay = Random.Range(minDelay, maxDelay);
        nextPlayTime = Time.time + delay;
        Debug.Log($"⏳ เสียงต่อไปจะเล่นในอีก {delay:F1} วินาที");
    }
}
