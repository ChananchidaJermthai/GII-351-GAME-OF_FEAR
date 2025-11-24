using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class DiaryEntryRow : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text mainText;     // ลาก TMP_Text ของบรรทัดหลัก
    public TMP_Text subText;      // (ไม่บังคับ) บรรทัดย่อย—ไว้โชว์พิกัด ฯลฯ

    /// <summary>
    /// ตั้งค่าบรรทัด entry ของไดอารี่
    /// </summary>
    /// <param name="text">ข้อความหลัก</param>
    /// <param name="completed">ทำสำเร็จหรือไม่</param>
    /// <param name="sub">ข้อความย่อย (optional)</param>
    public void Set(string text, bool completed, string sub = null)
    {
        if (mainText)
        {
            mainText.text = text;

            // ปรับ FontStyle ให้มี/ไม่มี Strikethrough
            mainText.fontStyle = completed
                ? mainText.fontStyle | FontStyles.Strikethrough
                : mainText.fontStyle & ~FontStyles.Strikethrough;
        }

        if (subText)
        {
            bool hasSub = !string.IsNullOrEmpty(sub);
            subText.gameObject.SetActive(hasSub);

            if (hasSub)
                subText.text = sub;
        }
    }
}
