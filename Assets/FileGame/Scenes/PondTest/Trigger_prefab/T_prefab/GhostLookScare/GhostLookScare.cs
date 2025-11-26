using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flow:
/// 1) Player เดินเข้าทริกเกอร์ = เปิดประตู เห็นผี
/// 2) เมื่อผู้เล่นหันกล้องไปมองผี (เมาส์หมุนไปโดนผี)
/// 3) ดับไฟทั้งหมด
/// 4) ผีหายไป
/// 5) ไฟกลับมาติด
/// </summary>
[RequireComponent(typeof(Collider))]
public class GhostLookScare : MonoBehaviour
{
    [Header("=== Player / Camera ===")]
    [Tooltip("กล้องของผู้เล่น (ถ้าไม่เซ็ต จะใช้ Camera.main)")]
    [SerializeField] private Camera playerCamera;

    [Header("=== Ghost ===")]
    [Tooltip("รากของผีตัวนี้ (GameObject หลักที่เปิด/ปิดตอนหลอก)")]
    [SerializeField] private GameObject ghostRoot;

    [Tooltip("เริ่มเกมให้ซ่อนผีก่อนหรือไม่")]
    [SerializeField] private bool hideGhostOnStart = true;

    [Header("=== Lights ===")]
    [Tooltip("ไฟทั้งหมดที่ต้องการให้ดับ/ติดระหว่างเหตุการณ์")]
    [SerializeField] private List<Light> lightsToToggle = new List<Light>();

    [Header("=== Look Detection ===")]
    [Tooltip("ระยะสูงสุดที่นับว่ายังมองผีได้ (เมตร)")]
    [SerializeField] private float maxLookDistance = 20f;

    [Tooltip("มุมองศาสูงสุดที่ถือว่า \"มองตรง\" ไปหาผีแล้ว (ยิ่งน้อย = ต้องเล็งตรงมาก)")]
    [Range(1f, 60f)]
    [SerializeField] private float lookAngleThreshold = 10f;

    [Tooltip("ต้องใช้ Raycast ตรวจโดน Collider ของผีด้วยหรือไม่ (แนะนำให้ผีมี Collider)")]
    [SerializeField] private bool useRaycastHitCheck = true;

    [Tooltip("LayerMask สำหรับ Raycast (ใช้เฉพาะตอน useRaycastHitCheck = true)")]
    [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;

    [Header("=== Timing ===")]
    [Tooltip("ดีเลย์เล็กน้อยหลังจากมองผี ก่อนดับไฟ")]
    [SerializeField] private float delayBeforeLightsOff = 0.05f;

    [Tooltip("ระยะเวลาที่ไฟดับ ก่อนที่จะกลับมาติดใหม่")]
    [SerializeField] private float lightOffDuration = 0.6f;

    [Header("=== Audio (ถ้ามี) ===")]
    [Tooltip("AudioSource สำหรับเล่นเสียงไฟดับ/ไฟติด (ถ้าไม่เซ็ต จะดึงจากตัวเอง)")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("เสียงตอนดับไฟ (Optional)")]
    [SerializeField] private AudioClip powerOffClip;

    [Tooltip("เสียงตอนไฟกลับมาติด (Optional)")]
    [SerializeField] private AudioClip powerOnClip;

    [Header("=== Options ===")]
    [Tooltip("ให้เหตุการณ์นี้เล่นได้ครั้งเดียวเท่านั้น")]
    [SerializeField] private bool oneShot = true;

    // internal state
    private bool triggerActivated = false; // เดินเข้าทริกเกอร์แล้ว (ประตูเปิดแล้ว)
    private bool scareDone = false;       // เหตุการณ์นี้จบไปแล้ว
    private bool isRunning = false;       // ป้องกันไม่ให้ Coroutine ซ้อนกัน

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (hideGhostOnStart && ghostRoot != null)
        {
            ghostRoot.SetActive(false);
        }

        if (useRaycastHitCheck && raycastMask.value == 0)
        {
            // ถ้าไม่ตั้ง mask เลย ให้ใช้ default layers
            raycastMask = Physics.DefaultRaycastLayers;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // ถ้า oneShot และเคยเล่นไปแล้ว → ไม่ต้องทำอีก
        if (oneShot && scareDone)
            return;

        // ถ้าเคย Activated ไปแล้ว (กำลังรอให้มองผี) → ไม่ต้องเริ่มซ้ำ
        if (triggerActivated)
            return;

        triggerActivated = true;

        // เปิดผีให้เห็นเมื่อประตูเปิด (เดินเข้าทริกเกอร์)
        if (ghostRoot != null)
        {
            ghostRoot.SetActive(true);
        }
        else
        {
            Debug.LogWarning("GhostLookScareSimple: ghostRoot ยังไม่ได้เซ็ต");
        }

        // เผื่อ playerCamera ยังไม่ถูก Assign พยายามหาอีกครั้งจาก Player
        if (playerCamera == null)
        {
            Camera cam = other.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCamera = cam;
            }
        }

        // Debug.Log("GhostLookScareSimple: Trigger entered, ghost appears.");
    }

    private void Update()
    {
        // ยังไม่เดินเข้าทริกเกอร์ / เคยเล่นจบแล้ว / กำลังรัน Coroutine → ไม่ทำอะไร
        if (!triggerActivated || scareDone || isRunning)
            return;

        if (ghostRoot == null || playerCamera == null)
            return;

        // ถ้าผีโดนปิดไปแล้ว (ด้วยเหตุผลอื่น) ก็ไม่ต้องเช็กต่อ
        if (!ghostRoot.activeInHierarchy)
            return;

        // ตรวจว่า "มองไปทางผี" หรือยัง

        // 1) ระยะ
        Vector3 toGhost = ghostRoot.transform.position - playerCamera.transform.position;
        float distance = toGhost.magnitude;
        if (distance > maxLookDistance)
            return;

        // 2) มุมมอง
        Vector3 dirToGhost = toGhost / Mathf.Max(distance, 0.0001f);
        float angle = Vector3.Angle(playerCamera.transform.forward, dirToGhost);
        if (angle > lookAngleThreshold)
            return;

        // 3) Raycast (ถ้าเลือกใช้)
        if (useRaycastHitCheck)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxLookDistance, raycastMask, QueryTriggerInteraction.Ignore))
            {
                // ถ้าชนผีจริง ๆ (Collider ของผีหรือ child ของมัน)
                if (hit.collider != null && ghostRoot != null && hit.collider.transform.IsChildOf(ghostRoot.transform))
                {
                    StartCoroutine(DoScareRoutine());
                }
                else
                {
                    // Ray ไม่ชนผี = อาจมีของบังอยู่ เช่น กำแพง / ท่อ
                    return;
                }
            }
            else
            {
                // ไม่โดนอะไรเลย
                return;
            }
        }
        else
        {
            // ไม่ใช้ Raycast → แค่เช็กมุมมองพอ
            StartCoroutine(DoScareRoutine());
        }
    }

    /// <summary>
    /// ดับไฟ → ผีหาย → รอ → ไฟกลับมาติด → (ปิดทริกเกอร์ถ้า oneShot)
    /// </summary>
    private IEnumerator DoScareRoutine()
    {
        if (isRunning) yield break;

        isRunning = true;
        scareDone = true;

        // หน่วงเล็กน้อยก่อนดับไฟ (กันอาการกระตุก)
        if (delayBeforeLightsOff > 0f)
            yield return new WaitForSeconds(delayBeforeLightsOff);

        // ดับไฟทั้งหมด
        SetLightsState(false);

        // เสียงไฟดับ
        if (audioSource != null && powerOffClip != null)
        {
            audioSource.PlayOneShot(powerOffClip);
        }

        // ผีหายไป
        if (ghostRoot != null)
        {
            ghostRoot.SetActive(false);
        }

        // รอช่วงที่ไฟดับ
        if (lightOffDuration > 0f)
            yield return new WaitForSeconds(lightOffDuration);

        // ไฟกลับมาติด
        SetLightsState(true);

        // เสียงไฟกลับมาติด
        if (audioSource != null && powerOnClip != null)
        {
            audioSource.PlayOneShot(powerOnClip);
        }

        // เล่นรอบเดียวแล้วจบ
        if (oneShot)
        {
            if (triggerCollider != null)
                triggerCollider.enabled = false;

            enabled = false;
        }

        isRunning = false;
    }

    /// <summary>
    /// เปิด/ปิดไฟทั้งหมดตาม state
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
