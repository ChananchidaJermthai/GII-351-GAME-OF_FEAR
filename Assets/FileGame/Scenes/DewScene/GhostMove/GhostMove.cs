using UnityEngine;
using UnityEngine.AI;

public class GhostMove : MonoBehaviour
{
    [Header("Points")]
    public Transform point2Spawn;
    public Transform point3Destination;  // ถ้า null → ผีอยู่กับที่

    [Header("Ghost Prefabs")]
    public GameObject[] ghostPrefabs;  // ใส่ Prefab หลายตัว

    [Header("Ghost Settings")]
    public float moveSpeed = 3.5f;
    public float destroyTime = 5f;

    [Header("Random Rate (%)")]
    [Range(0,100)]
    public float rate = 30f;  // % โอกาสสุ่ม Spawn

    [Header("Options")]
    public bool spawnOnce = false; // ถ้าติ๊กถูก จะ Spawn ครั้งเดียว

    // เก็บผีตัวล่าสุด
    private GameObject currentGhost;
    private bool hasSpawnedOnce = false; // เช็คว่า Spawn ครั้งเดียวแล้วหรือยัง

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // ถ้า spawnOnce แล้ว Spawn ไปแล้ว → ไม่ทำงาน
        if (spawnOnce && hasSpawnedOnce) return;

        // ถ้าผียังไม่ลบ → ไม่ Spawn
        if (currentGhost != null) return;

        // ถ้า spawnOnce → rate = 100% (ชนปุ๊ป Spawn เลย)
        float effectiveRate = spawnOnce ? 100f : rate;

        // Random chance
        if (Random.Range(0f,100f) > effectiveRate) return;

        // เลือก Prefab แบบสุ่ม
        if (ghostPrefabs.Length == 0)
        {
            Debug.LogWarning("ไม่มี Ghost Prefab ใส่ใน Array!");
            return;
        }
        GameObject prefab = ghostPrefabs[Random.Range(0, ghostPrefabs.Length)];

        // Sample spawn position on NavMesh
        NavMeshHit spawnHit;
        Vector3 spawnPos = point2Spawn.position;
        if (NavMesh.SamplePosition(spawnPos, out spawnHit, 1f, NavMesh.AllAreas))
            spawnPos = spawnHit.position;

        // Instantiate ghost
        currentGhost = Instantiate(prefab, spawnPos, Quaternion.identity);

        if (point3Destination != null)
        {
            // Sample destination on NavMesh
            NavMeshHit destHit;
            Vector3 destPos = point3Destination.position;
            if (NavMesh.SamplePosition(destPos, out destHit, 1f, NavMesh.AllAreas))
                destPos = destHit.position;

            // Rotate ghost to face destination
            Vector3 lookPos = destPos;
            lookPos.y = currentGhost.transform.position.y;
            currentGhost.transform.LookAt(lookPos);

            // NavMeshAgent setup
            NavMeshAgent agent = currentGhost.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.updateRotation = true;
                agent.updatePosition = true;
                agent.SetDestination(destPos);
            }
            else
            {
                Debug.LogWarning("Ghost Prefab ต้องมี NavMeshAgent!");
            }
        }

        // Destroy after time และเคลียร์ reference
        Destroy(currentGhost, destroyTime);
        Invoke(nameof(ClearCurrentGhost), destroyTime);

        // ถ้า spawnOnce = true → กำหนดว่า Spawn ครั้งเดียวแล้ว
        if (spawnOnce)
            hasSpawnedOnce = true;
    }

    private void ClearCurrentGhost()
    {
        currentGhost = null;
    }
}
