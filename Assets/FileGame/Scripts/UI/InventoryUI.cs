using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventoryUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private InventoryLite inventory;

    [Header("Panel & Layout")]
    [SerializeField] private GameObject panel;             // Panel หลัก (SetActive)
    [SerializeField] private Transform contentRoot;        // ScrollView/Viewport/Content
    [SerializeField] private GameObject itemEntryPrefab;   // พรีแฟบ 1 แถว (Image + TMP + ItemUIEntry)
    [SerializeField] private ScrollRect scrollRect;        // อ้าง ScrollRect (สำคัญ)

    [Header("Icons (ID -> Sprite)")]
    [SerializeField] private List<IconMap> iconMaps = new();

    [Header("Open/Close (Hold-to-open) - Input System")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("ปุ่มค้างเพื่อเปิด ปล่อยเพื่อปิด (Input System)")]
    [SerializeField] private InputActionProperty holdAction; // type=Button
    [SerializeField] private bool useDefaultTabIfEmpty = true; // ถ้าไม่เซ็ต action, จะผูก Tab/LB ให้เอง
#endif
    [SerializeField, Tooltip("รีเฟรชทุกครั้งที่เปิด")]
    private bool refreshOnOpen = true;

    [Header("Auto Refresh While Open")]
    [SerializeField] private bool autoRefreshWhileOpen = true;
    [SerializeField, Min(0.05f)] private float autoRefreshInterval = 0.25f;

    [Header("Mouse Wheel Scroll (Global)")]
    [SerializeField, Tooltip("เลื่อนด้วยล้อเมาส์ได้แม้ไม่เอาเมาส์ไปวางบน ScrollView")]
    private bool globalWheelScroll = true;
    [SerializeField, Min(0.1f), Tooltip("จำนวนพิกเซลต่อ 1 notch ของล้อเมาส์ (ปรับความไว)")]
    private float wheelPixelsPerNotch = 24f;

    // runtime
    private readonly Dictionary<string, Sprite> iconDict = new(StringComparer.OrdinalIgnoreCase);
    private int lastInventoryHash = 0;
    private float nextRefreshAt = 0f;

#if ENABLE_INPUT_SYSTEM
    private InputAction _runtimeHoldAction; // สร้าง runtime ถ้าไม่กำหนด holdAction
#endif

    [Serializable]
    public struct IconMap { public string id; public Sprite sprite; }

    private void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();
        if (!scrollRect && contentRoot) scrollRect = contentRoot.GetComponentInParent<ScrollRect>();
        BuildIconDict();
        if (panel) panel.SetActive(false);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        SetupHoldAction();
        var act = GetHoldActionSafe();
        if (act != null) act.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        var act = GetHoldActionSafe();
        if (act != null) act.Disable();
#endif
    }

    private void Update()
    {
        // 1) กดค้างเพื่อเปิด / ปล่อยเพื่อปิด
        bool wantOpen = IsHoldPressed();
        if (panel && panel.activeSelf != wantOpen)
        {
            panel.SetActive(wantOpen);
            if (wantOpen && refreshOnOpen) { RebuildList(); UpdateInventoryHash(); }
        }

        // 2) Auto refresh ขณะเปิด
        if (panel && panel.activeSelf && autoRefreshWhileOpen && Time.unscaledTime >= nextRefreshAt)
        {
            if (UpdateInventoryHashIfChanged()) RebuildList();
            nextRefreshAt = Time.unscaledTime + autoRefreshInterval;
        }

        // 3) ล้อเมาส์แบบ Global (ไม่ต้องโฟกัสที่ ScrollView)
        if (panel && panel.activeSelf && scrollRect && globalWheelScroll)
        {
#if ENABLE_INPUT_SYSTEM
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            float wheel = 0f;
#endif
            if (Mathf.Abs(wheel) > 0.01f)
                ScrollByPixels(wheel * wheelPixelsPerNotch);
        }
    }

    // ----- Public -----
    public void RefreshNow()
    {
        RebuildList();
        UpdateInventoryHash();
    }

    // ----- Build List -----
    private void BuildIconDict()
    {
        iconDict.Clear();
        foreach (var m in iconMaps)
            if (!string.IsNullOrEmpty(m.id) && m.sprite) iconDict[m.id] = m.sprite;
    }

    private void RebuildList()
    {
        if (!inventory || !contentRoot || !itemEntryPrefab) return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var all = inventory.GetAll(); // IReadOnlyDictionary<string,int>
        foreach (var kv in all)
        {
            var go = Instantiate(itemEntryPrefab, contentRoot);
            var row = go.GetComponent<ItemUIEntry>();
            if (!row) row = go.AddComponent<ItemUIEntry>();
            Sprite icon = iconDict.TryGetValue(kv.Key, out var sp) ? sp : null;
            row.SetData(kv.Key, kv.Value, icon);
        }

        if (scrollRect) scrollRect.verticalNormalizedPosition = 1f; // ไปบนสุด
    }

    private bool UpdateInventoryHashIfChanged()
    {
        int newHash = ComputeHash(inventory.GetAll());
        if (newHash != lastInventoryHash) { lastInventoryHash = newHash; return true; }
        return false;
    }

    private void UpdateInventoryHash()
    {
        lastInventoryHash = ComputeHash(inventory.GetAll());
    }

    private int ComputeHash(IReadOnlyDictionary<string, int> dict)
    {
        unchecked
        {
            int h = 17;
            foreach (var kv in dict)
            {
                h = h * 23 + (kv.Key != null ? kv.Key.GetHashCode() : 0);
                h = h * 23 + kv.Value.GetHashCode();
            }
            return h;
        }
    }

    // ----- Wheel scrolling by pixels (no focus required) -----
    private void ScrollByPixels(float deltaPixels)
    {
        if (!scrollRect || !scrollRect.content) return;
        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport ? scrollRect.viewport : (RectTransform)scrollRect.transform;

        // ใน ScrollRect: pos.y มากขึ้น = เลื่อนลง
        Vector2 pos = content.anchoredPosition;
        float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        pos.y = Mathf.Clamp(pos.y - deltaPixels, 0f, maxY);
        content.anchoredPosition = pos;
    }

#if ENABLE_INPUT_SYSTEM
    // ----- Input System helpers -----
    private void SetupHoldAction()
    {
        if (holdAction.reference != null) return;
        if (_runtimeHoldAction != null) return;

        if (useDefaultTabIfEmpty)
        {
            _runtimeHoldAction = new InputAction(name: "HoldInventory", type: InputActionType.Button);
            _runtimeHoldAction.AddBinding("<Keyboard>/tab");          // คีย์บอร์ด Tab
            _runtimeHoldAction.AddBinding("<Gamepad>/leftShoulder");  // จอย LB/L1
        }
    }

    private InputAction GetHoldActionSafe()
    {
        if (holdAction.reference != null) return holdAction.reference.action;
        return _runtimeHoldAction;
    }

    private bool IsHoldPressed()
    {
        var act = GetHoldActionSafe();
        return act != null && act.ReadValue<float>() > 0.5f;
    }
#else
    private bool IsHoldPressed() => false; // โปรเจกต์นี้ควรเปิด Input System อยู่แล้ว
#endif
}
