using UnityEngine;
using UnityEngine.AI;


[DisallowMultipleComponent]
public class EnemyMove : MonoBehaviour
{
    [Header("Waypoints (เรียงลำดับ)")]
    public Transform[] points;

    [Header("Target (ผู้เล่น)")]
    public string playerTag = "Player"; // Tag ของผู้เล่น
    private Transform target; // หาอัตโนมัติ
    public float detectRange = 10f;
    public float chaseDuration = 5f; // วินาที
    public LayerMask visionMask = ~0;

    [Header("Move (Transform Mode)")]
    public float speed = 3.5f;
    public float arriveDistance = 0.2f;

    [Header("Rotate")]
    public bool faceMoveDirection = true;
    public float rotateSpeedDeg = 540f;

    private int _i;
    private bool chasingTarget;
    private float chaseTimer;

    void OnEnable()
    {
        _i = 0;

        // หา target จาก Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            target = playerObj.transform;
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
        }

        Vector3 targetPos = (chasingTarget && target != null) ? target.position : (points.Length > 0 ? points[_i].position : transform.position);
        Vector3 to = targetPos - transform.position;
        Vector3 desiredDir = new Vector3(to.x, 0f, to.z).normalized;

        if (faceMoveDirection && desiredDir.sqrMagnitude > 0.001f)
        {
            Quaternion face = Quaternion.LookRotation(desiredDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, face, rotateSpeedDeg * Time.deltaTime);
        }

        transform.position += desiredDir * speed * Time.deltaTime;

        if (!chasingTarget && points.Length > 0 && Vector3.Distance(transform.position.WithY(0f), points[_i].position.WithY(0f)) <= arriveDistance)
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
        _i = (_i + 1) % points.Length;
    }
}

static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
