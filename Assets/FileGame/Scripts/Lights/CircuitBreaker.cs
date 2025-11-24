using System.Collections;
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
    public GameObject messageRoot;  
    public TMP_Text messageText;
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

        if (autoCollectLights)
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
            if (l && ((1 << l.gameObject.layer) & lightLayers.value) != 0)
                _collected.Add(l);
        }

        foreach (var l in extraLights)
        {
            if (l && !_collected.Contains(l))
                _collected.Add(l);
        }
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

        int count = SafeGetCount(playerInventory, fuseKeyId);
        if (count <= 0)
        {
            ShowMessage("You need a fuse to restore power.");
            return;
        }

        if (consumeFuseOnUse)
        {
            if (!TryConsume(playerInventory, fuseKeyId, 1))
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

    private IEnumerator MessageRoutine(string text)
    {
        messageRoot.SetActive(true);
        messageText.text = text;
        yield return new WaitForSeconds(messageDuration);
        messageRoot.SetActive(false);
    }

    // ---------------- Helpers ----------------
    int SafeGetCount(InventoryLite inv, string key)
    {
        if (inv == null || string.IsNullOrEmpty(key)) return 0;

        var mi = inv.GetType().GetMethod("GetCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        mi = inv.GetType().GetMethod("CountOf", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        if (mi != null && mi.ReturnType == typeof(int)) return (int)mi.Invoke(inv, new object[] { key });

        return 0;
    }

    bool TryConsume(InventoryLite inv, string key, int amount)
    {
        if (inv == null || string.IsNullOrEmpty(key) || amount <= 0) return false;

        var miB = inv.GetType().GetMethod("Consume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miB != null && miB.ReturnType == typeof(bool)) return (bool)miB.Invoke(inv, new object[] { key, amount });

        miB = inv.GetType().GetMethod("Remove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miB != null && miB.ReturnType == typeof(bool)) return (bool)miB.Invoke(inv, new object[] { key, amount });

        var miV = inv.GetType().GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        if (miV != null && miV.ReturnType == typeof(void))
        {
            miV.Invoke(inv, new object[] { key, -amount });
            return true;
        }

        return false;
    }
}
