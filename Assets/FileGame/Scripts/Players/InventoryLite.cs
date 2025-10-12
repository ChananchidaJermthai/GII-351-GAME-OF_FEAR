using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryLite : MonoBehaviour
{
    // ====== Runtime store (ใช้งานจริง) ======
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    // ====== สำหรับ "ดู" ใน Inspector ======
    [System.Serializable]
    public struct ItemEntry
    {
        public string id;
        public int count;
    }

    [Header("Inspector View (อ่านค่าได้)")]
    [SerializeField] private List<ItemEntry> view = new List<ItemEntry>();
    [SerializeField] private bool sortViewById = true;

    [Header("Initial Items (ใส่ค่าเริ่มต้นได้ถ้าต้องการ)")]
    [SerializeField] private List<ItemEntry> initialItems = new List<ItemEntry>();

    void Awake()
    {
        // โหลดค่าเริ่มต้น (ถ้าใส่ไว้ใน Inspector)
        if (initialItems != null)
        {
            foreach (var e in initialItems)
            {
                if (string.IsNullOrEmpty(e.id) || e.count <= 0) continue;
                if (!counts.ContainsKey(e.id)) counts[e.id] = 0;
                counts[e.id] += e.count;
            }
        }
        RefreshView();
    }

    // ---------- Public API ----------
    public void AddItem(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return;
        if (!counts.ContainsKey(id)) counts[id] = 0;
        counts[id] += amount;
        RefreshView();
    }

    public bool HasItem(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return false;
        return counts.TryGetValue(id, out var c) && c >= amount;
    }

    public bool Consume(string id, int amount = 1)
    {
        if (!HasItem(id, amount)) return false;
        counts[id] -= amount;
        if (counts[id] <= 0) counts.Remove(id);
        RefreshView();
        return true;
    }

    public int GetCount(string id) => counts.TryGetValue(id, out var c) ? c : 0;

    public IReadOnlyDictionary<string, int> GetAll() => counts;

    public void ClearAll()
    {
        counts.Clear();
        RefreshView();
    }

    // ---------- Inspector Helpers ----------
    [ContextMenu("Rebuild View (Editor)")]
    private void RebuildViewInEditor() => RefreshView();

    private void RefreshView()
    {
        view.Clear();
        if (counts.Count == 0) return;

        IEnumerable<KeyValuePair<string, int>> src = counts;
        if (sortViewById) src = src.OrderBy(kv => kv.Key);

        foreach (var kv in src)
        {
            view.Add(new ItemEntry { id = kv.Key, count = kv.Value });
        }
        // หมายเหตุ: view มีไว้ "ดู" อย่างเดียว ไม่เขียนกลับ counts เพื่อกันข้อมูลเพี้ยน
    }
}
