using UnityEngine;
using UnityEngine.UI;

public class IntroLoadController : MonoBehaviour
{
    [Header("UI Load Panel")]
    [Tooltip("Panel ที่จะแสดงตอนโหลด (ใส่ GameObject ที่มี Canvas/Panel)")]
    public GameObject loadPanel;

    [Tooltip("Slider สำหรับแสดงความก้าวหน้าของเวลา")]
    public Slider progressSlider;

    [Header("Timing")]
    [Tooltip("เวลารอ (วินาที) ก่อนเริ่มเล่น CamFaint")]
    public float countdownDuration = 7f;

    [Header("Cam Faint Sequence")]
    [Tooltip("Component CamFaint ที่อยู่บน GameObject กล้อง/ตัวที่ต้องการให้เดินตาม waypoint")]
    public CamFaint camFaint;   // ✅ เก็บเป็น Component ไม่ใช่ GameObject

    bool _started;

    void Awake()
    {
        // ปิด CamFaint ไว้ก่อน (ไม่ให้ Start ทำงานตั้งแต่โหลดซีน)
        if (camFaint != null)
        {
            camFaint.enabled = false;
        }
        else
        {
            Debug.LogWarning("[IntroLoadController] camFaint ไม่ได้ถูกตั้งค่าใน Inspector", this);
        }
    }

    void Start()
    {
        if (!loadPanel)
            Debug.LogWarning("[IntroLoadController] loadPanel ไม่ได้ถูกตั้งค่า", this);

        if (!progressSlider)
            Debug.LogWarning("[IntroLoadController] progressSlider ไม่ได้ถูกตั้งค่า", this);

        // เปิด Panel โหลด
        if (loadPanel)
            loadPanel.SetActive(true);

        // ตั้งค่า Slider เริ่มต้น
        if (progressSlider)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
        }

        // กันใส่เวลา <= 0
        if (countdownDuration <= 0f)
            countdownDuration = 0.01f;

        if (!_started)
        {
            _started = true;
            StartCoroutine(CountdownRoutine());
        }
    }

    System.Collections.IEnumerator CountdownRoutine()
    {
        float t = 0f;

        // นับเวลา 0 → countdownDuration
        while (t < countdownDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / countdownDuration);

            if (progressSlider)
                progressSlider.value = progress;

            yield return null;
        }

        // เวลาเต็ม → ปิด Panel
        if (loadPanel)
            loadPanel.SetActive(false);

        // เปิด CamFaint หลังจากนับถอยหลังครบ
        if (camFaint)
        {
            camFaint.enabled = true;   // ✅ เปิด Component
            // ไม่ต้องเรียก PlaySequence เอง ถ้าใน CamFaint ตั้ง playOnStart = true
            // ถ้าใน Inspector ปิด playOnStart ไว้เอง และอยากให้เล่นทันที ให้เติมบรรทัดนี้เพิ่ม:
            // camFaint.PlaySequence();
        }
        else
        {
            Debug.LogWarning("[IntroLoadController] ไม่มี camFaint ให้เปิดหลังโหลดจบ", this);
        }
    }
}
