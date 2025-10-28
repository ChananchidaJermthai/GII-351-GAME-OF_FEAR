using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class KnockdownTrigger : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";
    public bool oneShot = true;
    public bool disableTriggerAfterRun = true;

    [Header("Target Object (Thing to fall)")]
    [Tooltip("กำหนดวัตถุที่จะให้ 'ตก/หล่น' แนะนำให้เป็นตัวที่มี Rigidbody (เช่น ชั้น/ชิ้นของตก)")]
    public Rigidbody targetRb;

    [Tooltip("ถ้าไม่กำหนด จะหันกล้องไปที่ Transform ของ targetRb")]
    public Transform lookAtOverride;

    [Header("Push Settings")]
    [Tooltip("ดีเลย์ก่อนผลัก (วินาที) เพื่อทำจังหวะหลอกผู้เล่น)")]
    public Vector2 delayBeforePush = new Vector2(0.1f, 0.35f);

    [Tooltip("เปิด gravity ให้เป้าหมายทันทีเมื่อเริ่มผลัก")]
    public bool enableGravityOnPush = true;

    [Tooltip("ปลด isKinematic เมื่อเริ่มผลัก (ถ้า Rigidbody เป็นคิเนมาติกอยู่)")]
    public bool disableKinematicOnPush = true;

    [Tooltip("แรงผลักโดยรวม (ทิศทางในแกน local หรือ world ตาม useLocalDirection)")]
    public float pushForce = 4f;

    [Tooltip("ทิศทางแรงผลัก (ปกติใช้ลง/เฉียง)")]
    public Vector3 pushDirection = new Vector3(0.2f, -1f, 0f);

    [Tooltip("ใช้แกน local ของวัตถุเป้าหมายในการคูณทิศทางแรง")]
    public bool useLocalDirection = false;

    [Tooltip("แรงบิด (ปั่นให้ของหมุน)")]
    public Vector3 torqueImpulse = new Vector3(0f, 0f, 0.5f);

    [Header("Camera / Control")]
    [Tooltip("ความไวการหันตาม (ใช้กับ PlayerController3D.StartLookFollow)")]
    public float followRotateSpeed = 8f;

    [Tooltip("เวลาหน่วงเล็กน้อยหลังผลัก ก่อนคืนคอนโทรล")]
    public float holdAfterPush = 0.5f;

    [Header("Audio")]
    public AudioClip fallSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // runtime
    AudioSource _audio;
    bool _fired;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        _audio = GetComponent<AudioSource>();
        if (!_audio)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 1.5f;
            _audio.maxDistance = 20f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;

        var playerRoot = other.GetComponentInParent<Transform>();
        if (!playerRoot) return;

        _fired = true;
        if (disableTriggerAfterRun) GetComponent<Collider>().enabled = false;

        StartCoroutine(RunSequence(playerRoot));
    }

    IEnumerator RunSequence(Transform player)
    {
        // 1) เตรียม look target
        Transform lookTarget = lookAtOverride;
        if (!lookTarget && targetRb) lookTarget = targetRb.transform;

        // 2) ล็อกคอนโทรล + ให้กล้องหันตามวัตถุ
        var pc = player.GetComponent<PlayerController3D>();
        if (pc && lookTarget)
            pc.StartLookFollow(lookTarget, followRotateSpeed, true);

        // 3) รอดีเลย์สุ่มก่อนผลัก (เพื่อสร้างจังหวะลวง/ตกใจ)
        float wait = Mathf.Clamp(Random.Range(delayBeforePush.x, delayBeforePush.y), 0f, 10f);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // 4) ผลักของให้ตก
        DoKnockdown();

        // 5) เล่นเสียง ณ จุดที่ของตก
        if (fallSfx)
        {
            Vector3 pos = targetRb ? targetRb.transform.position : transform.position;
            _audio.transform.position = pos;
            _audio.PlayOneShot(fallSfx, sfxVolume);
        }

        // 6) ค้างกล้องอีกเล็กน้อย แล้วคืนคอนโทรล
        if (holdAfterPush > 0f) yield return new WaitForSeconds(holdAfterPush);
        if (pc) pc.StopLookFollow(true);
    }

    void DoKnockdown()
    {
        if (!targetRb)
        {
            Debug.LogWarning("[KnockdownTrigger] targetRb ไม่ถูกกำหนด");
            return;
        }

        if (disableKinematicOnPush) targetRb.isKinematic = false;
        if (enableGravityOnPush) targetRb.useGravity = true;

        // คำนวณทิศทางแรง
        Vector3 dir = pushDirection;
        if (useLocalDirection) dir = targetRb.transform.TransformDirection(dir);
        dir = dir.normalized;

        // ใส่แรง/แรงบิดแบบ impulse
        if (pushForce > 0f) targetRb.AddForce(dir * pushForce, ForceMode.Impulse);
        if (torqueImpulse.sqrMagnitude > 0f) targetRb.AddTorque(torqueImpulse, ForceMode.Impulse);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!targetRb) return;
        Gizmos.color = Color.yellow;
        Vector3 from = targetRb.transform.position;
        Vector3 dir = useLocalDirection
            ? targetRb.transform.TransformDirection(pushDirection)
            : pushDirection;
        Gizmos.DrawRay(from, dir.normalized * Mathf.Max(0.5f, pushForce * 0.25f));
    }
#endif
}
