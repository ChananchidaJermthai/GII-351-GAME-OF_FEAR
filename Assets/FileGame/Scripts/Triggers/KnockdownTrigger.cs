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

    [Header("Target Object")]
    public Rigidbody targetRb;
    public Transform lookAtOverride;

    [Header("Push Settings")]
    public Vector2 delayBeforePush = new Vector2(0.1f, 0.35f);
    public bool enableGravityOnPush = true;
    public bool disableKinematicOnPush = true;
    public float pushForce = 4f;
    public Vector3 pushDirection = new Vector3(0.2f, -1f, 0f);
    public bool useLocalDirection = false;
    public Vector3 torqueImpulse = new Vector3(0f, 0f, 0.5f);

    [Header("Camera / Control")]
    public float followRotateSpeed = 8f;
    public float holdAfterPush = 0.5f;

    [Header("Audio")]
    public AudioClip fallSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // runtime
    private AudioSource _audio;
    private bool _fired;

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

        Transform playerRoot = other.transform;
        if (!playerRoot) return;

        _fired = true;
        if (disableTriggerAfterRun) GetComponent<Collider>().enabled = false;

        StartCoroutine(RunSequence(playerRoot));
    }

    private IEnumerator RunSequence(Transform player)
    {
        // 1) กล้องมองเป้าหมาย
        Transform lookTarget = lookAtOverride ? lookAtOverride : targetRb?.transform;
        var pc = player.GetComponent<PlayerController3D>();
        if (pc && lookTarget) pc.StartLookFollow(lookTarget, followRotateSpeed, lockControl: true);

        // 2) รอ delay ก่อนผลัก
        float wait = Mathf.Clamp(Random.Range(delayBeforePush.x, delayBeforePush.y), 0f, 10f);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // 3) ผลัก/เขย่า Rigidbody
        DoKnockdown();

        // 4) เสียงตก/ล้ม
        if (fallSfx)
        {
            Vector3 pos = targetRb ? targetRb.transform.position : transform.position;
            _audio.transform.position = pos;
            _audio.PlayOneShot(fallSfx, sfxVolume);
            AmbientRoomAudioManager.FocusDuck(0.15f, 0.04f, 1.6f, 2f);
        }

        // 5) รอ hold หลัง push
        if (holdAfterPush > 0f) yield return new WaitForSeconds(holdAfterPush);
        if (pc) pc.StopLookFollow(unlockControl: true);
    }

    private void DoKnockdown()
    {
        if (!targetRb)
        {
            Debug.LogWarning("[KnockdownTrigger] targetRb ไม่ถูกกำหนด!");
            return;
        }

        if (disableKinematicOnPush) targetRb.isKinematic = false;
        if (enableGravityOnPush) targetRb.useGravity = true;

        Vector3 dir = pushDirection.normalized;
        if (useLocalDirection) dir = targetRb.transform.TransformDirection(dir);

        if (pushForce > 0f) targetRb.AddForce(dir * pushForce, ForceMode.Impulse);
        if (torqueImpulse.sqrMagnitude > 0f) targetRb.AddTorque(torqueImpulse, ForceMode.Impulse);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!targetRb) return;
        Gizmos.color = Color.yellow;
        Vector3 from = targetRb.transform.position;
        Vector3 dir = useLocalDirection ? targetRb.transform.TransformDirection(pushDirection) : pushDirection;
        Gizmos.DrawRay(from, dir.normalized * Mathf.Max(0.5f, pushForce * 0.25f));
    }
#endif
}
