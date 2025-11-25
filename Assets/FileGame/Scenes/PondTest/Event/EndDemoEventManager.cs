using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndDemoEventManager : MonoBehaviour
{
    [Header("Player / Camera")]
    [Tooltip("PlayerController3D ของผู้เล่น (ถ้าไม่เซ็ต จะพยายามหา Tag = Player)")]
    public PlayerController3D player;
    [Tooltip("กล้องหลักของผู้เล่น")]
    public Camera playerCamera;

    [Header("Ghosts")]
    [Tooltip("ผีด้านหลัง (มีตัว / Animator สำหรับ Peek)")]
    public GameObject backGhostRoot;
    [Tooltip("Animator ของผีด้านหลังสำหรับ Peek")]
    public Animator backGhostAnimator;
    public string backGhostPeekTrigger = "Peek";

    [Tooltip("ผีตัวหน้า (ผี 1 ที่จะเห็นตอนหันกลับมาข้างหน้า)")]
    public GameObject frontGhostRoot;

    [Header("Look Targets")]
    [Tooltip("ตำแหน่งด้านหลังที่อยากให้ผู้เล่นหันไปหา (เช่น head ผีด้านหลัง)")]
    public Transform backLookTarget;
    [Tooltip("ตำแหน่งด้านหน้าที่ผู้เล่นต้องหันกลับมามองเพื่อเห็นผี 1")]
    public Transform frontLookTarget;

    [Header("Sounds")]
    [Tooltip("เสียงหายใจด้านหลัง (AudioSource ตัวไหนก็ได้ วางไว้ด้านหลังผู้เล่น)")]
    public AudioSource breathBehindSfx;
    [Tooltip("Ambience หลักที่ต้องหยุดเมื่อเริ่มเหตุการณ์")]
    public AudioSource ambientSfx;

    [Header("Look Detection")]
    [Tooltip("มุมองศาที่ถือว่า \"มองตรง\" ไปยังเป้าหมายแล้ว")]
    [Range(1f, 60f)]
    public float lookAngleThreshold = 20f;
    [Tooltip("เวลารอสูงสุดให้ผู้เล่นหันไปมอง (กันเคสดื้อไม่หัน)")]
    public float maxWaitForLook = 6f;
    [Tooltip("ดีเลย์หลังเห็นผี / หลัง Peek ก่อน step ถัดไป")]
    public float holdAfterSeen = 0.4f;

    [Header("UI / End Flow")]
    [Tooltip("Canvas/Panel สำหรับจบเดโม")]
    public GameObject endDemoUI;
    public bool freezeOnEnd = true;
    [Tooltip("ชื่อ Scene ที่จะโหลดหลังจบเดโม")]
    public string endCreditsSceneName = "EndCredits";
    [Tooltip("เวลาหน่วงก่อนโหลดฉาก (วินาที, ใช้ unscaled time)")]
    public float waitBeforeLoad = 4f;

    [Header("Options")]
    [Tooltip("ซ่อนผีทั้งหมดตอนเริ่มเกม")]
    public bool hideGhostsOnStart = true;
    [Tooltip("ให้ทำเหตุการณ์ได้ครั้งเดียวเท่านั้น")]
    public bool useOnce = true;

    private bool used = false;

    private void Awake()
    {
        // ซ่อนผีทุกตัวก่อนเริ่ม (เพื่อให้กด Play แล้วกล้องปกติแน่นอน)
        if (hideGhostsOnStart)
        {
            if (backGhostRoot != null) backGhostRoot.SetActive(false);
            if (frontGhostRoot != null) frontGhostRoot.SetActive(false);
        }
    }

    private void Start()
    {
        // พยายามหา Player / Camera อัตโนมัติ ถ้าไม่เซ็ต
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.GetComponentInChildren<PlayerController3D>();
        }

        if (playerCamera == null && player != null)
        {
            playerCamera = player.playerCamera;
        }

        if (playerCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null) playerCamera = cam;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Trigger นี้ให้ทำงานเฉพาะตอน Player ชน
        if (!other.CompareTag("Player")) return;
        if (used && useOnce) return;

        used = true;
        Debug.Log("EndDemoEventManager: Player entered trigger, start ending flow.");

        // ถ้ายังไม่มี playerCamera ลองอีกรอบ
        if (playerCamera == null)
        {
            var p = other.GetComponentInParent<PlayerController3D>();
            if (p != null) playerCamera = p.playerCamera;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning("EndDemoEventManager: ไม่มี playerCamera, แต่จะลองรันต่อไป");
        }

        StartCoroutine(EndingSequence());
    }

    private IEnumerator EndingSequence()
    {
        // 1) หยุด Ambience
        if (ambientSfx != null)
        {
            ambientSfx.Stop();
        }

        // 2) เสียงหายใจด้านหลัง + เปิดผีด้านหลัง
        if (backGhostRoot != null)
            backGhostRoot.SetActive(true);

        if (breathBehindSfx != null)
        {
            breathBehindSfx.Play();
        }

        // 3) รอให้ผู้เล่น "หันไปด้านหลังเอง"
        yield return StartCoroutine(WaitUntilLookAt(backLookTarget, "ด้านหลัง"));

        // 4) ผู้เล่นหันไปหาแล้ว → Peek Animation + ปิดเสียงหายใจ
        if (backGhostAnimator != null && !string.IsNullOrEmpty(backGhostPeekTrigger))
        {
            backGhostAnimator.SetTrigger(backGhostPeekTrigger);
        }

        if (breathBehindSfx != null && breathBehindSfx.isPlaying)
        {
            breathBehindSfx.Stop();
        }

        // ให้มีจังหวะเห็น Peek แป๊บหนึ่ง
        yield return new WaitForSeconds(holdAfterSeen);

        // 5) ไม่เจออะไร → ซ่อนผีหลัง
        if (backGhostRoot != null)
            backGhostRoot.SetActive(false);

        // 6) เปิดผี 1 ข้างหน้า
        if (frontGhostRoot != null)
            frontGhostRoot.SetActive(true);

        // 7) รอจนผู้เล่นหันกลับมาหน้าตรง เห็นผี 1
        yield return StartCoroutine(WaitUntilLookAt(frontLookTarget, "ข้างหน้า"));

        // ให้เห็นผีหน้าเต็ม ๆ แป๊บนึง
        yield return new WaitForSeconds(holdAfterSeen);

        // 8) ผีหาย → เรียก JumpScareSpawnInFront
        if (frontGhostRoot != null)
            frontGhostRoot.SetActive(false);

        if (JumpScareSpawnInFront.Instance != null)
        {
            JumpScareSpawnInFront.Instance.PlayScare();
        }
        else
        {
            Debug.LogWarning("EndDemoEventManager: JumpScareSpawnInFront.Instance เป็น null");
        }

        // 🔒 ล็อกจอ / ล็อกการควบคุมหลังโดนจั้มสแกร์
        if (player != null)
        {
            player.LockControl(true);
        }

        // 9) ขึ้น EndDemo UI + Freeze เกม
        if (endDemoUI != null)
            endDemoUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (freezeOnEnd)
            Time.timeScale = 0f;

        // 10) Fade out เสียงทั้งหมด
        yield return StartCoroutine(FadeOutAllAudio(0.4f));

        if (freezeOnEnd)
            Time.timeScale = 0f;

        // 11) หน่วงก่อนโหลด EndCredits
        yield return new WaitForSecondsRealtime(waitBeforeLoad);

        // ปลด freeze ก่อนโหลด scene ใหม่
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(endCreditsSceneName))
        {
            SceneManager.LoadScene(endCreditsSceneName);
        }
        else
        {
            Debug.LogWarning("EndDemoEventManager: ยังไม่ได้ตั้งชื่อ scene สำหรับ EndCredits");
        }
    }

    /// <summary>
    /// รอจนกว่ากล้องจะหันไปมอง target ภายในมุมที่กำหนด หรือหมดเวลา
    /// </summary>
    private IEnumerator WaitUntilLookAt(Transform target, string label)
    {
        if (playerCamera == null || target == null)
        {
            // ถ้าไม่มี target หรือกล้อง – อย่าให้ค้าง, แค่รอเวลาแล้วผ่านไป
            Debug.LogWarning($"EndDemoEventManager: WaitUntilLookAt ไม่มี {(playerCamera == null ? "Camera" : "Target")} ({label})");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        float timer = 0f;
        bool looked = false;

        while (!looked && timer < maxWaitForLook)
        {
            Vector3 toTarget = target.position - playerCamera.transform.position;
            float angle = Vector3.Angle(playerCamera.transform.forward, toTarget);

            if (angle <= lookAngleThreshold)
            {
                looked = true;
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // ถ้าผู้เล่นไม่หันภายในเวลา maxWaitForLook ก็ปล่อยผ่าน ไม่ให้ sequence พัง
    }

    private IEnumerator FadeOutAllAudio(float duration)
    {
        AudioSource[] audios = FindObjectsOfType<AudioSource>();
        if (audios.Length == 0 || duration <= 0f)
            yield break;

        float[] startVolumes = new float[audios.Length];
        for (int i = 0; i < audios.Length; i++)
        {
            if (audios[i] != null)
                startVolumes[i] = audios[i].volume;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);

            for (int i = 0; i < audios.Length; i++)
            {
                if (audios[i] != null)
                    audios[i].volume = startVolumes[i] * k;
            }

            yield return null;
        }

        // ให้เงียบสนิท
        for (int i = 0; i < audios.Length; i++)
        {
            if (audios[i] != null)
                audios[i].volume = 0f;
        }
    }
}
