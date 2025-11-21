using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Components")]
    [SerializeField] private Animator animator;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource breathSource;
    [SerializeField] private AudioSource roarSource;

    private Transform targetPoint;
    private bool isWalking = false;
    private bool sequenceStarted = false;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (!isWalking) return;

        // เดินจากตำแหน่งปัจจุบันไปหา targetPoint
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );

        // หมุนหน้าไปทางเดิน (optional)
        Vector3 dir = targetPoint.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 5f);
        }

        // เช็คว่าถึงปลายทางรึยัง
        float distance = Vector3.Distance(transform.position, targetPoint.position);
        if (distance < 0.05f)
        {
            ReachDestination();
        }
    }

    public void StartSequence()
    {
        if (sequenceStarted) return;
        sequenceStarted = true;

        // เอาผีไปวางที่จุดเริ่ม
        if (pointA != null)
        {
            transform.position = pointA.position;
        }

        targetPoint = pointB;
        isWalking = true;

        // สั่ง animator
        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
        }

        // เปิดเสียงหายใจ (loop)
        if (breathSource != null && !breathSource.isPlaying)
        {
            breathSource.loop = true;
            breathSource.Play();
        }

        // เสียงเท้าใช้จาก Animation Event ก็ได้ (ดูหัวข้อถัดไป)
        if (footstepSource != null)
        {
            // ถ้าอยากเปิดเป็น loop ธรรมดา:
            // footstepSource.loop = true;
            // footstepSource.Play();
        }
    }

    private void ReachDestination()
    {
        isWalking = false;

        // หยุดเดิน
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetTrigger("Roar");  // เปลี่ยนไปอนิเม Roar
        }

        // หยุดเท้า
        if (footstepSource != null && footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }

        // ยังให้หายใจต่อก็ได้ หรือจะหยุดก็ได้ แล้วแต่ mood
        // เล่นเสียง Roar
        if (roarSource != null)
        {
            roarSource.Play();
        }
    }

    // เรียกจาก Animation Event เวลาเท้าก้าว
    public void PlayFootstep()
    {
        if (footstepSource != null)
        {
            footstepSource.PlayOneShot(footstepSource.clip);
        }
    }
}
