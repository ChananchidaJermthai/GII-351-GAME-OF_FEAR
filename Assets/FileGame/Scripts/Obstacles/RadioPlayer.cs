using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class RadioPlayer : MonoBehaviour
{
    public enum SanityEffectType { Increase, Decrease, Random }
    public enum DurationMode { ClipLength, CustomSeconds }

    [System.Serializable]
    public class TapeConfig
    {
        [Header("Item Key (ใน InventoryLite)")]
        public string tapeKeyId = "Tape1";
        [Header("เสียงของเทป")]
        public AudioClip clip;
        [Header("เอฟเฟกต์กับ Sanity")]
        public SanityEffectType effect = SanityEffectType.Increase;
        [Tooltip("Sanity ต่อวินาที (เช่น 5 = +5/s ถ้า Increase; -5/s ถ้า Decrease)")]
        public float amountPerSecond = 5f;
        [Header("ชื่อสำหรับ UI")]
        public string displayName = "Tape 1";
    }

    [Header("References")]
    public InventoryLite playerInventory;
    public SanityApplier sanityTarget;
    public AudioSource audioSource;

    [Header("Tape List")]
    public List<TapeConfig> tapes = new List<TapeConfig>();

    [Header("Options")]
    public bool randomizeEachPlay = true;

    [Header("Optional: Battery System")]
    public bool useBattery = false;
    public string batteryKeyId = "Battery";
    public float secondsPerBattery = 30f;

    [Header("Effect Range")]
    public bool requireInRange = true;
    public bool useAudioSourceMaxDistance = true;
    [Min(0f)] public float effectRadius = 10f;
    [Min(0f)] public float rangeHysteresis = 0.5f;

    [Header("3D Audio")]
    public bool use3DAudio = true;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    [Min(0.01f)] public float minDistance = 1f;
    [Min(0.02f)] public float maxDistance = 20f;
    public AnimationCurve customRolloff = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public bool disableDoppler = true;

    [Header("Cut Audio Outside Range")]
    public bool muteOutsideRange = true;

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("UI & Events")]
    public UnityEvent<string> onPlayStartedDisplay;
    public UnityEvent onPlayStopped;
    public UnityEvent<string> onNoItem;

    // runtime
    Coroutine _playRoutine;
    TapeConfig _currentTape;
    float _batteryTimer;
    int _randomSignThisPlay = +1;
    bool _wasInRange = false;
    float _storedVolume = 1f;

    // cached reflection for InventoryLite
    MethodInfo _miGetCount, _miConsume, _miRemove, _miAdd;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        Apply3DAudioSettings();
    }

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        Apply3DAudioSettings();

#if UNITY_2023_1_OR_NEWER
        if (!playerInventory) playerInventory = FindFirstObjectByType<InventoryLite>(FindObjectsInactive.Include);
        if (!sanityTarget) sanityTarget = FindFirstObjectByType<SanityApplier>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        if (!playerInventory) playerInventory = FindObjectOfType<InventoryLite>(true);
        if (!sanityTarget)    sanityTarget    = FindObjectOfType<SanityApplier>(true);
#pragma warning restore 618
#endif
        CacheInventoryMethods();
    }

    void OnDestroy()
    {
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = null;
        _currentTape = null;
    }

#if UNITY_EDITOR
    void OnValidate() { Apply3DAudioSettings(); }
#endif

    void CacheInventoryMethods()
    {
        if (!playerInventory) return;
        var type = playerInventory.GetType();
        _miGetCount = type.GetMethod("GetCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
        _miConsume = type.GetMethod("Consume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        _miRemove = type.GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
        _miAdd = type.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
    }

    public void StopTape()
    {
        if (_playRoutine != null) { StopCoroutine(_playRoutine); _playRoutine = null; }
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        if (audioSource) audioSource.mute = false;

        _currentTape = null;
        onPlayStopped?.Invoke();
    }

    public void UseTape(string tapeKeyId) =>
        UseTapeWithMode(tapeKeyId, DurationMode.ClipLength, 0f);

    public void UseTapeIndex(int index) =>
        UseTapeIndexWithMode(index, DurationMode.ClipLength, 0f);

    public void UseTapeWithMode(string tapeKeyId, DurationMode mode, float customSeconds)
    {
        var cfg = tapes.Find(t => t.tapeKeyId == tapeKeyId);
        if (cfg == null) { onNoItem?.Invoke($"ไม่มีเทป {tapeKeyId} ในรายการ"); return; }
        StartTape(cfg, mode, customSeconds);
    }

    public void UseTapeIndexWithMode(int index, DurationMode mode, float customSeconds)
    {
        if (index < 0 || index >= tapes.Count) { onNoItem?.Invoke("Tape index ไม่ถูกต้อง"); return; }
        StartTape(tapes[index], mode, customSeconds);
    }

    void StartTape(TapeConfig cfg, DurationMode mode, float customSeconds)
    {
        if (_playRoutine != null) StopTape();
        if (!cfg.clip) { onNoItem?.Invoke("เทปนี้ไม่มี AudioClip"); return; }
        if (!playerInventory) { onNoItem?.Invoke("ไม่พบ InventoryLite ของผู้เล่น"); return; }

        if (!TryConsume(playerInventory, cfg.tapeKeyId, 1)) { onNoItem?.Invoke($"ไม่มีเทป {cfg.tapeKeyId}"); return; }
        if (debugLogs) Debug.Log($"[RadioPlayer] Consumed 1x {cfg.tapeKeyId}");

        if (useBattery)
        {
            if (secondsPerBattery <= 0f) { onNoItem?.Invoke("secondsPerBattery ต้อง > 0"); return; }
            if (!TryConsume(playerInventory, batteryKeyId, 1)) { onNoItem?.Invoke($"ไม่มีแบตเตอรี่ ({batteryKeyId})"); return; }
            _batteryTimer = secondsPerBattery;
            if (debugLogs) Debug.Log($"[RadioPlayer] Consumed 1x {batteryKeyId} (timer={_batteryTimer}s)");
        }

        if (cfg.effect == SanityEffectType.Random && randomizeEachPlay)
            _randomSignThisPlay = Random.value < 0.5f ? -1 : +1;

        _currentTape = cfg;
        _storedVolume = audioSource.volume;

        audioSource.clip = cfg.clip;
        audioSource.loop = false;
        audioSource.mute = false;
        audioSource.Play();

        onPlayStartedDisplay?.Invoke(string.IsNullOrEmpty(cfg.displayName) ? cfg.tapeKeyId : cfg.displayName);

        _playRoutine = StartCoroutine(Co_PlayTape(cfg, mode, Mathf.Max(0f, customSeconds)));
    }

    IEnumerator Co_PlayTape(TapeConfig cfg, DurationMode mode, float customSeconds)
    {
        float elapsed = 0f;
        _wasInRange = false;

        while (true)
        {
            bool shouldContinue = (mode == DurationMode.ClipLength) ? (audioSource && audioSource.isPlaying)
                                                                  : (elapsed < customSeconds);
            if (!shouldContinue) break;

            if (useBattery)
            {
                _batteryTimer -= Time.deltaTime;
                if (_batteryTimer <= 0f)
                {
                    _batteryTimer += secondsPerBattery;
                    if (!TryConsume(playerInventory, batteryKeyId, 1)) { onNoItem?.Invoke($"แบตเตอรี่ ({batteryKeyId}) หมด"); break; }
                    if (debugLogs) Debug.Log($"[RadioPlayer] Consumed 1x {batteryKeyId} (extend {secondsPerBattery}s)");
                }
            }

            bool inRangeNow = true;
            if (requireInRange && sanityTarget)
            {
                float r = GetEffectiveRadius();
                float d = Vector3.Distance(sanityTarget.transform.position, transform.position);
                inRangeNow = _wasInRange ? (d <= r) : (d <= Mathf.Max(0f, r - rangeHysteresis));
            }

            if (muteOutsideRange && audioSource)
            {
                if (inRangeNow && !_wasInRange) { audioSource.mute = false; audioSource.volume = _storedVolume; }
                else if (!inRangeNow && _wasInRange) { _storedVolume = audioSource.volume; audioSource.mute = true; }
            }

            _wasInRange = inRangeNow;

            if (inRangeNow && cfg.amountPerSecond > 0f && sanityTarget)
            {
                float sign = cfg.effect switch
                {
                    SanityEffectType.Increase => +1f,
                    SanityEffectType.Decrease => -1f,
                    SanityEffectType.Random => _randomSignThisPlay,
                    _ => 0f
                };
                sanityTarget.AddSanity(sign * cfg.amountPerSecond * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            if (mode == DurationMode.CustomSeconds && audioSource && audioSource.isPlaying && elapsed >= customSeconds)
                audioSource.Stop();

            yield return null;
        }

        _currentTape = null;
        if (audioSource)
        {
            audioSource.mute = false;
            audioSource.volume = _storedVolume;
            if (audioSource.isPlaying) audioSource.Stop();
        }
        _playRoutine = null;
        onPlayStopped?.Invoke();
    }

    bool TryConsume(InventoryLite inv, string key, int amount)
    {
        if (inv == null || string.IsNullOrEmpty(key) || amount <= 0) return false;

        int count = _miGetCount != null ? (int)_miGetCount.Invoke(inv, new object[] { key }) : int.MaxValue;
        if (count < amount) { if (debugLogs) Debug.LogWarning($"[RadioPlayer] Not enough '{key}' in inventory ({count}/{amount})"); return false; }

        if (_miConsume != null && (bool)_miConsume.Invoke(inv, new object[] { key, amount })) return true;
        if (_miRemove != null && (bool)_miRemove.Invoke(inv, new object[] { key, amount })) return true;
        if (_miAdd != null) { _miAdd.Invoke(inv, new object[] { key, -amount }); return true; }

        if (debugLogs) Debug.LogWarning($"[RadioPlayer] Consume failed for '{key}'", inv);
        return false;
    }

    float GetEffectiveRadius() => useAudioSourceMaxDistance && audioSource ? audioSource.maxDistance : effectRadius;

    void Apply3DAudioSettings()
    {
        if (!audioSource) return;
        audioSource.spatialBlend = use3DAudio ? 1f : 0f;
        audioSource.rolloffMode = rolloffMode;

        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;

        if (rolloffMode == AudioRolloffMode.Custom)
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloff);

        if (disableDoppler) audioSource.dopplerLevel = 0f;
    }

    void OnDrawGizmosSelected()
    {
        if (!requireInRange) return;
        float r = effectRadius;
        if (useAudioSourceMaxDistance && audioSource) r = audioSource.maxDistance;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, r);
        if (rangeHysteresis > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, r - rangeHysteresis));
        }
    }
}
