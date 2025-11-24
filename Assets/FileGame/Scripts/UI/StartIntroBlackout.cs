using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartIntroBlackout : MonoBehaviour
{
    [Header("Player Lock (ใส่สคริปต์ที่ควบคุมการเดิน/กล้อง)")]
    public MonoBehaviour[] movementScriptsToDisable;

    [Header("Overlay (ถ้าไม่ใส่ จะสร้างให้อัตโนมัติ)")]
    public Image blackOverlay;
    public bool destroyOverlayAfter = true;

    [Header("Effect Timing")]
    public float flickerDuration = 1f;
    public float flickerFrequency = 8f;
    [Range(0f, 1f)] public float flickerMaxAlpha = 0.9f;
    public float fadeOutDuration = 0.8f;

    [Header("Options")]
    public bool unlockCursorAfter = false;
    public bool useUnscaledTime = true;

    Canvas _autoCanvas;
    GameObject _overlayRoot;

    void Start()
    {
        Cursor.visible = false;
        SetPlayerLock(true);
        EnsureOverlay();
        StartCoroutine(Co_Run());
    }

    IEnumerator Co_Run()
    {
        if (!blackOverlay) yield break;

        // เริ่มจากโปร่งใส
        SetOverlayAlpha(0f);

        // Flicker phase
        float t = 0f;
        while (t < flickerDuration)
        {
            t += Dt();
            float phase = t * flickerFrequency * Mathf.PI * 2f;
            float alpha = Mathf.Abs(Mathf.Sin(phase)) * flickerMaxAlpha;
            SetOverlayAlpha(alpha);
            yield return null;
        }

        // Fade out
        float ft = 0f;
        float startAlpha = blackOverlay.color.a;
        while (ft < fadeOutDuration)
        {
            ft += Dt();
            float k = Mathf.Clamp01(ft / Mathf.Max(0.0001f, fadeOutDuration));
            SetOverlayAlpha(Mathf.Lerp(startAlpha, 0f, k));
            yield return null;
        }

        SetOverlayAlpha(0f);

        // Unlock player
        SetPlayerLock(false);

        if (unlockCursorAfter)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Cleanup overlay
        if (destroyOverlayAfter && _overlayRoot) Destroy(_overlayRoot);
        else if (blackOverlay) blackOverlay.gameObject.SetActive(false);
    }

    void EnsureOverlay()
    {
        if (blackOverlay) return;

        // สร้าง Canvas + Image ดำเต็มจอ
        _overlayRoot = new GameObject("IntroBlackOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _autoCanvas = _overlayRoot.GetComponent<Canvas>();
        _autoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayRoot.layer = LayerMask.NameToLayer("UI");

        var imgGO = new GameObject("BlackOverlay", typeof(Image));
        imgGO.transform.SetParent(_overlayRoot.transform, false);
        blackOverlay = imgGO.GetComponent<Image>();
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void SetOverlayAlpha(float a)
    {
        if (!blackOverlay) return;
        var c = blackOverlay.color;
        c.a = Mathf.Clamp01(a);
        blackOverlay.color = c;
    }

    void SetPlayerLock(bool locked)
    {
        if (movementScriptsToDisable == null) return;
        foreach (var mb in movementScriptsToDisable)
        {
            if (mb != null) mb.enabled = !locked;
        }
    }

    float Dt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
}
