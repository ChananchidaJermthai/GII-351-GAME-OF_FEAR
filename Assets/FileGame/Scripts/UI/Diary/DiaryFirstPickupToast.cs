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
    public float showDuration = 1.5f;        // ตั้ง 1–2 วินาทีตามต้องการ

    [Header("Optional")]
    public AudioSource audioSrc;
    public AudioClip sfxShow;                // เสียงตอนขึ้นข้อความ (ถ้ามี)

    bool _alreadyShown;                      // กันแสดงซ้ำ
    int _lastCount;

    void Awake()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryLite>();
        if (toastRoot) toastRoot.SetActive(false);
        if (toastText) toastText.text = message;

        // บันทึกจำนวนเริ่มต้น เพื่อดูการเปลี่ยนจาก 0 -> 1
        _lastCount = inventory ? inventory.GetCount(diaryItemId) : 0;
        _alreadyShown = _lastCount >= requiredCount; // ถ้าเริ่มเกมมีอยู่แล้ว จะไม่แสดงซ้ำ
    }

    void Update()
    {
        if (_alreadyShown || inventory == null) return;

        int now = inventory.GetCount(diaryItemId);
        if (_lastCount < requiredCount && now >= requiredCount)
        {
            // เปลี่ยนผ่าน threshold → แสดงครั้งเดียว
            ShowOnce();
        }
        _lastCount = now;
    }

    /// เรียกเองจากสคริปต์เก็บของก็ได้ (ถ้าอยากทริกเกอร์ตรงนั้น)
    public void ShowOnce()
    {
        if (_alreadyShown) return;
        _alreadyShown = true;
        StartCoroutine(Co_ShowToast());
    }

    IEnumerator Co_ShowToast()
    {
        if (toastText) toastText.text = message;
        if (toastRoot) toastRoot.SetActive(true);
        if (audioSrc && sfxShow) audioSrc.PlayOneShot(sfxShow);

        yield return new WaitForSeconds(showDuration);

        if (toastRoot) toastRoot.SetActive(false);
    }
}
