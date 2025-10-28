using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AmbientRoomAudioManager : MonoBehaviour
{
    public static AmbientRoomAudioManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("ตัวละคร/ผู้เล่น (ไว้ตรวจว่าคนที่เข้าโซนคือผู้เล่นจริง)")]
    public Transform Player;

    [Tooltip("AudioSource สำหรับเสียง default (เล่นตลอด)")]
    public AudioSource defaultSource;

    [Tooltip("AudioSource สำหรับเสียงในห้อง (จะ cross-fade กับ default)")]
    public AudioSource roomSource;

    [Header("Fade")]
    [Range(0.01f, 5f)] public float fadeToRoomTime = 0.3f;
    [Range(0.01f, 5f)] public float fadeToDefaultTime = 0.3f;

    //[Header("Room ID ")]
    public enum IdSource { Tag, PhysicMaterialName }
    [Tooltip("อ่านชื่อห้องจาก Tag ของ GameObject โซน หรือจาก PhysicMaterial.name ของ Collider โซน")]
    public IdSource idSource = IdSource.Tag;

    //[Header("Room Mappings (id -> clip)")]
    [Serializable]
    public class RoomMap
    {
        [Tooltip("id ห้อง เช่น bathroom, kitchen ฯลฯ (ต้องตรงกับ Tag หรือชื่อ PhysicMaterial)")]
        public string id = "bathroom";
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }
    public RoomMap[] roomClips;

    // runtime
    string _currentRoomId = null;   // ห้องที่กำลังเล่น
    string _pendingRoomId = null;   // ห้องล่าสุดที่เข้าทับ
    float _roomTargetVol = 0f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!defaultSource)
        {
            defaultSource = gameObject.AddComponent<AudioSource>();
            defaultSource.loop = true; defaultSource.playOnAwake = false;
            defaultSource.spatialBlend = 1f;
            defaultSource.volume = 1f;
        }

        if (!roomSource)
        {
            roomSource = gameObject.AddComponent<AudioSource>();
            roomSource.loop = true; roomSource.playOnAwake = false;
            roomSource.spatialBlend = 1f;
            roomSource.volume = 0f;
        }
    }

    void Start()
    {
        // ให้ default เล่นตลอด (ถ้าตั้ง clip ไว้)
        if (defaultSource.clip && !defaultSource.isPlaying)
            defaultSource.Play();
    }

    void Update()
    {
        // cross-fade
        float kRoom = Time.deltaTime / Mathf.Max(0.01f,
                          (_pendingRoomId != null ? fadeToRoomTime : fadeToDefaultTime));

        // room volume ไปหา target
        roomSource.volume = Mathf.MoveTowards(roomSource.volume, _roomTargetVol, kRoom);

        // default ผกผันกับ room (ให้รวมกันไม่พุ่ง)
        defaultSource.volume = 1f - roomSource.volume;

        // เริ่ม/หยุด roomSource ตามความดัง
        if (_roomTargetVol > 0.001f)
        {
            if (!roomSource.isPlaying && roomSource.clip) roomSource.Play();
        }
        else
        {
            if (roomSource.isPlaying && roomSource.volume <= 0.001f) roomSource.Stop();
        }

        // เมื่อเฟดเข้าห้องเสร็จ ให้ถือว่า “อยู่ในห้อง” แล้ว
        if (_pendingRoomId != null && Mathf.Abs(roomSource.volume - _roomTargetVol) < 0.01f)
        {
            _currentRoomId = _pendingRoomId;
            _pendingRoomId = null;
        }
        // เมื่อเฟดกลับ default เสร็จ ให้เคลียร์ current
        if (_pendingRoomId == null && _roomTargetVol <= 0.001f && roomSource.volume <= 0.001f)
        {
            _currentRoomId = null;
        }
    }

    /// เรียกเมื่อผู้เล่น “เข้า” โซนห้อง
    public void EnterRoom(GameObject zoneGO, Collider zoneCollider)
    {
        if (!IsPlayerValid()) return;

        string id = GetRoomIdFromZone(zoneGO, zoneCollider);
        if (string.IsNullOrEmpty(id)) return;

        // หาแม็ป
        if (TryGetRoomClip(id, out var clip, out var vol))
        {
            // เปลี่ยนคลิปถ้าจำเป็น
            if (roomSource.clip != clip)
            {
                roomSource.clip = clip;
                if (roomSource.volume > 0.001f) roomSource.Play();
            }
            _roomTargetVol = vol;
            _pendingRoomId = id;
        }
        else
        {
            // ไม่พบ mapping → กลับ default
            ExitAllRooms();
        }
    }

    /// เรียกเมื่อผู้เล่น “ออก” โซนห้อง (ถ้าออกจากห้องที่กำลังเล่นอยู่)
    public void ExitRoom(GameObject zoneGO, Collider zoneCollider)
    {
        if (!IsPlayerValid()) return;

        string id = GetRoomIdFromZone(zoneGO, zoneCollider);
        if (string.IsNullOrEmpty(id)) return;

        // ถ้าโซนที่ออกคือห้องปัจจุบันหรือ pending → กลับ default
        if (id == _currentRoomId || id == _pendingRoomId)
        {
            ExitAllRooms();
        }
    }

    public void ExitAllRooms()
    {
        _pendingRoomId = null;
        _roomTargetVol = 0f; // เฟดกลับ default
    }

    bool TryGetRoomClip(string id, out AudioClip clip, out float vol)
    {
        if (roomClips != null)
        {
            for (int i = 0; i < roomClips.Length; i++)
            {
                if (roomClips[i] != null && roomClips[i].id == id)
                {
                    clip = roomClips[i].clip;
                    vol = roomClips[i].volume;
                    return clip != null;
                }
            }
        }
        clip = null; vol = 0f;
        return false;
    }

    string GetRoomIdFromZone(GameObject zoneGO, Collider zoneCol)
    {
        if (idSource == IdSource.Tag)
        {
            return zoneGO.tag; // ใช้ Tag ของ GameObject โซนนั้น
        }
        else
        {
            var pm = zoneCol ? zoneCol.sharedMaterial : null;
            return pm ? pm.name : null; // ใช้ชื่อ PhysicMaterial
        }
    }

    bool IsPlayerValid()
    {
        return Player != null && defaultSource != null && roomSource != null;
    }
}
