using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TitleFadeIn : MonoBehaviour
{
    public TMP_Text title;
    public float duration = 2.5f;
    public AudioSource revealSound;

    void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        Color c = title.color;
        c.a = 0;
        title.color = c;

        if (revealSound) revealSound.Play();

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0, 1, t / duration);
            title.color = c;
            yield return null;
        }
    }
}
