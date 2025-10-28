using UnityEngine;

public class RandomAppearObject : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab ของวัตถุที่จะสุ่มเกิด (เช่น ผี, เงา, ของตกใจ ฯลฯ)")]
    public GameObject[] randomObjects;

    [Tooltip("โอกาส (%) ที่จะให้วัตถุเกิดเมื่อ Player เดินชน (เช่น 20 = 20%)")]
    [Range(0, 100)] public int spawnChancePercent = 20;

    [Tooltip("เวลาที่วัตถุอยู่ก่อนจะหาย (วินาที)")]
    public float appearDuration = 3f;

    [Tooltip("ระยะห่างจากหน้าผู้เล่น (เมตร)")]
    public float spawnDistanceInFront = 2f;

    [Header("Sound Settings")]
    [Tooltip("เสียงที่สุ่มเล่นเมื่อเกิดวัตถุ (กำหนดใน Inspector)")]
    public AudioClip[] randomSounds;

    [Tooltip("Audio Source ที่ใช้เล่นเสียง (ต้องใส่ใน Inspector)")]
    public AudioSource audioSource;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int randomValue = Random.Range(1, 101); // 1–100

            if (randomValue <= spawnChancePercent)
            {
                // ✅ สุ่มวัตถุ
                if (randomObjects.Length > 0)
                {
                    GameObject selectedPrefab = randomObjects[Random.Range(0, randomObjects.Length)];

                    Transform player = other.transform;
                    Vector3 spawnPos = player.position + player.forward * spawnDistanceInFront;

                    // ✅ สร้างวัตถุ
                    GameObject spawned = Instantiate(selectedPrefab, spawnPos, Quaternion.LookRotation(-player.forward));

                    // ✅ ลบหลังเวลาที่กำหนด
                    Destroy(spawned, appearDuration);
                }

                // ✅ สุ่มเสียง
                if (audioSource != null && randomSounds.Length > 0)
                {
                    AudioClip selectedClip = randomSounds[Random.Range(0, randomSounds.Length)];
                    audioSource.PlayOneShot(selectedClip);
                }

                Debug.Log("👻 Spawned object and played random sound!");
            }
            else
            {
                Debug.Log($"🎲 Random {randomValue} > {spawnChancePercent} → ไม่เกิดวัตถุ");
            }
        }
    }
}
