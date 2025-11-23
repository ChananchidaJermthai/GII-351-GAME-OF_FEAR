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

    public SanityApplier sanityApplier;
    [SerializeField] float damage = 10f;

    [Header("Sound")]
    public AudioSource sfxSource;           // AudioSource สำหรับ SFX
    public AudioClip scareClip;             // เสียงเร้าอารมณ์ตอนเจอผี
    public AudioClip powerCutClip;          // เสียงไฟดับ (ถ้ามี)

    [Header("Camera Shake")]
    public float shakeDuration = 0.4f;
    public float shakeMagnitude = 0.18f;

    [Header("Lights to Turn Off")]
    public Light[] environmentLights;       // ไฟรอบ ๆ ที่จะดับ

    [Header("Sequence Timing")]
    [Tooltip("ดีเลย์หลังปิดไฟรอบแรกก่อนให้ผีเกิด (ความมืดล้วนๆ ก่อนผีโผล่)")]
    public float firstDarkDelay = 0.2f;
    [Tooltip("เวลาที่เปิดไฟให้เห็นผีพร้อมเสียงหลอก")]
    public float lightsOnDuration = 0.8f;
    [Tooltip("เวลาที่ปิดไฟรอบสองตอนผีกำลังหายไป ก่อนคืนสภาพปกติ")]
    public float secondDarkDuration = 0.5f;

    [Header("Flashlight")]
    public Flashlight flashlight;           // อ้างอิงไปที่สคริปต์ Flashlight

    [Header("Control")]
    public bool playOnlyOnce = true;
    public bool autoDeactivateGhost = true;

    [Header("Debug")]
    public bool isActive = false;           // Trigger เรียกให้เป็น true
    public bool hasPlayed = false;

    private Vector3 camOriginalPos;

    // เก็บค่าไฟเดิมไว้เพื่อคืนค่าทีหลัง
    private float[] originalIntensities;
    private bool[] originalEnabledStates;

    private void Start()
    {
        sanityApplier = FindAnyObjectByType<SanityApplier>();
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (ghost != null)
            ghost.SetActive(false);

        // เก็บสภาพไฟเดิม
        if (environmentLights != null && environmentLights.Length > 0)
        {
            originalIntensities = new float[environmentLights.Length];
            originalEnabledStates = new bool[environmentLights.Length];

            for (int i = 0; i < environmentLights.Length; i++)
            {
                if (environmentLights[i] == null) continue;
                originalIntensities[i] = environmentLights[i].intensity;
                originalEnabledStates[i] = environmentLights[i].enabled;
            }
        }
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

        // ลำดับใหม่ตามที่ขอ:
        // 1) ปิดไฟรอบๆ + ปิดไฟฉาย + เสียงไฟดับ
        TurnOffEnvironmentLights();

        if (flashlight != null)
        {
            // ดับไฟฉาย (ถ้าอยากให้มีเสียง toggle off จาก Flashlight เอง ใช้ true)
            flashlight.ForceOff(false);
        }

        if (sfxSource && powerCutClip)
        {
            sfxSource.PlayOneShot(powerCutClip);
        }

        // ความมืดสนิทสักพักก่อนผีเกิด
        if (firstDarkDelay > 0f)
            yield return new WaitForSeconds(firstDarkDelay);

        // 2) ผีเกิด (ในความมืด)
        if (ghost != null && ghostSpawnPoint != null)
        {
            ghost.transform.position = ghostSpawnPoint.position;
            ghost.transform.rotation = ghostSpawnPoint.rotation;
            ghost.SetActive(true);
        }

        if (ghostAnimator != null && !string.IsNullOrEmpty(ghostAppearTrigger))
        {
            ghostAnimator.SetTrigger(ghostAppearTrigger);
        }

        // อาจจะรอให้อนิเมชั่นผีโผล่เล่นไปหน่อยก่อนเปิดไฟก็ได้
        // ถ้าไม่อยากดีเลย์เพิ่ม ก็คอมเมนต์บรรทัดนี้ทิ้ง
        yield return new WaitForSeconds(0.1f);

        // 3) เปิดไฟ + เสียงหลอก + กล้องกระตุก
        TurnOnEnvironmentLights();
        sanityApplier.AddSanity(damage);

        if (sfxSource && scareClip)
        {
            sfxSource.PlayOneShot(scareClip);
        }

        // กล้องสั่นตอนที่เห็นผีชัด ๆ
        yield return StartCoroutine(DoCameraShake(shakeDuration));

        // เปิดไฟค้างให้เห็นผีชัด ๆ ช่วงหนึ่ง
        if (lightsOnDuration > 0f)
            yield return new WaitForSeconds(lightsOnDuration);

        // 4) ปิดไฟรอบสอง (ไฟดับอีกครั้งตอนผีกำลังจะหาย)
        TurnOffEnvironmentLights();

        if (secondDarkDuration > 0f)
            yield return new WaitForSeconds(secondDarkDuration);

        // ผีหาย: เล่นอนิเมชั่นหาย + ปิด object
        if (ghostAnimator != null && !string.IsNullOrEmpty(ghostDisappearTrigger))
        {
            ghostAnimator.SetTrigger(ghostDisappearTrigger);
            // รออนิเมชั่นหายแป๊บนึง
            yield return new WaitForSeconds(0.3f);
        }

        if (autoDeactivateGhost && ghost != null)
        {
            ghost.SetActive(false);
        }

        // 5) เปิดไฟกลับเป็นปกติ (ตามค่าเดิมก่อน event)
        TurnOnEnvironmentLights();

        // ถ้าอยากบังคับให้ไฟฉายกลับมาเปิดเอง ก็ต้องไปเพิ่ม method ForceOn ใน Flashlight แล้วเรียกที่นี่
        // ตอนนี้ปล่อยให้ผู้เล่นกดเปิดเองเพื่อเพิ่มความหลอน

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

    private void TurnOnEnvironmentLights()
    {
        if (environmentLights == null) return;

        for (int i = 0; i < environmentLights.Length; i++)
        {
            Light l = environmentLights[i];
            if (!l) continue;

            if (originalIntensities != null && originalIntensities.Length == environmentLights.Length)
            {
                l.intensity = originalIntensities[i];
            }
            else
            {
                // ถ้าจำค่าไม่ได้ ให้ใช้ 1 เป็น default
                l.intensity = 1f;
            }

            if (originalEnabledStates != null && originalEnabledStates.Length == environmentLights.Length)
            {
                l.enabled = originalEnabledStates[i];
            }
            else
            {
                l.enabled = true;
            }
        }
    }
}
