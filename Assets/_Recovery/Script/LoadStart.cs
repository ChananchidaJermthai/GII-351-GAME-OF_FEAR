using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadStart : MonoBehaviour
{
    private void Awake()
    {
        Time.timeScale = 1.0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void Play()
    {
        SceneManager.LoadScene("WalkinScene");
    }

    public void Quit()
    {
        Application.Quit();

    }
}