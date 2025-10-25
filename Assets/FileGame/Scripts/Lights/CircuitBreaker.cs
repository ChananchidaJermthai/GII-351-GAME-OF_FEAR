using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

[DisallowMultipleComponent]
public class CircuitBreaker : MonoBehaviour
{
    [Header("Lights to control")]
    public LayerMask lightLayers;
    public bool autoCollectLights = true;
    public List<Light> extraLights = new List<Light>();

    [Header("On Start")]
    public bool turnOffOnStart = true;

    [Header("Fuse / Inventory")]
    public InventoryLite playerInventory;
    public string fuseKeyId = "Fuse";
    public bool consumeFuseOnUse = true;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip powerOnSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("UI Feedback")]
    public GameObject messageRoot;     // Canvas / Panel
    public TMP_Text messageText;       // TextMeshPro สำหรับข้อความ
    [Tooltip("เวลาที่ข้อความจะหายไปหลังแสดง (วินาที)")]
    public float messageDuration = 2f;

    [Header("Events")]
    public UnityEvent onPowerRestored;

    private readonly List<Light> _collected = new List<Light>();
    private bool _powerRestored = false;
    private Coroutine _msgCoroutine;

    void Awake()
    {
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }

#if UNITY_2023_1_OR_NEWER
        if (!playerInventory)
            playerInventory = FindFirstObjectByType<InventoryLite>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        if (!playerInventory)
            playerInventory = FindObjectOfType<InventoryLite>(true);
#pragma warning restore 618
#endif

        CollectLights();
        if (turnOffOnStart)
            ApplyPower(false);

        if (messageRoot) messageRoot.SetActive(false);
    }

    void CollectLights()
    {
#if UNITY_2023_1_OR_NEWER
        var all = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var all = GameObject.FindObjectsOfType<Light>(false);
#endif
        _collected.Clear();
        foreach (var l in all)
        {
            if (((1 << l.gameObject.layer) & lightLayers.value) != 0)
                _collected.Add(l);
        }
        foreach (var l in extraLights)
            if (l && !_collected.Contains(l))
                _collected.Add(l);
    }

    void ApplyPower(bool on)
    {
        foreach (var l in _collected)
            if (l) l.enabled = on;
    }

    public void TryInteract(GameObject interactor)
    {
        if (_powerRestored) return;

        if (!playerInventory)
        {
            ShowMessage("No player inventory found.");
            return;
        }

        int count = playerInventory.GetCount(fuseKeyId);
        if (count <= 0)
        {
            ShowMessage("You need a fuse to restore power.");
            return;
        }

        if (consumeFuseOnUse)
        {
            bool ok = playerInventory.Consume(fuseKeyId, 1);
            if (!ok)
            {
                ShowMessage("Fuse could not be used.");
                return;
            }
        }

        ApplyPower(true);
        _powerRestored = true;

        if (powerOnSfx && audioSource)
            audioSource.PlayOneShot(powerOnSfx, sfxVolume);

        onPowerRestored?.Invoke();
        ShowMessage("Power has been restored!");
    }

    private void ShowMessage(string text)
    {
        if (!messageRoot || !messageText) return;
        if (_msgCoroutine != null) StopCoroutine(_msgCoroutine);
        _msgCoroutine = StartCoroutine(MessageRoutine(text));
    }

    private System.Collections.IEnumerator MessageRoutine(string text)
    {
        messageRoot.SetActive(true);
        messageText.text = text;
        yield return new WaitForSeconds(messageDuration);
        messageRoot.SetActive(false);
    }
}
