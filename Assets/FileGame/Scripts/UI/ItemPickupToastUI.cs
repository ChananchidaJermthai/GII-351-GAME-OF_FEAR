using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ItemPickupToastUI : MonoBehaviour
{
    [Header("References")]
    public RectTransform root;      // Panel หลัก
    public TMP_Text label;          // ข้อความแสดงชื่อไอเทม
    public CanvasGroup canvasGroup; // ใช้เฟดโปร่งใส

    [Header("Animation")]
    public float upDistance = 60f;      // ระยะเลื่อนขึ้น (หน่วย: px)
    public float upDuration = 0.25f;    // เวลาที่ใช้เลื่อนขึ้น
    public float holdDuration = 1.0f;   // เวลาค้างอยู่กับที่
    public float slideDistance = 200f;  // ระยะเลื่อนไปทางซ้าย
    public float slideDuration = 0.35f; // เวลาที่ใช้สไลด์ออก

    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    Coroutine _routine;
    Vector2 _basePos;

    void Awake()
    {
        if (!root) root = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!label) label = GetComponentInChildren<TMP_Text>();

        if (root) _basePos = root.anchoredPosition;
        if (canvasGroup) canvasGroup.alpha = 0f;
        if (root) root.gameObject.SetActive(false);
    }

    /// <summary>
    /// เรียกโชว์ข้อความตอนเก็บไอเทม
    /// </summary>
    public void Show(string text)
    {
        if (!root || !canvasGroup || !label) return;

        label.text = text;

        if (_routine != null)
            StopCoroutine(_routine);

        _routine = StartCoroutine(Co_Show());
    }

    IEnumerator Co_Show()
    {
        root.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        root.anchoredPosition = _basePos;

        // 1) เลื่อนขึ้นจากด้านล่าง
        Vector2 startPos = _basePos;
        Vector2 endPos = _basePos + Vector2.up * upDistance;

        float t = 0f;
        while (t < upDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / upDuration);
            float eval = moveCurve != null ? moveCurve.Evaluate(k) : k;
            root.anchoredPosition = Vector2.Lerp(startPos, endPos, eval);
            yield return null;
        }
        root.anchoredPosition = endPos;

        // 2) ค้างอยู่ ~1 วิ
        float timer = 0f;
        while (timer < holdDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        // 3) สไลด์ไปทางซ้าย + เฟดหายไป
        startPos = endPos;
        endPos = endPos + Vector2.left * slideDistance;
        t = 0f;

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / slideDuration);
            float eval = moveCurve != null ? moveCurve.Evaluate(k) : k;

            root.anchoredPosition = Vector2.Lerp(startPos, endPos, eval);
            canvasGroup.alpha = 1f - k;

            yield return null;
        }

        canvasGroup.alpha = 0f;
        root.gameObject.SetActive(false);
        _routine = null;
    }
}
