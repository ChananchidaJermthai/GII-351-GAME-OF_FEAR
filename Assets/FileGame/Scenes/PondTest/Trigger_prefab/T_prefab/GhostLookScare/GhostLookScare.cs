using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorGhostLookScare : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("กล้องของผู้เล่น (ไม่ตั้งจะดึง Camera.main อัตโนมัติ)")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("รากของผี (GameObject หลักของผี)")]
    [SerializeField] private GameObject ghostRoot;

    [Tooltip("ไฟทั้งหมดที่จะเปิด/ปิด ระหว่างเหตุการณ์")]
    [SerializeField] private List<Light> lightsToToggle = new List<Light>();

    [Header("=== Look Detection ===")]
    [Tooltip("ระยะสูงสุดที่ถือว่ายังมองเห็นผีได้")]
    [SerializeField] private float maxLookDistance = 20f;

    [Tooltip("ความแคบของมุมมอง (ค่า Dot ใกล้ 1 คือ ต้องมองตรงมาก)")]
    [Range(0.8f, 1f)]
    [SerializeField] private float viewDotThreshold = 0.97f;

    [Tooltip("LayerMask สำหรับ Raycast (ให้ใส่ Layer ของผีและสิ่งที่บังผี)")]
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("=== Timing ===")]
    [Tooltip("หน่วงเวลาก่อนดับไฟ (ตอนมองโดนผีแล้ว)")]
    [SerializeField] private float delayBeforeLightsOff = 0.1f;

    [Tooltip("ระยะเวลาที่ไฟดับก่อนกลับมาติดอีกครั้ง")]
    [SerializeField] private float lightOffDuration = 0.8f;

    [Header("=== Audio ===")]
    [Tooltip("AudioSource ที่จะใช้เล่นเสียง")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("เสียงตอนดับไฟ")]
    [SerializeField] private AudioClip powerOffClip;

    [Tooltip("เสียงตอนไฟกลับมาติด (ถ้าไม่มีก็ปล่อยว่างได้)")]
    [SerializeField] private AudioClip powerOnClip;

    [Header("=== Options ===")]
    [Tooltip("ให้เหตุการณ์นี้เล่นได้แค่ครั้งเดียวหรือไม่")]
    [SerializeField] private bool oneShot = true;

    private bool eventActivated = false;   // เริ่มเหตุการณ์แล้ว (ผู้อยู่ +รอให้ผู้เล่นมอง)
    private bool scareDone = false;        // ทำเหตุการณ์ไปแล้ว (กันไม่ให้ซ้ำ)
    private bool isRunningCoroutine = false;

    private void Awake()
    {
        // ดึงกล้องอัตโนมัติถ้าไม่ตั้ง
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // ดึง AudioSource จากตัวเอง ถ้าไม่ตั้ง
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // ผีเริ่มต้นเป็นปิดไว้ (ยังไม่เปิดประตู)
        if (ghostRoot != null)
        {
            ghostRoot.SetActive(false);
        }

        // ถ้าไม่ตั้ง LayerMask เลย ให้ยิงโดนทุกอย่าง
        if (raycastMask.value == 0)
        {
            raycastMask = ~0;
        }
    }

    /// <summary>
    /// เมื่อผู้เล่นเดินเข้าทริกเกอร์ที่หน้าประตู = ถือว่าเปิดประตูเจอผี
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (eventActivated) return;
        if (oneShot && scareDone) return;

        if (!other.CompareTag("Player")) return;

        eventActivated = true;

        // เปิดผีให้เห็นทันทีเมื่อเข้าทริกเกอร์ (เปิดประตู)
        if (ghostRoot != null)
        {
            ghostRoot.SetActive(true);
        }
    }

    private void Update()
    {
        // ยังไม่เริ่มเหตุการณ์ / หรือจบไปแล้ว = ไม่ต้องเช็กอะไร
        if (!eventActivated || scareDone || isRunningCoroutine)
            return;

        if (playerCamera == null || ghostRoot == null)
            return;

        // คำนวณเวคเตอร์จากกล้องไปหาผี
        Vector3 toGhost = ghostRoot.transform.position - playerCamera.transform.position;
        float distance = toGhost.magnitude;

        if (distance > maxLookDistance)
            return; // ผีอยู่ไกลเกินไป

        Vector3 dirToGhost = toGhost / distance;

        // เช็กว่ากล้องหันไปทางผีใกล้พอไหม (Dot ใกล้ 1 = หันตรง)
        float dot = Vector3.Dot(playerCamera.transform.forward, dirToGhost);
        if (dot < viewDotThreshold)
            return; // ยังมองไม่ตรงพอ

        // ยิง Raycast จากกล้องไปด้านหน้า เช็กว่าชนผีจริงไหม (มีของบังหรือเปล่า)
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxLookDistance, raycastMask, QueryTriggerInteraction.Ignore))
        {
            // ใช้ IsChildOf เผื่อโครงสร้างผีมีหลายชั้นใน Hierarchy
            if (hit.collider != null && ghostRoot != null && hit.collider.transform.IsChildOf(ghostRoot.transform))
            {
                // เริ่มเหตุการณ์หลอก
                StartCoroutine(DoGhostScareRoutine());
            }
        }
    }

    /// <summary>
    /// ดับไฟ → ผีหาย → เสียงดับไฟ → รอ → ไฟกลับมา → (เล่นได้ครั้งเดียวถ้า oneShot)
    /// </summary>
    private IEnumerator DoGhostScareRoutine()
    {
        isRunningCoroutine = true;

        // กันซ้ำ
        scareDone = true;
        eventActivated = false;

        // หน่วงนิดหน่อยก่อนดับไฟ (ทำให้รู้สึกว่ามองผีปุ๊บ ไฟค่อยวูบ)
        if (delayBeforeLightsOff > 0f)
            yield return new WaitForSeconds(delayBeforeLightsOff);

        // ดับไฟ
        SetLightsState(false);

        // เล่นเสียงดับไฟ
        if (audioSource != null && powerOffClip != null)
        {
            audioSource.PlayOneShot(powerOffClip);
        }

        // ทำให้ผีหายไป
        if (ghostRoot != null)
        {
            ghostRoot.SetActive(false);
        }

        // รอช่วงที่ไฟดับ
        if (lightOffDuration > 0f)
            yield return new WaitForSeconds(lightOffDuration);

        // เปิดไฟกลับมา
        SetLightsState(true);

        // เสียงไฟกลับมาติด (จะใส่ / ไม่ใส่ก็ได้)
        if (audioSource != null && powerOnClip != null)
        {
            audioSource.PlayOneShot(powerOnClip);
        }

        // ถ้าให้เล่นครั้งเดียว ปิดสคริปต์ไปเลย
        if (oneShot)
        {
            enabled = false;
        }

        isRunningCoroutine = false;
    }

    /// <summary>
    /// เปิด/ปิดไฟทั้งหมดที่กำหนดใน Inspector
    /// </summary>
    private void SetLightsState(bool state)
    {
        if (lightsToToggle == null) return;

        for (int i = 0; i < lightsToToggle.Count; i++)
        {
            if (lightsToToggle[i] == null) continue;
            lightsToToggle[i].enabled = state;
        }
    }
}
