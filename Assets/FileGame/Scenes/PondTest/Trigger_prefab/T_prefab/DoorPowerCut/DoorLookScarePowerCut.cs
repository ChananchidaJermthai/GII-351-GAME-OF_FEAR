using System.Collections;
using UnityEngine;

public class DoorLookScarePowerCut : MonoBehaviour
{
    [Header("Player / Camera")]
    public Transform playerCamera;          // กล้องผู้เล่น

    [Header("Look Settings")]
    public Transform lookTarget;            // จุดทิศทางเป้าหมาย (เช่น ประตู/โถง)
    public float lookAngle = 20f;           // มองเข้าใกล้มุมนี้ = ถือว่า "มองเจอ"
    public float maxCheckDistance = 50f;    // ระยะสูงสุดที่ยังเช็ค

    [Header("Ghost")]
    public GameObject ghost;                // ผีที่จะโผล่
    public Transform ghostSpawnPoint;       // จุด spawn ผี
    public Animator ghostAnimator;          // Animator ผี (ถ้ามี)
    public string ghostAppearTrigger = "Appear";
    public string ghostDisappearTrigger = "Disappear";

    [Header("Sound")]
    public AudioSource sfxSource;           // AudioSource สำหรับ SFX
    public AudioClip scareClip;             // เสียงเร้าอารมณ์ตอนเจอผี
    public AudioClip powerCutClip;          // เสียงไฟดับ (ถ้ามี)

    [Header("Camera Shake")]
    public float shakeDuration = 0.4f;
    public float shakeMagnitude = 0.18f;

    [Header("Lights to Turn Off")]
    public Light[] environmentLights;       // ไฟรอบ ๆ ที่จะดับ
    public float lightsOffDelay = 0.15f;    // ดีเลย์หลังจากเห็นผีก่อนดับไฟ

    [Header("Flashlight")]
    public Flashlight flashlight;           // อ้างอิงไปที่สคริปต์ Flashlight

    [Header("Control")]
    public bool playOnlyOnce = true;
    public bool autoDeactivateGhost = true;

    [Header("Debug")]
    public bool isActive = false;           // Trigger เรียกให้เป็น true
    public bool hasPlayed = false;

    private Vector3 camOriginalPos;

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (ghost != null)
            ghost.SetActive(false);
    }

    private void Update()
    {
        if (!isActive) return;
        if (playOnlyOnce && hasPlayed) return;
        if (playerCamera == null || lookTarget == null) return;

        Vector3 dirToTarget = lookTarget.position - playerCamera.position;
        float distance = dirToTarget.magnitude;
        if (distance > maxCheckDistance) return;

        dirToTarget.Normalize();

        Vector3 camForward = playerCamera.forward;
        camForward.y = 0f;
        dirToTarget.y = 0f;

        float angle = Vector3.Angle(camForward, dirToTarget);

        if (angle <= lookAngle)
        {
            StartCoroutine(ScareRoutine());
        }
    }

    public void Activate()
    {
        isActive = true;
    }

    private IEnumerator ScareRoutine()
    {
        if (playOnlyOnce && hasPlayed) yield break;
        hasPlayed = true;

        // Spawn ผี
        if (ghost != null && ghostSpawnPoint != null)
        {
            ghost.transform.position = ghostSpawnPoint.position;
            ghost.transform.rotation = ghostSpawnPoint.rotation;
            ghost.SetActive(true);
        }

        // เล่นอนิเมชั่นโผล่
        if (ghostAnimator != null && !string.IsNullOrEmpty(ghostAppearTrigger))
        {
            ghostAnimator.SetTrigger(ghostAppearTrigger);
        }

        // เสียงเร้าอารมณ์
        if (sfxSource && scareClip)
        {
            sfxSource.PlayOneShot(scareClip);
        }

        // กล้องกระตุก + รอเล็กน้อย ก่อนไฟดับ
        yield return StartCoroutine(DoCameraShake(shakeDuration));

        // ดีเลย์เล็กน้อยก่อนดับไฟ (ถ้าอยากให้ทันทีตั้ง lightsOffDelay = 0)
        if (lightsOffDelay > 0f)
            yield return new WaitForSeconds(lightsOffDelay);

        // ดับไฟรอบ ๆ
        TurnOffEnvironmentLights();

        // ดับไฟฉาย
        if (flashlight != null)
        {
            flashlight.ForceOff(false); // ใส่ true ถ้าอยากให้มีเสียงปิดไฟฉาย
        }

        // เสียงไฟดับ (ถ้ามี)
        if (sfxSource && powerCutClip)
        {
            sfxSource.PlayOneShot(powerCutClip);
        }

        // ผีหาย (อนิเมชั่นหายหรือปิด object เลย)
        if (ghostAnimator != null && !string.IsNullOrEmpty(ghostDisappearTrigger))
        {
            ghostAnimator.SetTrigger(ghostDisappearTrigger);
            yield return new WaitForSeconds(1.0f);
        }

        if (autoDeactivateGhost && ghost != null)
        {
            ghost.SetActive(false);
        }

        // จบ event
        isActive = false;
    }

    private IEnumerator DoCameraShake(float duration)
    {
        if (playerCamera == null) yield break;

        camOriginalPos = playerCamera.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            playerCamera.localPosition = camOriginalPos + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.localPosition = camOriginalPos;
    }

    private void TurnOffEnvironmentLights()
    {
        if (environmentLights == null) return;
        foreach (var l in environmentLights)
        {
            if (!l) continue;
            l.enabled = false;
            l.intensity = 0f;
        }
    }
}
