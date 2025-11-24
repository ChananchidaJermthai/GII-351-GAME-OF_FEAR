using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameOverPanelController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup panelGroup;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Image dimBackground;

    [Header("Texts")]
    [TextArea] public string title = "GAME OVER";
    [TextArea] public string bodyEn =
        "You have died. If you wish to feel fear again, press Restart now.";

    [Header("Fade In/Out")]
    public float fadeDuration = 1f;
    [Range(0f, 1f)] public float startAlpha = 0f;
    [Range(0f, 1f)] public float endAlpha = 1f;

    [Header("Typewriter")]
    public float charsPerSecond = 35f;
    public bool showCaret = false;
    public string caret = "▌";
    public bool allowSkipTyping = true;

    [Header("Optional SFX")]
    public AudioSource typeAudioSource;
    public AudioClip typeTick;
    [Range(0f,1f)] public float tickVolume = 0.6f;
    public float minTickInterval = 0.03f;

    [Header("Events")]
    public UnityEvent onShown;
    public UnityEvent onTypingFinished;

    [Header("Start Options")]
    public bool playOnEnable = false;
    public bool lockCursor = true;

    Coroutine _routine;
    float _nextTickAt;

    void Reset() => panelGroup = GetComponent<CanvasGroup>();

    void Awake()
    {
        if (!panelGroup) panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup)
        {
            panelGroup.alpha = startAlpha;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
        if (titleText) titleText.text = "";
        if (bodyText) bodyText.text = "";
        if (dimBackground) dimBackground.raycastTarget = false;
    }

    void OnEnable()
    {
        if (playOnEnable) Show();
    }

    public void Show()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Show());
    }

    public void Hide(float fadeOut = 0.5f)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Hide(fadeOut));
    }

    IEnumerator Co_Show()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        titleText?.SetText("");
        bodyText?.SetText("");

        if (panelGroup)
        {
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
        }

        // Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, endAlpha, t / Mathf.Max(0.0001f, fadeDuration)));
            yield return null;
        }
        SetAlpha(endAlpha);

        titleText?.SetText(title);
        onShown?.Invoke();

        yield return Co_Type(bodyEn);
        onTypingFinished?.Invoke();

        _routine = null;
    }

    IEnumerator Co_Hide(float fadeOut)
    {
        float start = panelGroup ? panelGroup.alpha : 1f;
        float t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, 0f, t / Mathf.Max(0.0001f, fadeOut)));
            yield return null;
        }
        SetAlpha(0f);

        if (panelGroup)
        {
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
        }

        _routine = null;
    }

    IEnumerator Co_Type(string full)
    {
        if (string.IsNullOrEmpty(full))
        {
            bodyText?.SetText("");
            yield break;
        }

        int index = 0;
        float cps = Mathf.Max(1f, charsPerSecond);
        float perChar = 1f / cps;
        float acc = 0f;
        _nextTickAt = Time.unscaledTime;

        var sb = new StringBuilder(full.Length);

        while (index < full.Length)
        {
            if (allowSkipTyping && Input.anyKeyDown)
            {
                sb.Clear().Append(full);
                bodyText?.SetText(sb.ToString());
                typeAudioSource?.Stop();
                break;
            }

            acc += Time.unscaledDeltaTime;
            while (acc >= perChar && index < full.Length)
            {
                acc -= perChar;
                sb.Append(full[index]);
                index++;

                if (bodyText != null)
                    bodyText.text = showCaret && index < full.Length ? sb.ToString() + caret : sb.ToString();

                if (typeAudioSource && typeTick != null && Time.unscaledTime >= _nextTickAt)
                {
                    typeAudioSource.PlayOneShot(typeTick, tickVolume);
                    _nextTickAt = Time.unscaledTime + Mathf.Max(0.001f, minTickInterval);
                }
            }
            yield return null;
        }

        bodyText?.SetText(full);
    }

    void SetAlpha(float a)
    {
        if (panelGroup) panelGroup.alpha = Mathf.Clamp01(a);
        if (dimBackground)
        {
            var c = dimBackground.color;
            c.a = Mathf.Clamp01(a * c.a);
            dimBackground.color = c;
        }
    }
}
