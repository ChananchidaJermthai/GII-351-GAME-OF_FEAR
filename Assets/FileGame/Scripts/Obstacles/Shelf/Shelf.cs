using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Shelf : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [System.Serializable]
    public class DoorConfig
    {
        public Transform door;
        [Tooltip("แกนหมุนของบานนี้ (ส่วนใหญ่เป็น Y)")]
        public Axis axis = Axis.Y;
        [Tooltip("องศาที่เปิดจากตำแหน่งปิด (สัมพัทธ์)")]
        public float openAngle = 90f;
        [Tooltip("กลับทิศทาง (เช่น ซ้าย = - , ขวา = +)")]
        public bool invert = false;

        [HideInInspector] public Quaternion closedRot;
        [HideInInspector] public Quaternion openRot;
    }

    [Header("Doors (ตั้งได้แยกบาน)")]
    public DoorConfig left = new DoorConfig();
    public DoorConfig right = new DoorConfig();

    [Header("Animation")]
    [Min(0.1f)] public float openSpeed = 2f;
    public bool allowToggle = false;        // เปิดแล้วกดซ้ำให้ปิดได้

    [Header("Lock Settings")]
    public bool isLocked = false;
    public string requiredKeyId = "Key_Shelf";
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
    [Tooltip("ทดสอบ: ให้เปิดเองทันทีเมื่อกด Play (ไว้เช็คว่าบานหมุนจริง)")]
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

        CacheDoorRotations(left);
        CacheDoorRotations(right);

        if (autoOpenOnPlay)
            OpenNow(); // ทดสอบทันทีตอนเริ่มเกม
    }

    void CacheDoorRotations(DoorConfig d)
    {
        if (!d.door) return;
        d.closedRot = d.door.localRotation;

        Vector3 axis = Vector3.up;
        switch (d.axis)
        {
            case Axis.X: axis = Vector3.right; break;
            case Axis.Y: axis = Vector3.up; break;
            case Axis.Z: axis = Vector3.forward; break;
        }

        float signedAngle = (d.invert ? -1f : 1f) * d.openAngle;
        d.openRot = d.closedRot * Quaternion.AngleAxis(signedAngle, axis);
    }

    // ===== Interact entry =====
    public void TryInteract(GameObject playerGO)
    {
        if (debugLogs) Debug.Log("[Shelf] TryInteract");
        if (!_isOpen)
        {
            // ถ้าล็อก ให้เช็คกุญแจ
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
            if (allowToggle)
                CloseNow();
            // ถ้าไม่ allowToggle ก็ไม่ทำอะไร (เปิดแล้ว)
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
            ApplyProgress(left, toOpen ? k : 1f - k);
            ApplyProgress(right, toOpen ? k : 1f - k);
            yield return null;
        }
        ApplyProgress(left, toOpen ? 1f : 0f);
        ApplyProgress(right, toOpen ? 1f : 0f);

        _isOpen = toOpen;
        if (toOpen) onOpened?.Invoke();
        else onClosed?.Invoke();
    }

    void ApplyProgress(DoorConfig d, float k)
    {
        if (!d.door) return;
        d.door.localRotation = Quaternion.Slerp(d.closedRot, d.openRot, k);
    }

    void ShowMsg(string msg)
    {
        if (messageUI) messageUI.ShowMessage(msg, 2f);
        else Debug.Log($"[Shelf] {msg}");
    }

    // ===== Gizmos ช่วยดู (สีเขียว=แกน, สีเหลือง=ทิศเปิด) =====
    void OnDrawGizmosSelected()
    {
        DrawDoorGizmo(left, Color.yellow);
        DrawDoorGizmo(right, Color.cyan);
    }

    void DrawDoorGizmo(DoorConfig d, Color c)
    {
        if (d == null || d.door == null) return;
        Gizmos.color = c;
        var p = d.door.position;
        Vector3 axis = Vector3.up;
        switch (d.axis)
        {
            case Axis.X: axis = d.door.right; break;
            case Axis.Y: axis = d.door.up; break;
            case Axis.Z: axis = d.door.forward; break;
        }
        Gizmos.DrawLine(p, p + axis * 0.25f);
    }
}
