using UnityEngine;

public class CallGhost : MonoBehaviour
{
    [Header("Ghost Settings")]
    [SerializeField] private GameObject ghostPrefab; // พรีแฟบผี
    [SerializeField, Range(0, 100)] private int percentJumpScare = 30; // โอกาส JumpScare (%)
    [SerializeField] private float ghostLifetime = 5f; // เวลาที่ผีอยู่ก่อนถูกลบ

    [Header("Player & Spawn Settings")]
    [SerializeField] private Transform player; // ตัวผู้เล่น (target)
    [SerializeField] private Transform[] spawnPoints; // จุดเกิดของผี
    [SerializeField] private float triggerDistance = 10f; // ระยะที่ต้องอยู่ใกล้ที่สุดเพื่อให้ผีเกิด

    private AudioSource audioSource; // เก็บ AudioSource ของ GameObject นี้

    private void Awake()
    {
        // ดึง AudioSource จาก GameObject ที่ใส่สคริปต์นี้
        audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int percent = Random.Range(0, 100);

            if (percent <= percentJumpScare)
            {
                if (spawnPoints.Length > 0 && ghostPrefab != null && player != null)
                {
                    Transform closestPoint = null;
                    float closestDistance = Mathf.Infinity;

                    foreach (Transform point in spawnPoints)
                    {
                        float distance = Vector3.Distance(player.position, point.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestPoint = point;
                        }
                    }

                    if (closestPoint != null && closestDistance <= triggerDistance)
                    {
                        // ✅ เล่นเสียงก่อนเกิดผี
                        if (audioSource != null)
                        {
                            audioSource.Play();
                        }
                        else
                        {
                            Debug.LogWarning("⚠️ ไม่มี AudioSource อยู่ใน GameObject นี้!");
                        }

                        // 👻 จากนั้นค่อย Spawn ผี
                        GameObject ghost = Instantiate(ghostPrefab, closestPoint.position, closestPoint.rotation);
                        Destroy(ghost, ghostLifetime);

                        Debug.Log($"👻 Ghost spawned near player at {closestPoint.name} (distance: {closestDistance:F1})");
                    }
                    else
                    {
                        Debug.Log($"ℹ️ Player not close enough to any spawn point (min distance: {closestDistance:F1})");
                    }
                }
                else
                {
                    Debug.LogWarning("❌ Missing ghostPrefab, player, or spawnPoints in inspector!");
                }
            }
        }
    }
}
