using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class BathroomDoorEvent : MonoBehaviour
{
    [Header("Door / Focus")]
    public Transform doorRoot;               
    public Transform lookTarget;             
    public float lookDuration = 0.2f;       
    public float lookRotateSpeed = 12f;     

    [Header("Audio")]
    public AudioSource audioSrc;             
    public AudioClip knockClip;              
    public AudioClip laughClip;              
    public AudioClip bangClip;               

    [Header("Sanity Gain (+)")]
    public float sanitySmall = 2f;           
    public float sanityLarge = 15f;          

    [Header("Shake While Laughing")]
    public bool useAnimator = false;         
    public Animator doorAnimator;
    public string animatorTrigger = "Bang";

    public float shakeDuration = 1.25f;      
    public float shakePosAmp = 0.03f;        
    public float shakeRotAmp = 4f;           
    public float shakeFreq = 28f;            

    [Header("One-shot")]
    public bool disableColliderAfterPlay = true;   
    public bool autoDeactivateAfterPlay = true;    

    private bool _played = false;
    private Vector3 _doorPos0;
    private Quaternion _doorRot0;
    private Collider _col;

    void Reset()
    {
        if (TryGetComponent(out Collider c)) c.isTrigger = true;
        if (!audioSrc) TryGetComponent(out audioSrc);
        if (!doorRoot) doorRoot = transform;
        if (!lookTarget) lookTarget = doorRoot;
    }

    void Awake()
    {
        if (!TryGetComponent(out _col)) _col = null;
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
        // 1) Focus camera
        player.StartLookFollow(lookTarget, lookRotateSpeed, lockControl: true);
        yield return new WaitForSeconds(lookDuration);
        player.StopLookFollow(unlockControl: true);

        if (sanitySmall != 0f) player.AddSanity(sanitySmall);

        // 2) Play knock sound
        yield return PlayClip(knockClip);

        // 3) Play laugh + bang
        var laughRoutine = PlayClip(laughClip);
        if (bangClip) audioSrc?.PlayOneShot(bangClip);

        if (sanityLarge != 0f) player.AddSanity(sanityLarge);

        if (useAnimator && doorAnimator)
        {
            doorAnimator.SetTrigger(animatorTrigger);
            yield return laughRoutine;
        }
        else
        {
            float dur = (laughClip ? laughClip.length : shakeDuration);
            yield return StartCoroutine(ShakeDoor(dur));
        }

        // 4) Reset
        doorRoot.localPosition = _doorPos0;
        doorRoot.localRotation = _doorRot0;

        if (disableColliderAfterPlay && _col) _col.enabled = false;
        if (autoDeactivateAfterPlay) gameObject.SetActive(false);
    }

    IEnumerator PlayClip(AudioClip clip)
    {
        if (audioSrc && clip)
        {
            audioSrc.PlayOneShot(clip);
            yield return new WaitForSeconds(clip.length);
        }
    }

    IEnumerator ShakeDoor(float duration)
    {
        float t0 = Time.time;
        while (Time.time - t0 < duration)
        {
            float t = Time.time - t0;
            float s = Mathf.Sin(t * shakeFreq);
            doorRoot.localPosition = _doorPos0 + new Vector3(s * shakePosAmp, 0f, 0f);
            doorRoot.localRotation = _doorRot0 * Quaternion.Euler(0f, 0f, s * shakeRotAmp);
            yield return null;
        }
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
