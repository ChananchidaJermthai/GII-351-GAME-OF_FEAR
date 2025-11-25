using UnityEngine;
using TMPro;

public class DiaryEntryRow : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text mainText;     // ลาก TMP_Text ของบรรทัดหลัก
    public TMP_Text subText;      // (ไม่บังคับ) บรรทัดย่อย—ไว้โชว์พิกัด ฯลฯ

    public void Set(string text, bool completed, string sub = null)
    {
        if (mainText)
        {
            mainText.text = text;
            mainText.fontStyle = completed
                ? mainText.fontStyle | FontStyles.Strikethrough
                : mainText.fontStyle & ~FontStyles.Strikethrough;
        }

        if (subText)
        {
            if (string.IsNullOrEmpty(sub)) subText.gameObject.SetActive(false);
            else { subText.gameObject.SetActive(true); subText.text = sub; }
        }
    }
}
