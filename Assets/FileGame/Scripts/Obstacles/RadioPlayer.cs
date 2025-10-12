using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// เครื่องเล่นเทป/วิทยุที่รองรับ "หลายม้วน" กำหนดได้ใน Inspector:
/// - แต่ละเทปกำหนด: Item KeyID ใน InventoryLite, AudioClip, EffectType(เพิ่ม/ลด/สุ่ม), amountPerSecond
/// - เมื่อเริ่มเล่น: จะหักเทปใน InventoryLite -1 ชิ้น
/// - ระหว่างเล่น: ส่งค่า ΔSanity ต่อวินาที ไปยัง SanityApplier ตลอดจนกว่าเสียงจะหยุด
/// - เมื่อเสียงหยุด: หยุดส่งค่า (Sanity หยุดเปลี่ยน)
/// - ตัวเลือกเสริม: ใช้แบตเตอรี่ (หักตามเวลา) — เปิด/ปิดได้
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class RadioPlayer : MonoBehaviour
{
    public enum SanityEffectType { Increase, Decrease, Random }

    [System.Serializable]
    public class TapeConfig
    {
        [Header("Item Key (ใน InventoryLite)")]
        public string tapeKeyId = "Tape1";

        [Header("เสียงของเทป")]
        public AudioClip clip;

        [Header("เอฟเฟกต์กับ Sanity")]
        public SanityEffectType effect = SanityEffectType.Increase;

        [Tooltip("จำนวน Sanity ต่อวินาที (เช่น 5 = +5/วินาที ถ้า Increase, และ -5/วินาที ถ้า Decrease)")]
        public float amountPerSecond = 5f;

        [Header("ชื่อสำหรับ UI")]
        public string displayName = "Tape 1";
    }

    [Header("References")]
    public InventoryLite playerInventory;         // ชี้ไปที่ Inventory ของผู้เล่น
    public SanityApplier sanityTarget;            // ชี้ไปที่ Player (ตัวรับ ΔSanity/เฟรม)
    public AudioSource audioSource;               // จะหาอัตโนมัติถ้าเว้นว่าง

    [Header("Tape List (ตั้งค่าเทปทั้งหมด)")]
    public List<TapeConfig> tapes = new List<TapeConfig>();

    [Header("Options")]
    [Tooltip("หากเทปที่เลือก effect=Random จะสุ่ม 'ทิศทาง' ทุกครั้งที่เริ่มเล่น")]
    public bool randomizeEachPlay = true;

    [Header("Optional: Battery System")]
    public bool useBattery = false;
    [Tooltip("Item Key ของแบตเตอรี่ใน InventoryLite")]
    public string batteryKeyId = "Battery";
    [Tooltip("ใช้แบต 1 ชิ้น ต่อระยะเวลานี้ (วินาที) ระหว่างเล่น")]
    public float secondsPerBattery = 30f;

    [Header("UI & Events (ไม่บังคับ)")]
    public UnityEvent<string> onPlayStartedDisplay;   // ส่งชื่อเทปตอนเริ่ม (displayName)
    public UnityEvent onPlayStopped;                  // หยุดเล่น (ครบเพลง/แบตหมด/กดหยุด)
    public UnityEvent<string> onNoItem;               // แจ้งข้อความเมื่อไม่มีเทป/แบต

    // --- runtime ---
    Coroutine _playRoutine;
    TapeConfig _currentTape;
    float _batteryTimer;
    int _randomSignThisPlay = +1; // สำหรับ Random mode (+1=เพิ่ม, -1=ลด)

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    [System.Obsolete]
    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!playerInventory) playerInventory = FindObjectOfType<InventoryLite>();
        if (!sanityTarget) sanityTarget = FindObjectOfType<SanityApplier>();
    }

    /// <summary>
    /// เรียกด้วย KeyID ของเทป (เช่นจากเมนู หรือกดใช้ในเกม)
    /// จะหักเทปใน Inventory -1 ถ้ามี แล้วเริ่มเล่น
    /// </summary>
    public void UseTape(string tapeKeyId)
    {
        var cfg = tapes.Find(t => t.tapeKeyId == tapeKeyId);
        if (cfg == null)
        {
            onNoItem?.Invoke($"ไม่มีเทปชื่อ {tapeKeyId} ในรายการตั้งค่า");
            return;
        }
        StartTape(cfg);
    }

    /// <summary>
    /// เรียกด้วย index ของเทปในลิสต์
    /// </summary>
    public void UseTapeIndex(int index)
    {
        if (index < 0 || index >= tapes.Count)
        {
            onNoItem?.Invoke("Tape index ไม่ถูกต้อง");
            return;
        }
        StartTape(tapes[index]);
    }

    /// <summary>
    /// หยุดเล่นเทปปัจจุบัน (ถ้ามี)
    /// </summary>
    public void StopTape()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        if (audioSource && audioSource.isPlaying)
            audioSource.Stop();

        _currentTape = null;
        onPlayStopped?.Invoke();
    }

    void StartTape(TapeConfig cfg)
    {
        if (cfg.clip == null)
        {
            onNoItem?.Invoke("เทปนี้ไม่มี AudioClip");
            return;
        }
        if (!playerInventory)
        {
            onNoItem?.Invoke("ไม่พบ InventoryLite ของผู้เล่น");
            return;
        }

        // เช็คเทปก่อน: ต้องมีของถึงจะเล่นได้
        if (!playerInventory.Consume(cfg.tapeKeyId, 1))
        {
            onNoItem?.Invoke($"ไม่มีเทป {cfg.tapeKeyId}");
            return;
        }

        // ถ้าเปิดแบตเตอรี่: ตรวจสอบว่ามีแบตหรือกำหนด secondsPerBattery > 0
        if (useBattery)
        {
            if (secondsPerBattery <= 0f)
            {
                onNoItem?.Invoke("secondsPerBattery ต้อง > 0 เมื่อเปิดใช้ Battery");
                return;
            }
            // เตรียมตัวนับแบตช่วงแรก: ถ้าไม่มีแบตเลยให้แจ้งเตือน
            if (playerInventory.GetCount(batteryKeyId) <= 0)
            {
                onNoItem?.Invoke($"ไม่มีแบตเตอรี่ ({batteryKeyId})");
                return;
            }
            _batteryTimer = secondsPerBattery;
            // เริ่มต้นให้หักแบต 1 ชิ้นทันที (ตอนเริ่มเล่น)
            if (!playerInventory.Consume(batteryKeyId, 1))
            {
                onNoItem?.Invoke($"ไม่มีแบตเตอรี่ ({batteryKeyId})");
                return;
            }
        }

        // ตั้งทิศของ Random ถ้าต้องการ
        if (cfg.effect == SanityEffectType.Random && randomizeEachPlay)
            _randomSignThisPlay = Random.value < 0.5f ? -1 : +1;

        // ลุยเล่น
        _currentTape = cfg;
        audioSource.clip = cfg.clip;
        audioSource.loop = false;
        audioSource.Play();

        onPlayStartedDisplay?.Invoke(string.IsNullOrEmpty(cfg.displayName) ? cfg.tapeKeyId : cfg.displayName);

        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(Co_PlayTape(cfg));
    }

    IEnumerator Co_PlayTape(TapeConfig cfg)
    {
        // ความยาวตามคลิป: เราจะใช้ audioSource.isPlaying เป็นหลัก
        // ส่งค่า ΔSanity ต่อเฟรม ไปยัง sanityTarget ขณะเล่น
        while (audioSource && audioSource.isPlaying)
        {
            // --- Battery consumption ---
            if (useBattery)
            {
                _batteryTimer -= Time.deltaTime;
                if (_batteryTimer <= 0f)
                {
                    _batteryTimer += secondsPerBattery; // next window
                    if (!playerInventory.Consume(batteryKeyId, 1))
                    {
                        // แบตหมด: หยุดเล่น
                        onNoItem?.Invoke($"แบตเตอรี่ ({batteryKeyId}) หมด");
                        break;
                    }
                }
            }

            // --- Sanity effect per second -> per frame ---
            float perSec = Mathf.Max(0f, cfg.amountPerSecond);
            if (perSec > 0f && sanityTarget != null)
            {
                float sign = 0f;
                switch (cfg.effect)
                {
                    case SanityEffectType.Increase: sign = +1f; break;
                    case SanityEffectType.Decrease: sign = -1f; break;
                    case SanityEffectType.Random: sign = _randomSignThisPlay; break;
                }

                float delta = sign * perSec * Time.deltaTime;
                sanityTarget.AddSanity(delta);
            }

            yield return null;
        }

        // หยุด
        _currentTape = null;
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        _playRoutine = null;
        onPlayStopped?.Invoke();
    }
}
