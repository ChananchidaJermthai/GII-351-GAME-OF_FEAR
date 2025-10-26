using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyNavMeshEdgeAvoider : MonoBehaviour
{
    [Header("Probe")]
    public float edgeDetectDistance = 0.7f;
    public float forwardProbeDistance = 1.5f;
    [Range(0.5f, 3f)] public float radiusMargin = 1.5f;

    [Header("Steer")]
    [Range(0f, 1f)] public float awayWeight = 0.45f;
    [Range(0f, 1f)] public float slideWeight = 0.55f;
    public float steerDistance = 1.6f;
    public float holdSeconds = 0.6f;
    public float cooldown = 0.35f;

    [Header("Takeover Destination")]
    public bool takeoverDestination = true;
    public float takeoverSeconds = 0.5f;

    [Header("Debug")]
    public bool draw = true;
    public Color cEdge = new Color(1, 0.3f, 0.2f, 1);
    public Color cSlide = new Color(0.2f, 0.9f, 1, 1);
    public Color cAway = new Color(1, 0.9f, 0.2f, 1);

    NavMeshAgent ag;
    Vector3 origDest;
    bool hasOrigDest;
    float holdT, cdT, takeoverT;

    void Awake()
    {
        ag = GetComponent<NavMeshAgent>();
        ag.autoRepath = true;
        if (!ag.isOnNavMesh) Debug.LogWarning("[EdgeAvoider] Agent is not on NavMesh.", this);
    }

    void Update()
    {
        if (!ag.isOnNavMesh) return;

        if (cdT > 0f) cdT -= Time.deltaTime;
        if (takeoverT > 0f) takeoverT -= Time.deltaTime;

        Vector3 desired = ag.desiredVelocity; desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f) { ReleaseIfReady(); return; }
        Vector3 dir = desired.normalized;

        bool nearEdge = NavMesh.FindClosestEdge(transform.position, out var edgeHit, ag.areaMask)
                        && edgeHit.distance <= edgeDetectDistance;

        // *** แก้ตรงนี้: ประกาศ forwardHit ให้แน่ชัดก่อน แล้วค่อยกำหนดค่าใน if ***
        NavMeshHit forwardHit = default;
        bool hitForwardEdge = false;
        if (forwardProbeDistance > 0.01f)
        {
            Vector3 from = transform.position;
            Vector3 to = from + dir * forwardProbeDistance;
            hitForwardEdge = NavMesh.Raycast(from, to, out forwardHit, ag.areaMask);
        }

        if ((nearEdge || hitForwardEdge) && cdT <= 0f)
        {
            Vector3 normal = nearEdge ? edgeHit.normal : forwardHit.normal;
            normal.y = 0f; if (normal.sqrMagnitude < 1e-4f) normal = -dir;

            // Tangent = ทิศไถเลียบขอบ
            Vector3 tangent = Vector3.Cross(Vector3.up, normal).normalized;

            float minRepel = ag.radius * Mathf.Max(1f, radiusMargin);

            // ผสมทิศ: ไถตามขอบ + หนีขอบเล็กน้อย
            Vector3 blended = (tangent * slideWeight + normal * awayWeight).normalized;

            Vector3 steerTarget = transform.position + blended * Mathf.Max(steerDistance, minRepel);

            if (NavMesh.SamplePosition(steerTarget, out var sp, minRepel * 1.5f, ag.areaMask))
            {
                if (!hasOrigDest && !ag.pathPending) { origDest = ag.destination; hasOrigDest = true; }

                if (takeoverDestination) takeoverT = Mathf.Max(takeoverT, takeoverSeconds);

                ag.SetDestination(sp.position);
                holdT = holdSeconds;
                cdT = cooldown;

                if (draw)
                {
                    Vector3 hitPos = nearEdge ? edgeHit.position : forwardHit.position;
                    Debug.DrawLine(transform.position, hitPos, cEdge, 0.1f);
                    Debug.DrawRay(hitPos, normal * 0.8f, cAway, 0.1f);
                    Debug.DrawRay(transform.position, tangent * 1.2f, cSlide, 0.1f);
                }
            }
        }
        else
        {
            ReleaseIfReady();
        }

        // กันปลายทางหลุดนอก NavMesh
        if (!ag.pathPending)
        {
            if (!NavMesh.SamplePosition(ag.destination, out _, ag.radius * 2f, ag.areaMask))
            {
                if (NavMesh.SamplePosition(transform.position, out var center, ag.radius * 4f, ag.areaMask))
                    ag.SetDestination(center.position);
            }
        }
    }

    void ReleaseIfReady()
    {
        if (!hasOrigDest) return;
        if (holdT > 0f) { holdT -= Time.deltaTime; return; }
        if (takeoverT > 0f) return;

        ag.SetDestination(origDest);
        hasOrigDest = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!draw) return;
        if (!ag) ag = GetComponent<NavMeshAgent>();
        Gizmos.color = cEdge;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.05f, edgeDetectDistance));
        if (ag)
        {
            Vector3 desired = ag.desiredVelocity; desired.y = 0f;
            if (desired.sqrMagnitude > 0.0001f && forwardProbeDistance > 0.01f)
            {
                Vector3 dir = desired.normalized;
                Gizmos.color = cSlide;
                Gizmos.DrawLine(transform.position, transform.position + dir * forwardProbeDistance);
            }
        }
    }
}
