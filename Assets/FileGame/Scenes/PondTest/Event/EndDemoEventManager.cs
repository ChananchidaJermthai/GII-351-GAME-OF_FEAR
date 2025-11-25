using System.Collections;
using UnityEngine;

public class EndDemoEventManager : MonoBehaviour
{
    [Header("Player")]
    public PlayerController3D player;      
    public Camera playerCamera;            

    [Header("Ghost Roots")]
    [Tooltip("ตัว GameObject หลักของผีตัวแรก (ห้อยหัว)")]
    public GameObject firstGhostRoot;

    [Tooltip("ตัว GameObject หลักของผีตัวที่สอง (ด้านหลังผู้เล่น)")]
    public GameObject secondGhostRoot;

    [Header("Ghost Anim / Look")]
    public Animator firstGhostAnimator;    
    public string firstGhostPeekTrigger = "Peek";
    public Transform firstGhostLookTarget; 

    [Tooltip("จุดหน้า / face root ของผีตัวที่สอง สำหรับหันกล้อง + ซูม")]
    public Transform secondGhostFaceRoot;

    [Header("Sounds")]
    public AudioSource behindSfx1;         
    public AudioSource behindSfx2;        
    public AudioSource finalStingSfx;      
    public AudioSource ambientToStop;      

    [Header("Camera Motion")]
    public float firstTurnSpeed = 2f;      
    public float firstHoldTime = 1.2f;     
    public float secondTurnSpeed = 10f;    
    public float zoomFOV = 30f;            
    public float zoomDuration = 0.4f;      
    public float zoomHoldTime = 1.0f;      

    [Header("UI End Demo")]
    public GameObject endDemoUI;
    public bool pauseGameOnEnd = true;

    [Header("Options")]
    public bool useOnce = true;
    public bool hideGhostsOnStart = true; 

    bool used = false;
    float originalFOV;

    private void Awake()
    {
        
        if (hideGhostsOnStart)
        {
            if (firstGhostRoot != null)
                firstGhostRoot.SetActive(false);

            if (secondGhostRoot != null)
                secondGhostRoot.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (used && useOnce) return;

        used = true;

        if (!player)
            player = other.GetComponentInParent<PlayerController3D>();

        if (!player)
        {
            Debug.LogWarning("EndDemoEventManager: ไม่เจอ PlayerController3D บน Player");
            return;
        }

        if (!playerCamera)
            playerCamera = player.playerCamera;

        if (!playerCamera)
        {
            Debug.LogWarning("EndDemoEventManager: ไม่มี Camera ให้ใช้งาน");
            return;
        }

        originalFOV = playerCamera.fieldOfView;

        StartCoroutine(EndDemoSequence());
    }

    private IEnumerator EndDemoSequence()
    {
        // 1) ล็อกการควบคุม
        player.LockControl(true);

        // หยุด ambience ถ้ามี
        if (ambientToStop) ambientToStop.Stop();

        // ✅ เปิดผีตัวแรก (ห้อยหัว) ตอนจะใช้
        if (firstGhostRoot != null)
            firstGhostRoot.SetActive(true);

        // 2) เล่นเสียงขู่จากด้านหลังครั้งแรก
        if (behindSfx1)
            behindSfx1.Play();

        if (behindSfx1)
            yield return new WaitForSeconds(behindSfx1.clip.length * 0.6f);
        else
            yield return new WaitForSeconds(0.8f);

        

        // 3) บังคับหันกล้องช้าๆ ไปหาผีตัวแรก + เล่นอนิเมชัน peek
        if (firstGhostAnimator && !string.IsNullOrEmpty(firstGhostPeekTrigger))
            firstGhostAnimator.SetTrigger(firstGhostPeekTrigger);

        if (firstGhostLookTarget)
        {
            player.StartLookFollow(firstGhostLookTarget, firstTurnSpeed, false);
            yield return new WaitForSeconds(firstHoldTime);
            player.StopLookFollow(false);
        }
        else
        {
            yield return new WaitForSeconds(firstHoldTime);
        }
        // ✅ เปิดผีตัวที่สอง ตอนจะหันไปหา
        if (secondGhostRoot != null)
            secondGhostRoot.SetActive(true);

        // 4) เล่นเสียงจากด้านหลังอีกครั้ง
        if (behindSfx2)
            behindSfx2.Play();

        yield return new WaitForSeconds(0.3f);

        

        // 5) หันกล้องกลับไปหา ผีตัวที่สอง แบบไวๆ
        if (secondGhostFaceRoot)
        {
            player.StartLookFollow(secondGhostFaceRoot, secondTurnSpeed, false);
            yield return new WaitForSeconds(0.4f);
            player.StopLookFollow(false);
        }

        // 6) ซูมกล้อง + เสียงตุ้งแช่
        if (finalStingSfx)
            finalStingSfx.Play();

        float t = 0f;
        float startFOV = playerCamera.fieldOfView;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / zoomDuration);
            playerCamera.fieldOfView = Mathf.Lerp(startFOV, zoomFOV, k);
            yield return null;
        }

        yield return new WaitForSeconds(zoomHoldTime);

        // 7) ตัดกล้อง + เปิด End Demo UI
        playerCamera.fieldOfView = zoomFOV;

        if (endDemoUI)
            endDemoUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 8) Fade Out เสียงทั้งหมดก่อน Freeze
        yield return StartCoroutine(FadeOutAllAudio(0.4f)); 

        if (pauseGameOnEnd)
            Time.timeScale = 0f;

    }
    private IEnumerator FadeOutAllAudio(float duration)
    {
        AudioSource[] audios = FindObjectsOfType<AudioSource>();

        // เก็บค่า volume เดิม
        float[] originalVolumes = new float[audios.Length];
        for (int i = 0; i < audios.Length; i++)
            originalVolumes[i] = audios[i].volume;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - (t / duration);

            for (int i = 0; i < audios.Length; i++)
            {
                if (audios[i] != null)
                    audios[i].volume = originalVolumes[i] * k;
            }

            yield return null;
        }

        
        for (int i = 0; i < audios.Length; i++)
        {
            if (audios[i] != null)
                audios[i].volume = 0f;
        }
    }

}
