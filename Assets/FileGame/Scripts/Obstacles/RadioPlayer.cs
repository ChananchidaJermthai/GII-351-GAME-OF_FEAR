using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("สุ่มทิศทาง (+/-) ใหม่ทุกครั้งเมื่อ effect = Random")]
    public bool randomizeEachPlay = true;

    [Header("Optional: Battery System")]
    public bool useBattery = false;
    public string batteryKeyId = "Battery";
    [Tooltip("เวลาต่อแบตฯ 1 ก้อน (วินาที) ระหว่างเล่น")]
    public float secondsPerBattery = 30f;

    [Header("Effect Range (ต้องอยู่ในระยะเสียงถึงจะมีผล)")]
    [Tooltip("ถ้าปิด = เพิ่ม/ลด Sanity แม้อยู่ไกล; ถ้าเปิด = ต้องอยู่ในระยะเสียง")]
    public bool requireInRange = true;

    [Tooltip("ใช้รัศมีจาก AudioSource.maxDistance (เหมาะกับ Spatial 3D)")]
    public bool useAudioSourceMaxDistance = true;

    [Min(0f), Tooltip("รัศมีเอฟเฟกต์แบบกำหนดเอง (ถ้าไม่ใช้ maxDistance)")]
    public float effectRadius = 10f;

    [Min(0f), Tooltip("ฮิสเทอรีซิสกันกระพริบ เมื่อเข้า/ออกขอบรัศมี (เมตร)")]
    public float rangeHysteresis = 0.5f;

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

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        // หา InventoryLite ของผู้เล่น
#if UNITY_2023_1_OR_NEWER
        if (!playerInventory)
            playerInventory = FindFirstObjectByType<InventoryLite>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        if (!playerInventory)
            playerInventory = FindObjectOfType<InventoryLite>(true);
#pragma warning restore 618
#endif

        // หา SanityApplier
#if UNITY_2023_1_OR_NEWER
        if (!sanityTarget)
            sanityTarget = FindFirstObjectByType<SanityApplier>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        if (!sanityTarget)
            sanityTarget = FindObjectOfType<SanityApplier>(true);
#pragma warning restore 618
#endif
    }

    // ===== Public controls =====

    public void StopTape()
    {
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = null;

        if (audioSource && audioSource.isPlaying) audioSource.Stop();
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

    // ===== Core =====

    void StartTape(TapeConfig cfg, DurationMode mode, float customSeconds)
    {
        if (_playRoutine != null) StopTape(); // ปิดของเดิมก่อน

        if (!cfg.clip) { onNoItem?.Invoke("เทปนี้ไม่มี AudioClip"); return; }
        if (!playerInventory) { onNoItem?.Invoke("ไม่พบ InventoryLite ของผู้เล่น"); return; }

        // ต้องมีเทปในคลัง
        if (!playerInventory.Consume(cfg.tapeKeyId, 1))
        { onNoItem?.Invoke($"ไม่มีเทป {cfg.tapeKeyId}"); return; }

        // แบตเตอรี่ (ถ้าเปิด)
        if (useBattery)
        {
            if (secondsPerBattery <= 0f) { onNoItem?.Invoke("secondsPerBattery ต้อง > 0 เมื่อใช้แบต"); return; }
            if (playerInventory.GetCount(batteryKeyId) <= 0) { onNoItem?.Invoke($"ไม่มีแบตเตอรี่ ({batteryKeyId})"); return; }
            _batteryTimer = secondsPerBattery;
            if (!playerInventory.Consume(batteryKeyId, 1)) { onNoItem?.Invoke($"ไม่มีแบตเตอรี่ ({batteryKeyId})"); return; }
        }

        if (cfg.effect == SanityEffectType.Random && randomizeEachPlay)
            _randomSignThisPlay = Random.value < 0.5f ? -1 : +1;

        _currentTape = cfg;
        audioSource.clip = cfg.clip;
        audioSource.loop = false;
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
            bool shouldContinue =
                (mode == DurationMode.ClipLength) ? (audioSource && audioSource.isPlaying)
                                                  : (elapsed < customSeconds);
            if (!shouldContinue) break;

            // --- Battery ---
            if (useBattery)
            {
                _batteryTimer -= Time.deltaTime;
                if (_batteryTimer <= 0f)
                {
                    _batteryTimer += secondsPerBattery;
                    if (!playerInventory.Consume(batteryKeyId, 1))
                    { onNoItem?.Invoke($"แบตเตอรี่ ({batteryKeyId}) หมด"); break; }
                }
            }

            // --- Range gating ---
            bool inRangeNow = true;
            if (requireInRange && sanityTarget)
            {
                float r = GetEffectiveRadius();
                float d = Vector3.Distance(sanityTarget.transform.position, transform.position);

                // hysteresis: ออกจากระยะเมื่อ d > r, กลับเข้าระยะเมื่อ d < r - rangeHysteresis
                if (_wasInRange)
                    inRangeNow = d <= r;                       // ออกเมื่อเกิน r
                else
                    inRangeNow = d <= Mathf.Max(0f, r - rangeHysteresis); // เข้าเมื่อ < r - hysteresis
            }

            _wasInRange = inRangeNow;

            // --- Sanity per frame (เฉพาะเมื่ออยู่ในระยะ) ---
            if (inRangeNow)
            {
                float perSec = Mathf.Max(0f, cfg.amountPerSecond);
                if (perSec > 0f && sanityTarget)
                {
                    float sign = cfg.effect switch
                    {
                        SanityEffectType.Increase => +1f,
                        SanityEffectType.Decrease => -1f,
                        SanityEffectType.Random => _randomSignThisPlay,
                        _ => 0f
                    };
                    float delta = sign * perSec * Time.deltaTime;
                    sanityTarget.AddSanity(delta);
                }
            }

            elapsed += Time.deltaTime;

            // โหมด CustomSeconds: ถ้าเสียงยาวกว่าเวลาที่กำหนด ให้ตัดเสียงเมื่อครบเวลา
            if (mode == DurationMode.CustomSeconds && audioSource && audioSource.isPlaying && elapsed >= customSeconds)
                audioSource.Stop();

            yield return null;
        }

        _currentTape = null;
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        _playRoutine = null;
        onPlayStopped?.Invoke();
    }

    float GetEffectiveRadius()
    {
        if (useAudioSourceMaxDistance && audioSource)
            return audioSource.maxDistance; // ใช้ค่าจากเสียง (ต้องตั้ง Spatial Blend ~1)
        return effectRadius;
    }

    // Gizmo ให้เห็นรัศมีเอฟเฟกต์ใน Scene
    void OnDrawGizmosSelected()
    {
        if (!requireInRange) return;
        float r = effectRadius;
        var src = GetComponent<AudioSource>();
        if (useAudioSourceMaxDistance && src) r = src.maxDistance;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, r);
        if (rangeHysteresis > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, r - rangeHysteresis));
        }
    }
}
