using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    [Header("Refs")]
    public Image iconImage;          // รูปไอเท็มในช่อง
    public TMP_Text nameText;        // ชื่อไอเท็ม (เช่น Flashlight)
    public TMP_Text countText;       // จำนวน xN
    public Image slotBackground;     // พื้นหลังช่อง (เทา/โปร่งจาง)
    public Image highlight;          // ไฮไลต์ (เช่น สีส้มเมื่อมีของ) – ไม่มีก็เว้นได้

    [Header("Empty State")]
    public Sprite emptyIcon;         // รูปว่าง/โปร่ง (optional)
    public string emptyName = "";    // ชื่อเมื่อว่าง
    public bool dimEmpty = true;     // ทำให้ช่องว่างดูจางลง

    /// เติมข้อมูลลงช่อง (null/จำนวน 0 = ว่าง)
    public void SetItem(string itemId, int count, Sprite icon)
    {
        bool hasItem = !string.IsNullOrEmpty(itemId) && count > 0;

        if (hasItem)
        {
            if (iconImage) { iconImage.enabled = true; iconImage.sprite = icon; }
            if (nameText) nameText.text = itemId;
            if (countText) countText.text = count > 1 ? "x" + count.ToString() : "";
            if (highlight) highlight.enabled = true;
            if (slotBackground) slotBackground.color = new Color(1, 1, 1, 1);
        }
        else
        {
            if (iconImage)
            {
                iconImage.sprite = emptyIcon;
                iconImage.enabled = emptyIcon != null;
            }
            if (nameText) nameText.text = emptyName;
            if (countText) countText.text = "";
            if (highlight) highlight.enabled = false;
            if (slotBackground && dimEmpty) slotBackground.color = new Color(1, 1, 1, 0.35f);
        }
    }
}
