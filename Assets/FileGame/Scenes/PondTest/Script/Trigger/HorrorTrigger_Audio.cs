using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HorrorTrigger_Audio : MonoBehaviour
{
    public string playerTag = "Player";
    public bool singleUse = true;

    [Header("Sound")]
    public Transform soundPoint;
    public AudioClip clip;
    [Range(0, 1)] public float volume = 1f;
    public float minDistance = 1f;
    public float maxDistance = 50f; // ขยายขึ้น
    public bool spatialize = true;

    [Header("Debug")]
    public bool logEvents = true;

    bool used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (logEvents) Debug.Log($"[AudioTrigger] OnTriggerEnter by {other.name} tag={other.tag}", this);

        if (used && singleUse) { if (logEvents) Debug.Log("[AudioTrigger] already used", this); return; }
        if (!other.CompareTag(playerTag)) { if (logEvents) Debug.Log("[AudioTrigger] tag mismatch", this); return; }
        if (!clip) { Debug.LogWarning("[AudioTrigger] Missing clip", this); return; }

        Vector3 pos = soundPoint ? soundPoint.position : transform.position;
        PlayAt(pos);
        AmbientRoomAudioManager.FocusDuck();

        used = true;
    }

    [ContextMenu("Test Play Here (ignore trigger)")]
    void TestPlayHere()
    {
        Vector3 pos = soundPoint ? soundPoint.position : transform.position;
        PlayAt(pos);
    }

    void PlayAt(Vector3 worldPos)
    {
        // ทดลอง: ใช้ API ง่ายสุดก่อน
        AudioSource.PlayClipAtPoint(clip, worldPos, volume);

        // ถ้าต้องการ 3D rolloff แบบกำหนดเอง ให้ใช้วิธีสร้าง AudioSource:
        // var go = new GameObject("OneShotAudio"); go.transform.position = worldPos;
        // var src = go.AddComponent<AudioSource>();
        // src.clip = clip; src.volume = volume;
        // src.spatialBlend = spatialize ? 1f : 0f;
        // src.minDistance = minDistance; src.maxDistance = maxDistance;
        // src.rolloffMode = AudioRolloffMode.Linear;
        // src.Play(); Destroy(go, clip.length + 0.1f);

        if (logEvents) Debug.Log($"[AudioTrigger] Play '{clip.name}' @ {worldPos}", this);
    }
}
