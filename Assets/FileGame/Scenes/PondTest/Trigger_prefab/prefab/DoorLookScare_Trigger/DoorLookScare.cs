using System.Collections;
using UnityEngine;

public class DoorLookScare : MonoBehaviour
{
    [Header("Player / Camera")]
    public Transform playerCamera;         // กล้องผู้เล่น

    [Header("Look Settings")]
    public Transform doorLookPoint;        // จุดที่ถือว่าเป็น "ทิศทางประตู"
    public float lookAngle = 20f;          // มองเข้าใกล้มุมนี้ = ถือว่ามองประตู
    public float maxCheckDistance = 50f;   // ระยะสูงสุดที่ยังเช็ค

    [Header("Ghost Head")]
    public GameObject doorGhost;           // ผีที่โผล่หัว
    public Transform doorGhostSpawnPoint;  // จุดโผล่หัว
    public Animator doorGhostAnimator;     // Animator ผี
    public string appearTrigger = "Appear";
    public string disappearTrigger = "Disappear";

    [Header("Sound")]
    public AudioSource sfxSource;          // AudioSource สำหรับ SFX
    public AudioClip scareClip;            // เสียงเร้าอารมณ์

    [Header("Timing")]
    public float ghostAppearDelay = 0.1f;  // ดีเลย์ก่อนโผล่หัว
    public float ghostStayDuration = 1.0f; // เวลาที่ผีโผล่ก่อนหาย

    [Header("Debug")]
    public bool isActive = false;          // ถูกเปิดให้ทำงานจาก Trigger หรือยัง
    public bool hasPlayed = false;         // เล่นไปแล้วหรือยัง (ไม่เล่นซ้ำ)

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (doorGhost != null)
            doorGhost.SetActive(false);
    }

    private void Update()
    {
        if (!isActive || hasPlayed) return;
        if (playerCamera == null || doorLookPoint == null) return;

        Vector3 dirToDoor = doorLookPoint.position - playerCamera.position;
        float distance = dirToDoor.magnitude;
        if (distance > maxCheckDistance) return;

        dirToDoor.Normalize();

        // เอาแกน Y ออก เพื่อดูแค่แนวราบ (กันกรณีก้ม/เงยเยอะเกิน)
        Vector3 camForward = playerCamera.forward;
        camForward.y = 0f;
        dirToDoor.y = 0f;

        float angle = Vector3.Angle(camForward, dirToDoor);

        if (angle <= lookAngle)
        {
            // ผู้เล่นหันมาทางประตูแล้ว → เริ่มหลอก
            StartCoroutine(PlayDoorScareRoutine());
        }
    }

    public void Activate()
    {
        isActive = true;
    }

    private IEnumerator PlayDoorScareRoutine()
    {
        if (hasPlayed) yield break;
        hasPlayed = true;

        if (ghostAppearDelay > 0f)
            yield return new WaitForSeconds(ghostAppearDelay);

        if (doorGhost != null && doorGhostSpawnPoint != null)
        {
            doorGhost.transform.position = doorGhostSpawnPoint.position;
            doorGhost.transform.rotation = doorGhostSpawnPoint.rotation;
            doorGhost.SetActive(true);
        }

        // เล่นอนิเมชั่นโผล่หัว
        if (doorGhostAnimator != null && !string.IsNullOrEmpty(appearTrigger))
        {
            doorGhostAnimator.SetTrigger(appearTrigger);
        }

        // เล่นเสียง
        if (sfxSource != null && scareClip != null)
        {
            sfxSource.PlayOneShot(scareClip);
        }

        // อยู่สักพัก
        yield return new WaitForSeconds(ghostStayDuration);

        // เล่นอนิเมชั่นหาย (ถ้ามี)
        if (doorGhostAnimator != null && !string.IsNullOrEmpty(disappearTrigger))
        {
            doorGhostAnimator.SetTrigger(disappearTrigger);
        }

        // ถ้าอยากให้รอให้อนิเมชั่นจบจริง ๆ ค่อยปิด ก็เพิ่ม Wait อีกตัวได้
        yield return new WaitForSeconds(0.2f);

        if (doorGhost != null)
            doorGhost.SetActive(false);
    }
}
