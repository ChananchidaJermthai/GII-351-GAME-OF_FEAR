using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyMove : MonoBehaviour
{
    [Header("Waypoints (เรียงลำดับ)")]
    public Transform[] points;

    [Header("Move (โหมด transform)")]
    public float speed = 3.5f;
    public float arriveDistance = 0.2f;

    [Header("Rotate (โหมด transform)")]
    public bool faceMoveDirection = true;
    public float rotateSpeedDeg = 540f;

    [Header("Obstacle Avoid (โหมด transform)")]
    [Tooltip("เลเยอร์ของสิ่งกีดขวาง")]
    public LayerMask obstacleMask = ~0;
    [Tooltip("ระยะมองไปข้างหน้า")]
    public float lookAhead = 2.0f;
    [Tooltip("ระยะ ray ด้านข้างซ้าย/ขวา")]
    public float sideRayLength = 1.75f;
    [Tooltip("ระยะเลื่อนตำแหน่งยิง ray ไปด้านซ้าย/ขวา")]
    public float sideOffset = 0.5f;
    [Tooltip("น้ำหนักการหลบเมื่อเจอสิ่งกีดขวาง (ยิ่งสูงยิ่งหักหลบแรง)")]
    public float avoidWeight = 2.0f;

    private int _i;
    private NavMeshAgent _agent;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.stoppingDistance = arriveDistance;
            _agent.autoBraking = false;       // โค้งลื่นขึ้น
            _agent.updateRotation = true;
            // คุณสามารถจูนค่า Obstacle Avoid ใน Inspector ของ Agent เพิ่มเติมได้ (Radius/Quality/Priority)
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
        if (points == null || points.Length == 0) return;

        if (_agent != null)
        {
            // โหมด NavMeshAgent: ให้ Agent หาทางและหลบเอง
            if (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(arriveDistance, _agent.stoppingDistance))
                GoNext();
            return;
        }

        // โหมด transform: เดินเอง + เลี่ยงสิ่งกีดขวางด้วย raycast
        Vector3 target = points[_i].position;
        Vector3 to = target - transform.position;
        Vector3 desiredDir = new Vector3(to.x, 0f, to.z).normalized;

        // 1) เช็คข้างหน้าตรงๆ
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        bool hitFront = Physics.Raycast(origin, transform.forward, out RaycastHit frontHit, lookAhead, obstacleMask, QueryTriggerInteraction.Ignore);

        // 2) ยิง ray ซ้าย/ขวาเพื่อหาเส้นทางโล่ง
        Vector3 right = Quaternion.Euler(0f, 45f, 0f) * transform.forward;
        Vector3 left = Quaternion.Euler(0f, -45f, 0f) * transform.forward;

        Vector3 rightOrigin = origin + transform.right * sideOffset;
        Vector3 leftOrigin = origin - transform.right * sideOffset;

        bool rightBlocked = Physics.Raycast(rightOrigin, right, out RaycastHit rightHit, sideRayLength, obstacleMask, QueryTriggerInteraction.Ignore);
        bool leftBlocked = Physics.Raycast(leftOrigin, left, out RaycastHit leftHit, sideRayLength, obstacleMask, QueryTriggerInteraction.Ignore);

        // 3) คำนวณเวคเตอร์หลบ (เลือกฝั่งที่โล่งกว่า)
        Vector3 avoidDir = Vector3.zero;
        if (hitFront)
        {
            // ถ้าหน้าตัน ให้เลือกไปฝั่งที่ "ไม่ตัน" หรือมีระยะชนไกลกว่า
            float rightClear = rightBlocked ? rightHit.distance : sideRayLength;
            float leftClear = leftBlocked ? leftHit.distance : sideRayLength;

            if (rightClear > leftClear) avoidDir += right;
            else avoidDir += left;

            avoidDir = new Vector3(avoidDir.x, 0f, avoidDir.z).normalized;
        }
        else
        {
            // ถ้าไม่ชนตรงหน้า แต่มีฝั่งหนึ่งชิดสิ่งกีดขวาง ให้ผลักออกเบาๆ
            if (rightBlocked && !leftBlocked) avoidDir += left;
            if (leftBlocked && !rightBlocked) avoidDir += right;
            avoidDir = new Vector3(avoidDir.x, 0f, avoidDir.z).normalized;
        }

        // 4) ผสมทิศไปจุดหมาย + ทิศหลบ
        Vector3 finalDir = (desiredDir + avoidDir * avoidWeight).normalized;
        if (finalDir.sqrMagnitude < 0.0001f) finalDir = desiredDir; // กันกรณีศูนย์

        // หมุนหันหน้า
        if (faceMoveDirection && finalDir.sqrMagnitude > 0.0001f)
        {
            Quaternion face = Quaternion.LookRotation(finalDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, rotateSpeedDeg * Time.deltaTime);
        }

        // เดิน
        transform.position += finalDir * speed * Time.deltaTime;

        // ถึงจุดยัง?
        if ((points[_i].position - transform.position).WithY(0f).magnitude <= arriveDistance)
        {
            GoNext();
        }
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

        // Debug rays (เฉพาะในโหมด Scene)
#if UNITY_EDITOR
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        Vector3 right = Quaternion.Euler(0f, 45f, 0f) * transform.forward;
        Vector3 left = Quaternion.Euler(0f, -45f, 0f) * transform.forward;

        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawLine(origin, origin + transform.forward * lookAhead);
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawLine(origin + transform.right * sideOffset, origin + transform.right * sideOffset + right * sideRayLength);
        UnityEditor.Handles.DrawLine(origin - transform.right * sideOffset, origin - transform.right * sideOffset + left * sideRayLength);
#endif
    }
}

// helper เล็กๆ ให้เวคเตอร์ตั้งค่า y ได้ง่าย
static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
