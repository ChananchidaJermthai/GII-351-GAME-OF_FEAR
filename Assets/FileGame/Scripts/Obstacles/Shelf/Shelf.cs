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
    public AudioClip closeSfx;
    public AudioClip lockedSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

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
        }

        if (doors == null) doors = new List<DoorConfig>();
        NormalizeConfigs();
        CacheAllDoorStates();

        if (autoOpenOnPlay)
            OpenNow(); // ทดสอบทันทีตอนเริ่มเกม
    }

    void NormalizeConfigs()
    {
        foreach (var d in doors)
        {
            if (!d.door) continue;
            if (d.openAngle < 0) d.openAngle = -d.openAngle;
            if (d.slideDistance < 0) d.slideDistance = -d.slideDistance;
        }
    }

    void CacheAllDoorStates()
    {
        foreach (var d in doors)
        {
            if (!d.door) continue;

            // เก็บสถานะ "ปิด"
            d.closedRot = d.door.localRotation;
            d.closedPos = d.door.localPosition;

            // สร้างสถานะ "เปิด"
            switch (d.motion)
            {
                case MotionType.Rotation:
                    {
                        Vector3 axis = AxisToVector(d.axis);
                        float sign = d.invert ? -1f : 1f;
                        d.openRot = d.closedRot * Quaternion.AngleAxis(sign * d.openAngle, axis);
                        d.openPos = d.closedPos;
                        break;
                    }
                case MotionType.Slide:
                    {
                        Vector3 dir = AxisToVector(d.axis) * (d.invert ? -1f : 1f);
                        d.openPos = d.closedPos + dir * d.slideDistance;
                        d.openRot = d.closedRot;
                        break;
                    }
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

    void ShowMsg(string msg)
    {
        if (!messageUI) { if (debugLogs) Debug.Log(msg); return; }

        // ใช้รีเฟลกชันเพื่อรองรับหลายชื่อเมธอดของ UI:
        // 1) ShowCenter(string,float)  2) Show(string)  3) SetText(string)
        var uiType = messageUI.GetType();

        var m = uiType.GetMethod("ShowCenter", new Type[] { typeof(string), typeof(float) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg, 1.2f }); return; }

        m = uiType.GetMethod("Show", new Type[] { typeof(string) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg }); return; }

        m = uiType.GetMethod("SetText", new Type[] { typeof(string) });
        if (m != null) { m.Invoke(messageUI, new object[] { msg }); return; }

        if (debugLogs) Debug.Log(msg);
    }

    // ===== Interact entry (ใช้ร่วมกับ PlayerAimPickup) =====
    public void TryInteract(GameObject playerGO)
    {
        if (debugLogs) Debug.Log("[Shelf] TryInteract");

        // กันสแปม: คูลดาวน์
        if (Time.time - _lastInteractTime < interactCooldown)
        {
            if (debugLogs) Debug.Log("[Shelf] Interact ignored: cooldown");
            return;
        }
        // กันสแปม: บล็อกระหว่างอนิเมชัน
        if (blockWhileAnimating && (_isBusy || _anim != null))
        {
            if (debugLogs) Debug.Log("[Shelf] Interact ignored: animating");
            return;
        }

        _lastInteractTime = Time.time;

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
                    if (debugLogs) Debug.Log($"[Shelf] Consume key: {ok} for {requiredKeyId}");
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
        if (blockWhileAnimating && _anim != null) return; // กันซ้อน
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateDoors(true));
    }

    [ContextMenu("TEST / Close Now")]
    public void CloseNow()
    {
        if (blockWhileAnimating && _anim != null) return; // กันซ้อน
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateDoors(false));
    }

    System.Collections.IEnumerator AnimateDoors(bool toOpen)
    {
        _isBusy = true;
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

        _isBusy = false;
        _anim = null;
    }

    void ApplyProgressAll(float k01)
    {
        foreach (var d in doors)
            ApplyProgressOne(d, k01);
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

    // ===== Gizmos =====
    void OnDrawGizmosSelected()
    {
        if (doors == null) return;
        foreach (var d in doors)
        {
            if (!d.door) continue;
            DrawDoorAxisGizmo(d);
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

        Gizmos.color = (d.motion == MotionType.Rotation) ? new Color(1f, 0.85f, 0.2f, 0.9f)
                                                         : new Color(0.2f, 0.85f, 1f, 0.9f);
        var p = d.door.position;
        Gizmos.DrawLine(p, p + axisVec * 0.25f);
        Gizmos.DrawSphere(p + axisVec * 0.25f, 0.01f);
    }
}

/*
 // ถ้าคุณยังไม่มี UI แสดงข้อความ และอยากทดสอบเร็ว ๆ
 // สร้างไฟล์ใหม่ชื่อ SimplePlayerMessageUI.cs แล้ววางคลาสนี้แยกไฟล์ (อย่าใส่รวมไฟล์เดียวกันถ้าไม่ต้องการ)
 public class SimplePlayerMessageUI : MonoBehaviour
 {
     public void ShowCenter(string msg, float seconds = 1.2f)
     {
         Debug.Log($"[UI] {msg} ({seconds:0.##}s)");
     }
 }
*/
