using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("UI")]
    public GameObject menuRoot;
    public CanvasGroup menuGroup;
    public float fadeSpeed = 12f;

    [Header("Input")]
    public InputActionReference pauseAction;
#if ENABLE_INPUT_SYSTEM
    public Key fallbackKey = Key.Escape;
#else
    public KeyCode legacyKey = KeyCode.Escape;
#endif

    [Header("Flow")]
    public string mainMenuSceneName = "";
    public int mainMenuSceneIndex = -1;
    public bool pauseAudio = true;

    [Header("Player Control Lock")]
    public MonoBehaviour[] scriptsToDisable;

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
        if (ShouldTogglePause()) TogglePause();

        // Smooth fade
        if (menuGroup)
        {
            float target = isPaused ? 1f : 0f;
            float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            menuGroup.alpha = Mathf.Lerp(menuGroup.alpha, target, k);
        }
    }

    bool ShouldTogglePause()
    {
        if (pauseAction?.action != null) return pauseAction.action.WasPressedThisFrame();

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

        Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        if (pauseAudio) AudioListener.pause = false;

        if (menuGroup) menuGroup.blocksRaycasts = false;
        if (menuRoot && menuGroup == null) menuRoot.SetActive(false);

        SetScriptsEnabled(true);

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
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else if (mainMenuSceneIndex >= 0)
            SceneManager.LoadScene(mainMenuSceneIndex);
        else
            Debug.LogWarning("[PauseMenu] mainMenuScene not set");
    }

    public void OnClick_Exit()
    {
        Time.timeScale = 1f;
        if (pauseAudio) AudioListener.pause = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
