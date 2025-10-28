using UnityEngine;

/// <summary>
/// รับ delta Sanity ต่อเฟรมจากแหล่งภายนอก (เช่น RadioPlayer) แล้วนำไปใช้กับผู้เล่นจริง
/// - ไม่ล็อกชนิดสคริปต์ผู้เล่น: ใช้ได้กับ PlayerController3D/อื่น ๆ ผ่าน Reflection
/// - รองรับทั้ง SetSanity(), เขียน field ตรง (_sanity/sanity และ sanityMax/maxSanity) และเรียก UpdateSanityUI() ถ้ามี
/// - เพิ่ม BeginSession/EndSession เพื่อกันความรู้สึกว่า "เด้งกลับ" ตอนหยุดเล่นสื่อ
/// </summary>
public class SanityApplier : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("ใส่ Component ของสคริปต์ผู้เล่นตัวจริง (เช่น PlayerController3D)")]
    public Component player;   // เดิมเป็น PlayerControllerTest (แก้ให้ยืดหยุ่นขึ้น)

    [Header("Debug")]
    public bool logWhenApplied = false;
    public bool warnIfNoWritableTarget = true;

    // บันทึกผลรวมที่เพิ่มระหว่าง session ปัจจุบัน (เพื่อดีบัก/ตรวจสอบ)
    float _netAdded;
    bool _warnedOnce = false;

    void Reset()
    {
        if (!player) player = GetComponentInParent<Component>();
    }

    void Awake()
    {
        if (!player) player = GetComponentInParent<Component>();
    }

    /// <summary>เริ่ม session ใหม่ (เรียกตอนเริ่มเล่นเพลง/เอฟเฟกต์ต่อเนื่อง)</summary>
    public void BeginSession()
    {
        _netAdded = 0f;
        if (logWhenApplied) Debug.Log("[SanityApplier] BeginSession()", this);
    }

    /// <summary>จบ session (ไม่ดึงค่าคืน ปล่อยผลรวมที่เพิ่มค้างไว้)</summary>
    public void EndSession()
    {
        if (logWhenApplied) Debug.Log($"[SanityApplier] EndSession()  netAdded={_netAdded:F3}", this);
    }

    /// <summary>เพิ่ม/ลด Sanity ตาม amount (หน่วย/เฟรม)</summary>
    public void AddSanity(float amount)
    {
        if (!player)
        {
            if (warnIfNoWritableTarget && !_warnedOnce)
            {
                Debug.LogWarning("[SanityApplier] ไม่มีอ้างอิง player — กรุณาลากสคริปต์ผู้เล่น (เช่น PlayerController3D) เข้ามาใน Inspector", this);
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

        // ข้อมูลครบพอ: current + max → คำนวณ next แล้วเขียนแน่ ๆ
        if (currentOpt.HasValue && maxOpt.HasValue)
        {
            float next = Mathf.Clamp(currentOpt.Value + amount, 0f, maxOpt.Value);

            if (setMethod != null)
            {
                setMethod.Invoke(player, new object[] { next });
                if (logWhenApplied) Debug.Log($"[SanityApplier] SetSanity({next:F2}) via method", player);
            }
            else
            {
                sanityField.SetValue(player, next);
                if (uiMethod != null) uiMethod.Invoke(player, null);
                if (logWhenApplied) Debug.Log($"[SanityApplier] sanityField = {next:F2} (direct write) + UpdateSanityUI()", player);
            }

            _netAdded += amount;
            return;
        }

        // มี SetSanity แต่หา current/max ไม่ได้ → ใช้ค่าประมาณ
        if (setMethod != null)
        {
            float cur = currentOpt ?? 0f;
            float max = maxOpt ?? 100f;
            float next = Mathf.Clamp(cur + amount, 0f, max);
            setMethod.Invoke(player, new object[] { next });
            if (logWhenApplied) Debug.Log($"[SanityApplier] SetSanity({next:F2}) (fallback, guessed bounds)", player);
            _netAdded += amount;
            return;
        }

        // สุดท้าย: ไม่มีทั้ง field และ method ให้เขียน
        if (warnIfNoWritableTarget && !_warnedOnce)
        {
            Debug.LogWarning("[SanityApplier] ไม่พบทั้ง field (sanity/_sanity + sanityMax/maxSanity) และเมธอด SetSanity(.)\n" +
                             "ทางแก้: เพิ่ม field หรือเมธอดดังกล่าวในสคริปต์ผู้เล่น หรือผูกเมธอดสาธารณะ SetSanity(float)", player);
            _warnedOnce = true;
        }
    }
}
