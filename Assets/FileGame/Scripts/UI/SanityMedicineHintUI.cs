using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class SanityMedicineHintUI : MonoBehaviour
{
    [Header("References")]
    public PlayerController3D player;   // ลาก PlayerController3D มาใส่
    public CanvasGroup canvasGroup;     // Panel / Text ที่จะเฟดเข้าออก
    public TMP_Text label;              // ข้อความที่จะแสดง

    [Header("Logic")]
    [Tooltip("จะแสดงข้อความเมื่อ Sanity (0–100) มากกว่าหรือเท่าค่านี้")]
    [Range(0f, 100f)]
    public float showThreshold = 70f;

    [Tooltip("ข้อความภาษาอังกฤษที่จะแสดง")]
    public string message = "Press [1] to take medicine";

    [Tooltip("เวลาที่จะให้ข้อความแสดง (วินาที)")]
    [Range(0.1f, 10f)]
    public float showDuration = 2f;     // แสดง 2 วิแล้วหายไป

    [Tooltip("ความเร็วในการเฟดข้อความเข้า/ออก")]
    [Range(1f, 30f)]
    public float fadeSpeed = 10f;

    float _targetAlpha = 0f;
    bool _lastShouldShow = false;
    bool _lastSanityHigh = false;   // จำว่าก่อนหน้า sanity สูงไหม
    float _showUntilTime = -1f;     // เวลา unscaledTime ที่ข้อความควรอยู่ถึง

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!label) label = GetComponentInChildren<TMP_Text>();

        if (canvasGroup)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void LateUpdate()
    {
        if (!player || !canvasGroup || !label) return;

        float sanityValue = player.CurrentSanity;   // 0–100 จาก PlayerController3D
        bool sanityHigh = sanityValue >= showThreshold;

        // ✅ ทริกเกอร์ครั้งเดียวตอน sanity จาก "ต่ำ" → "สูง"
        if (sanityHigh && !_lastSanityHigh)
        {
            _showUntilTime = Time.unscaledTime + showDuration;
        }

        // ถ้า sanity ต่ำกว่าค่า → ยกเลิก prompt ทันที
        if (!sanityHigh)
        {
            _showUntilTime = -1f;
        }

        bool shouldShow = _showUntilTime > Time.unscaledTime;

        // เซ็ตข้อความเฉพาะตอนเปลี่ยนจากไม่โชว์ → โชว์
        if (shouldShow && !_lastShouldShow)
        {
            label.text = message;
        }

        _targetAlpha = shouldShow ? 1f : 0f;

        // เฟดเข้า/ออกด้วย unscaledDeltaTime
        float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, _targetAlpha, k);

        bool visible = canvasGroup.alpha > 0.01f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        _lastShouldShow = shouldShow;
        _lastSanityHigh = sanityHigh;
    }
}
