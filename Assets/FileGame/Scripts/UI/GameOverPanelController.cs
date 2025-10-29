using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameOverPanelController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup panelGroup;      // CanvasGroup ของ Panel GameOver
    public TMP_Text titleText;          // ข้อความ "Game Over"
    public TMP_Text bodyText;           // ข้อความพิมพ์ดีด (อังกฤษ)
    public Image dimBackground;         // (ตัวเลือก) พื้นหลังดำจาง ๆ

    [Header("Texts")]
    [TextArea] public string title = "GAME OVER";
    [TextArea]
    public string bodyEn =
        "You have died. If you wish to feel fear again, press Restart now.";

    [Header("Fade In")]
    [Tooltip("เวลาที่ใช้ในการค่อย ๆ เฟดทั้ง Panel ให้ปรากฏ")]
    public float fadeDuration = 1.0f;
    [Range(0f, 1f)] public float startAlpha = 0f;
    [Range(0f, 1f)] public float endAlpha = 1f;

    [Header("Typewriter")]
    [Tooltip("ตัวอักษร/วินาที (ใช้เวลาแบบ Unscaled)")]
    public float charsPerSecond = 35f;
    public bool showCaret = false;
    public string caret = "▌";
    public bool allowSkipTyping = true;     // กดปุ่มใด ๆ เพื่อข้ามพิมพ์ดีดให้เสร็จทันที

    [Header("Optional SFX")]
    public AudioSource typeAudioSource;
    public AudioClip typeTick;               // จะถูกเล่นเป็นจังหวะ
    [Range(0f, 1f)] public float tickVolume = 0.6f;
    [Tooltip("ดีเลย์ขั้นต่ำระหว่างเสียง type (วินาที)")]
    public float minTickInterval = 0.03f;

    [Header("Events")]
    public UnityEvent onShown;               // เรียกเมื่อเฟดเสร็จและเริ่มพิมพ์ดีด
    public UnityEvent onTypingFinished;      // เรียกเมื่อพิมพ์ดีดจบ

    [Header("Start Options")]
    public bool playOnEnable = false;        // ถ้าติ๊กไว้ จะเล่นอัตโนมัติเมื่อ Enable
    public bool lockCursor = true;           // ล็อก/ปลดล็อกเมาส์ระหว่างโชว์

    Coroutine _routine;
    bool _typing;
    float _nextTickAt = 0f;

    void Reset()
    {
        panelGroup = GetComponent<CanvasGroup>();
    }

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

        if (titleText) titleText.text = "";
        if (bodyText) bodyText.text = "";

        // เปิดรับอินเทอร์แอคชันหลังจากโชว์
        if (panelGroup)
        {
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
        }

        // ขั้นตอนเฟดเข้า
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeDuration));
            SetAlpha(Mathf.Lerp(startAlpha, endAlpha, k));
            yield return null;
        }
        SetAlpha(endAlpha);

        // ตั้ง Title แล้วเริ่มพิมพ์ดีด
        if (titleText) titleText.text = title;
        onShown?.Invoke();

        _typing = true;
        yield return StartCoroutine(Co_Type(bodyEn));
        _typing = false;

        onTypingFinished?.Invoke();
        _routine = null;
    }

    IEnumerator Co_Hide(float fadeOut)
    {
        float a0 = panelGroup ? panelGroup.alpha : 1f;
        float t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeOut));
            SetAlpha(Mathf.Lerp(a0, 0f, k));
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
            if (bodyText) bodyText.text = "";
            yield break;
        }

        int shown = 0;
        float cps = Mathf.Max(1f, charsPerSecond);
        float perChar = 1f / cps;
        float acc = 0f;

        _nextTickAt = Time.unscaledTime;

        while (shown < full.Length)
        {
            // skip typing?
            if (allowSkipTyping && AnyKeyDownThisFrame())
            {
                shown = full.Length;
                if (typeAudioSource) typeAudioSource.Stop();
                break;
            }

            acc += Time.unscaledDeltaTime;
            while (acc >= perChar && shown < full.Length)
            {
                acc -= perChar;
                shown++;

                // อัปเดตข้อความ
                if (bodyText)
                {
                    if (showCaret && shown < full.Length)
                        bodyText.text = full.Substring(0, shown) + caret;
                    else
                        bodyText.text = full.Substring(0, shown);
                }

                // เล่นเสียงพิมพ์ (ถ้ามี)
                if (typeAudioSource && typeTick && Time.unscaledTime >= _nextTickAt)
                {
                    typeAudioSource.PlayOneShot(typeTick, tickVolume);
                    _nextTickAt = Time.unscaledTime + Mathf.Max(0.001f, minTickInterval);
                }
            }
            yield return null;
        }

        // เคลียร์ caret ตอนจบ
        if (bodyText)
            bodyText.text = full;
    }

    bool AnyKeyDownThisFrame()
    {
        // ใช้ unscaled time / ไม่พึ่ง Input System โดยตรง เพื่อให้ทำงานได้ทุกโปรเจกต์
        return Input.anyKeyDown;
    }

    void SetAlpha(float a)
    {
        if (panelGroup) panelGroup.alpha = Mathf.Clamp01(a);
        if (dimBackground)
        {
            var c = dimBackground.color;
            c.a = Mathf.Clamp01(a * c.a); // รักษา base alpha ของภาพ
            dimBackground.color = c;
        }
    }
}
