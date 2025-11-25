using UnityEngine;
using System.Collections;

public class DoorGhostLookScare_Debug : MonoBehaviour
{
    [Header("Ghost Root")]
    public GameObject ghostRoot;

    [Header("Player Camera")]
    public Camera playerCam;

    [Header("Light to toggle")]
    public Light[] lightsToToggle;

    [Header("Distance / View")]
    public float maxDistance = 20f;
    [Range(0.5f, 1f)] public float dotThreshold = 0.95f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip powerOff;

    private bool entered = false;
    private bool looked = false;

    void Start()
    {
        if (playerCam == null) playerCam = Camera.main;

        if (ghostRoot != null)
        {
            ghostRoot.SetActive(false);
            Debug.Log("<color=yellow>[DEBUG]</color> ผีเริ่มต้นเป็น OFF");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        entered = true;

        ghostRoot.SetActive(true);
        Debug.Log("<color=green>[DEBUG]</color> ผู้เล่นเข้า Trigger -> เปิดผีแล้ว");
    }

    void Update()
    {
        if (!entered) return;  // ยังไม่เข้า Trigger = ไม่ทำงาน
        if (looked) return;    // ทำเหตุการณ์ไปแล้ว = ไม่ต้องเช็กต่อ

        if (ghostRoot == null)
        {
            Debug.Log("<color=red>[DEBUG ERROR]</color> ghostRoot = NULL");
            return;
        }

        // --- เช็กระยะ ---
        float dist = Vector3.Distance(playerCam.transform.position, ghostRoot.transform.position);
        Debug.Log("[DEBUG] ระยะจากกล้องถึงผี = " + dist);

        if (dist > maxDistance)
        {
            Debug.Log("[DEBUG] ผีอยู่ไกลเกินไป ยังไม่ทำงาน");
            return;
        }

        // --- เช็ก Dot (มุมมอง) ---
        Vector3 toGhost = (ghostRoot.transform.position - playerCam.transform.position).normalized;
        float dot = Vector3.Dot(playerCam.transform.forward, toGhost);

        Debug.Log("[DEBUG] ค่า DOT = " + dot);

        if (dot < dotThreshold)
        {
            Debug.Log("[DEBUG] กล้องยังไม่ได้มองตรงผีพอ");
            return;
        }

        // --- เช็ก Raycast ---
        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Debug.Log("[DEBUG] Raycast โดน: " + hit.collider.name);

            if (hit.collider.transform.IsChildOf(ghostRoot.transform))
            {
                Debug.Log("<color=cyan>[DEBUG]</color> Raycast มองโดนผีแล้ว! -> เริ่มหลอก");
                StartCoroutine(Scare());
                looked = true;
            }
            else
            {
                Debug.Log("[DEBUG] Raycast โดนอย่างอื่น ไม่ใช่ผี");
            }
        }
        else
        {
            Debug.Log("[DEBUG] Raycast ไม่โดนอะไรเลย");
        }
    }

    IEnumerator Scare()
    {
        // ดับไฟ
        foreach (var li in lightsToToggle)
        {
            if (li) li.enabled = false;
        }
        Debug.Log("<color=magenta>[DEBUG]</color> ดับไฟแล้ว");

        // เสียง
        if (audioSource && powerOff)
        {
            audioSource.PlayOneShot(powerOff);
            Debug.Log("<color=magenta>[DEBUG]</color> เล่นเสียงไฟดับ");
        }

        // ผีหาย
        ghostRoot.SetActive(false);
        Debug.Log("<color=magenta>[DEBUG]</color> ผีหาย");

        yield return new WaitForSeconds(1f);

        // ไฟเปิดกลับ
        foreach (var li in lightsToToggle)
        {
            if (li) li.enabled = true;
        }
        Debug.Log("<color=magenta>[DEBUG]</color> เปิดไฟกลับ");

        yield break;
    }
}
