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

    [Header("Footstep SFX")]
    public AudioSource footstepSource;
    public AudioClip footstepLoop;
    [Range(0f,1f)] public float footstepVolume = 0.7f;
    [Range(0f,0.3f)] public float footstepFade = 0.08f;

    [Header("Hit SFX")]
    public AudioSource sfxSource;
    public AudioClip hitSfx;
    public float delayAfterHit = 0.1f;

    [Header("Faint Effect")]
    public float faintDuration = 1.2f;
    public float flickerFrequency = 9f;
    [Range(0f,1f)] public float maxFlickerAlpha = 0.8f;
    public float shakeDuration = 1.2f;
    public float shakePosIntensity = 0.05f;
    public float shakeRotIntensity = 1.5f;

    [Header("Blackout & Scene")]
    public float blackoutDuration = 2f;
    public string sceneNameToLoad = "";
    public int sceneIndexToLoad = -1;

    [Header("UI Overlay")]
    public Image blackOverlayImage;
    public bool keepOverlayAcrossScenes = false;
    public bool destroyOverlayAfterLoad = true;

    // runtime
    Vector3 _basePos;
    Quaternion _baseRot;
    bool _sequenceRunning;
    Canvas _overlayCanvas;
    GameObject _overlayRoot;

    private Vector3 _shakeOffset;
    private Quaternion _shakeRot;
    private Color _overlayColor;
    private WaitForSeconds _waitAfterHit;

    void Start()
    {
        EnsureAudio();
        EnsureOverlay();
        _waitAfterHit = new WaitForSeconds(delayAfterHit);
        if (playOnStart) PlaySequence();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void EnsureAudio()
    {
        if (!footstepSource) footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;
        footstepSource.spatialBlend = 0f;
        footstepSource.volume = 0f;
        if (footstepLoop) footstepSource.clip = footstepLoop;

        if (!sfxSource) sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    void EnsureOverlay()
    {
        if (blackOverlayImage != null) { SetOverlayAlpha(0f); return; }
        if (_overlayRoot != null) { SetOverlayAlpha(0f); return; }

        _overlayRoot = new GameObject("FaintOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _overlayCanvas = _overlayRoot.GetComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (keepOverlayAcrossScenes) DontDestroyOnLoad(_overlayRoot);

        var imgGO = new GameObject("BlackOverlay", typeof(Image));
        imgGO.transform.SetParent(_overlayRoot.transform, false);
        blackOverlayImage = imgGO.GetComponent<Image>();
        blackOverlayImage.color = Color.clear;

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        SetOverlayAlpha(0f);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (keepOverlayAcrossScenes && destroyOverlayAfterLoad && _overlayRoot)
        {
            SetOverlayAlpha(0f);
            _overlayRoot.SetActive(false);
        }
    }

    void SetOverlayAlpha(float a)
    {
        if (!blackOverlayImage) return;
        _overlayColor.r = 0f;
        _overlayColor.g = 0f;
        _overlayColor.b = 0f;
        _overlayColor.a = Mathf.Clamp01(a);
        blackOverlayImage.color = _overlayColor;
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence()
    {
        if (_sequenceRunning) return;
        if (waypoints == null || waypoints.Length == 0) { Debug.LogError("No waypoints"); return; }
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        _sequenceRunning = true;

        if (footstepLoop && footstepSource && !footstepSource.isPlaying)
        {
            footstepSource.clip = footstepLoop;
            footstepSource.Play();
        }

        yield return Co_MoveThroughWaypoints();

        if (footstepSource) yield return Co_FootstepFadeOut();

        if (hitSfx && sfxSource) sfxSource.PlayOneShot(hitSfx);
        if (delayAfterHit > 0f) yield return _waitAfterHit;

        yield return Co_FaintEffect();
        yield return Co_BlackoutThenLoad();

        _sequenceRunning = false;
    }

    IEnumerator Co_MoveThroughWaypoints()
    {
        foreach (var target in waypoints)
        {
            if (!target) continue;

            while (true)
            {
                Vector3 to = target.position - transform.position;
                float dist = to.magnitude;
                if (dist <= arriveThreshold) break;

                if (lookAtNextPoint && dist > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(to.normalized, Vector3.up),
                        Time.deltaTime * lookRotateSpeed);

                float step = moveSpeed * Time.deltaTime;
                transform.position += to.normalized * Mathf.Min(step, dist);

                UpdateFootstepVolume(dist);
                yield return null;
            }
        }
    }

    void UpdateFootstepVolume(float dist)
    {
        if (!footstepSource) return;
        float targetVol = dist > arriveThreshold ? footstepVolume : 0f;

        if (footstepFade > 0f)
            footstepSource.volume = Mathf.MoveTowards(footstepSource.volume, targetVol, Time.deltaTime / footstepFade);
        else
            footstepSource.volume = targetVol;

        if (footstepSource.volume <= 0.001f && footstepSource.isPlaying && targetVol <= 0f) footstepSource.Stop();
        else if (footstepLoop && !footstepSource.isPlaying && targetVol > 0f) footstepSource.Play();
    }

    IEnumerator Co_FootstepFadeOut()
    {
        if (!footstepSource) yield break;
        float startVol = footstepSource.volume;
        float t = 0f;
        while (t < footstepFade)
        {
            t += Time.deltaTime;
            footstepSource.volume = Mathf.MoveTowards(startVol, 0f, t / footstepFade);
            yield return null;
        }
        footstepSource.Stop();
        footstepSource.volume = 0f;
    }

    IEnumerator Co_FaintEffect()
    {
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;
        SetOverlayAlpha(0f);

        float t = 0f;
        while (t < faintDuration)
        {
            t += Time.deltaTime;

            // Flicker
            float flick = Mathf.Sin(t * flickerFrequency * Mathf.PI * 2f);
            SetOverlayAlpha(Mathf.Abs(flick) * maxFlickerAlpha);

            // Shake
            float nt = Mathf.Clamp01(t / shakeDuration);
            float falloff = 1f - nt;
            _shakeOffset = Random.insideUnitSphere * shakePosIntensity * falloff;
            _shakeRot = _baseRot * Quaternion.Euler(Random.insideUnitSphere * shakeRotIntensity * falloff);
            transform.localPosition = _basePos + _shakeOffset;
            transform.localRotation = _shakeRot;

            yield return null;
        }

        transform.localPosition = _basePos;
        transform.localRotation = _baseRot;

        // Fade to black
        float fadeDur = 0.25f;
        float ft = 0f;
        while (ft < fadeDur)
        {
            ft += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(0f,1f, ft/fadeDur));
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
            Debug.LogWarning("[CamFaint] No scene specified");
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        Gizmos.color = new Color(1f,0.8f,0.2f,0.9f);
        for (int i=0;i<waypoints.Length;i++)
        {
            var t = waypoints[i]; if(!t) continue;
            Gizmos.DrawSphere(t.position, 0.08f);
            if(i+1<waypoints.Length && waypoints[i+1]) Gizmos.DrawLine(t.position, waypoints[i+1].position);
        }
    }
}
