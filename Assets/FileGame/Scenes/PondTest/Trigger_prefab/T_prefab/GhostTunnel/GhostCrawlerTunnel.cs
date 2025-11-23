using UnityEngine;

public class GhostCrawlerTunnel : MonoBehaviour
{
    [Header("Path (ใช้ทีละหลายจุด)")]
    public Transform[] pathPoints;      // ใส่จุดเรียงตามทางท่อ
    public float speed = 5f;

    [Header("Visual")]
    [Tooltip("ใส่ GameObject ที่เป็นโมเดลผี (มักจะเป็น child)")]
    public GameObject ghostModel;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip metalHitSFX;
    public AudioClip crawlLoopSFX;

    [Header("Option")]
    public bool autoFaceDirection = true;

    private bool isChasing = false;
    private int currentIndex = 0;
    private Vector3 moveDir;
    private Collider ghostCollider;

    private void Awake()
    {
        ghostCollider = GetComponent<Collider>();
        HideGhost();
    }

    public void StartChase()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogWarning("GhostCrawlerTunnel: ต้องมี pathPoints อย่างน้อย 2 จุด");
            return;
        }

        // เริ่มที่จุดแรก
        currentIndex = 0;

        transform.position = pathPoints[currentIndex].position;

        // คำนวณทิศทางไปจุดถัดไป
        UpdateMoveDirection();

        ShowGhost();
        isChasing = true;

        // เสียงเหล็กดัง
        if (audioSource != null && metalHitSFX != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.PlayOneShot(metalHitSFX);
        }

        // เสียงคลาน loop
        if (audioSource != null && crawlLoopSFX != null)
        {
            audioSource.clip = crawlLoopSFX;
            audioSource.loop = true;
            audioSource.PlayDelayed(0.1f);
        }
    }

    private void Update()
    {
        if (!isChasing) return;
        if (pathPoints == null || pathPoints.Length == 0) return;

        // เดินไปยังจุดเป้าหมายปัจจุบัน
        Transform targetPoint = pathPoints[currentIndex];
        Vector3 toTarget = targetPoint.position - transform.position;

        // ถ้าใกล้พอแล้ว → ข้ามไปจุดถัดไป
        if (toTarget.magnitude <= 0.05f)
        {
            if (currentIndex < pathPoints.Length - 1)
            {
                currentIndex++;
                UpdateMoveDirection();
                targetPoint = pathPoints[currentIndex];
                toTarget = targetPoint.position - transform.position;
            }
            else
            {
                // ถึงจุดสุดท้ายแล้ว
                StopChase();
                return;
            }
        }

        // เคลื่อนที่
        moveDir = toTarget.normalized;
        transform.position += moveDir * speed * Time.deltaTime;

        // หันหน้าไปทางเดิน
        if (autoFaceDirection && moveDir != Vector3.zero)
        {
            transform.forward = moveDir;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isChasing) return;
        if (!other.CompareTag("Player")) return;

        JumpScareSpawnInFront.Instance.PlayScare();

        StopChase();
    }

    private void StopChase()
    {
        isChasing = false;

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        HideGhost();
    }

    private void UpdateMoveDirection()
    {
        if (currentIndex < pathPoints.Length - 1)
        {
            Vector3 dir = pathPoints[currentIndex + 1].position - pathPoints[currentIndex].position;
            moveDir = dir.normalized;
        }
        else
        {
            moveDir = transform.forward;
        }
    }

    private void HideGhost()
    {
        if (ghostModel != null)
            ghostModel.SetActive(false);

        if (ghostCollider != null)
            ghostCollider.enabled = false;
    }

    private void ShowGhost()
    {
        if (ghostModel != null)
            ghostModel.SetActive(true);

        if (ghostCollider != null)
            ghostCollider.enabled = true;
    }
}
