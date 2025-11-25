using UnityEngine;
using UnityEngine.UI;

/// Sanity Applier (เสถียรกว่าเดิม)
// - รับ "หน่วยต่อวินาที" แล้วคูณ dt ภายใน (AddPerSecond)
// - โหมด Auto: ตรวจเจอ Sanity01 (0..1) หรือคู่ field (sanity/sanityMax)
// - รองรับ SetSanity(float), UpdateSanityUI()
// - มี Smoothing กันเด้ง + Debug Overlay
[DisallowMultipleComponent]
public class SanityApplierV2 : MonoBehaviour
{
    public enum TargetMode { Auto, AbsoluteFields, Normalized01 }

    [Header("Target")]
    [Tooltip("ใส่คอมโพเนนต์ Player จริง เช่น PlayerController3D")]
    public Component player;
    public TargetMode mode = TargetMode.Auto;

    [Header("Apply Settings")]
    [Tooltip("เปิดไว้ถ้า caller ส่งค่า 'ต่อวินาที' มา (เช่น +5f/s)")]
    public bool inputIsPerSecond = true;
    [Tooltip("ความแรงรวม (global multiplier)")]
    public float gain = 1f;
    [Tooltip("ทำให้การเปลี่ยนค่าเนียนขึ้น")]
    public float smooth = 10f;

    [Header("Clamp (ใช้ตอน AbsoluteFields)")]
    public float minSanity = 0f;
    public float maxSanityFallback = 100f; // ถ้าหา max field ไม่เจอ

    [Header("Debug")]
    public bool debugLogs = false;
    public bool showOverlay = false;
    public Text overlayText;

    // ===== reflection cache =====
    System.Type _t;
    System.Reflection.FieldInfo _fSanity, _fMax;
    System.Reflection.PropertyInfo _pSanity01;
    System.Reflection.MethodInfo _mSetSanity, _mUpdateUI;

    // runtime state
    float _pendingDelta;   // ค่าที่สะสมจาก Add / AddPerSecond
    float _curVal;         // ค่าปัจจุบัน (absolute หรือ normalized แล้วแต่โหมด)
    float _curMax = 100f;  // absolute max
    bool _isNormalized;

    void Awake()
    {
        if (!player) player = GetComponentInParent<Component>();
        Bind();
        ReadCurrent();
        UpdateOverlay(true);
    }

    void Update()
    {
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0f);
        float apply = _pendingDelta;

        if (inputIsPerSecond)
            apply *= dt;

        if (Mathf.Abs(apply) > 0f)
        {
            ApplyDeltaInternal(apply * gain, dt);
            _pendingDelta = 0f;
        }

        UpdateOverlay(false);
    }

    // ====== Public API ======
    public void Add(float delta) { _pendingDelta += delta; }
    public void AddPerSecond(float perSecond) { _pendingDelta += perSecond; } // per-second input

    public void BeginSession() { _pendingDelta = 0f; }
    public void EndSession() { /* ไม่ดึงค่าคืน */ }

    // ====== Core ======
    void Bind()
    {
        if (!player) { if (debugLogs) Debug.LogWarning("[SanityApplierV2] No player bound"); return; }
        _t = player.GetType();

        // ลองหา Property Sanity01 ก่อน (normalized)
        _pSanity01 = _t.GetProperty("Sanity01",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // หา field แบบ absolute
        _fSanity = _t.GetField("_sanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? _t.GetField("sanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        _fMax = _t.GetField("sanityMax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?? _t.GetField("maxSanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // method (ถ้ามี)
        _mSetSanity = _t.GetMethod("SetSanity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        _mUpdateUI = _t.GetMethod("UpdateSanityUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // ตัดสินโหมด
        if (mode == TargetMode.Auto)
        {
            if (_pSanity01 != null)
            {
                mode = TargetMode.Normalized01;
                _isNormalized = true;
            }
            else
            {
                mode = TargetMode.AbsoluteFields;
                _isNormalized = false;
            }
        }
        else _isNormalized = (mode == TargetMode.Normalized01);

        if (debugLogs)
            Debug.Log($"[SanityApplierV2] Bound mode={mode}, has Sanity01? {(_pSanity01 != null)}, has fields? {(_fSanity != null)}/{(_fMax != null)}, has SetSanity? {(_mSetSanity != null)}", player);
    }

    void ReadCurrent()
    {
        if (!player) return;
        if (_isNormalized && _pSanity01 != null)
        {
            _curVal = Mathf.Clamp01((float)_pSanity01.GetValue(player, null));
            _curMax = 1f;
        }
        else
        {
            float cur = 0f, max = maxSanityFallback;

            if (_fSanity != null && _fSanity.FieldType == typeof(float))
                cur = (float)_fSanity.GetValue(player);

            if (_fMax != null && _fMax.FieldType == typeof(float))
                max = (float)_fMax.GetValue(player);

            _curVal = Mathf.Clamp(cur, minSanity, max);
            _curMax = Mathf.Max(0.0001f, max);
        }
    }

    void ApplyDeltaInternal(float delta, float dt)
    {
        // อ่านค่าใหม่ก่อน
        ReadCurrent();

        float target = _curVal + (_isNormalized ? delta : delta); // delta เดียวกัน (แหล่งส่งควบคุมสเกล)
        if (_isNormalized)
        {
            target = Mathf.Clamp01(target);
            float sm = 1f - Mathf.Exp(-smooth * dt);
            float next = Mathf.Lerp(_curVal, target, sm);
            WriteNormalized(next);
        }
        else
        {
            float max = Mathf.Max(0.0001f, _curMax);
            float next = Mathf.Clamp(_curVal + delta, minSanity, max);
            float sm = 1f - Mathf.Exp(-smooth * dt);
            next = Mathf.Lerp(_curVal, next, sm);
            WriteAbsolute(next);
        }
    }

    void WriteNormalized(float v01)
    {
        if (!player) return;

        // 1) ถ้ามี property Sanity01 → เขียนตรง
        if (_pSanity01 != null && _pSanity01.CanWrite)
        {
            _pSanity01.SetValue(player, Mathf.Clamp01(v01), null);
            CallUI();
            if (debugLogs) Debug.Log($"[SanityApplierV2] Sanity01={v01:0.###}", player);
            return;
        }

        // 2) ถ้าไม่มี → แปลงเป็น absolute แล้วพยายามเขียน
        float max = Mathf.Max(_curMax, maxSanityFallback);
        WriteAbsolute(v01 * max);
    }

    void WriteAbsolute(float v)
    {
        if (!player) return;

        if (_mSetSanity != null)
        {
            _mSetSanity.Invoke(player, new object[] { v });
            CallUI();
            if (debugLogs) Debug.Log($"[SanityApplierV2] SetSanity({v:0.##}) via method", player);
            return;
        }

        if (_fSanity != null && _fSanity.FieldType == typeof(float))
        {
            _fSanity.SetValue(player, v);
            CallUI();
            if (debugLogs) Debug.Log($"[SanityApplierV2] sanityField={v:0.##} (direct)", player);
            return;
        }

        if (debugLogs) Debug.LogWarning("[SanityApplierV2] No writable target (SetSanity or sanity field). Please expose one.", player);
    }

    void CallUI()
    {
        if (_mUpdateUI != null) _mUpdateUI.Invoke(player, null);
    }

    void UpdateOverlay(bool force)
    {
        if (!showOverlay || !overlayText) return;
        overlayText.text = $"SanityApplierV2\nmode={mode}\ncur={_curVal:0.###}/{_curMax:0.##}\nΔpending={_pendingDelta:0.###}";
    }
}
