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
    public AudioSource footstepSource;
    public AudioClip footstepLoop;
    [Range(0f, 1f)] public float footstepVolume = 0.7f;
    [Range(0f, 0.3f)] public float footstepFade = 0.08f;

    [Header("SFX - On Hit (at last point)")]
    public AudioSource sfxSource;
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

    [Header("Overlay Lifetime")]
    [Tooltip("ถ้าเปิด จะคง Overlay ข้ามซีน (ส่วนใหญ่ไม่ต้องเปิด)")]
    public bool keepOverlayAcrossScenes = false;
    [Tooltip("ถ้า keepOverlayAcrossScenes=true ให้ลบ Overlay หลังโหลดซีนไหม")]
    public bool destroyOverlayAfterLoad = true;

    // runtime
    Vector3 _basePos;
    Quaternion _baseRot;
    bool _sequenceRunning;
    Canvas _overlayCanvas;
    GameObject _overlayRoot;
    float _footVolVel = 0f;

    void Start()
    {
        EnsureAudio();
        EnsureBlackOverlay();
        if (playOnStart) PlaySequence();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void EnsureAudio()
    {
        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.spatialBlend = 0f;
        footstepSource.volume = 0f;
        if (footstepLoop && footstepSource.clip != footstepLoop) footstepSource.clip = footstepLoop;

        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
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

        if (footstepLoop && !footstepSource.isPlaying) footstepSource.Play();

        for (int i = 0; i < waypoints.Length; i++)
        {
            var target = waypoints[i];
            if (!target) continue;

            while (true)
            {
                Vector3 to = target.position - transform.position;
                float dist = to.magnitude;

                if (lookAtNextPoint && dist > 0.0001f)
                {
                    var lookRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * lookRotateSpeed);
                }

                float targetVol = dist > arriveThreshold ? footstepVolume : 0f;
                if (footstepSource)
                {
                    if (footstepFade > 0f)
                        footstepSource.volume = Mathf.SmoothDamp(footstepSource.volume, targetVol, ref _footVolVel, footstepFade);
                    else
                        footstepSource.volume = targetVol;

                    if (footstepSource.volume <= 0.001f && footstepSource.isPlaying && targetVol <= 0f)
                        footstepSource.Stop();
                    if (footstepLoop && !footstepSource.isPlaying && targetVol > 0f)
                    {
                        footstepSource.clip = footstepLoop;
                        footstepSource.Play();
                    }
                }

                if (dist <= arriveThreshold) break;
                Vector3 step = to.normalized * moveSpeed * Time.deltaTime;
                if (step.magnitude > dist) step = to;
                transform.position += step;

                yield return null;
            }
        }

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

        if (hitSfx && sfxSource) sfxSource.PlayOneShot(hitSfx);
        if (delayAfterHit > 0f) yield return new WaitForSeconds(delayAfterHit);

        yield return StartCoroutine(Co_FaintEffect());
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

            float phase = t * flickerFrequency * Mathf.PI * 2f;
            float a = Mathf.Abs(Mathf.Sin(phase));
            float alpha = Mathf.Lerp(0f, maxFlickerAlpha, a);
            SetOverlayAlpha(alpha);

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

        // ถ้าไม่ต้องคง overlay ข้ามซีน → ปล่อยให้ถูกทำลายพร้อมซีน (ค่าเริ่มต้น)
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

        _overlayRoot = new GameObject("FaintOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _overlayCanvas = _overlayRoot.GetComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // **เปลี่ยนพฤติกรรมเริ่มต้น:** ไม่ DontDestroyOnLoad เพื่อให้ซีนใหม่ “ไม่มีจอดำค้าง”
        if (keepOverlayAcrossScenes)
            DontDestroyOnLoad(_overlayRoot);

        var imgGO = new GameObject("BlackOverlay", typeof(Image));
        imgGO.transform.SetParent(_overlayRoot.transform, false);
        blackOverlayImage = imgGO.GetComponent<Image>();
        blackOverlayImage.color = new Color(0f, 0f, 0f, 0f);

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        SetOverlayAlpha(0f);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // เผื่อกรณี dev เปิด keepOverlayAcrossScenes เอาไว้:
        if (keepOverlayAcrossScenes && destroyOverlayAfterLoad && _overlayRoot)
        {
            Destroy(_overlayRoot);
            _overlayRoot = null;
            blackOverlayImage = null;
            _overlayCanvas = null;
        }
        else
        {
            // ไม่คง overlay ข้ามซีน → ไม่ต้องทำอะไร (ถูกทำลายไปแล้ว)
        }
    }

    void SetOverlayAlpha(float a)
    {
        if (!blackOverlayImage) return;
        var c = blackOverlayImage.color;
        c.a = Mathf.Clamp01(a);
        blackOverlayImage.color = c;
    }

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
