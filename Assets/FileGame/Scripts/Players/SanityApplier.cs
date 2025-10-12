using UnityEngine;

/// <summary>
/// รับ delta Sanity ต่อเฟรมจากแหล่งภายนอก (เช่น RadioPlayer) แล้วนำไปใช้กับผู้เล่นจริง
/// - ไม่ล็อกกับวิธีเดียว: ใช้ได้ทั้ง SetSanity(), set field ตรง และเรียก UpdateSanityUI() ถ้ามี
/// - รองรับชื่อ field หลายแบบ: _sanity / sanity  และ  sanityMax / maxSanity
/// </summary>
public class SanityApplier : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerTest player;   // ลากสคริปต์ผู้เล่นเข้ามา

    [Header("Debug")]
    public bool logWhenApplied = false;   // เปิดไว้จะเห็น Log ทุกครั้งที่มีการเขียนค่า
    public bool warnIfNoWritableTarget = true;

    bool _warnedOnce = false;

    void Reset()
    {
        if (!player) player = GetComponentInParent<PlayerControllerTest>();
    }

    void Awake()
    {
        if (!player) player = GetComponentInParent<PlayerControllerTest>();
    }

    /// <summary>
    /// เพิ่ม/ลด Sanity ตาม amount (หน่วยเป็น "ต่อเฟรม" ที่คำนวณมาจากภายนอกแล้ว)
    /// </summary>
    public void AddSanity(float amount)
    {
        if (!player)
        {
            if (warnIfNoWritableTarget && !_warnedOnce)
            {
                Debug.LogWarning("[SanityApplier] ไม่มีอ้างอิง PlayerControllerTest — กรุณาลาก player เข้ามาใน Inspector", this);
                _warnedOnce = true;
            }
            return;
        }

        var t = player.GetType();

        // 1) พยายามอ่าน field ค่าปัจจุบันและค่า Max
        var sanityField = t.GetField("_sanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? t.GetField("sanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        var maxField = t.GetField("sanityMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?? t.GetField("maxSanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        float? currentOpt = null;
        float? maxOpt = null;

        if (sanityField != null && sanityField.FieldType == typeof(float))
            currentOpt = (float)sanityField.GetValue(player);

        if (maxField != null && maxField.FieldType == typeof(float))
            maxOpt = (float)maxField.GetValue(player);

        // 2) หาเมธอด SetSanity / UpdateSanityUI ถ้ามี
        var setMethod = t.GetMethod("SetSanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        var uiMethod = t.GetMethod("UpdateSanityUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // กรณีข้อมูลครบพอ: current + max → คำนวน next แล้วเขียนให้สำเร็จแน่ ๆ
        if (currentOpt.HasValue && maxOpt.HasValue)
        {
            float next = Mathf.Clamp(currentOpt.Value + amount, 0f, maxOpt.Value);

            if (setMethod != null)
            {
                setMethod.Invoke(player, new object[] { next });
                if (logWhenApplied) Debug.Log($"[SanityApplier] SetSanity({next:F2}) via method", player);
                return;
            }

            // ไม่มี SetSanity → เขียน field ตรง แล้วเรียกอัปเดต UI ถ้ามี
            sanityField.SetValue(player, next);
            if (uiMethod != null) uiMethod.Invoke(player, null);
            if (logWhenApplied) Debug.Log($"[SanityApplier] sanityField = {next:F2} (direct write) + UpdateSanityUI()", player);
            return;
        }

        // ถ้ามี SetSanity แต่หา current/max ไม่ได้: ลองเรียกด้วย "ค่าประมาณ"
        if (setMethod != null)
        {
            // ถ้าไม่มี current -> ถือว่า 0, ถ้าไม่มี max -> ใช้ 100 เป็นดีฟอลต์
            float cur = currentOpt ?? 0f;
            float max = maxOpt ?? 100f;
            float next = Mathf.Clamp(cur + amount, 0f, max);
            setMethod.Invoke(player, new object[] { next });
            if (logWhenApplied) Debug.Log($"[SanityApplier] SetSanity({next:F2}) (fallback, guessed bounds)", player);
            return;
        }

        // มาทางสุดท้าย: ไม่มี method และอ่าน field ไม่ได้ → แจ้งเตือน
        if (warnIfNoWritableTarget && !_warnedOnce)
        {
            Debug.LogWarning("[SanityApplier] ไม่พบทั้ง field (sanity/_sanity + sanityMax/maxSanity) และเมธอด SetSanity(..)\n" +
                             "ทางแก้ที่แนะนำอย่างน้อย 1 อย่าง:\n" +
                             "  • เพิ่ม field float _sanity และ float sanityMax ใน PlayerControllerTest\n" +
                             "  • หรือเพิ่ม public void SetSanity(float v) เพื่อให้ SanityApplier เรียกใช้\n" +
                             "  • และถ้ามี UI ให้เพิ่มเมธอด UpdateSanityUI()", player);
            _warnedOnce = true;
        }
    }
}
