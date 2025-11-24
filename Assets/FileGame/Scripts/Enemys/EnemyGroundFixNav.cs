using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ปรับ NavMeshAgent ให้ยืนอยู่ระดับพื้นอย่างพอดี (แก้ปัญหาจม/ลอย)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class EnemyGroundFixNav : MonoBehaviour
{
    [Header("Offsets")]
    [Tooltip("ความสูงของ 'ฝ่าเท้า' เหนือพื้น NavMesh (เผื่อความหนาของรองเท้า/พื้น)")]
    public float feetClearance = 0.02f;

    [Tooltip("ถ้า pivot ของโมเดลไม่ได้อยู่ที่ฝ่าเท้า ให้ตั้งระยะจาก pivot ถึงฝ่าเท้า (+ขึ้น / -ลง)")]
    public float pivotToFeet = 0f;

    [Header("Sampling")]
    [Tooltip("รัศมีค้นหา NavMesh ใต้ตัว (เมตร)")]
    public float sampleRadius = 1f;

    [Tooltip("เช็คปรับระดับทุก ๆ กี่วินาที (0 = ทุกเฟรม)")]
    public float checkInterval = 0.1f;

    [Tooltip("ปรับ baseOffset แบบลื่น ๆ")]
    public float offsetMoveSpeed = 15f;

    [Header("Rigidbody (ถ้ามี)")]
    [Tooltip("ปิด gravity ของ Rigidbody อัตโนมัติ (แนะนำสำหรับ NavMeshAgent)")]
    public bool disableRigidbodyGravity = true;

    private NavMeshAgent _agent;
    private Rigidbody _rb;
    private float _timer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();

        if (_rb && disableRigidbodyGravity)
        {
            _rb.useGravity = false;
            _rb.isKinematic = true; // ให้ NavMeshAgent ควบคุมการเคลื่อนที่
        }

        _agent.autoRepath = true;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (checkInterval > 0f && _timer < checkInterval)
            return;
        _timer = 0f;

        if (!_agent.isOnNavMesh) return;

        // หา NavMesh ใต้ตัว
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRadius, _agent.areaMask))
        {
            float desiredBaseOffset = pivotToFeet + feetClearance;
            float targetOffset = desiredBaseOffset + (transform.position.y - hit.position.y);

            // ปรับ baseOffset แบบลื่น
            _agent.baseOffset = Mathf.MoveTowards(_agent.baseOffset, targetOffset, offsetMoveSpeed * Time.deltaTime);

            // ถ้า deviation สูง รีเซ็ตตำแหน่งทันที
            float worldYShouldBe = hit.position.y + desiredBaseOffset;
            if (Mathf.Abs(transform.position.y - worldYShouldBe) > 0.08f)
            {
                Vector3 newPos = transform.position;
                newPos.y = worldYShouldBe;
                _agent.Warp(newPos);
            }
        }
    }

    void OnValidate()
    {
        sampleRadius = Mathf.Max(0.1f, sampleRadius);
        offsetMoveSpeed = Mathf.Max(0f, offsetMoveSpeed);
        checkInterval = Mathf.Max(0f, checkInterval);
    }

    void OnDrawGizmosSelected()
    {
        if (!_agent) _agent = GetComponent<NavMeshAgent>();

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, sampleRadius);

        if (Application.isPlaying && _agent && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRadius, _agent.areaMask))
        {
            float desired = hit.position.y + pivotToFeet + feetClearance;
            Vector3 a = new Vector3(transform.position.x - 0.3f, desired, transform.position.z - 0.3f);
            Vector3 b = new Vector3(transform.position.x + 0.3f, desired, transform.position.z + 0.3f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(a, b);
        }
    }
}
