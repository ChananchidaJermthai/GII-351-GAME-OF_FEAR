using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AmbientRoomAudioManager : MonoBehaviour
{
    public static AmbientRoomAudioManager Instance { get; private set; }

    [Header("References")]
    public Transform Player;

    [Tooltip("เสียง ambient พื้นฐานเมื่ออยู่นอกทุกห้อง")]
    public AudioClip defaultClip;

    [Header("Fade Settings")]
    [Range(0.01f, 5f)] public float fadeToRoomTime = 0.5f;
    [Range(0.01f, 5f)] public float fadeToDefaultTime = 0.5f;

    //[Header("Room ID Source")]
    public enum IdSource { Tag, PhysicMaterialName }
    public IdSource idSource = IdSource.Tag;

    //[Header("Room Mappings (id -> clip)")]
    [Serializable]
    public class RoomMap
    {
        public string id = "bathroom";
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }
    public RoomMap[] roomClips;

    // runtime audio
    AudioSource defaultSource, roomSource;

    // กองห้องที่ผู้เล่นกำลังอยู่ (ลำดับตามเวลาเข้า) — Last = ห้องล่าสุดที่เข้า
    readonly List<string> activeRoomStack = new List<string>();
    // map โซน (Collider) -> roomId เพื่อรู้ว่าห้องไหนกำลัง active
    readonly Dictionary<Collider, string> zoneIdByCollider = new Dictionary<Collider, string>();

    string CurrentRoomId => activeRoomStack.Count > 0 ? activeRoomStack[activeRoomStack.Count - 1] : null;
    float roomTargetVol = 0f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        defaultSource = gameObject.AddComponent<AudioSource>();
        defaultSource.loop = true; defaultSource.playOnAwake = false;
        defaultSource.spatialBlend = 1f; defaultSource.volume = 1f; defaultSource.clip = defaultClip;

        roomSource = gameObject.AddComponent<AudioSource>();
        roomSource.loop = true; roomSource.playOnAwake = false;
        roomSource.spatialBlend = 1f; roomSource.volume = 0f;
    }

    void Start()
    {
        if (defaultSource.clip) defaultSource.Play();
    }

    void Update()
    {
        string cur = CurrentRoomId;

        if (string.IsNullOrEmpty(cur))
        {
            // ไม่มีห้อง → เฟดกลับ default
            roomSource.volume = Mathf.MoveTowards(roomSource.volume, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
            defaultSource.volume = 1f - roomSource.volume;
            if (roomSource.isPlaying && roomSource.volume <= 0.01f) roomSource.Stop();
        }
        else
        {
            // มีห้อง → เฟดเข้า room
            roomSource.volume = Mathf.MoveTowards(roomSource.volume, roomTargetVol, Time.deltaTime / Mathf.Max(0.01f, fadeToRoomTime));
            defaultSource.volume = 1f - roomSource.volume;

            if (roomSource.volume > 0.01f && roomSource.clip && !roomSource.isPlaying) roomSource.Play();
        }
    }

    // ====== เรียกจากโซน ======
    public void OnZoneEnter(GameObject zoneGO, Collider zoneCol)
    {
        if (!Player) return;

        string id = GetRoomId(zoneGO, zoneCol);
        if (string.IsNullOrEmpty(id)) return;

        // จดจำว่า collider นี้คือห้องอะไร
        zoneIdByCollider[zoneCol] = id;

        // push ห้องใหม่ (ถ้ายังไม่มีใน stack)
        if (!activeRoomStack.Contains(id))
            activeRoomStack.Add(id);
        else
        {
            // ถ้าอยู่แล้วใน stack ให้ย้ายไปไว้ท้าย (ถือว่า re-enter ล่าสุด)
            activeRoomStack.Remove(id);
            activeRoomStack.Add(id);
        }

        // ตั้ง clip/vol ตามห้องล่าสุด
        ApplyRoom(CurrentRoomId);
    }

    public void OnZoneExit(GameObject zoneGO, Collider zoneCol)
    {
        if (zoneCol && zoneIdByCollider.TryGetValue(zoneCol, out var id))
        {
            zoneIdByCollider.Remove(zoneCol);
            // ลบห้องนี้ออกจาก stack (ถ้ามีหลาย collider ชี้ห้องเดียวกัน จะต้องออกหมดก่อน)
            bool stillAnyColliderForThatRoom = false;
            foreach (var kv in zoneIdByCollider)
            {
                if (kv.Value == id) { stillAnyColliderForThatRoom = true; break; }
            }
            if (!stillAnyColliderForThatRoom)
                activeRoomStack.Remove(id);

            // ตั้ง clip/vol ตามห้องล่าสุดที่เหลืออยู่ (หรือกลับ default ถ้าไม่มี)
            ApplyRoom(CurrentRoomId);
        }
    }

    // ====== ภายใน ======
    void ApplyRoom(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            roomTargetVol = 0f; // เฟดกลับ default
            return;
        }

        if (TryGetRoomClip(id, out var clip, out var vol))
        {
            if (roomSource.clip != clip)
            {
                roomSource.clip = clip;
                if (roomSource.volume > 0.01f) roomSource.Play();
            }
            roomTargetVol = vol;
        }
        else
        {
            // ไม่มี mapping → ถือว่าไม่เข้าเงื่อนไขห้อง, กลับ default
            roomTargetVol = 0f;
        }
    }

    string GetRoomId(GameObject zone, Collider col)
    {
        if (idSource == IdSource.Tag)
            return zone ? zone.tag : null;
        var pm = col ? col.sharedMaterial : null;
        return pm ? pm.name : null;
    }

    bool TryGetRoomClip(string id, out AudioClip clip, out float vol)
    {
        if (roomClips != null)
        {
            foreach (var rm in roomClips)
            {
                if (rm != null && rm.id == id)
                {
                    clip = rm.clip; vol = rm.volume;
                    return clip != null;
                }
            }
        }
        clip = null; vol = 0f;
        return false;
    }
}
