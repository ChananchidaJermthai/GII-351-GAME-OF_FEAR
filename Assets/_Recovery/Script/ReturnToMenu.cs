using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToMenu : MonoBehaviour
{
    void Update()
    {
        // กด Enter = KeyCode.Return
        if (Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene("MainMenu");  // ชื่อ Scene ของหน้าเมนู
        }
    }
}
