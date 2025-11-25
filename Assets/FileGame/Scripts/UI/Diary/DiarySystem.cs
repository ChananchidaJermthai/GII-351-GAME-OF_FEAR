using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DiarySystemPanels : MonoBehaviour
{
    public enum UnlockMode { ByCount, BySpecificIds }

    [Header("Inventory")]
    public InventoryLite inventory;
    public UnlockMode unlockMode = UnlockMode.ByCount;

    [Header("ByCount Settings")]
    [Tooltip("Count this item: 1 piece unlocks Page 1, 2 pieces unlock Page 2, ...")]
    public string unlockItemId = "Diary";

    [Header("UI Root (optional)")]
    public GameObject diaryPanel;            // แพนหลักที่รวมทุกหน้า (ไว้เปิด/ปิด)

    [Header("Paging")]
    public int currentPage = 0;
    public Key toggleKey = Key.B;            // เปลี่ยนเป็นปุ่ม B ตามที่ผู้ใช้ต้องการ

    [Header("Audio")]
    public AudioSource audioSrc;
    public AudioClip sfxPageTurn;

    [Header("Auto Refresh")]
    public float refreshWhileOpenSec = 0.25f;
    float _nextRefreshAt;

    [Header("Pages (Bind your own Panels/Rows)")]
    public List<PageBinding> pages = new();

    [Serializable]
    public class PageBinding
    {
        [Tooltip("Drag your Panel here (e.g., DiaryPage1Panel).")]
        public RectTransform panelRoot;

        [Tooltip("Title text on this page (optional). Leave empty if your page has its own title.")]
        public TMP_Text pageTitleOverride;

        [Header("Unlock (used in BySpecificIds mode)")]
        [Tooltip("e.g., \"Diary 1\", \"Diary 2\". Leave empty if using ByCount mode.")]
        public string requiredUnlockItemId = "";

        [Header("Rows on this page")]
        public List<ObjectiveBinding> objectives = new();
    }

    [Serializable]
    public class ObjectiveBinding
    {
        [Header("Row/UI")]
        public DiaryEntryRow row;                 // ลาก Row (ที่อยู่ใต้ Panel นั้น) มาใส่
        [TextArea(1, 3)] public string lineText = "Find the Fuse in Ward 1.";
        public bool showWorldPosition = false;    // ถ้าอยากโชว์พิกัด

        [Header("Completion Condition")]
        public string requiredItemId = "Fuse_Ward1";
        public int requiredCount = 1;

        [Header("Persistent Completion")]
        [Tooltip("Unique ID for this objective (e.g., Fuse_Ward1, Key_Bathroom). Persist completion even if item is consumed.")]
        public string objectiveId = "";           // ตั้งค่าใน Inspector ให้ไม่ซ้ำ
        [SerializeField] bool completedPersistent = false;

        [Header("World Ref (optional)")]
        public Transform worldRef;                // ลากจุดอ้างอิงในฉาก (สำหรับโชว์พิกัด)
        [SerializeField] Vector3 cachedPos;       // แคชพิกัด

        public Vector3 GetPosition()
        {
            if (worldRef) cachedPos = worldRef.position;
            return cachedPos;
        }

#if UNITY_EDITOR
        public void OnValidateRuntime()
        {
            if (worldRef) cachedPos = worldRef.position;
        }
#endif
        // ===== Persistent helpers =====
        const string PREF_KEY_PREFIX = "DiaryObj_";
        public bool LoadCompleted()
        {
            if (!string.IsNullOrEmpty(objectiveId))
                completedPersistent = PlayerPrefs.GetInt(PREF_KEY_PREFIX + objectiveId, 0) == 1;
            return completedPersistent;
        }
        public void SaveCompleted()
        {
            completedPersistent = true;
            if (!string.IsNullOrEmpty(objectiveId))
            {
                PlayerPrefs.SetInt(PREF_KEY_PREFIX + objectiveId, 1);
                PlayerPrefs.Save();
            }
        }
        public void ResetCompletedForTesting()
        {
            completedPersistent = false;
            if (!string.IsNullOrEmpty(objectiveId))
            {
                PlayerPrefs.DeleteKey(PREF_KEY_PREFIX + objectiveId);
            }
        }
    }

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (diaryPanel) diaryPanel.SetActive(false);

        // โหลดสถานะสำเร็จถาวรของทุก Objective ล่วงหน้า
        foreach (var p in pages)
            foreach (var o in p.objectives)
                o?.LoadCompleted();

        ClampCurrentPageToUnlocked();
        ApplyActivePageOnly();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        foreach (var p in pages)
            foreach (var o in p.objectives)
                o?.OnValidateRuntime();
    }
#endif

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) ToggleDiary();

        if (diaryPanel && diaryPanel.activeSelf)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) NextPage();
                if (mouse.rightButton.wasPressedThisFrame) PrevPage();
            }

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshCurrentPageRows();
                _nextRefreshAt = Time.unscaledTime + refreshWhileOpenSec;
            }
        }
    }

    // ===== Public =====
    public void ToggleDiary()
    {
        if (!diaryPanel) return;

        // 🔒 ถ้ายังไม่มีไอเท็ม Diary ห้ามเปิด
        if (inventory == null || inventory.GetCount(unlockItemId) <= 0)
        {
            Debug.Log("[DiarySystem] Player has no Diary item, cannot open.");
            return;
        }

        bool open = !diaryPanel.activeSelf;
        diaryPanel.SetActive(open);

        if (open)
        {
            ClampCurrentPageToUnlocked();
            ApplyActivePageOnly();
            RefreshCurrentPageRows();
            _nextRefreshAt = 0f;
        }
    }

    public void NextPage()
    {
        int unlocked = GetUnlockedPageCount();
        if (unlocked <= 0) return;

        int before = currentPage;
        currentPage = Mathf.Clamp(currentPage + 1, 0, Mathf.Min(unlocked - 1, pages.Count - 1));

        if (currentPage != before)
        {
            PlayPageTurn();
            ApplyActivePageOnly();
            RefreshCurrentPageRows();
        }
    }

    public void PrevPage()
    {
        int unlocked = GetUnlockedPageCount();
        if (unlocked <= 0) return;

        int before = currentPage;
        currentPage = Mathf.Clamp(currentPage - 1, 0, Mathf.Min(unlocked - 1, pages.Count - 1));

        if (currentPage != before)
        {
            PlayPageTurn();
            ApplyActivePageOnly();
            RefreshCurrentPageRows();
        }
    }

    // เรียกตอนผู้เล่นเก็บ Diary ชิ้นใหม่ (ถ้าปลดล็อคแบบนับจำนวน)
    public void NotifyDiaryPicked()
    {
        ClampCurrentPageToUnlocked();
        ApplyActivePageOnly();
        if (diaryPanel && diaryPanel.activeSelf) RefreshCurrentPageRows();
    }

    // ===== Core =====
    int GetUnlockedPageCount()
    {
        if (pages.Count == 0 || inventory == null) return 0;

        if (unlockMode == UnlockMode.ByCount)
        {
            if (string.IsNullOrEmpty(unlockItemId)) return 0;
            int have = inventory.GetCount(unlockItemId);
            return Mathf.Clamp(have, 0, pages.Count);
        }
        else // BySpecificIds
        {
            int unlocked = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                string pid = pages[i].requiredUnlockItemId;
                if (!string.IsNullOrEmpty(pid) && inventory.GetCount(pid) > 0) unlocked++;
                else break; // sequential unlock
            }
            return unlocked;
        }
    }

    void ClampCurrentPageToUnlocked()
    {
        int unlocked = GetUnlockedPageCount();
        if (unlocked <= 0) { currentPage = 0; return; }
        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Min(unlocked - 1, pages.Count - 1));
    }

    void ApplyActivePageOnly()
    {
        // เปิดเฉพาะ panel ของหน้าปัจจุบัน ปิดที่เหลือ
        for (int i = 0; i < pages.Count; i++)
        {
            if (!pages[i].panelRoot) continue;
            bool active = (i == Mathf.Clamp(currentPage, 0, pages.Count - 1))
                          && i < GetUnlockedPageCount();
            pages[i].panelRoot.gameObject.SetActive(active);
        }
    }

    void RefreshCurrentPageRows()
    {
        int idx = Mathf.Clamp(currentPage, 0, pages.Count - 1);
        if (idx >= pages.Count) return;

        var page = pages[idx];

        // ตั้งชื่อหน้าจาก pageTitleOverride ได้ตามต้องการ (ถ้ามี)

        foreach (var obj in page.objectives)
        {
            if (!obj.row) continue;

            // 1) โหลด/ตรวจสถานะสำเร็จถาวร
            bool done = obj.LoadCompleted();

            // 2) ถ้ายังไม่สำเร็จถาวร ให้เช็คเงื่อนไข ณ ตอนนี้
            if (!done && IsObjectiveCompletedNow(obj))
            {
                obj.SaveCompleted();   // ทำให้สำเร็จถาวร (จะคงขีดค่าตลอด)
                done = true;
            }

            string sub = null;
            if (obj.showWorldPosition)
            {
                Vector3 p = obj.GetPosition();
                sub = $"Location: {p.x:0.0}, {p.y:0.0}, {p.z:0.0}";
            }
            obj.row.Set(obj.lineText, done, sub);
        }
    }

    // เดิมชื่อ IsObjectiveCompleted -> เปลี่ยนเป็น Now ให้ความหมายชัดว่าเช็คตาม Inventory ปัจจุบัน
    bool IsObjectiveCompletedNow(ObjectiveBinding obj)
    {
        if (!inventory) return false;
        if (string.IsNullOrEmpty(obj.requiredItemId) || obj.requiredCount <= 0) return false;
        return inventory.GetCount(obj.requiredItemId) >= obj.requiredCount;
    }

    void PlayPageTurn()
    {
        if (audioSrc && sfxPageTurn) audioSrc.PlayOneShot(sfxPageTurn);
    }

    // ===== (ตัวเลือก) ฟังก์ชัน Reset สำหรับทดสอบใน Editor =====
    [ContextMenu("Reset All Objective Persistences (Testing)")]
    void __ResetAllObjectivePersistences()
    {
        foreach (var p in pages)
            foreach (var o in p.objectives)
                o?.ResetCompletedForTesting();
        if (diaryPanel && diaryPanel.activeSelf) RefreshCurrentPageRows();
        Debug.Log("[DiarySystem] Reset all persistent objective states for testing.");
    }
}
