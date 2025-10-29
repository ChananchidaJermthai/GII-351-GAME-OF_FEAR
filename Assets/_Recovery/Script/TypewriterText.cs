using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterText : MonoBehaviour
{
    public TMP_Text textUI;
    public float delay = 0.05f;

    private string fullText;

    void Start()
    {
        fullText = textUI.text;
        textUI.text = "";
        StartCoroutine(ShowText());
    }

    IEnumerator ShowText()
    {
        foreach (char c in fullText)
        {
            textUI.text += c;
            yield return new WaitForSeconds(delay);
        }
    }
}
