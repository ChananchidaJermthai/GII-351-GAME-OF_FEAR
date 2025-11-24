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
    private readonly Dictionary<string, Sprite> _iconDict =
        new(StringComparer.OrdinalIgnoreCase);

    private float _nextRefreshAt = 0f;
    private int _lastHash = 0;

    // 🔹 buffer สำหรับ sort ลด GC แทนการใช้ OrderBy().ToList()
    private readonly List<KeyValuePair<string, int>> _sortedBuffer = new();

#if ENABLE_INPUT_SYSTEM
    private InputAction _runtimeHoldAction;
#endif

    [Serializable] public struct IconMap { public string id; public Sprite sprite; }

    private static readonly IComparer<KeyValuePair<string, int>> _keyComparer =
        Comparer<KeyValuePair<string, int>>.Create(
            (a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)
        );

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();

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
        bool wantOpen = IsHoldPressed();
        if (panel && panel.activeSelf != wantOpen)
        {
            panel.SetActive(wantOpen);
            if (wantOpen && refreshOnOpen)
            {
                Rebuild();
                UpdateHash();
            }
        }

        if (panel && panel.activeSelf && autoRefreshWhileOpen && Time.unscaledTime >= _nextRefreshAt)
        {
            if (UpdateHashIfChanged())
                Rebuild();

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

        foreach (var s in slots)
            s.SetItem(null, 0, null);

        // ✨ เดิมใช้ OrderBy + ToList → ตอนนี้ใช้ buffer + Sort เพื่อลด GC
        _sortedBuffer.Clear();
        var dict = inventory.GetAll();
        foreach (var kv in dict)
            _sortedBuffer.Add(kv);

        _sortedBuffer.Sort(_keyComparer);

        int max = Mathf.Min(slots.Count, _sortedBuffer.Count);
        for (int i = 0; i < max; i++)
        {
            var id = _sortedBuffer[i].Key;
            var count = _sortedBuffer[i].Value;
            var icon = ResolveIcon(id);
            slots[i].SetItem(id, count, icon);
        }
    }

    private void BuildIconDict()
    {
        _iconDict.Clear();
        foreach (var m in iconMaps)
            if (!string.IsNullOrEmpty(m.id) && m.sprite)
                _iconDict[m.id] = m.sprite;
    }

    private Sprite ResolveIcon(string id)
    {
        if (_iconDict.TryGetValue(id, out var sp) && sp) return sp;
        var res = Resources.Load<Sprite>($"Icons/{id}");
        if (res)
        {
            _iconDict[id] = res;
            return res;
        }
        return null;
    }

    private bool UpdateHashIfChanged()
    {
        int h = ComputeHash(inventory.GetAll());
        if (h != _lastHash) { _lastHash = h; return true; }
        return false;
    }

    private void UpdateHash()
    {
        _lastHash = ComputeHash(inventory.GetAll());
    }

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
            _runtimeHoldAction = new InputAction("HoldInventoryGrid", InputActionType.Button);
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
