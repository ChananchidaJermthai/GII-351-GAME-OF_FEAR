using UnityEngine;
using UnityEngine.UI;

/// Sanity Applier V2 - Optimized & Safe
[DisallowMultipleComponent]
public class SanityApplierV2 : MonoBehaviour
{
    public enum TargetMode { Auto, AbsoluteFields, Normalized01 }

    [Header("Target")]
    [Tooltip("ใส่คอมโพเนนต์ Player จริง เช่น PlayerController3D")]
    public Component player;
    public TargetMode mode = TargetMode.Auto;

    [Header("Apply Settings")]
    [Tooltip("เปิดไว้ถ้า caller ส่งค่า 'ต่อวินาที' เช่น +5f/s")]
    public bool inputIsPerSecond = true;
    [Tooltip("ความแรงรวม (global multiplier)")]
    public float gain = 1f;
    [Tooltip("ทำให้การเปลี่ยนค่าเนียนขึ้น")]
    public float smooth = 10f;

    [Header("Clamp (ใช้ตอน AbsoluteFields)")]
    public float minSanity = 0f;
    public float maxSanityFallback = 100f;

    [Header("Debug Overlay")]
    public bool debugLogs = false;
    public bool showOverlay = false;
    public Text overlayText;

    // ===== Reflection cache =====
    System.Type _t;
    System.Reflection.FieldInfo _fSanity, _fMax;
    System.Reflection.PropertyInfo _pSanity01;
    System.Reflection.MethodInfo _mSetSanity, _mUpdateUI;

    // runtime
    float _pendingDelta = 0f;
    float _curVal = 0f;
    float _curMax = 100f;
    bool _isNormalized = false;

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
        if (Mathf.Abs(_pendingDelta) > 0f)
        {
            float apply = inputIsPerSecond ? _pendingDelta * dt : _pendingDelta;
            ApplyDeltaInternal(apply * gain, dt);
            _pendingDelta = 0f;
        }
        UpdateOverlay(false);
    }

    // ===== Public API =====
    public void Add(float delta) => _pendingDelta += delta;
    public void AddPerSecond(float perSecond) => _pendingDelta += perSecond;
    public void BeginSession() => _pendingDelta = 0f;
    public void EndSession() { }
    public void SetSanity(float value)
    {
        _pendingDelta = 0f;
        if (_isNormalized) WriteNormalized(value);
        else WriteAbsolute(value);
    }

    // ===== Core =====
    void Bind()
    {
        if (!player)
        {
            if (debugLogs) Debug.LogWarning("[SanityApplierV2] No player bound");
            return;
        }

        _t = player.GetType();

        _pSanity01 = _t.GetProperty("Sanity01",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        _fSanity = _t.GetField("_sanity",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?? _t.GetField("sanity",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        _fMax = _t.GetField("sanityMax",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?? _t.GetField("maxSanity",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        _mSetSanity = _t.GetMethod("SetSanity",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        _mUpdateUI = _t.GetMethod("UpdateSanityUI",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        // Auto detect mode
        if (mode == TargetMode.Auto)
        {
            if (_pSanity01 != null) { mode = TargetMode.Normalized01; _isNormalized = true; }
            else { mode = TargetMode.AbsoluteFields; _isNormalized = false; }
        }
        else _isNormalized = (mode == TargetMode.Normalized01);

        if (debugLogs)
            Debug.Log($"[SanityApplierV2] Bound mode={mode}, Sanity01? {_pSanity01 != null}, Fields? {_fSanity != null}/{_fMax != null}, SetSanity? {_mSetSanity != null}", player);
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
            if (_fSanity != null && _fSanity.FieldType == typeof(float)) cur = (float)_fSanity.GetValue(player);
            if (_fMax != null && _fMax.FieldType == typeof(float)) max = (float)_fMax.GetValue(player);
            _curVal = Mathf.Clamp(cur, minSanity, max);
            _curMax = Mathf.Max(0.0001f, max);
        }
    }

    void ApplyDeltaInternal(float delta, float dt)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        ReadCurrent();

        if (_isNormalized)
        {
            float target = Mathf.Clamp01(_curVal + delta);
            float sm = Mathf.Max(0.0001f, 1f - Mathf.Exp(-smooth * dt));
            float next = Mathf.Lerp(_curVal, target, sm);
            WriteNormalized(next);
        }
        else
        {
            float next = Mathf.Clamp(_curVal + delta, minSanity, _curMax);
            float sm = Mathf.Max(0.0001f, 1f - Mathf.Exp(-smooth * dt));
            next = Mathf.Lerp(_curVal, next, sm);
            WriteAbsolute(next);
        }
    }

    void WriteNormalized(float v01)
    {
        if (!player) return;
        v01 = Mathf.Clamp01(v01);

        if (_pSanity01 != null && _pSanity01.CanWrite)
        {
            _pSanity01.SetValue(player, v01, null);
            CallUI();
            if (debugLogs) Debug.Log($"[SanityApplierV2] Sanity01={v01:0.###}", player);
            return;
        }

        // fallback → absolute
        float max = Mathf.Max(_curMax, maxSanityFallback);
        WriteAbsolute(v01 * max);
    }

    void WriteAbsolute(float v)
    {
        if (!player) return;
        v = Mathf.Clamp(v, minSanity, _curMax);

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

        if (debugLogs) Debug.LogWarning("[SanityApplierV2] No writable target (SetSanity or field)", player);
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
