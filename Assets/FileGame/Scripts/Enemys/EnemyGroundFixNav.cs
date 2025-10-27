using UnityEngine;
using UnityEngine.AI;

/// ตรึงศัตรูให้อยู่ระดับพื้นของ NavMesh อย่างพอดีเท้า แก้ปัญหาจม/ลอย
[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class EnemyGroundFixNav : MonoBehaviour
{
    [Header("Offsets")]
    [Tooltip("ความสูงของ 'ฝ่าเท้า' เหนือผิว NavMesh (เผื่อความหนาของรองเท้า/พื้น)")]
    public float feetClearance = 0.02f;

    [Tooltip("ถ้า pivot ของโมเดลไม่ได้อยู่ที่ฝ่าเท้า ให้ตั้งระยะจาก pivot ถึงฝ่าเท้า (+ ขึ้น / - ลง)")]
    public float pivotToFeet = 0.0f;

    [Header("Sampling")]
    [Tooltip("รัศมีค้นหา NavMesh ใต้ตัว (เมตร)")]
    public float sampleRadius = 1.0f;

    [Tooltip("เช็คปรับระดับทุก ๆ กี่วินาที (0 = ทุกเฟรม)")]
    public float checkInterval = 0.1f;

    [Tooltip("ปรับ baseOffset แบบลื่น ๆ")]
    public float offsetLerpSpeed = 15f;

    [Header("Rigidbody (ถ้ามี)")]
    [Tooltip("ปิด gravity ของ Rigidbody อัตโนมัติตอนเริ่ม (แนะนำสำหรับ NavMeshAgent)")]
    public bool disableRigidbodyGravity = true;

    NavMeshAgent _agent;
    Rigidbody _rb;
    float _timer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();

        if (_rb && disableRigidbodyGravity)
        {
            _rb.useGravity = false;
            //_rb.isKinematic = true; // ให้ NavMeshAgent คุมการเคลื่อนที่
        }

        // ให้ Agent แก้ path เองเวลาเลื่อนไปตามพื้น
        _agent.autoRepath = true;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (checkInterval > 0f && _timer < checkInterval) return;
        _timer = 0f;

        if (!_agent.isOnNavMesh) return;

        // หาความสูงของพื้น NavMesh ใต้ตัว
        if (NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, _agent.areaMask))
        {
            // ค่าที่ควรเป็น: ระยะยกจากพื้น = ระยะ pivot->เท้า + เผื่อเท้า
            float desiredBaseOffset = pivotToFeet + feetClearance;

            // baseOffset คือระยะจากพื้น NavMesh -> ตำแหน่งทรานสฟอร์ม (แกน Y)
            // ค่อย ๆ ไล่เข้าไปหาเพื่อไม่ให้กระตุก
            _agent.baseOffset = Mathf.Lerp(_agent.baseOffset, desiredBaseOffset + (transform.position.y - hit.position.y), Time.deltaTime * offsetLerpSpeed);

            // ถ้าเลื่อนนานแล้วยังเพี้ยนมาก ให้ warp แกน Y เพื่อรีเซ็ต
            float worldYShouldBe = hit.position.y + desiredBaseOffset;
            if (Mathf.Abs(transform.position.y - worldYShouldBe) > 0.08f)
            {
                var pos = transform.position;
                pos.y = worldYShouldBe;
                _agent.Warp(pos); // ปรับแนบพื้นทันที (ไม่ทำให้ path พัง)
            }
        }
    }

    void OnValidate()
    {
        sampleRadius = Mathf.Max(0.1f, sampleRadius);
        offsetLerpSpeed = Mathf.Max(0f, offsetLerpSpeed);
        checkInterval = Mathf.Max(0f, checkInterval);
    }

    void OnDrawGizmosSelected()
    {
        if (!_agent) _agent = GetComponent<NavMeshAgent>();
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, sampleRadius);

        // แสดงระดับพื้นประมาณการ
        if (Application.isPlaying && _agent && NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, _agent.areaMask))
        {
            float desired = hit.position.y + pivotToFeet + feetClearance;
            Vector3 a = new Vector3(transform.position.x - 0.3f, desired, transform.position.z - 0.3f);
            Vector3 b = new Vector3(transform.position.x + 0.3f, desired, transform.position.z + 0.3f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(a, b);
        }
    }
}
