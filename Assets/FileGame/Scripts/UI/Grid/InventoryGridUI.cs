using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventoryGridUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InventoryLite inventory;

    [Header("Panel")]
    [SerializeField] private GameObject panel;         // แผง Inventory ทั้งชุด (จะ SetActive)
    [SerializeField] private TMP_Text titleText;       // ข้อความหัวข้อ "INVENTORY" (ถ้ามี)

    [Header("Slots")]
    [Tooltip("ใส่ช่องทั้งหมดตามลำดับซ้าย->ขวา บน->ล่าง (ปล่อยว่างให้ตัวสคริปต์หาอัตโนมัติจากลูกก็ได้)")]
    [SerializeField] private List<InventorySlotUI> slots = new();

    [Header("Icons (ID -> Sprite)")]
    [SerializeField] private List<IconMap> iconMaps = new();

    [Header("Open/Close (Hold-to-open)")]
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionProperty holdAction; // type=Button
    [SerializeField] private bool useDefaultTabIfEmpty = true;
#endif

    [Header("Behavior")]
    [SerializeField] private bool refreshOnOpen = true;
    [SerializeField] private bool autoRefreshWhileOpen = true;
    [SerializeField, Min(0.05f)] private float autoRefreshInterval = 0.25f;

    // runtime
    private readonly Dictionary<string, Sprite> _iconDict = new(StringComparer.OrdinalIgnoreCase);
    private float _nextRefreshAt = 0f;
    private int _lastHash = 0;
#if ENABLE_INPUT_SYSTEM
    private InputAction _runtimeHoldAction;
#endif

    [Serializable] public struct IconMap { public string id; public Sprite sprite; }

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();

        // ถ้าไม่ได้ลาก slots มา ให้หาจากลูกทั้งหมด (ลำดับตาม Hierarchy)
        if (slots == null || slots.Count == 0)
            slots = new List<InventorySlotUI>(GetComponentsInChildren<InventorySlotUI>(true));

        BuildIconDict();
        if (panel) panel.SetActive(false);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        SetupHoldAction();
        var act = GetHoldAction();
        if (act != null) act.Enable();
#endif
    }
    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        var act = GetHoldAction();
        if (act != null) act.Disable();
#endif
    }

    private void Update()
    {
        // Hold to open
        bool wantOpen = IsHoldPressed();
        if (panel && panel.activeSelf != wantOpen)
        {
            panel.SetActive(wantOpen);
            if (wantOpen && refreshOnOpen) { Rebuild(); UpdateHash(); }
        }

        // Auto refresh
        if (panel && panel.activeSelf && autoRefreshWhileOpen && Time.unscaledTime >= _nextRefreshAt)
        {
            if (UpdateHashIfChanged()) Rebuild();
            _nextRefreshAt = Time.unscaledTime + autoRefreshInterval;
        }
    }

    // ---------- PUBLIC ----------
    public void RefreshNow()
    {
        Rebuild();
        UpdateHash();
    }

    // ---------- CORE ----------
    private void Rebuild()
    {
        if (!inventory || slots == null || slots.Count == 0) return;

        // ล้างทุกช่องก่อน
        foreach (var s in slots) s.SetItem(null, 0, null);

        // อ่านของทั้งหมด เรียงตามชื่อเพื่อ UI คงที่
        var all = inventory.GetAll().OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();

        int max = Mathf.Min(slots.Count, all.Count);
        for (int i = 0; i < max; i++)
        {
            var id = all[i].Key;
            var count = all[i].Value;
            var icon = ResolveIcon(id);
            slots[i].SetItem(id, count, icon);
        }
    }

    private void BuildIconDict()
    {
        _iconDict.Clear();
        foreach (var m in iconMaps)
            if (!string.IsNullOrEmpty(m.id) && m.sprite) _iconDict[m.id] = m.sprite;
    }

    private Sprite ResolveIcon(string id)
    {
        if (_iconDict.TryGetValue(id, out var sp) && sp) return sp;
        var res = Resources.Load<Sprite>($"Icons/{id}");
        if (res) { _iconDict[id] = res; return res; }
        return null;
    }

    private bool UpdateHashIfChanged()
    {
        int h = ComputeHash(inventory.GetAll());
        if (h != _lastHash) { _lastHash = h; return true; }
        return false;
    }
    private void UpdateHash() => _lastHash = ComputeHash(inventory.GetAll());

    private int ComputeHash(IReadOnlyDictionary<string, int> dict)
    {
        unchecked
        {
            int h = 17;
            foreach (var kv in dict)
            {
                h = h * 23 + kv.Key.GetHashCode();
                h = h * 23 + kv.Value.GetHashCode();
            }
            return h;
        }
    }

    // ---------- Input System ----------
#if ENABLE_INPUT_SYSTEM
    private void SetupHoldAction()
    {
        if (holdAction.reference != null) return;
        if (_runtimeHoldAction != null) return;

        if (useDefaultTabIfEmpty)
        {
            _runtimeHoldAction = new InputAction("HoldInventory", InputActionType.Button);
            _runtimeHoldAction.AddBinding("<Keyboard>/tab");
            _runtimeHoldAction.AddBinding("<Gamepad>/leftShoulder");
        }
    }
    private InputAction GetHoldAction()
    {
        if (holdAction.reference != null) return holdAction.reference.action;
        return _runtimeHoldAction;
    }
    private bool IsHoldPressed()
    {
        var act = GetHoldAction();
        return act != null && act.ReadValue<float>() > 0.5f;
    }
#else
    private bool IsHoldPressed() => false;
#endif
}
