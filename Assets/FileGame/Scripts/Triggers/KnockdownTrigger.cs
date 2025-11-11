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
    [Tooltip("¡ÓË¹´ÇÑµ¶Ø·Õè¨ÐãËé 'µ¡/ËÅè¹' á¹Ð¹ÓãËéà»ç¹µÑÇ·ÕèÁÕ Rigidbody (àªè¹ ªÑé¹/ªÔé¹¢Í§µ¡)")]
    public Rigidbody targetRb;

    [Tooltip("¶éÒäÁè¡ÓË¹´ ¨ÐËÑ¹¡ÅéÍ§ä»·Õè Transform ¢Í§ targetRb")]
    public Transform lookAtOverride;

    [Header("Push Settings")]
    [Tooltip("´ÕàÅÂì¡èÍ¹¼ÅÑ¡ (ÇÔ¹Ò·Õ) à¾×èÍ·Ó¨Ñ§ËÇÐËÅÍ¡¼ÙéàÅè¹)")]
    public Vector2 delayBeforePush = new Vector2(0.1f, 0.35f);

    [Tooltip("à»Ô´ gravity ãËéà»éÒËÁÒÂ·Ñ¹·ÕàÁ×èÍàÃÔèÁ¼ÅÑ¡")]
    public bool enableGravityOnPush = true;

    [Tooltip("»Å´ isKinematic àÁ×èÍàÃÔèÁ¼ÅÑ¡ (¶éÒ Rigidbody à»ç¹¤Ôà¹ÁÒµÔ¡ÍÂÙè)")]
    public bool disableKinematicOnPush = true;

    [Tooltip("áÃ§¼ÅÑ¡â´ÂÃÇÁ (·ÔÈ·Ò§ã¹á¡¹ local ËÃ×Í world µÒÁ useLocalDirection)")]
    public float pushForce = 4f;

    [Tooltip("·ÔÈ·Ò§áÃ§¼ÅÑ¡ (»¡µÔãªéÅ§/à©ÕÂ§)")]
    public Vector3 pushDirection = new Vector3(0.2f, -1f, 0f);

    [Tooltip("ãªéá¡¹ local ¢Í§ÇÑµ¶Øà»éÒËÁÒÂã¹¡ÒÃ¤Ù³·ÔÈ·Ò§áÃ§")]
    public bool useLocalDirection = false;

    [Tooltip("áÃ§ºÔ´ (»Ñè¹ãËé¢Í§ËÁØ¹)")]
    public Vector3 torqueImpulse = new Vector3(0f, 0f, 0.5f);

    [Header("Camera / Control")]
    [Tooltip("¤ÇÒÁäÇ¡ÒÃËÑ¹µÒÁ (ãªé¡Ñº PlayerController3D.StartLookFollow)")]
    public float followRotateSpeed = 8f;

    [Tooltip("àÇÅÒË¹èÇ§àÅç¡¹éÍÂËÅÑ§¼ÅÑ¡ ¡èÍ¹¤×¹¤Í¹â·ÃÅ")]
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
        // 1) àµÃÕÂÁ look target
        Transform lookTarget = lookAtOverride;
        if (!lookTarget && targetRb) lookTarget = targetRb.transform;

        // 2) ÅçÍ¡¤Í¹â·ÃÅ + ãËé¡ÅéÍ§ËÑ¹µÒÁÇÑµ¶Ø
        var pc = player.GetComponent<PlayerController3D>();
        if (pc && lookTarget)
            pc.StartLookFollow(lookTarget, followRotateSpeed, true);

        // 3) ÃÍ´ÕàÅÂìÊØèÁ¡èÍ¹¼ÅÑ¡ (à¾×èÍÊÃéÒ§¨Ñ§ËÇÐÅÇ§/µ¡ã¨)
        float wait = Mathf.Clamp(Random.Range(delayBeforePush.x, delayBeforePush.y), 0f, 10f);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // 4) ¼ÅÑ¡¢Í§ãËéµ¡
        DoKnockdown();

        // 5) àÅè¹àÊÕÂ§ ³ ¨Ø´·Õè¢Í§µ¡
        if (fallSfx)
        {
            Vector3 pos = targetRb ? targetRb.transform.position : transform.position;
            _audio.transform.position = pos;
            _audio.PlayOneShot(fallSfx, sfxVolume);
            AmbientRoomAudioManager.FocusDuck(0.15f, 0.04f, 1.6f, 2f);
        }

        // 6) ¤éÒ§¡ÅéÍ§ÍÕ¡àÅç¡¹éÍÂ áÅéÇ¤×¹¤Í¹â·ÃÅ
        if (holdAfterPush > 0f) yield return new WaitForSeconds(holdAfterPush);
        if (pc) pc.StopLookFollow(true);
    }

    void DoKnockdown()
    {
        if (!targetRb)
        {
            Debug.LogWarning("[KnockdownTrigger] targetRb äÁè¶Ù¡¡ÓË¹´");
            return;
        }

        if (disableKinematicOnPush) targetRb.isKinematic = false;
        if (enableGravityOnPush) targetRb.useGravity = true;

        // ¤Ó¹Ç³·ÔÈ·Ò§áÃ§
        Vector3 dir = pushDirection;
        if (useLocalDirection) dir = targetRb.transform.TransformDirection(dir);
        dir = dir.normalized;

        // ãÊèáÃ§/áÃ§ºÔ´áºº impulse
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
