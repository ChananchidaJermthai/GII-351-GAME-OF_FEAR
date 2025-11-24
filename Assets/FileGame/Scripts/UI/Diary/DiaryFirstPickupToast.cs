using System.Collections;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class DiaryFirstPickupToast : MonoBehaviour
{
    [Header("Inventory")]
    public InventoryLite inventory;          // อ้างอิง InventoryLite ของผู้เล่น
    public string diaryItemId = "Diary";     // ไอเท็มที่นับว่าคือ Diary
    public int requiredCount = 1;            // เก็บครบกี่ชิ้นถึงแสดง (ค่าเริ่ม = 1)

    [Header("UI")]
    public GameObject toastRoot;             // Panel/Container ของข้อความ (เปิด/ปิดทั้งก้อน)
    public TMP_Text toastText;               // ข้อความที่จะแสดง
    [TextArea(1, 3)] public string message = "You found a Diary page.";

    [Header("Timing")]
    [Tooltip("เวลาที่แสดง (วินาที)")]
    public float showDuration = 1.5f;

    [Header("Optional")]
    public AudioSource audioSrc;
    public AudioClip sfxShow;

    // runtime
    private bool _alreadyShown;
    private int _lastCount;

    void Awake()
    {
        // หาผู้เล่น/Inventory แบบปลอดภัย
        if (!inventory)
        {
            inventory = FindFirstObjectByType<InventoryLite>();
            if (!inventory) inventory = FindObjectOfType<InventoryLite>();
        }

        if (toastRoot) toastRoot.SetActive(false);
        if (toastText) toastText.text = message;

        _lastCount = inventory ? inventory.GetCount(diaryItemId) : 0;
        _alreadyShown = _lastCount >= requiredCount;
    }

    void Update()
    {
        if (_alreadyShown || inventory == null) return;

        int now = inventory.GetCount(diaryItemId);

        // Trigger แสดงครั้งเดียวเมื่อเก็บครบ threshold
        if (_lastCount < requiredCount && now >= requiredCount)
        {
            ShowOnce();
        }

        _lastCount = now;
    }

    /// <summary>
    /// เรียกเองจากสคริปต์ก็ได้ เพื่อแสดง toast
    /// </summary>
    public void ShowOnce()
    {
        if (_alreadyShown) return;

        _alreadyShown = true;
        StartCoroutine(Co_ShowToast());
    }

    private IEnumerator Co_ShowToast()
    {
        if (toastText) toastText.text = message;
        if (toastRoot) toastRoot.SetActive(true);

        if (audioSrc && sfxShow) audioSrc.PlayOneShot(sfxShow);

        yield return new WaitForSeconds(showDuration);

        if (toastRoot) toastRoot.SetActive(false);
    }
}
