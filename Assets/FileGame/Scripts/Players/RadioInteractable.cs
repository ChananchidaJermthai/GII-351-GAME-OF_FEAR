using UnityEngine;

[DisallowMultipleComponent]
public class RadioInteractable : MonoBehaviour
{
    [Header("Refs")]
    public RadioPlayer radio;
    public RadioInventoryUI inventoryUI;

    [Header("Duration Mode สำหรับการเล่น")]
    public RadioPlayer.DurationMode durationMode = RadioPlayer.DurationMode.ClipLength;
    public float customSeconds = 10f;

    [Header("UI Prompt")]
    public string promptText = "Press E Interact to select tape.";

    void Reset()
    {
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
#if UNITY_2023_1_OR_NEWER
        if (!inventoryUI) inventoryUI = FindFirstObjectByType<RadioInventoryUI>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        if (!inventoryUI) inventoryUI = FindObjectOfType<RadioInventoryUI>(true);
#pragma warning restore 618
#endif
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

        // ถ้า UI เปิดอยู่แล้ว ไม่เปิดซ้ำ (ป้องกัน prevLock ถูกทับ)
        if (inventoryUI.IsOpen) return;

        inventoryUI.Open(radio, durationMode, Mathf.Max(0f, customSeconds), playerGO.transform);
    }
}
