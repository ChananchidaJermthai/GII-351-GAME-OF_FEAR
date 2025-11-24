using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DiarySystem : MonoBehaviour
{
    public enum UnlockMode { ByCount, BySpecificIds }

    [Header("Inventory")]
    public InventoryLite inventory;
    public UnlockMode unlockMode = UnlockMode.ByCount;
    [Tooltip("ใช้เมื่อ UnlockMode = ByCount")]
    public string unlockItemId = "Diary";

    [Header("UI Root")]
    public GameObject diaryPanel;
    public Key toggleKey = Key.B; // ปุ่มเปิด/ปิด Diary
    public AudioSource audioSrc;
    public AudioClip sfxPageTurn;

    [Header("Pages")]
    public List<PageBinding> pages = new();

    [Header("Diary Toast (First Pickup)")]
    public DiaryFirstPickupToast firstPickupToast;

    [Header("Auto Refresh (Optional)")]
    public float refreshWhileOpenSec = 0.25f;

    [Serializable]
    public class PageBinding
    {
        public RectTransform panelRoot;
        public TMP_Text pageTitleOverride;
        [Tooltip("ใช้ UnlockMode.BySpecificIds")]
        public string requiredUnlockItemId = "";
        public List<ObjectiveBinding> objectives = new();
    }

    [Serializable]
    public class ObjectiveBinding
    {
        public DiaryEntryRow row;
        [TextArea(1, 3)] public string lineText;
        public string requiredItemId;
        public int requiredCount = 1;
        public string objectiveId = "";
        public bool showWorldPosition = false;
        public Transform worldRef;

        [NonSerialized] public Vector3 cachedPos;
        [NonSerialized] bool completedPersistent = false;

        const string PREF_KEY_PREFIX = "DiaryObj_";

        public void OnValidateRuntime()
        {
            if (worldRef) cachedPos = worldRef.position;
        }

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
                PlayerPrefs.DeleteKey(PREF_KEY_PREFIX + objectiveId);
        }

        public Vector3 GetPosition()
        {
            if (worldRef) cachedPos = worldRef.position;
            return cachedPos;
        }
    }

    int currentPage = 0;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (diaryPanel) diaryPanel.SetActive(false);

        // โหลด Persistent Objective
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
        if (kb != null && kb[toggleKey].wasPressedThisFrame)
            ToggleDiary();

        // Page change by mouse (แค่ถ้า panel เปิด)
        if (diaryPanel && diaryPanel.activeSelf)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) NextPage();
                if (mouse.rightButton.wasPressedThisFrame) PrevPage();
            }
        }
    }

    // ===== Event-driven =====
    public void NotifyDiaryPicked()
    {
        if (firstPickupToast != null)
            firstPickupToast.ShowOnce();

        ClampCurrentPageToUnlocked();
        ApplyActivePageOnly();
        if (diaryPanel && diaryPanel.activeSelf)
            RefreshCurrentPageRows();
    }

    public void ToggleDiary()
    {
        if (!diaryPanel || !inventory) return;
        if (inventory.GetCount(unlockItemId) <= 0) return; // ยังไม่มี Diary

        bool open = !diaryPanel.activeSelf;
        diaryPanel.SetActive(open);

        if (open)
        {
            ClampCurrentPageToUnlocked();
            ApplyActivePageOnly();
            RefreshCurrentPageRows();
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

    int GetUnlockedPageCount()
    {
        if (pages.Count == 0 || inventory == null) return 0;

        if (unlockMode == UnlockMode.ByCount)
        {
            int have = inventory.GetCount(unlockItemId);
            return Mathf.Clamp(have, 0, pages.Count);
        }
        else
        {
            int unlocked = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                string pid = pages[i].requiredUnlockItemId;
                if (!string.IsNullOrEmpty(pid) && inventory.GetCount(pid) > 0) unlocked++;
                else break;
            }
            return unlocked;
        }
    }

    void ClampCurrentPageToUnlocked()
    {
        int unlocked = GetUnlockedPageCount();
        currentPage = (unlocked <= 0) ? 0 : Mathf.Clamp(currentPage, 0, Mathf.Min(unlocked - 1, pages.Count - 1));
    }

    void ApplyActivePageOnly()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            if (!pages[i].panelRoot) continue;
            bool active = (i == Mathf.Clamp(currentPage, 0, pages.Count - 1)) && i < GetUnlockedPageCount();
            pages[i].panelRoot.gameObject.SetActive(active);
        }
    }

    void RefreshCurrentPageRows()
    {
        if (currentPage < 0 || currentPage >= pages.Count) return;
        var page = pages[currentPage];

        foreach (var obj in page.objectives)
        {
            if (!obj.row) continue;

            bool done = obj.LoadCompleted();
            if (!done && IsObjectiveCompletedNow(obj))
            {
                obj.SaveCompleted();
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

    bool IsObjectiveCompletedNow(ObjectiveBinding obj)
    {
        if (!inventory || string.IsNullOrEmpty(obj.requiredItemId) || obj.requiredCount <= 0) return false;
        return inventory.GetCount(obj.requiredItemId) >= obj.requiredCount;
    }

    void PlayPageTurn()
    {
        if (audioSrc && sfxPageTurn) audioSrc.PlayOneShot(sfxPageTurn);
    }

    [ContextMenu("Reset All Objective Persistences (Testing)")]
    void ResetAllObjectivePersistences()
    {
        foreach (var p in pages)
            foreach (var o in p.objectives)
                o?.ResetCompletedForTesting();

        if (diaryPanel && diaryPanel.activeSelf)
            RefreshCurrentPageRows();

        Debug.Log("[DiarySystem] Reset all persistent objectives.");
    }
}
