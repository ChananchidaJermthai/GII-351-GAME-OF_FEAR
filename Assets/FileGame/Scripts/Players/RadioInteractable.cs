using UnityEngine;

[DisallowMultipleComponent]
public class RadioInteractable : MonoBehaviour
{
    [Header("Refs")]
    public RadioPlayer radio;
    public RadioInventoryUI inventoryUI;   // << ลากมา หรือสคริปต์จะพยายามหาให้

    [Header("Duration Mode สำหรับการเล่น")]
    public RadioPlayer.DurationMode durationMode = RadioPlayer.DurationMode.ClipLength;
    public float customSeconds = 10f;

    [Header("UI Prompt")]
    public string promptText = "Press E Interact to select tape.";

    void Reset()
    {
        if (!radio) radio = GetComponentInParent<RadioPlayer>();
    }

    // ถูกเรียกจาก PlayerAimPickup เมื่อผู้เล่นเล็งแล้วกด Interact
    public void TryInteract(GameObject playerGO)
    {
        // หาอัตโนมัติถ้าไม่ได้ลาก
        if (!radio)
        {
            radio = GetComponentInParent<RadioPlayer>();
            if (!radio)
            {
                Debug.LogError("[RadioInteractable] ไม่พบ RadioPlayer (โปรดลากอ้างอิงใน Inspector)", this);
                return;
            }
        }
        if (!inventoryUI)
        {
#if UNITY_2023_1_OR_NEWER
            inventoryUI = FindFirstObjectByType<RadioInventoryUI>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
            inventoryUI = FindObjectOfType<RadioInventoryUI>(true);
#pragma warning restore 618
#endif
            if (!inventoryUI)
            {
                Debug.LogError("[RadioInteractable] ไม่พบ RadioInventoryUI ในฉาก (โปรดวาง Canvas/RadioInventoryUI แล้วลากให้เรียบร้อย)", this);
                return;
            }
        }

        if (playerGO == null)
        {
            Debug.LogError("[RadioInteractable] playerGO เป็น null (ควรส่ง GameObject ผู้เล่นเข้ามา)", this);
            return;
        }

        // เปิดพาเนลเลือกเทป—แสดงเฉพาะเทปที่ "ผู้เล่นมี"
        inventoryUI.Open(radio, durationMode, Mathf.Max(0f, customSeconds), playerGO.transform);
    }
}
