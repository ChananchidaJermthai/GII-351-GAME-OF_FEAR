using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadStart : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("WalkinScene");
    }

    public void Quit()
    {
        Application.Quit();
    }
}