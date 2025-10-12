using UnityEngine;

/// <summary>
/// รับ "ΔSanity" จากแหล่งต่าง ๆ (เช่น RadioPlayer) แล้วนำไปใช้กับผู้เล่นจริง
/// ไม่บังคับโครงภายในของ PlayerControllerTest — แค่มีฟังก์ชันหรือช่องให้ปรับก็พอ
/// เวอร์ชันนี้รองรับสองแบบ:
/// 1) ถ้า PlayerControllerTest มีเมธอด public void SetSanity(float) และ property SanityMax/Sanity (ไม่จำเป็นทั้งหมด) → ใช้วิธีคำนวน set ตรง
/// 2) ถ้าไม่มีเมธอด (กรณีฟังก์ชันถูกซ่อน) → เราจะใช้ "overlay" ภายนอกโดยเก็บค่าเสริมไว้ แล้วยิงอีเวนต์กลับไปอัปเดต UI เอง (ให้เลือกผ่าน UnityEvent เพิ่มเติมได้)
/// แนะนำ: เพิ่มเมธอด public ให้ PlayerControllerTest:
///     public void SetSanity(float v) { _sanity = Mathf.Clamp(v, 0f, sanityMax); UpdateSanityUI(); }
///     public float Sanity => _sanity; public float sanityMax => sanityMax;
/// </summary>
public class SanityApplier : MonoBehaviour
{
    [Header("Refs")]
    public PlayerControllerTest player;     // อ้างถึงสคริปต์ของผู้เล่น

    // ถ้า PlayerControllerTest ไม่มี SetSanity/Sanity ให้เปิด debugWarn เพื่อดูคำเตือน
    public bool debugWarnIfNoSetter = true;

    void Reset()
    {
        if (!player) player = GetComponentInParent<PlayerControllerTest>();
    }

    void Awake()
    {
        if (!player) player = GetComponentInParent<PlayerControllerTest>();
    }

    /// <summary>
    /// เพิ่ม/ลด Sanity ตาม amount (หน่วย) — เป็น "ต่อเฟรม" แล้ว (Radio คิดต่อวินาทีให้)
    /// </summary>
    public void AddSanity(float amount)
    {
        if (!player)
        {
            if (debugWarnIfNoSetter) Debug.LogWarning("[SanityApplier] No PlayerControllerTest assigned.");
            return;
        }

        // พยายามใช้เมธอด SetSanity ถ้ามี
        // เราคาดหวังว่ามีเมธอด public void SetSanity(float)
        try
        {
            var t = player.GetType();
            var setMethod = t.GetMethod("SetSanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var sanityField = t.GetField("_sanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var maxField = t.GetField("sanityMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (setMethod != null && sanityField != null && maxField != null)
            {
                float current = (float)sanityField.GetValue(player);
                float max = (float)maxField.GetValue(player);
                float next = Mathf.Clamp(current + amount, 0f, max);
                setMethod.Invoke(player, new object[] { next });
                return;
            }
        }
        catch { /* เงียบไว้ ใช้ fallback ต่อ */ }

        // ถ้าไม่มี SetSanity: ลองหาช่อง public ให้เขียน (กรณีคุณเพิ่ม property เอง)
        // หรือหากไม่มีจริง ๆ เราจะแจ้งเตือนครั้งเดียว
        if (debugWarnIfNoSetter)
        {
            Debug.LogWarning("[SanityApplier] PlayerControllerTest ไม่มี SetSanity/_sanity/sanityMax ที่เข้าถึงได้ — กรุณาเพิ่ม public void SetSanity(float)");
            debugWarnIfNoSetter = false; // กัน spam
        }
    }
}
