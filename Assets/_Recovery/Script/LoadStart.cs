using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadStart : MonoBehaviour
{
    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1.0f;
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