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

    private NavMeshAgent agent;
    private int currentIndex;
    private bool chasingTarget;
    private float chaseTimer;

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
                Destroy(gameObject);
                return;
            }

            // ไล่ผู้เล่น
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

        // เมื่อถึงจุด -> ไปจุดต่อไป
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
}
