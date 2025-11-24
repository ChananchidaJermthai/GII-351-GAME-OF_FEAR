using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlotUI : MonoBehaviour
{
    [Header("Refs")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text countText;
    public Image slotBackground;
    public Image highlight;

    [Header("Empty State")]
    public Sprite emptyIcon;
    public string emptyName = "";
    public bool dimEmpty = true;

    // Runtime cache
    private string _currentItemId;
    private int _currentCount;
    private Sprite _currentIcon;

    public void SetItem(string itemId, int count, Sprite icon)
    {
        // ป้องกันการอัปเดตซ้ำ
        if (_currentItemId == itemId && _currentCount == count && _currentIcon == icon)
            return;

        _currentItemId = itemId;
        _currentCount = count;
        _currentIcon = icon;

        bool hasItem = !string.IsNullOrEmpty(itemId) && count > 0;

        // Icon
        if (iconImage)
        {
            iconImage.sprite = hasItem ? icon : emptyIcon;
            iconImage.enabled = hasItem || emptyIcon != null;
        }

        // Name
        if (nameText)
            nameText.text = hasItem ? itemId : emptyName;

        // Count
        if (countText)
            countText.text = hasItem && count > 1 ? $"x{count}" : "";

        // Highlight
        if (highlight) highlight.enabled = hasItem;

        // Background dim
        if (slotBackground)
            slotBackground.color = hasItem || !dimEmpty ? Color.white : new Color(1, 1, 1, 0.35f);
    }
}
