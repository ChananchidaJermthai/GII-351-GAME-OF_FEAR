using UnityEngine;

[DisallowMultipleComponent]
public class RadioInteractable : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("อ้างอิง RadioPlayer จาก parent หรือ drag จาก inspector")]
    public RadioPlayer radio;

    [Tooltip("อ้างอิง RadioInventoryUI จาก scene, แนะนำ drag จาก inspector")]
    public RadioInventoryUI inventoryUI;

    [Header("Duration Mode สำหรับการเล่น")]
    public RadioPlayer.DurationMode durationMode = RadioPlayer.DurationMode.ClipLength;
    public float customSeconds = 10f;

    [Header("UI Prompt")]
    public string promptText = "Press E Interact to select tape.";

    // Cache for runtime optimization
    private static RadioInventoryUI _cachedInventoryUI;

    void Reset()
    {
        // อัตโนมัติค้นหา RadioPlayer จาก parent
        if (!radio) radio = GetComponentInParent<RadioPlayer>();
    }

    public void TryInteract(GameObject playerGO)
    {
        if (!radio)
        {
            radio = GetComponentInParent<RadioPlayer>();
            if (!radio)
            {
                Debug.LogError("[RadioInteractable] ไม่พบ RadioPlayer (โปรดลากอ้างอิงใน Inspector)", this);
                return;
            }
        }

        // ใช้ cached UI ถ้ามี ลดการค้นหาแบบ runtime
        if (!inventoryUI)
        {
            inventoryUI = _cachedInventoryUI;
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
            _cachedInventoryUI = inventoryUI; // cache ไว้สำหรับ interactables ตัวอื่น
        }

        if (!inventoryUI)
        {
            Debug.LogError("[RadioInteractable] ไม่พบ RadioInventoryUI ในฉาก (โปรดวาง Canvas/RadioInventoryUI แล้วลากให้เรียบร้อย)", this);
            return;
        }

        if (playerGO == null)
        {
            Debug.LogError("[RadioInteractable] playerGO เป็น null", this);
            return;
        }

        // ถ้า UI เปิดอยู่แล้ว ไม่เปิดซ้ำ
        if (inventoryUI.IsOpen) return;

        inventoryUI.Open(radio, durationMode, Mathf.Max(0f, customSeconds), playerGO.transform);
    }
}
