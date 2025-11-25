using System;
using System.Collections.Generic;
using System.Reflection;
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
        [HideInInspector] public Vector3 closedPos;
        [HideInInspector] public Vector3 openPos;
    }

    [Header("Doors")]
    public List<DoorConfig> doors = new List<DoorConfig>();

    [Header("Interact")]
    [Tooltip("อนุญาตให้กดสลับ เปิด/ปิด ได้")]
    public bool allowToggle = true;

    [Header("Lock")]
    [Tooltip("เริ่มต้นเป็นตู้ล็อกอยู่หรือไม่")]
    public bool isLocked = false;

    [Tooltip("ไอเท็ม ID ของกุญแจที่ต้องใช้ (ใช้ string ให้เข้ากับ InventoryLite)")]
    public string requiredKeyId = "Key01";

    [Tooltip("ใช้กุญแจแล้วให้หายไปหรือไม่")]
    public bool consumeKeyOnUse = false;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip openSfx;
    public AudioClip openWoodCrackSfx;
    public AudioClip closeSfx;
    public AudioClip lockedSfx;
    public AudioClip lockedSfx2;

    [Header("Destroy When Open")]
    public GameObject Wood;

    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("ดีเลย์ระหว่าง lockedSfx -> lockedSfx2 (วินาที)")]
    [Min(0f)] public float lockedSfx2Delay = 0.15f;

    [Header("UI Feedback (optional)")]
    [Tooltip("โยนคอมโพเนนต์ UI อะไรก็ได้ที่มี ShowCenter(string,float) หรือ Show(string) หรือ SetText(string)")]
    public MonoBehaviour messageUI;

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
    MethodInfo _msgMethod;

    [Header("Interact Guard")]
    [Tooltip("กันสแปม: เวลาขั้นต่ำระหว่างการกด (วินาที)")]
    [Min(0f)] public float interactCooldown = 0.35f;

    [Tooltip("บล็อกการกดระหว่างอนิเมชันกำลังเล่นอยู่")]
    public bool blockWhileAnimating = true;

    float _lastInteractTime = -999f;
    bool _isBusy = false;

    [Header("Animation")]
    [Tooltip("ความเร็วเปิด/ปิด (ตัวคูณเวลา)")]
    public float openSpeed = 3f;

    void Awake()
    {
        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        if (doors == null) doors = new List<DoorConfig>();
        NormalizeConfigs();
        CacheAllDoorStates();
        CacheMessageUIMethod();

        if (autoOpenOnPlay)
            OpenNow();
    }

    void OnDestroy()
    {
        if (_anim != null)
            StopCoroutine(_anim);

        _anim = null;
        _playerInv = null;
    }

    void NormalizeConfigs()
    {
        if (doors == null) return;
        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            if (!d.door) continue;
            if (d.openAngle < 0) d.openAngle = -d.openAngle;
            if (d.slideDistance < 0) d.slideDistance = -d.slideDistance;
        }
    }

    void CacheAllDoorStates()
    {
        if (doors == null || doors.Count == 0) return;

        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            if (!d.door) continue;

            d.closedRot = d.door.localRotation;
            d.closedPos = d.door.localPosition;

            switch (d.motion)
            {
                case MotionType.Rotation:
                    Vector3 axis = AxisToVector(d.axis);
                    float sign = d.invert ? -1f : 1f;
                    d.openRot = d.closedRot * Quaternion.AngleAxis(sign * d.openAngle, axis);
                    d.openPos = d.closedPos;
                    break;
                case MotionType.Slide:
                    Vector3 dir = AxisToVector(d.axis) * (d.invert ? -1f : 1f);
                    d.openPos = d.closedPos + dir * d.slideDistance;
                    d.openRot = d.closedRot;
                    break;
            }
        }
    }

    Vector3 AxisToVector(Axis a)
    {
        switch (a)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            default: return Vector3.forward;
        }
    }

    void CacheMessageUIMethod()
    {
        if (!messageUI) return;
        var t = messageUI.GetType();
        _msgMethod = t.GetMethod("ShowCenter", new Type[] { typeof(string), typeof(float) }) ??
                     t.GetMethod("Show", new Type[] { typeof(string) }) ??
                     t.GetMethod("SetText", new Type[] { typeof(string) });
    }

    void ShowMsg(string msg)
    {
        if (_msgMethod != null)
        {
            var parameters = _msgMethod.GetParameters().Length == 2 ? new object[] { msg, 1.2f } : new object[] { msg };
            _msgMethod.Invoke(messageUI, parameters);
        }
        else if (debugLogs) Debug.Log(msg);
    }

    public void TryInteract(GameObject playerGO)
    {
        if (debugLogs) Debug.Log("[Shelf] TryInteract");

        if (Time.time - _lastInteractTime < interactCooldown)
        {
            if (debugLogs) Debug.Log("[Shelf] Interact ignored: cooldown");
            return;
        }

        if (blockWhileAnimating && (_isBusy || _anim != null))
        {
            if (debugLogs) Debug.Log("[Shelf] Interact ignored: animating");
            return;
        }

        _lastInteractTime = Time.time;

        if (!_isOpen)
        {
            if (isLocked)
            {
                if (!_playerInv && playerGO)
                    _playerInv = playerGO.GetComponentInParent<InventoryLite>();

                if (!_playerInv || _playerInv.GetCount(requiredKeyId) <= 0)
                {
                    if (lockedSfx) audioSource.PlayOneShot(lockedSfx, sfxVolume);
                    if (lockedSfx2) StartCoroutine(PlayLockedSecond());
                    onLockedTry?.Invoke();
                    ShowMsg("It's locked. You need a key.");
                    if (debugLogs) Debug.Log("[Shelf] Locked: no key.");
                    return;
                }

                if (consumeKeyOnUse)
                {
                    bool ok = _playerInv.Consume(requiredKeyId, 1);
                    if (debugLogs) Debug.Log($"[Shelf] Consume key: {ok} for {requiredKeyId}");
                }

                isLocked = false;
            }

            OpenNow();
        }
        else if (allowToggle)
        {
            CloseNow();
        }
    }

    [ContextMenu("TEST / Open Now")]
    public void OpenNow()
    {
        if (blockWhileAnimating && _anim != null) return;
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
        _anim = StartCoroutine(AnimateDoors(true));
    }

    [ContextMenu("TEST / Close Now")]
    public void CloseNow()
    {
        if (blockWhileAnimating && _anim != null) return;
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
        _anim = StartCoroutine(AnimateDoors(false));
    }

    System.Collections.IEnumerator AnimateDoors(bool toOpen)
    {
        _isBusy = true;
        if (debugLogs) Debug.Log("[Shelf] AnimateDoors " + (toOpen ? "Open" : "Close"));

        var sfx = toOpen ? openSfx : closeSfx;
        if (sfx) audioSource.PlayOneShot(sfx, sfxVolume);
            audioSource.PlayOneShot(openWoodCrackSfx);
        DestroyWood();
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

        _isBusy = false;
        _anim = null;
    }

    System.Collections.IEnumerator PlayLockedSecond()
    {
        if (lockedSfx2Delay > 0f) yield return new WaitForSeconds(lockedSfx2Delay);
        if (lockedSfx2) audioSource.PlayOneShot(lockedSfx2, sfxVolume);
    }

    void ApplyProgressAll(float k01)
    {
        if (doors == null) return;
        for (int i = 0; i < doors.Count; i++)
            ApplyProgressOne(doors[i], k01);
    }

    void ApplyProgressOne(DoorConfig d, float k01)
    {
        if (!d.door) return;

        switch (d.motion)
        {
            case MotionType.Rotation:
                d.door.localRotation = Quaternion.Slerp(d.closedRot, d.openRot, k01);
                break;
            case MotionType.Slide:
                d.door.localPosition = Vector3.Lerp(d.closedPos, d.openPos, k01);
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (doors == null) return;
        for (int i = 0; i < doors.Count; i++)
        {
            if (!doors[i].door) continue;
            DrawDoorAxisGizmo(doors[i]);
        }
    }

    void DrawDoorAxisGizmo(DoorConfig d)
    {
        Vector3 axisVec;
        switch (d.axis)
        {
            case Axis.X: axisVec = d.door.right; break;
            case Axis.Y: axisVec = d.door.up; break;
            default: axisVec = d.door.forward; break;
        }
        if (d.invert) axisVec = -axisVec;

        Gizmos.color = (d.motion == MotionType.Rotation)
            ? new Color(1f, 0.85f, 0.2f, 0.9f)
            : new Color(0.2f, 0.85f, 1f, 0.9f);

        var p = d.door.position;
        Gizmos.DrawLine(p, p + axisVec * 0.25f);
        Gizmos.DrawSphere(p + axisVec * 0.25f, 0.01f);
    }
    void DestroyWood()
    {
        // 1. ตรวจสอบว่าตัวแปร 'Wood' มีค่าหรือไม่ เพื่อป้องกัน Error
        if (Wood != null)
        {
            // 2. ทำลาย GameObject ที่ถูกเก็บไว้ในตัวแปร 'Wood'
            Destroy(Wood);

            // (เสริม) หากคุณต้องการทำลายตัวมันเองทันทีที่ทำลาย Wood เสร็จ
            // Destroy(gameObject); 
        }
    }
}

/* 
ตัวอย่าง UI ทดสอบ:
public class SimplePlayerMessageUI : MonoBehaviour
{
    public void ShowCenter(string msg, float seconds = 1.2f)
    {
        Debug.Log($"[UI] {msg} ({seconds:0.##}s)");
    }
}
*/
