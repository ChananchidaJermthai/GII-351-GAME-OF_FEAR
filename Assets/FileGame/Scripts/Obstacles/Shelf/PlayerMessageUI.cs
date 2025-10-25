using UnityEngine;
using TMPro;

public class PlayerMessageUI : MonoBehaviour
{
    public TMP_Text messageText;
    public GameObject rootPanel;
    Coroutine _co;

    public void ShowMessage(string text, float duration = 2f)
    {
        if (!messageText) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Routine(text, duration));
    }

    System.Collections.IEnumerator Routine(string text, float dur)
    {
        rootPanel.SetActive(true);
        messageText.text = text;
        yield return new WaitForSeconds(dur);
        rootPanel.SetActive(false);
    }
}
