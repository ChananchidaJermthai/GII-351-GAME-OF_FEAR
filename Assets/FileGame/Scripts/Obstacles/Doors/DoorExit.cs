using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DoorExit : MonoBehaviour
{
    [Header("Requirements")]
    public InventoryLite playerInventory;
    public string requiredKeyId = "KeyExit";
    public bool consumeKeyOnUse = true;

    [Header("Hold-to-Open")]
    public Key holdKey = Key.E;
    [Min(0.1f)] public float holdDuration = 1.8f;
    [Min(0f)] public float interactCooldown = 0.25f;

    [Header("UI Progress (use one)")]
    [Tooltip("ถ้าใช้ Image แบบ Filled ใส่ช่องนี้")]
    public Image holdProgressFill;
    [Tooltip("ถ้าใช้ Slider ใส่ช่องนี้ (ตั้ง min=0, max=1)")]
    public Slider holdProgressSlider;
    public bool autoHideProgress = true;

    [Header("Feedback UI / Message (optional)")]
    [Tooltip("UI script ที่มี ShowCenter(string,float) / Show(string) / SetText(string)")]
    public MonoBehaviour messageUI;

    [Header("SFX (optional)")]
    public AudioSource audioSource;
    public AudioClip sfxLocked;
    public AudioClip sfxProgressStart;
    public AudioClip sfxProgressCancel;
    public AudioClip sfxOpen;

    [Header("Exit Scene")]
    public string targetSceneName = "NextScene";
    public bool loadAdditive = false;

    [Header("Events")]
    public UnityEvent onBeginHold;
    public UnityEvent onCancelHold;
    public UnityEvent onCompleted;

    [Header("Debug")]
    public bool debugLogs = false;

    // ===== Runtime =====
    float _lastInteractAt = -999f;
    bool _isHolding = false;
    float _holdTimer = 0f;
    GameObject _playerGO;
    PlayerInput _playerInput;

    void Awake()
    {
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        SetProgressVisible(false);
        SetProgress(0f);
    }

    void Update()
    {
        if (!_isHolding) return;

        bool pressed = ReadHoldInput();
        if (!pressed)
        {
            CancelHolding();
            return;
        }

        _holdTimer += Time.deltaTime;
        SetProgress(Mathf.Clamp01(_holdTimer / holdDuration));

        if (_holdTimer >= holdDuration)
        {
            CompleteOpen();
        }
    }

    // ===== Entry point (เรียกจาก Interactable/Player Raycast) =====
    public void TryBeginInteract(GameObject playerGO)
    {
        if (Time.time - _lastInteractAt < interactCooldown)
        {
            if (debugLogs) Debug.Log("[DoorExit] Cooldown");
            return;
        }
        _lastInteractAt = Time.time;

        _playerGO = playerGO;
        if (!playerInventory && _playerGO)
            playerInventory = _playerGO.GetComponentInParent<InventoryLite>();
        if (!_playerInput && _playerGO)
            _playerInput = _playerGO.GetComponentInParent<PlayerInput>();

        if (!HasRequiredKey())
        {
            if (sfxLocked) audioSource.PlayOneShot(sfxLocked);
            ShowMsg("It's locked. You need the exit key.");
            if (debugLogs) Debug.Log("[DoorExit] No KeyExit");
            return;
        }

        BeginHolding();
    }

    // ===== Holding flow =====
    void BeginHolding()
    {
        _isHolding = true;
        _holdTimer = 0f;
        SetProgress(0f);
        SetProgressVisible(true);

        if (sfxProgressStart) audioSource.PlayOneShot(sfxProgressStart);
        onBeginHold?.Invoke();
        if (debugLogs) Debug.Log("[DoorExit] Begin holding E…");
    }

    void CancelHolding()
    {
        if (!_isHolding) return;

        _isHolding = false;
        _holdTimer = 0f;
        SetProgress(0f);
        if (autoHideProgress) SetProgressVisible(false);

        if (sfxProgressCancel) audioSource.PlayOneShot(sfxProgressCancel);
        onCancelHold?.Invoke();
        if (debugLogs) Debug.Log("[DoorExit] Holding canceled");
    }

    void CompleteOpen()
    {
        _isHolding = false;
        SetProgress(1f);
        if (autoHideProgress) SetProgressVisible(false);

        if (consumeKeyOnUse && playerInventory && !string.IsNullOrEmpty(requiredKeyId))
            playerInventory.Consume(requiredKeyId, 1);

        if (sfxOpen) audioSource.PlayOneShot(sfxOpen);
        onCompleted?.Invoke();
        if (debugLogs) Debug.Log("[DoorExit] Load scene: " + targetSceneName);

        if (!string.IsNullOrEmpty(targetSceneName))
        {
            if (loadAdditive)
                SceneManager.LoadScene(targetSceneName, LoadSceneMode.Additive);
            else
                SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }
    }

    // ===== Helpers =====
    bool HasRequiredKey()
    {
        if (!playerInventory) return false;
        if (string.IsNullOrEmpty(requiredKeyId)) return false;
        return playerInventory.GetCount(requiredKeyId) > 0;
    }

    bool ReadHoldInput()
    {
        if (_playerInput)
        {
            var act = _playerInput.actions.FindAction("InteractHold", false)
                      ?? _playerInput.actions.FindAction("Interact", false);
            if (act != null) return act.IsPressed();
        }
        var kb = Keyboard.current;
        return kb != null && kb[holdKey].isPressed;
    }

    void SetProgress(float k01)
    {
        k01 = Mathf.Clamp01(k01);
        if (holdProgressFill) holdProgressFill.fillAmount = k01;
        if (holdProgressSlider) holdProgressSlider.value = k01; // ตั้ง Slider min=0, max=1
    }

    void SetProgressVisible(bool on)
    {
        if (holdProgressFill && holdProgressFill.gameObject.activeSelf != on)
            holdProgressFill.gameObject.SetActive(on);
        if (holdProgressSlider && holdProgressSlider.gameObject.activeSelf != on)
            holdProgressSlider.gameObject.SetActive(on);
    }

    void ShowMsg(string msg)
    {
        if (!messageUI) { if (debugLogs) Debug.Log(msg); return; }

        var t = messageUI.GetType();
        var m = t.GetMethod("ShowCenter", new System.Type[] { typeof(string), typeof(float) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg, 1.5f }); return; }
        m = t.GetMethod("Show", new System.Type[] { typeof(string) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg }); return; }
        m = t.GetMethod("SetText", new System.Type[] { typeof(string) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg }); return; }

        if (debugLogs) Debug.Log(msg);
    }
}