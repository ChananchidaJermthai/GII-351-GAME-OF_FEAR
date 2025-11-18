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
    public float maxDistance = 50f;
    public bool spatialize = true;

    [Header("Objects To Move (Array)")]
    [Tooltip("Object ที่จะถูกขยับเมื่อ Trigger ทำงาน")]
    public Transform[] moveTargets;

    [Tooltip("ตำแหน่งใหม่ (ถ้าเวคเตอร์ว่างๆ จะไม่เปลี่ยนตำแหน่งตัวนั้น)")]
    public Vector3[] targetPositions;

    [Tooltip("Rotation ใหม่เป็นองศา (Euler) (ถ้าเวคเตอร์ว่างๆ จะไม่เปลี่ยน rotation ตัวนั้น)")]
    public Vector3[] targetRotations;

    [Tooltip("ถ้าเปิด = ใช้ LocalPosition / LocalRotation, ถ้าปิด = ใช้ World Position / Rotation")]
    public bool useLocalSpace = false;

    [Header("Debug")]
    public bool logEvents = true;

    bool used;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

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

        if (used && singleUse)
        {
            if (logEvents) Debug.Log("[AudioTrigger] already used", this);
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            if (logEvents) Debug.Log("[AudioTrigger] tag mismatch", this);
            return;
        }

        if (!clip)
        {
            Debug.LogWarning("[AudioTrigger] Missing clip", this);
            return;
        }

        Vector3 pos = soundPoint ? soundPoint.position : transform.position;
        PlayAt(pos);

        // เรียกระบบลดเสียง ambient เหมือนเดิม
        AmbientRoomAudioManager.FocusDuck();

        // ขยับ objects ตามที่ตั้งค่าไว้
        ApplyObjectTransforms();

        used = true;
    }

    [ContextMenu("Test Play Here (ignore trigger)")]
    void TestPlayHere()
    {
        Vector3 pos = soundPoint ? soundPoint.position : transform.position;
        PlayAt(pos);
        ApplyObjectTransforms();
    }

    void PlayAt(Vector3 worldPos)
    {
        // ใช้แบบง่าย
        AudioSource.PlayClipAtPoint(clip, worldPos, volume);

        // ถ้าอยากใช้แบบกำหนด rolloff เองให้ใช้วิธีสร้าง AudioSource ใหม่แทน (คอมเมนต์ไว้):
        /*
        var go = new GameObject("OneShotAudio");
        go.transform.position = worldPos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = spatialize ? 1f : 0f;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.Play();
        Destroy(go, clip.length + 0.1f);
        */

        if (logEvents) Debug.Log($"[AudioTrigger] Play '{clip.name}' @ {worldPos}", this);
    }

    void ApplyObjectTransforms()
    {
        if (moveTargets == null || moveTargets.Length == 0)
            return;

        for (int i = 0; i < moveTargets.Length; i++)
        {
            Transform t = moveTargets[i];
            if (!t) continue;

            // เปลี่ยนตำแหน่ง ถ้ามีค่าใน targetPositions
            if (targetPositions != null && i < targetPositions.Length)
            {
                Vector3 p = targetPositions[i];
                if (p != Vector3.zero)
                {
                    if (useLocalSpace) t.localPosition = p;
                    else t.position = p;
                }
            }

            // เปลี่ยน rotation ถ้ามีค่าใน targetRotations
            if (targetRotations != null && i < targetRotations.Length)
            {
                Vector3 euler = targetRotations[i];
                if (euler != Vector3.zero)
                {
                    Quaternion q = Quaternion.Euler(euler);
                    if (useLocalSpace) t.localRotation = q;
                    else t.rotation = q;
                }
            }

            if (logEvents)
            {
                Debug.Log($"[AudioTrigger] Moved '{t.name}'", this);
            }
        }
    }
}
