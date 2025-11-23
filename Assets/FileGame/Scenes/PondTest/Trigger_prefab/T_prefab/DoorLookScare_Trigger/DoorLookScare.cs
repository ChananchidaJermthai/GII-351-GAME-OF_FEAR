using System.Collections;
using UnityEngine;

public class DoorLookScare : MonoBehaviour
{
    [Header("Player / Camera")]
    public Transform playerCamera;         // กล้องผู้เล่น

    [Header("Look At Player")]
    public bool makeGhostLookAtPlayer = true;   // เปิด/ปิดฟีเจอร์นี้
    public float lookAtTurnSpeed = 10f;         // ความเร็วในการหมุนมอง

    [Header("Look Settings")]
    public Transform doorLookPoint;        // จุดที่ถือว่าเป็น "ทิศทางประตู"
    public float lookAngle = 20f;          // มองเข้าใกล้มุมนี้ = ถือว่ามองประตู
    public float maxCheckDistance = 50f;   // ระยะสูงสุดที่ยังเช็ค

    public SanityApplier sanityApplier;
    [SerializeField] float damage = 10f;

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

    private void Awake()
    {
        sanityApplier = FindAnyObjectByType<SanityApplier>();
    }
    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (doorGhost != null)
            doorGhost.SetActive(false);
    }

    private void Update()
    {
        if (!isActive || hasPlayed)
        {

        }
        else
        {
            if (playerCamera == null || doorLookPoint == null) return;

            Vector3 dirToDoor = doorLookPoint.position - playerCamera.position;
            float distance = dirToDoor.magnitude;
            if (distance <= maxCheckDistance)
            {
                dirToDoor.Normalize();

                Vector3 camForward = playerCamera.forward;
                camForward.y = 0f;
                dirToDoor.y = 0f;

                float angle = Vector3.Angle(camForward, dirToDoor);

                if (angle <= lookAngle)
                {
                    StartCoroutine(PlayDoorScareRoutine());
                }
            }
        }
      
        if (makeGhostLookAtPlayer && doorGhost != null && doorGhost.activeInHierarchy && playerCamera != null)
        {
            Vector3 targetPos = playerCamera.position;
            targetPos.y = doorGhost.transform.position.y;

            Vector3 dir = (targetPos - doorGhost.transform.position);
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                doorGhost.transform.rotation = Quaternion.Slerp(
                    doorGhost.transform.rotation,
                    targetRot,
                    Time.deltaTime * lookAtTurnSpeed
                );
            }
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
            sanityApplier.AddSanity(damage);
        }

        if (doorGhostAnimator != null && !string.IsNullOrEmpty(appearTrigger))
        {
            doorGhostAnimator.SetTrigger(appearTrigger);
        }

        if (sfxSource != null && scareClip != null)
        {
            sfxSource.PlayOneShot(scareClip);
        }

        yield return new WaitForSeconds(ghostStayDuration);

        if (doorGhostAnimator != null && !string.IsNullOrEmpty(disappearTrigger))
        {
            doorGhostAnimator.SetTrigger(disappearTrigger);
        }

        yield return new WaitForSeconds(0.2f);

        if (doorGhost != null)
            doorGhost.SetActive(false);
    }
}
