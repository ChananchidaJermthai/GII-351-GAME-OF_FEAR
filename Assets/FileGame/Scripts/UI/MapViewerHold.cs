using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// กด M "ค้าง" เพื่อเปิด Map; ปล่อย = ปิด
/// ต้องมีไอเทมใน InventoryLite: key = "Map"
[DisallowMultipleComponent]
public class MapViewerHold : MonoBehaviour
{
    [Header("Requirements")]
    public InventoryLite inventory;                 // ลาก InventoryLite ของผู้เล่น
    public string mapItemId = "Map";                // ไอเทมที่ต้องมี

    [Header("Input")]
    public InputActionReference holdAction;         // (แนะนำ) Action แบบ Button (e.g. "Map")
#if ENABLE_INPUT_SYSTEM
    public Key fallbackKey = Key.M;                 // กรณีไม่มี action
#else
    public KeyCode legacyKey = KeyCode.M;
#endif

    [Header("UI")]
    public GameObject mapRoot;                      // Canvas/Panel ของแผนที่
    public CanvasGroup fadeGroup;                   // (ทางเลือก) ถ้าอยากเฟด
    [Range(0f, 20f)] public float fadeSpeed = 12f;   // เร็วแค่ไหนในการเฟด

    [Header("Built-in Hint (auto-hide 2s)")]
    public CanvasGroup hintGroup;                   // กล่องข้อความเล็ก ๆ (ถ้ามี)
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
    public TMPro.TMP_Text hintLabel;                // ข้อความใน hint
#endif
    [Tooltip("เวลาที่โชว์ข้อความเตือน (วินาที)")]
    public float hintSeconds = 2f;                  // << ต้องการ 2 วินาที
    [Range(0f, 20f)] public float hintFade = 12f;    // ความไวในการเฟด hint

    [Header("Options")]
    public bool lockTimeScale = false;              // เปิด map แล้วหยุดเวลา
    [Range(0f, 1f)] public float timeScaleWhileOpen = 0f;

    [Tooltip("ให้สคริปต์นี้จัดการ lockState/visible ของ cursor ตอนเปิด/ปิด map")]
    public bool lockCursor = true;                  // ตอนนี้: เปิด map ก็ยังซ่อน cursor เหมือนเดิม

    [Tooltip("ปิดสคริปต์กล้องตอนเปิด Map เพื่อไม่ให้หันตามเมาส์")]
    public bool disablePlayerLookWhileOpen = false; // ถ้ามีระบบมุมกล้อง ให้ปิดตอนเปิดแผนที่

    [Tooltip("สคริปต์กล้อง/มุมมองที่ต้องการปิดเมื่อเปิด Map (เช่น PlayerLook, CameraController ฯลฯ)")]
    public Behaviour lookScriptToDisable;

    [Header("Fallback message (ถ้าไม่ใช้ hintGroup)")]
    public MonoBehaviour messageUI;                 // มีเมธอด ShowCenter(string,float) หรือ Show(string)
    public string needMapText = "You need a map.";

    [Header("Events")]
    public UnityEvent onMapOpened;
    public UnityEvent onMapClosed;

    [Header("Debug")]
    public bool debugLogs = false;

    bool _isOpen;           // สถานะจริงของแผนที่ (target)
    bool _appliedLocks;     // เคยล็อก time/cursor แล้วหรือยัง

    // hint runtime
    float _hintUntil;        // เวลาเลิกโชว์
    float _nextHintAllowed;  // กันสแปม

    void Awake()
    {
        if (!inventory) inventory = GetComponentInParent<InventoryLite>();
        if (mapRoot) mapRoot.SetActive(false);
        if (fadeGroup) fadeGroup.alpha = 0f;

        if (hintGroup)
        {
            hintGroup.alpha = 0f;
            hintGroup.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        // ค่าเริ่มต้น: ซ่อน cursor + ล็อกเหมือน FPS ทั่วไป
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        holdAction?.action?.Enable();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        holdAction?.action?.Disable();
        SetOpen(false, true);
        HideHint(true);
    }

    void Update()
    {
        bool wantOpen = IsHoldPressed();
        bool hasMap = HasMap();

        // ถ้ากดค้างแต่ไม่มีแผนที่ → โชว์ข้อความ 2 วิ แล้วหายเอง
        if (wantOpen && !hasMap)
        {
            if (WasPressedThisFrame() && Time.unscaledTime >= _nextHintAllowed)
            {
                ShowNeedMapHint();                              // << โชว์ 2 วิ
                _nextHintAllowed = Time.unscaledTime + 0.25f;   // กันสแปมกดรัว
            }
            wantOpen = false;
        }

        // อัปเดตสถานะเป้าหมาย
        if (wantOpen != _isOpen)
            SetOpen(wantOpen, false);

        // เฟด Map
        if (fadeGroup)
        {
            float target = _isOpen ? 1f : 0f;
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            fadeGroup.alpha = Mathf.Lerp(fadeGroup.alpha, target, k);
            if (mapRoot && !mapRoot.activeSelf && fadeGroup.alpha > 0.01f) mapRoot.SetActive(true);
            if (mapRoot && mapRoot.activeSelf && fadeGroup.alpha < 0.01f && !_isOpen) mapRoot.SetActive(false);
        }

        // เฟด Hint (auto-hide)
        if (hintGroup && hintGroup.gameObject.activeSelf)
        {
            float target = (Time.unscaledTime < _hintUntil) ? 1f : 0f;
            float k = 1f - Mathf.Exp(-hintFade * Time.unscaledDeltaTime);
            hintGroup.alpha = Mathf.Lerp(hintGroup.alpha, target, k);
            if (hintGroup.alpha <= 0.01f && target == 0f) HideHint(true);
        }
    }

    bool HasMap()
    {
        if (!inventory || string.IsNullOrEmpty(mapItemId)) return false;
        // รองรับ InventoryLite แบบทั่วไป: มี GetCount(string)
        var mi = inventory.GetType().GetMethod("GetCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (mi != null && mi.ReturnType == typeof(int))
        {
            int c = (int)mi.Invoke(inventory, new object[] { mapItemId });
            return c > 0;
        }
        return false;
    }

    bool IsHoldPressed()
    {
        if (holdAction && holdAction.action != null) return holdAction.action.IsPressed();
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[fallbackKey].isPressed;
#else
        return Input.GetKey(legacyKey);
#endif
    }

    bool WasPressedThisFrame()
    {
        if (holdAction && holdAction.action != null) return holdAction.action.WasPressedThisFrame();
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[fallbackKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(legacyKey);
#endif
    }

    void SetOpen(bool open, bool forceInstant)
    {
        _isOpen = open;

        // จัดการ UI (กรณีไม่ใช้ fadeGroup)
        if (fadeGroup == null && mapRoot)
            mapRoot.SetActive(open);

        // จัดการ timeScale/cursor/ล็อกอื่น ๆ
        if (open && !_appliedLocks)
        {
            if (lockTimeScale)
                Time.timeScale = timeScaleWhileOpen;

            if (lockCursor)
            {
                // เปิด map → ซ่อนเมาส์ + ล็อกเคอร์เซอร์
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (disablePlayerLookWhileOpen)
            {
                // ปิดสคริปต์กล้องไม่ให้หมุนตามเมาส์
                if (lookScriptToDisable != null)
                    lookScriptToDisable.enabled = false;
            }

            _appliedLocks = true;
            onMapOpened?.Invoke();
            if (debugLogs) Debug.Log("[MapViewerHold] Open");
        }
        else if (!open && _appliedLocks)
        {
            if (lockTimeScale)
                Time.timeScale = 1f;

            if (lockCursor)
            {
                // ปิด map แล้วกลับไปสภาพ default: ซ่อน cursor + Lock (สไตล์ FPS)
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (disablePlayerLookWhileOpen)
            {
                // เปิดสคริปต์กล้องกลับมา
                if (lookScriptToDisable != null)
                    lookScriptToDisable.enabled = true;
            }

            _appliedLocks = false;
            onMapClosed?.Invoke();
            if (debugLogs) Debug.Log("[MapViewerHold] Close");
        }
    }

    // ---------- Hint helpers ----------
    void ShowNeedMapHint()
    {
        // ถ้ามี hintGroup → ใช้ hint ในตัว (auto-hide 2 วิ)
        if (hintGroup)
        {
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
            if (hintLabel) hintLabel.text = needMapText;
#endif
            hintGroup.gameObject.SetActive(true);
            _hintUntil = Time.unscaledTime + Mathf.Max(0.1f, hintSeconds); // ค่าเริ่ม 2 วิ
            hintGroup.alpha = 0f; // จะเฟดขึ้นใน Update
            return;
        }

        // ไม่มี hintGroup → พยายามใช้ messageUI แบบกำหนดเวลา
        if (messageUI)
        {
            var t = messageUI.GetType();
            var m = t.GetMethod("ShowCenter", new System.Type[] { typeof(string), typeof(float) });
            if (m != null) { m.Invoke(messageUI, new object[] { needMapText, Mathf.Max(0.1f, hintSeconds) }); return; }
            m = t.GetMethod("Show", new System.Type[] { typeof(string) });
            if (m != null) { m.Invoke(messageUI, new object[] { needMapText }); return; } // บาง UI ไม่มี duration จะค้าง (แนะนำใช้ hintGroup)
        }

        if (debugLogs) Debug.Log(needMapText);
    }

    void HideHint(bool deactivateGO)
    {
        if (!hintGroup) return;
        hintGroup.alpha = 0f;
        if (deactivateGO) hintGroup.gameObject.SetActive(false);
    }
}
