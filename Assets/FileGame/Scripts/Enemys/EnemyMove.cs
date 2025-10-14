using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyMove : MonoBehaviour
{
    [Header("Waypoints (เรียงลำดับ)")]
    public Transform[] points;

    [Header("Target (ผู้เล่น)")]
    public Transform target;
    public float detectRange = 10f;
    public float chaseDuration = 5f; // ตั้งเวลาไล่ตามได้ (วินาที)
    public bool destroyAfterChase = true; // ✅ ลบตัวเองหลังหมดเวลา
    public LayerMask visionMask = ~0;

    [Header("Move (โหมด transform)")]
    public float speed = 3.5f;
    public float arriveDistance = 0.2f;

    [Header("Rotate (โหมด transform)")]
    public bool faceMoveDirection = true;
    public float rotateSpeedDeg = 540f;

    [Header("Obstacle Avoid (โหมด transform)")]
    public LayerMask obstacleMask = ~0;
    public float lookAhead = 2.0f;
    public float sideRayLength = 1.75f;
    public float sideOffset = 0.5f;
    public float avoidWeight = 2.0f;

    private int _i;
    private NavMeshAgent _agent;
    private bool chasingTarget;
    private float chaseTimer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.stoppingDistance = arriveDistance;
            _agent.autoBraking = false;
            _agent.updateRotation = true;
        }
    }

    void OnEnable()
    {
        if (points == null || points.Length == 0) return;
        _i = 0;
        if (_agent != null) _agent.SetDestination(points[_i].position);
    }

    void Update()
    {
        // ตรวจจับการเห็นเป้าหมาย
        if (target != null && CanSeeTarget())
        {
            chasingTarget = true;
            chaseTimer = chaseDuration; // รีเซ็ตเวลาไล่
        }
        else if (chaseTimer > 0)
        {
            chaseTimer -= Time.deltaTime;
            if (chaseTimer <= 0)
            {
                if (destroyAfterChase)
                {
                    Destroy(gameObject); // 💣 ลบตัวเองเมื่อหมดเวลา
                    return;
                }
                else
                {
                    chasingTarget = false;
                }
            }
        }
        else
        {
            chasingTarget = false;
        }

        // ---------- โหมด NavMeshAgent ----------
        if (_agent != null)
        {
            if (chasingTarget && target != null)
            {
                _agent.SetDestination(target.position);
            }
            else
            {
                if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance))
                    GoNext();
            }
            return;
        }

        // ---------- โหมด Transform ----------
        Vector3 targetPos = chasingTarget && target != null ? target.position : points[_i].position;
        Vector3 to = targetPos - transform.position;
        Vector3 desiredDir = new Vector3(to.x, 0f, to.z).normalized;

        Vector3 origin = transform.position + Vector3.up * 0.2f;
        bool hitFront = Physics.Raycast(origin, transform.forward, out RaycastHit frontHit, lookAhead, obstacleMask, QueryTriggerInteraction.Ignore);

        Vector3 right = Quaternion.Euler(0f, 45f, 0f) * transform.forward;
        Vector3 left = Quaternion.Euler(0f, -45f, 0f) * transform.forward;

        Vector3 rightOrigin = origin + transform.right * sideOffset;
        Vector3 leftOrigin = origin - transform.right * sideOffset;

        bool rightBlocked = Physics.Raycast(rightOrigin, right, out RaycastHit rightHit, sideRayLength, obstacleMask, QueryTriggerInteraction.Ignore);
        bool leftBlocked = Physics.Raycast(leftOrigin, left, out RaycastHit leftHit, sideRayLength, obstacleMask, QueryTriggerInteraction.Ignore);

        Vector3 avoidDir = Vector3.zero;
        if (hitFront)
        {
            float rightClear = rightBlocked ? rightHit.distance : sideRayLength;
            float leftClear = leftBlocked ? leftHit.distance : sideRayLength;
            avoidDir += (rightClear > leftClear) ? right : left;
            avoidDir = new Vector3(avoidDir.x, 0f, avoidDir.z).normalized;
        }
        else
        {
            if (rightBlocked && !leftBlocked) avoidDir += left;
            if (leftBlocked && !rightBlocked) avoidDir += right;
            avoidDir = new Vector3(avoidDir.x, 0f, avoidDir.z).normalized;
        }

        Vector3 finalDir = (desiredDir + avoidDir * avoidWeight).normalized;
        if (finalDir.sqrMagnitude < 0.0001f) finalDir = desiredDir;

        if (faceMoveDirection && finalDir.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(finalDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, rotateSpeedDeg * Time.deltaTime);
        }

        transform.position += finalDir * speed * Time.deltaTime;

        if (!chasingTarget && (points[_i].position - transform.position).WithY(0f).magnitude <= arriveDistance)
        {
            GoNext();
        }
    }

    private bool CanSeeTarget()
    {
        if (target == null) return false;

        Vector3 dir = (target.position - transform.position);
        if (dir.magnitude > detectRange) return false;

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir.normalized, out RaycastHit hit, detectRange, visionMask))
        {
            if (hit.transform == target)
                return true;
        }
        return false;
    }

    private void GoNext()
    {
        _i = (_i + 1) % points.Length;
        if (_agent != null) _agent.SetDestination(points[_i].position);
    }

    void OnDrawGizmosSelected()
    {
        if (points == null || points.Length == 0) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        for (int k = 0; k < points.Length; k++)
        {
            if (points[k] == null) continue;
            Gizmos.DrawSphere(points[k].position, 0.12f);
            if (k + 1 < points.Length && points[k + 1] != null)
                Gizmos.DrawLine(points[k].position, points[k + 1].position);
        }
        if (points.Length > 1 && points[0] != null && points[^1] != null)
            Gizmos.DrawLine(points[^1].position, points[0].position);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, detectRange);
#endif
    }
}

static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
