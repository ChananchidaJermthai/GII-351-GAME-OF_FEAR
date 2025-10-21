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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int percent = Random.Range(0, 100);

            if (percent <= percentJumpScare)
            {
                // ตรวจว่ามีจุด spawn หรือไม่
                if (spawnPoints.Length > 0 && ghostPrefab != null && player != null)
                {
                    // หาจุด spawn ที่อยู่ใกล้ผู้เล่นที่สุด
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

                    // ถ้าอยู่ในระยะที่กำหนด ให้ spawn ผีที่จุดใกล้สุด
                    if (closestPoint != null && closestDistance <= triggerDistance)
                    {
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
