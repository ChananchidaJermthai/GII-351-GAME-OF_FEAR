using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class CamFaint : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;
    [Min(0.01f)] public float moveSpeed = 3.5f;
    [Min(0.01f)] public float arriveThreshold = 0.1f;
    public bool playOnStart = true;

    [Header("Look")]
    public bool lookAtNextPoint = true;
    public float lookRotateSpeed = 10f;

    [Header("SFX - Footstep (loop while moving)")]
    public AudioSource footstepSource;     // แนะนำให้เป็น AudioSource แยก
    public AudioClip footstepLoop;       // คลิปลูปฝีเท้า (2D/ambience ก็ได้)
    [Range(0f, 1f)] public float footstepVolume = 0.7f;
    [Tooltip("ค่อย ๆ เฟดเข้า/ออกป้องกันคลิกเสียง")]
    [Range(0f, 0.3f)] public float footstepFade = 0.08f;

    [Header("SFX - On Hit (at last point)")]
    public AudioSource sfxSource;          // ใช้เล่นเสียงโดนทุบ (OneShot)
    public AudioClip hitSfx;
    public float delayAfterHit = 0.1f;

    [Header("Faint Effect (Flicker+Shake)")]
    public float faintEffectDuration = 1.2f;
    public float flickerFrequency = 9f;
    [Range(0f, 1f)] public float maxFlickerAlpha = 0.8f;
    public float shakeDuration = 1.2f;
    public float shakePosIntensity = 0.05f;
    public float shakeRotIntensity = 1.5f;

    [Header("Blackout & Load Scene")]
    public float blackoutDuration = 2f;
    public string sceneNameToLoad = "";
    public int sceneIndexToLoad = -1;

    [Header("UI Overlay (auto)")]
    public Image blackOverlayImage;

    // runtime
    Vector3 _basePos;
    Quaternion _baseRot;
    bool _sequenceRunning;
    Canvas _overlayCanvas;

    float _footVolVel = 0f;  // smoothDamp vol

    void Start()
    {
        EnsureAudio();
        EnsureBlackOverlay();
        if (playOnStart) PlaySequence();
    }

    void EnsureAudio()
    {
        // Footstep source
        if (!footstepSource)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
        }
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.spatialBlend = 0f;           // 2D (ปรับเป็น 1f ถ้าอยาก 3D)
        footstepSource.volume = 0f;
        if (footstepLoop && footstepSource.clip != footstepLoop)
            footstepSource.clip = footstepLoop;

        // Hit SFX source
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;               // 2D
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        if (_sequenceRunning) return;
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogError("[CamFaint] ไม่มี Waypoints");
            return;
        }
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        _sequenceRunning = true;

        // เริ่มลูปฝีเท้า (ยังคง volume = 0 จะเฟดขึ้นเฉพาะตอนเคลื่อน)
        if (footstepLoop && !footstepSource.isPlaying)
            footstepSource.Play();

        for (int i = 0; i < waypoints.Length; i++)
        {
            var target = waypoints[i];
            if (!target) continue;

            while (true)
            {
                // ระยะ/ทิศทาง
                Vector3 to = target.position - transform.position;
                float dist = to.magnitude;

                // มองทางเดิน (optional)
                if (lookAtNextPoint && dist > 0.0001f)
                {
                    var lookRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * lookRotateSpeed);
                }

                // เฟดเสียงฝีเท้า: เคลื่อนที่อยู่ = เฟดเข้าตาม footstepVolume
                float targetVol = dist > arriveThreshold ? footstepVolume : 0f;
                if (footstepSource)
                {
                    if (footstepFade > 0f)
                        footstepSource.volume = Mathf.SmoothDamp(footstepSource.volume, targetVol, ref _footVolVel, footstepFade);
                    else
                        footstepSource.volume = targetVol;

                    // ถ้าลงถึง 0 และกำลังเล่นอยู่ ให้หยุดจริงเพื่อตัดภาระ (แต่จะสั่ง Play ใหม่อัตโนมัติขยับครั้งต่อไป)
                    if (footstepSource.volume <= 0.001f && footstepSource.isPlaying && targetVol <= 0f)
                        footstepSource.Stop();
                    if (footstepLoop && !footstepSource.isPlaying && targetVol > 0f)
                    {
                        footstepSource.clip = footstepLoop;
                        footstepSource.Play();
                    }
                }

                // เคลื่อนเข้าเป้าหมาย
                if (dist <= arriveThreshold) break;
                Vector3 step = to.normalized * moveSpeed * Time.deltaTime;
                if (step.magnitude > dist) step = to;
                transform.position += step;

                yield return null;
            }
        }

        // ถึงจุดสุดท้ายแล้ว → ปิด/เฟดเสียงฝีเท้า
        if (footstepSource)
        {
            if (footstepFade > 0f)
            {
                float t = 0f;
                float start = footstepSource.volume;
                while (t < footstepFade)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / footstepFade);
                    footstepSource.volume = Mathf.Lerp(start, 0f, k);
                    yield return null;
                }
            }
            footstepSource.Stop();
            footstepSource.volume = 0f;
        }

        // เล่นเสียงโดนทุบ
        if (hitSfx && sfxSource) sfxSource.PlayOneShot(hitSfx);
        if (delayAfterHit > 0f) yield return new WaitForSeconds(delayAfterHit);

        // เอฟเฟกต์หน้ามืด
        yield return StartCoroutine(Co_FaintEffect());

        // ดำค้างและโหลดซีน
        yield return StartCoroutine(Co_BlackoutThenLoad());
    }

    IEnumerator Co_FaintEffect()
    {
        float t = 0f;
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;

        SetOverlayAlpha(0f);

        while (t < faintEffectDuration)
        {
            t += Time.deltaTime;

            // Flicker
            float phase = t * flickerFrequency * Mathf.PI * 2f;
            float a = Mathf.Abs(Mathf.Sin(phase));
            float alpha = Mathf.Lerp(0f, maxFlickerAlpha, a);
            SetOverlayAlpha(alpha);

            // Shake (ค่อย ๆ ลดแรง)
            float nt = Mathf.Clamp01(t / shakeDuration);
            float falloff = 1f - nt;
            Vector3 randPos = new Vector3(
                (Random.value * 2f - 1f) * shakePosIntensity * falloff,
                (Random.value * 2f - 1f) * shakePosIntensity * falloff,
                (Random.value * 2f - 1f) * shakePosIntensity * falloff
            );
            Vector3 randRotEuler = new Vector3(
                (Random.value * 2f - 1f) * shakeRotIntensity * falloff,
                (Random.value * 2f - 1f) * shakeRotIntensity * falloff,
                (Random.value * 2f - 1f) * shakeRotIntensity * falloff
            );

            transform.localPosition = _basePos + randPos;
            transform.localRotation = _baseRot * Quaternion.Euler(randRotEuler);

            yield return null;
        }

        // รีเซ็ต และเฟดเข้าสู่ดำสนิท
        transform.localPosition = _basePos;
        transform.localRotation = _baseRot;

        float fade = Mathf.Min(0.25f, 0.35f);
        float ft = 0f;
        while (ft < fade)
        {
            ft += Time.deltaTime;
            float k = Mathf.Clamp01(ft / fade);
            SetOverlayAlpha(Mathf.Lerp(0f, 1f, k));
            yield return null;
        }
        SetOverlayAlpha(1f);
    }

    IEnumerator Co_BlackoutThenLoad()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, blackoutDuration));

        if (!string.IsNullOrEmpty(sceneNameToLoad))
            SceneManager.LoadScene(sceneNameToLoad);
        else if (sceneIndexToLoad >= 0)
            SceneManager.LoadScene(sceneIndexToLoad);
        else
            Debug.LogWarning("[CamFaint] ไม่ได้ระบุซีนที่จะโหลด");
    }

    // ===== Overlay =====
    void EnsureBlackOverlay()
    {
        if (blackOverlayImage != null) { SetOverlayAlpha(0f); return; }

        var canvasGO = new GameObject("FaintOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        DontDestroyOnLoad(canvasGO);

        var imgGO = new GameObject("BlackOverlay", typeof(Image));
        imgGO.transform.SetParent(canvasGO.transform, false);
        blackOverlayImage = imgGO.GetComponent<Image>();
        blackOverlayImage.color = new Color(0f, 0f, 0f, 0f);

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        SetOverlayAlpha(0f);
    }

    void SetOverlayAlpha(float a)
    {
        if (!blackOverlayImage) return;
        var c = blackOverlayImage.color;
        c.a = Mathf.Clamp01(a);
        blackOverlayImage.color = c;
    }

    // ===== Gizmos =====
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        for (int i = 0; i < waypoints.Length; i++)
        {
            var t = waypoints[i];
            if (!t) continue;
            Gizmos.DrawSphere(t.position, 0.08f);
            if (i + 1 < waypoints.Length && waypoints[i + 1])
                Gizmos.DrawLine(t.position, waypoints[i + 1].position);
        }
    }
}
