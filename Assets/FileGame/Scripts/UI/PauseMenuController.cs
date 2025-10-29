using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuRoot;        // กล่อง UI ของ Pause Menu (ซ่อน/แสดง)
    public CanvasGroup menuGroup;      // (ถ้ามี) ใช้เฟดนุ่ม ๆ ตอนเปิด/ปิด
    public float fadeSpeed = 12f;

    [Header("Input")]
    public InputActionReference pauseAction; // (แนะนำ) Action ปุ่ม Pause (เช่น ESC)
#if ENABLE_INPUT_SYSTEM
    public Key fallbackKey = Key.Escape;     // กรณีไม่ได้ตั้ง Action
#else
    public KeyCode legacyKey = KeyCode.Escape;
#endif

    [Header("Flow")]
    public string mainMenuSceneName = "";    // ชื่อซีนเมนูหลัก (เว้นว่างถ้าไม่ใช้)
    public int mainMenuSceneIndex = -1;      // หรือใช้ index (>=0)
    public bool pauseAudio = true;           // หยุดเสียงระหว่างพักเกม

    [Header("Player Control Lock")]
    public MonoBehaviour[] scriptsToDisable; // สคริปต์ควบคุมผู้เล่น/กล้องที่ต้องปิดตอนพักเกม (เช่น PlayerController3D)

    [Header("Cursor")]
    public bool unlockCursorOnPause = true;

    [Header("Debug")]
    public bool debugLogs = false;

    bool isPaused = false;
    float prevTimeScale = 1f;

    void Awake()
    {
        if (menuRoot) menuRoot.SetActive(false);
        if (menuGroup) menuGroup.alpha = 0f;
    }

    void OnEnable() { pauseAction?.action?.Enable(); }
    void OnDisable() { pauseAction?.action?.Disable(); }

    void Update()
    {
        if (ShouldTogglePause())
            TogglePause();

        // เฟด UI (ถ้ามี CanvasGroup)
        if (menuGroup)
        {
            float target = (isPaused ? 1f : 0f);
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            menuGroup.alpha = Mathf.Lerp(menuGroup.alpha, target, k);
        }
    }

    bool ShouldTogglePause()
    {
        if (pauseAction && pauseAction.action != null)
            return pauseAction.action.WasPressedThisFrame();

#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[fallbackKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(legacyKey);
#endif
    }

    public void TogglePause()
    {
        if (isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (isPaused) return;
        isPaused = true;

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (pauseAudio) AudioListener.pause = true;

        if (menuRoot) menuRoot.SetActive(true);
        if (menuGroup) menuGroup.blocksRaycasts = true;

        SetScriptsEnabled(false);

        if (unlockCursorOnPause)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (debugLogs) Debug.Log("[PauseMenu] Paused");
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale = prevTimeScale <= 0f ? 1f : prevTimeScale;
        if (pauseAudio) AudioListener.pause = false;

        if (menuGroup) { menuGroup.blocksRaycasts = false; }
        if (menuRoot) { if (!menuGroup) menuRoot.SetActive(false); }

        SetScriptsEnabled(true);

        // กลับไปล็อกเมาส์ตามเกมปกติ
        if (unlockCursorOnPause)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (debugLogs) Debug.Log("[PauseMenu] Resumed");
    }

    void SetScriptsEnabled(bool enabled)
    {
        if (scriptsToDisable == null) return;
        foreach (var mb in scriptsToDisable)
            if (mb) mb.enabled = enabled;
    }

    // ===== Buttons =====
    public void OnClick_Continue() => Resume();

    public void OnClick_MainMenu()
    {
        // เผื่อเธอใส่ปุ่มในเมนู—เรียกโหลดซีนเมนูหลัก
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else if (mainMenuSceneIndex >= 0)
            SceneManager.LoadScene(mainMenuSceneIndex);
        else
            Debug.LogWarning("[PauseMenu] mainMenuScene ไม่ได้ตั้งค่า");
    }

    public void OnClick_Exit()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
