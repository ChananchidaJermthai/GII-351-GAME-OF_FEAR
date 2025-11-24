using UnityEngine;
using System;
using System.Reflection;

/// <summary>
/// รับ delta Sanity ต่อเฟรมจากแหล่งภายนอก (เช่น RadioPlayer) แล้วนำไปใช้กับผู้เล่นจริง
/// - ใช้ Reflection ครั้งเดียวตอน Awake/FirstUse
/// - หลังจากนั้นเรียก AddSanity จะเร็วและไม่สร้าง GC
/// </summary>
public class SanityApplier : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController3D player; // ลากสคริปต์ผู้เล่นเข้ามา

    [Header("Debug")]
    public bool logWhenApplied = false;
    public bool warnIfNoWritableTarget = true;

    private bool _warnedOnce = false;

    // Cache reflection info
    private FieldInfo _sanityField;
    private FieldInfo _maxField;
    private MethodInfo _setMethod;
    private MethodInfo _uiMethod;

    private bool _initialized = false;

    void Reset()
    {
        if (!player) player = GetComponentInParent<PlayerController3D>();
    }

    void Awake()
    {
        if (!player) player = GetComponentInParent<PlayerController3D>();
        InitializeReflection();
    }

    private void InitializeReflection()
    {
        if (_initialized || player == null) return;

        var t = player.GetType();

        _sanityField = t.GetField("_sanity", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? t.GetField("sanity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        _maxField = t.GetField("sanityMax", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                  ?? t.GetField("maxSanity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        _setMethod = t.GetMethod("SetSanity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        _uiMethod = t.GetMethod("UpdateSanityUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        _initialized = true;
    }

    /// <summary>
    /// เพิ่ม/ลด Sanity ตาม amount (หน่วยเป็น "ต่อเฟรม" ที่คำนวณมาจากภายนอกแล้ว)
    /// </summary>
    public void AddSanity(float amount)
    {
        if (!_initialized) InitializeReflection();

        if (player == null)
        {
            if (warnIfNoWritableTarget && !_warnedOnce)
            {
                Debug.LogWarning("[SanityApplier] ไม่มีอ้างอิง PlayerController — กรุณาลาก player เข้ามาใน Inspector", this);
                _warnedOnce = true;
            }
            return;
        }

        float current = _sanityField != null && _sanityField.FieldType == typeof(float) 
                        ? (float)_sanityField.GetValue(player) : 0f;
        float max = _maxField != null && _maxField.FieldType == typeof(float)
                    ? (float)_maxField.GetValue(player) : 100f;

        float next = Mathf.Clamp(current + amount, 0f, max);

        if (_setMethod != null)
        {
            _setMethod.Invoke(player, new object[] { next });
            if (logWhenApplied) Debug.Log($"[SanityApplier] SetSanity({next:F2}) via method", player);
        }
        else if (_sanityField != null)
        {
            _sanityField.SetValue(player, next);
            if (_uiMethod != null) _uiMethod.Invoke(player, null);
            if (logWhenApplied) Debug.Log($"[SanityApplier] sanityField = {next:F2} (direct write) + UpdateSanityUI()", player);
        }
        else if (warnIfNoWritableTarget && !_warnedOnce)
        {
            Debug.LogWarning("[SanityApplier] ไม่พบ field (_sanity / sanity) และ method SetSanity(..)\n" +
                             "แนะนำเพิ่ม field float _sanity + float sanityMax หรือ public void SetSanity(float v) ใน PlayerController3D", player);
            _warnedOnce = true;
        }
    }
}
