using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Shelf : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    public enum MotionType { Rotation, Slide }

    [System.Serializable]
    public class DoorConfig
    {
        [Header("Target")]
        public Transform door;

        [Header("Motion")]
        public MotionType motion = MotionType.Rotation;

        [Tooltip("แกนสำหรับ Rotation หรือ Slide")]
        public Axis axis = Axis.Y;

        [Tooltip("สลับทิศทาง (เช่น ซ้าย/ขวา)")]
        public bool invert = false;

        [Header("Rotation Settings")]
        [Tooltip("องศาที่เปิดจากตำแหน่งปิด (สัมพัทธ์)")]
        public float openAngle = 90f;

        [Header("Slide Settings")]
        [Tooltip("ระยะเลื่อนจากตำแหน่งปิด (หน่วยเมตร)")]
        public float slideDistance = 0.3f;

        [HideInInspector] public Quaternion closedRot;
        [HideInInspector] public Quaternion openRot;
        [HideInInspector] public Vector3 closedLocalPos;
        [HideInInspector] public Vector3 openLocalPos;
    }

    [Header("Doors (List)")]
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("Animation")]
    [Min(0.1f)] public float openSpeed = 2f;
    [Tooltip("เปิดแล้วกดซ้ำเพื่อปิดได้")]
    public bool allowToggle = false;

    [Header("Lock Settings")]
    public bool isLocked = false;
    [Tooltip("Key ID ใน InventoryLite")]
    public string requiredKeyId = "Key_Shelf";
    [Tooltip("ใช้กุญแจแล้วให้หายไปหรือไม่")]
    public bool consumeKeyOnUse = false;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip openSfx;
    public AudioClip closeSfx;
    public AudioClip lockedSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("UI Feedback (optional)")]
    public PlayerMessageUI messageUI; // ถ้ามีจะโชว์ข้อความกลางจอ

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;
    public UnityEvent onLockedTry;

    [Header("Debug / Test")]
    public bool debugLogs = false;
    [Tooltip("ทดสอบ: ให้เปิดเองทันทีเมื่อกด Play (ไว้เช็คว่าบานขยับจริง)")]
    public bool autoOpenOnPlay = false;

    // runtime
    bool _isOpen = false;
    InventoryLite _playerInv;
    Coroutine _anim;

    void Awake()
    {
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f; // 3D ออกมาจากตำแหน่งตู้
        }

        CacheAllDoorStates();

        if (autoOpenOnPlay)
            OpenNow(); // ทดสอบทันทีตอนเริ่มเกม
    }

    void CacheAllDoorStates()
    {
        foreach (var d in doors)
        {
            if (!d.door) continue;

            // เก็บสถานะ "ปิด"
            d.closedRot = d.door.localRotation;
            d.closedLocalPos = d.door.localPosition;

            // คำนวณแกนท้องถิ่น
            Vector3 axisVec = AxisToLocal(d);

            // Rotation target
            float signedAngle = (d.invert ? -1f : 1f) * d.openAngle;
            d.openRot = d.closedRot * Quaternion.AngleAxis(signedAngle, axisVec);

            // Slide target
            float signedDist = (d.invert ? -1f : 1f) * d.slideDistance;
            d.openLocalPos = d.closedLocalPos + axisVec.normalized * signedDist;
        }
    }

    Vector3 AxisToLocal(DoorConfig d)
    {
        if (!d.door) return Vector3.up;
        switch (d.axis)
        {
            case Axis.X: return d.door.transform.right;
            case Axis.Y: return d.door.transform.up;
            case Axis.Z: return d.door.transform.forward;
        }
        return Vector3.up;
    }

    // ===== Interact entry (ใช้ร่วมกับ PlayerAimPickup) =====
    public void TryInteract(GameObject playerGO)
    {
        if (debugLogs) Debug.Log("[Shelf] TryInteract");

        if (!_isOpen)
        {
            // เช็คล็อก
            if (isLocked)
            {
                if (!_playerInv && playerGO) _playerInv = playerGO.GetComponentInParent<InventoryLite>();

                if (!_playerInv || _playerInv.GetCount(requiredKeyId) <= 0)
                {
                    if (lockedSfx) audioSource.PlayOneShot(lockedSfx, sfxVolume);
                    onLockedTry?.Invoke();
                    ShowMsg("It's locked. You need a key.");
                    if (debugLogs) Debug.Log("[Shelf] Locked: no key.");
                    return;
                }

                if (consumeKeyOnUse)
                {
                    bool ok = _playerInv.Consume(requiredKeyId, 1);
                    if (debugLogs) Debug.Log($"[Shelf] Consume key: {ok}");
                }
                isLocked = false; // ปลดล็อก
            }

            OpenNow();
        }
        else
        {
            if (allowToggle) CloseNow();
        }
    }

    // ===== Public controls / Test =====
    [ContextMenu("TEST / Open Now")]
    public void OpenNow()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateDoors(true));
    }

    [ContextMenu("TEST / Close Now")]
    public void CloseNow()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateDoors(false));
    }

    System.Collections.IEnumerator AnimateDoors(bool toOpen)
    {
        if (debugLogs) Debug.Log("[Shelf] AnimateDoors " + (toOpen ? "Open" : "Close"));

        // SFX
        var sfx = toOpen ? openSfx : closeSfx;
        if (sfx) audioSource.PlayOneShot(sfx, sfxVolume);

        // ระหว่างอนิเมชัน
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            float k = Mathf.SmoothStep(0, 1, t);
            ApplyProgressAll(toOpen ? k : 1f - k);
            yield return null;
        }
        ApplyProgressAll(toOpen ? 1f : 0f);

        _isOpen = toOpen;
        if (toOpen) onOpened?.Invoke();
        else onClosed?.Invoke();
    }

    void ApplyProgressAll(float k01)
    {
        foreach (var d in doors)
            ApplyProgressOne(d, k01);
    }

    void ApplyProgressOne(DoorConfig d, float k)
    {
        if (!d.door) return;

        if (d.motion == MotionType.Rotation)
        {
            d.door.localRotation = Quaternion.Slerp(d.closedRot, d.openRot, k);
        }
        else // Slide
        {
            d.door.localPosition = Vector3.Lerp(d.closedLocalPos, d.openLocalPos, k);
        }
    }

    void ShowMsg(string msg)
    {
        if (messageUI) messageUI.ShowMessage(msg, 2f);
        else Debug.Log($"[Shelf] {msg}");
    }

    // ===== Gizmos ช่วยดูทิศทาง =====
    void OnDrawGizmosSelected()
    {
        if (doors == null) return;
        foreach (var d in doors)
            DrawDoorGizmo(d);
    }

    void DrawDoorGizmo(DoorConfig d)
    {
        if (d == null || d.door == null) return;

        // แกนในพื้นที่โลคัลของบาน
        Vector3 axisVec;
        switch (d.axis)
        {
            case Axis.X: axisVec = d.door.right; break;
            case Axis.Y: axisVec = d.door.up; break;
            default: axisVec = d.door.forward; break;
        }
        if (d.invert) axisVec = -axisVec;

        Gizmos.color = (d.motion == MotionType.Rotation) ? new Color(1f, 0.85f, 0.2f, 0.9f)
                                                         : new Color(0.2f, 0.85f, 1f, 0.9f);
        var p = d.door.position;
        Gizmos.DrawLine(p, p + axisVec * 0.25f);
        Gizmos.DrawSphere(p + axisVec * 0.25f, 0.01f);
    }
}
