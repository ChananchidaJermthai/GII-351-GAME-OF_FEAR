using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMove : MonoBehaviour
{
    [Header("Waypoints (เรียงลำดับ)")]
    public Transform[] points;

    [Header("Target (ผู้เล่น)")]
    public string playerTag = "Player";
    private Transform target;
    public float detectRange = 10f;
    public float chaseDuration = 5f;
    public LayerMask visionMask = ~0;

    [Header("Move Settings")]
    public float arriveDistance = 0.5f;

    [Header("Spawn Control")]
    [Tooltip("อ้างถึง ShowQuestTrigger ที่ใช้ตรวจ isKeyExit")]
    public ShowQuestTrigger questTrigger;

    [Tooltip("Prefab ของผีที่จะ Spawn เมื่อ isKeyExit = true")]
    public GameObject ghostPrefab;

    private NavMeshAgent agent;
    private int currentIndex;
    private bool chasingTarget;
    private float chaseTimer;
    private bool ghostSpawned; // ✅ ป้องกัน spawn ซ้ำ

    void OnEnable()
    {
        agent = GetComponent<NavMeshAgent>();
        currentIndex = 0;

        // หา Target ตาม Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            target = playerObj.transform;

        // เริ่มเดินไปจุดแรก
        if (points.Length > 0)
            agent.SetDestination(points[currentIndex].position);
    }

    void Update()
    {
        // ✅ ถ้า questTrigger มีอยู่ และ isKeyExit เป็น true -> Spawn ผี 1 ครั้ง
        if (questTrigger != null && questTrigger.isKeyExit && !ghostSpawned)
        {
            SpawnGhost();
            ghostSpawned = true; // ป้องกันไม่ให้เกิดซ้ำ
            chasingTarget = false; // ผีจะไม่ถูกลบ
            return;
        }

        if (target != null && CanSeeTarget())
        {
            chasingTarget = true;
            chaseTimer = chaseDuration;
        }

        if (chasingTarget)
        {
            chaseTimer -= Time.deltaTime;

            if (chaseTimer <= 0f)
            {
                // ❌ ถ้ามีผี spawn แล้วจะไม่ลบตัวเอง
                if (!ghostSpawned)
                    Destroy(gameObject);
                return;
            }

            if (target != null)
                agent.SetDestination(target.position);
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (points.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= arriveDistance)
        {
            GoNext();
        }
    }

    private bool CanSeeTarget()
    {
        if (target == null) return false;

        Vector3 dir = target.position - transform.position;
        if (dir.magnitude > detectRange) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir.normalized, out RaycastHit hit, detectRange, visionMask))
        {
            return hit.transform == target;
        }

        return false;
    }

    private void GoNext()
    {
        if (points.Length == 0) return;

        currentIndex = (currentIndex + 1) % points.Length;
        agent.SetDestination(points[currentIndex].position);
    }

    private void SpawnGhost()
    {
        if (ghostPrefab == null || target == null)
        {
            Debug.LogWarning("⚠️ Missing ghostPrefab or target for SpawnGhost.");
            return;
        }

        Vector3 spawnPos = target.position + target.forward * 3f; // โผล่ตรงหน้าผู้เล่น
        Instantiate(ghostPrefab, spawnPos, Quaternion.LookRotation(-target.forward));
        Debug.Log("👻 Ghost spawned because isKeyExit = true!");
    }
}
