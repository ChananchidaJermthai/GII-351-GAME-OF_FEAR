using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class RadioInventoryUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject panelRoot;
    public Transform buttonsParent;
    public Button buttonPrefab;
    public TMP_Text headerText;
    public TMP_Text hintText;

    [Header("Auto Close")]
    public float autoCloseDistance = 4f;

    [Header("Cursor & Lock")]
    public bool lockLookOnOpen = true;
    public bool lockMoveOnOpen = false;

#if ENABLE_INPUT_SYSTEM
    [Header("Keys (Input System)")]
    public Key closeKey = Key.Escape;
#endif

    [Header("Hotkeys")]
    public bool enableNumberHotkeys = false;
    public bool enableCloseKey = true;

    [Header("Debug")]
    public bool verboseLog = true;

    // runtime
    RadioPlayer _radio;
    RadioPlayer.DurationMode _mode;
    float _customSeconds;
    Transform _radioTf;
    Transform _playerTf;

    // 🔹 Pool ของปุ่ม (ไม่ Destroy เวลารีเฟรช)
    readonly List<Button> _spawned = new List<Button>();
    readonly List<int> _indexMap = new List<int>();

    CursorLockMode _prevLock;
    bool _prevVisible;
    bool _cursorOverridden = false;

    object _playerCtrl;
    float _prevMouseX = -1f, _prevMouseY = -1f;
    float _prevWalk = -1f, _prevSprint = -1f, _prevCrouch = -1f;

    public bool IsOpen => panelRoot && panelRoot.activeSelf;

    void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);
        EnsureEventSystem();
        EnsureGraphicRaycaster();
    }

    void OnDisable()
    {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        if (_cursorOverridden) RestoreCursor();
        TryFreezePlayerControls(false);
=======
        CloseSafe();
>>>>>>> Stashed changes
=======
        CloseSafe();
>>>>>>> Stashed changes
    }

    void OnDestroy()
    {
        CloseSafe();
    }

    void CloseSafe()
    {
        if (_cursorOverridden) RestoreCursor();
        TryFreezePlayerControls(false);
        ClearButtons();
    }

    public void Open(RadioPlayer radio, RadioPlayer.DurationMode mode, float customSeconds, Transform player)
    {
        if (radio == null || radio.playerInventory == null || buttonPrefab == null || buttonsParent == null)
        {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            Debug.LogError("[RadioInventoryUI] Open(): radio เป็น null");
            return;
        }
        if (radio.playerInventory == null)
        {
            Debug.LogError("[RadioInventoryUI] RadioPlayer.playerInventory เป็น null (โปรดลาก InventoryLite ของผู้เล่นใส่ใน RadioPlayer)", radio);
        }
        if (buttonPrefab == null || buttonPrefab.GetComponent<Button>() == null)
        {
            Debug.LogError("[RadioInventoryUI] buttonPrefab ไม่มี Button component (โปรดใช้ UI > Button (TextMeshPro))", this);
            return;
        }
        if (buttonsParent == null)
        {
            Debug.LogError("[RadioInventoryUI] buttonsParent เป็น null (โปรดลากคอนเทนเนอร์ที่มี Vertical Layout Group)", this);
=======
            Debug.LogError("[RadioInventoryUI] Open(): Missing required references");
>>>>>>> Stashed changes
=======
            Debug.LogError("[RadioInventoryUI] Open(): Missing required references");
>>>>>>> Stashed changes
            return;
        }

        _radio = radio;
        _mode = mode;
        _customSeconds = customSeconds;
        _radioTf = radio.transform;
        _playerTf = player;

        // รีเฟรชปุ่มถ้า UI เปิดอยู่แล้ว
        if (IsOpen)
        {
            if (verboseLog) Debug.Log("[RadioInventoryUI] UI already open, refreshing buttons");
            RebuildButtons();
            return;
        }

        RebuildButtons();

        if (panelRoot) panelRoot.SetActive(true);

<<<<<<< Updated upstream
<<<<<<< Updated upstream
        // เซฟสถานะ cursor เฉพาะตอนเปลี่ยนจาก “ปิด → เปิด”
        if (!_cursorOverridden)
        {
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
        }

=======
=======
>>>>>>> Stashed changes
        // Cursor backup
        _prevLock = Cursor.lockState;
        _prevVisible = Cursor.visible;
>>>>>>> Stashed changes
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _cursorOverridden = true;

        if (lockLookOnOpen || lockMoveOnOpen)
            TryFreezePlayerControls(true);
    }

    public void Close()
    {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        if (_cursorOverridden) RestoreCursor();
        TryFreezePlayerControls(false);
=======
        if (!IsOpen) return;
        CloseSafe();
>>>>>>> Stashed changes
=======
        if (!IsOpen) return;
        CloseSafe();
>>>>>>> Stashed changes

        if (panelRoot) panelRoot.SetActive(false);
        _radio = null;
        _radioTf = null;
        _playerTf = null;

        if (verboseLog) Debug.Log("[RadioInventoryUI] Closed");
    }

    void RestoreCursor()
    {
        Cursor.visible = _prevVisible;
        Cursor.lockState = _prevLock;
        _cursorOverridden = false;
    }

    void Update()
    {
        if (!IsOpen) return;

        // Auto-close if out of range
        if (_playerTf && _radioTf && Vector3.Distance(_playerTf.position, _radioTf.position) > autoCloseDistance)
        {
            if (verboseLog) Debug.Log("[RadioInventoryUI] Auto-closing due to distance");
            Close();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (enableCloseKey && Keyboard.current != null && Keyboard.current[closeKey].wasPressedThisFrame) Close();

        if (enableNumberHotkeys && Keyboard.current != null)
        {
            for (int i = 0; i < _indexMap.Count && i < 9; i++)
            {
                var key = (Key)((int)Key.Digit1 + i);
                if (Keyboard.current[key].wasPressedThisFrame)
                {
                    OnPick(_indexMap[i]);
                    return;
                }
            }
        }
#else
        if (enableCloseKey && Input.GetKeyDown(KeyCode.Escape)) Close();
        if (enableNumberHotkeys)
        {
            for (int i = 0; i < _indexMap.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    OnPick(_indexMap[i]);
                    return;
                }
            }
        }
#endif
    }

    void RebuildButtons()
    {
        ClearButtons(); // ตอนนี้คือซ่อน + ล้าง indexMap ไม่ Destroy

        if (_radio.tapes == null || _radio.tapes.Count == 0)
        {
            SetHeader("No tape available");
            SetHint("Put the tape list in RadioPlayer first.");
            return;
        }

        int availableCount = 0;

        for (int i = 0; i < _radio.tapes.Count; i++)
        {
            var t = _radio.tapes[i];
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            int cnt = _radio.playerInventory ? _radio.playerInventory.GetCount(t.tapeKeyId) : 0;
=======
            int cnt = _radio.playerInventory?.GetCount(t.tapeKeyId) ?? 0;
>>>>>>> Stashed changes
=======
            int cnt = _radio.playerInventory?.GetCount(t.tapeKeyId) ?? 0;
>>>>>>> Stashed changes
            if (cnt <= 0) continue;

            // 🔹 ใช้ปุ่มจาก pool ก่อน ถ้าไม่พอค่อย Instantiate ใหม่
            Button btn;
            if (availableCount < _spawned.Count && _spawned[availableCount] != null)
            {
                btn = _spawned[availableCount];
            }
            else
            {
                btn = Instantiate(buttonPrefab, buttonsParent);
                _spawned.Add(btn);
            }

            btn.gameObject.SetActive(true);
            _indexMap.Add(i);

            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                string n = string.IsNullOrEmpty(t.displayName) ? t.tapeKeyId : t.displayName;
                label.text = $"{availableCount + 1}. {n} (x{cnt})";
            }

            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnPick(idx));

            availableCount++;
        }

        if (availableCount == 0)
        {
            SetHeader("No tape in bag.");
            SetHint("Go back to the tape first.");
            if (verboseLog) Debug.LogWarning("[RadioInventoryUI] Player has no tape.");
        }
        else
        {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            SetHeader("Select the tape you want to play.");
            SetHint(enableNumberHotkeys
                ? "Click the button or press the numbers 1–9 • ESC to close."
                : "Click the button to select • ESC to close.");
=======
            SetHeader("Select a tape to play.");
            SetHint(enableNumberHotkeys ? "Click button or press 1–9 • ESC to close" : "Click button • ESC to close");
>>>>>>> Stashed changes
=======
            SetHeader("Select a tape to play.");
            SetHint(enableNumberHotkeys ? "Click button or press 1–9 • ESC to close" : "Click button • ESC to close");
>>>>>>> Stashed changes
        }
    }

    void ClearButtons()
    {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
        // 🔹 แทน Destroy: แค่ซ่อน + reset onClick เพื่อลด GC และการจองแรมใหม่
        foreach (var b in _spawned)
        {
            if (!b) continue;
            b.onClick.RemoveAllListeners();
            b.gameObject.SetActive(false);
        }

        _indexMap.Clear();
        SetHeader("");
        SetHint("");
=======
=======
>>>>>>> Stashed changes
        foreach (var b in _spawned)
        {
            if (b)
            {
                b.onClick.RemoveAllListeners();
                Destroy(b.gameObject);
            }
        }
        _spawned.Clear();
        _indexMap.Clear();

        SetHeader(""); SetHint("");
>>>>>>> Stashed changes
    }

    void OnPick(int tapeIndex)
    {
        if (_radio == null || tapeIndex < 0 || tapeIndex >= _radio.tapes.Count)
        {
            Debug.LogError($"[RadioInventoryUI] Invalid tapeIndex {tapeIndex}");
            Close();
            return;
        }

        if (verboseLog)
        {
            var t = _radio.tapes[tapeIndex];
            Debug.Log($"[RadioInventoryUI] Using tape index {tapeIndex} / key={t.tapeKeyId}");
        }

        _radio.UseTapeIndexWithMode(tapeIndex, _mode, _customSeconds);
        Close();
    }

    void SetHeader(string s) { if (headerText) headerText.text = s; }
    void SetHint(string s) { if (hintText) hintText.text = s; }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>(true) == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.LogWarning("[RadioInventoryUI] EventSystem not found — created temporary", go);
        }
    }

    void EnsureGraphicRaycaster()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            Debug.LogWarning("[RadioInventoryUI] Added GraphicRaycaster to Canvas", canvas);
        }
    }

<<<<<<< Updated upstream
<<<<<<< Updated upstream
    // ---------- Freeze/Unfreeze player controls ----------
=======
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
    void TryFreezePlayerControls(bool freeze)
    {
        if (_playerTf == null) return;

        if (_playerCtrl == null)
        {
            var playerType = System.Type.GetType("PlayerControllerTest");
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            if (playerType != null)
                _playerCtrl = _playerTf.GetComponentInParent(playerType);

=======
            _playerCtrl = playerType != null ? _playerTf.GetComponentInParent(playerType) : null;
>>>>>>> Stashed changes
=======
            _playerCtrl = playerType != null ? _playerTf.GetComponentInParent(playerType) : null;
>>>>>>> Stashed changes
            if (_playerCtrl == null)
            {
                var pc3d = _playerTf.GetComponentInParent<PlayerController3D>();
                if (pc3d != null) _playerCtrl = pc3d;
            }
        }

        if (_playerCtrl == null) return;

        var t = _playerCtrl.GetType();
        void SetFloat(string name, ref float backup, float newVal)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(float)) return;
            if (backup < 0f) backup = (float)f.GetValue(_playerCtrl);
            f.SetValue(_playerCtrl, newVal);
        }
        void Restore(string name, ref float backup)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(float)) return;
            if (backup >= 0f) f.SetValue(_playerCtrl, backup);
            backup = -1f;
        }

        if (freeze)
        {
            if (lockLookOnOpen)
            {
                SetFloat("mouseSensitivityX", ref _prevMouseX, 0f);
                SetFloat("sensX", ref _prevMouseX, 0f);
                SetFloat("mouseSensitivityY", ref _prevMouseY, 0f);
                SetFloat("sensY", ref _prevMouseY, 0f);
            }
            if (lockMoveOnOpen)
            {
                SetFloat("walkSpeed", ref _prevWalk, 0f);
                SetFloat("sprintSpeed", ref _prevSprint, 0f);
                SetFloat("crouchSpeed", ref _prevCrouch, 0f);
            }
        }
        else
        {
            if (lockLookOnOpen)
            {
                Restore("mouseSensitivityX", ref _prevMouseX);
                Restore("sensX", ref _prevMouseX);
                Restore("mouseSensitivityY", ref _prevMouseY);
                Restore("sensY", ref _prevMouseY);
            }
            if (lockMoveOnOpen)
            {
                Restore("walkSpeed", ref _prevWalk);
                Restore("sprintSpeed", ref _prevSprint);
                Restore("crouchSpeed", ref _prevCrouch);
            }
        }
    }
}
