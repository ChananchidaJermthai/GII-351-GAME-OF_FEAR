using UnityEngine;

public class GhostCrawlerTunnel : MonoBehaviour
{
    [Header("Path")]
    public Transform startPoint;
    public Transform endPoint;
    public float speed = 5f;

    [Header("Audio")]
    public AudioSource audioSource;      // แนะนำให้แยก AudioSource ของผีไว้เลย
    public AudioClip metalHitSFX;        // เสียงตอนทริกเกอร์ดัง "เหล็กกระแทก"
    public AudioClip crawlLoopSFX;       // เสียงผีคลานยาว ๆ (loop)

    [Header("Option")]
    public bool autoFaceDirection = true;

    private bool isChasing = false;
    private Vector3 moveDir;

    public void StartChase()
    {
        // วาร์ปไปที่จุดเริ่ม
        if (startPoint != null)
            transform.position = startPoint.position;

        // หาทิศไปจุด B
        if (startPoint != null && endPoint != null)
            moveDir = (endPoint.position - startPoint.position).normalized;
        else
            moveDir = transform.forward;

        isChasing = true;

        // ===== เล่นเสียงเหล็กกระแทกตอน Trigger =====
        if (audioSource != null && metalHitSFX != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.PlayOneShot(metalHitSFX);
        }

        // ===== เริ่มเล่นเสียงคลานเป็น Loop =====
        // หน่วง 0.1 วิ เพื่อไม่ให้ทับกับเสียงเหล็ก
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

        transform.position += moveDir * speed * Time.deltaTime;

        if (autoFaceDirection && moveDir != Vector3.zero)
            transform.forward = moveDir;

        // ถึงจุด B → หยุดไล่และปิดเสียง
        if (endPoint != null)
        {
            Vector3 toEnd = endPoint.position - transform.position;
            if (Vector3.Dot(toEnd, moveDir) <= 0f)
            {
                StopChase();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isChasing) return;
        if (!other.CompareTag("Player")) return;

        // === ตรงนี้ให้เธอใส่ระบบโดนผีเอง ===
        Debug.Log("TODO: Player hit by tunnel crawler");

        StopChase();
    }

    // ฟังก์ชันหยุดไล่ผี
    private void StopChase()
    {
        isChasing = false;

        // ปิดเสียงคลาน
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        // ซ่อนผี
        gameObject.SetActive(false);
    }
}
