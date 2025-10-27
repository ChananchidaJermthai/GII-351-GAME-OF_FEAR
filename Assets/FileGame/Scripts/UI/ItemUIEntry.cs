using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// คอมโพเนนต์ของ "พรีแฟบ 1 แถว" สำหรับแสดงไอเท็มในอินเวนทอรี (ใช้ TMP)
public class ItemUIEntry : MonoBehaviour
{
    [Header("UI Refs (TMP)")]
    public Image iconImage;       // ไอคอน
    public TMP_Text nameText;     // ชื่อไอเท็ม (เช่น Heal, Battery)
    public TMP_Text countText;    // จำนวน xN

    [ContextMenu("Auto Bind (try)")]
    public void TryAutoBind()
    {
        if (!iconImage) iconImage = GetComponentInChildren<Image>(true);

        if (!nameText || !countText)
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps)
            {
                var nm = t.name.ToLowerInvariant();
                if (!nameText && nm.Contains("name")) nameText = t;
                if (!countText && nm.Contains("count")) countText = t;
            }
            // ถ้ายังไม่เจอ ก็ตั้งสำรอง
            if (!nameText && tmps.Length > 0) nameText = tmps[0];
            if (!countText && tmps.Length > 1) countText = tmps[1];
        }
    }

    public void SetData(string itemId, int count, Sprite icon)
    {
        if (!iconImage || !nameText || !countText) TryAutoBind();

        if (iconImage && icon) iconImage.sprite = icon;
        if (nameText) nameText.text = itemId;
        if (countText) countText.text = "x" + count.ToString();
    }
}
