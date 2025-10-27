using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class BathroomDoorEvent : MonoBehaviour
{
    [Header("Door / Focus")]
    public Transform doorRoot;               // ตัว Transform ของประตู (เขย่า/อนิเมต)
    public Transform lookTarget;             // จุดให้กล้องโฟกัส (ถ้าเว้นว่างจะใช้ doorRoot)
    public float lookDuration = 0.2f;        // โฟกัสกล้อง 0.2 วิ ตามโจทย์
    public float lookRotateSpeed = 12f;      // ความไวในการหมุนไปหาเป้า (ส่งให้ PlayerController3D)

    [Header("Audio")]
    public AudioSource audioSrc;             // แนะนำใส่ AudioSource ที่ประตู
    public AudioClip knockClip;              // เสียง "เคาะประตู"
    public AudioClip laughClip;              // เสียง "หัวเราะ"
    public AudioClip bangClip;               // (ไม่บังคับ) เสียง "กระแทก/เขย่าประตู" แทรกระหว่างหัวเราะ

    [Header("Sanity Gain (+)")]
    public float sanitySmall = 2f;           // ตอนโฟกัส/ได้ยินเคาะเล็กน้อย
    public float sanityLarge = 15f;          // ตอนหัวเราะ+ประตูกระแทก เพิ่มมาก

    [Header("Shake While Laughing")]
    public bool useAnimator = false;         // ถ้ามีแอนิเมชันกระแทก ประตู ให้เปิดแล้วตั้ง trigger ด้านล่าง
    public Animator doorAnimator;
    public string animatorTrigger = "Bang";

    public float shakeDuration = 1.25f;      // ระยะเวลาเขย่ารวม (ถ้าไม่ใช้ Animator)
    public float shakePosAmp = 0.03f;        // ระยะสั่นตำแหน่ง (เมตร)
    public float shakeRotAmp = 4f;           // องศาเอียงซ้ายขวา
    public float shakeFreq = 28f;            // ความถี่การสั่น

    [Header("One-shot")]
    public bool disableColliderAfterPlay = true;   // ป้องกันโดนซ้ำ
    public bool autoDeactivateAfterPlay = true;    // จบเหตุการณ์แล้วปิดวัตถุ

    bool _played = false;
    Vector3 _doorPos0;
    Quaternion _doorRot0;
    Collider _col;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) { c.isTrigger = true; }
        if (!audioSrc) audioSrc = GetComponent<AudioSource>();
        if (!doorRoot) doorRoot = transform;
        if (!lookTarget) lookTarget = doorRoot;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (!doorRoot) doorRoot = transform;
        if (!lookTarget) lookTarget = doorRoot;
        _doorPos0 = doorRoot.localPosition;
        _doorRot0 = doorRoot.localRotation;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_played) return;

        if (other.TryGetComponent<PlayerController3D>(out var player))
        {
            _played = true;
            StartCoroutine(PlaySequence(player));
        }
    }

    IEnumerator PlaySequence(PlayerController3D player)
    {
        // 1) โฟกัสกล้องไปที่ประตู + ล็อกคอนโทรลชั่วคราว (0.2 วิ)
        player.StartLookFollow(lookTarget, lookRotateSpeed, lockControl: true);
        yield return new WaitForSeconds(lookDuration);
        player.StopLookFollow(unlockControl: true);

        // เพิ่ม sanity เล็กน้อย
        if (sanitySmall != 0f) player.AddSanity(sanitySmall);

        // 2) เล่นเสียงเคาะประตู
        float knockLen = PlayOneShotSafe(knockClip);
        if (knockLen > 0f) yield return new WaitForSeconds(knockLen);

        // 3) เล่นเสียงหัวเราะ + เขย่าประตู/กระแทก (พร้อมกัน)
        float laughLen = PlayOneShotSafe(laughClip);
        // ถ้ามีเสียงกระแทกแยก ให้เล่นซ้ำๆ ระหว่างหัวเราะก็ได้
        if (bangClip) audioSrc.PlayOneShot(bangClip);

        // เพิ่ม sanity อย่างมากตอนนี้
        if (sanityLarge != 0f) player.AddSanity(sanityLarge);

        // เล่นแอนิเมชัน หรือสั่นแบบ procedural
        if (useAnimator && doorAnimator)
        {
            doorAnimator.SetTrigger(animatorTrigger);
            if (laughLen > 0f) yield return new WaitForSeconds(laughLen);
        }
        else
        {
            float dur = (laughLen > 0f ? laughLen : shakeDuration);
            yield return StartCoroutine(ShakeDoor(dur));
        }

        // 4) รีเซ็ตทุกอย่างกลับเหมือนเดิม
        doorRoot.localPosition = _doorPos0;
        doorRoot.localRotation = _doorRot0;

        if (disableColliderAfterPlay && _col) _col.enabled = false;
        if (autoDeactivateAfterPlay) gameObject.SetActive(false);
    }

    float PlayOneShotSafe(AudioClip clip)
    {
        if (audioSrc && clip)
        {
            audioSrc.PlayOneShot(clip);
            return clip.length;
        }
        return 0f;
    }

    IEnumerator ShakeDoor(float duration)
    {
        float t0 = Time.time;
        while (Time.time - t0 < duration)
        {
            float t = Time.time - t0;
            float s = Mathf.Sin(t * shakeFreq);

            // ตำแหน่งสั่นเล็กน้อย
            Vector3 off = new Vector3(s * shakePosAmp, 0f, 0f);
            // หมุนสั่นเล็กน้อย
            Quaternion rot = Quaternion.Euler(0f, 0f, s * shakeRotAmp);

            doorRoot.localPosition = _doorPos0 + off;
            doorRoot.localRotation = _doorRot0 * rot;

            yield return null;
        }
        // คืนค่าเดิม
        doorRoot.localPosition = _doorPos0;
        doorRoot.localRotation = _doorRot0;
    }

    void OnDrawGizmosSelected()
    {
        if (!doorRoot) doorRoot = transform;
        if (!lookTarget) lookTarget = doorRoot;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(lookTarget.position, 0.08f);
        Gizmos.DrawLine(lookTarget.position, lookTarget.position + Vector3.up * 0.25f);
    }
}
