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

    [Header("Follow / Output Mode")]
    [Tooltip("ให้ตัว Manager ตามผู้เล่นเพื่อให้เสียง 3D ติดตัวผู้เล่น")]
    public bool attachToPlayer = true;

    [Tooltip("ใช้ AudioSource ที่อยู่บน Player แทนการสร้างใหม่")]
    public bool usePlayersAudioSources = false;
    [Tooltip("กำหนด AudioSource บน Player สำหรับ Default (ปล่อยว่างถ้าไม่ใช้)")]
    public AudioSource defaultSourceFromPlayer;
    [Header("Default Settings")]
    [Range(0f, 1f)] public float defaultVolume = 1f; // ความดังพื้นฐานของ default clip

    [Tooltip("กำหนด AudioSource บน Player สำหรับ Room แทร็กแรก (ที่เหลือจะสร้างเพิ่มอัตโนมัติ)")]
    public AudioSource roomSourceFromPlayer;

    [Header("Fade Settings")]
    [Range(0.01f, 5f)] public float fadeToRoomTime = 0.5f;
    [Range(0.01f, 5f)] public float fadeToDefaultTime = 0.5f;

    public enum IdSource { Tag, PhysicMaterialName }
    public IdSource idSource = IdSource.Tag;

    [Serializable]
    public class SubClip
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f; // เลเวลของแทร็กย่อย
        public bool loop = true;
    }

    [Serializable]
    public class RoomMap
    {
        public string id = "bathroom";
        [Tooltip("ปรับเลเวลรวม (bus) ของห้องนี้")]
        [Range(0f, 1f)] public float busVolume = 1f;
        [Tooltip("รายการเสียงหลายแทร็กในห้องเดียว (เล่นซ้อนกันได้)")]
        public List<SubClip> clips = new List<SubClip>();
    }
    public RoomMap[] roomClips;

    // ===== runtime =====
    AudioSource defaultSource;
    readonly List<AudioSource> roomSources = new List<AudioSource>(); // รองรับหลายแทร็ก
    Transform roomMixerRoot; // โหนดไว้ใส่ซับซอร์ส

    // ห้องที่ผู้เล่นกำลังอยู่ (ลำดับเวลาเข้า) — ตัวท้ายสุดคือ “ห้องล่าสุด”
    readonly List<string> activeRoomStack = new List<string>();
    // โซน (Collider) -> roomId
    readonly Dictionary<Collider, string> zoneIdByCollider = new Dictionary<Collider, string>();

    string CurrentRoomId => activeRoomStack.Count > 0 ? activeRoomStack[activeRoomStack.Count - 1] : null;

    float roomBus;               // ค่าปัจจุบันของ bus (0..1) ใช้ cross-fade กับ default
    float roomBusTarget;         // เป้าหมายของ bus
    RoomMap currentRoomConfig;   // เก็บคอนฟิกล่าสุดเพื่ออัปเดตซอร์ส

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (attachToPlayer && Player)
        {
            transform.SetParent(Player, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
        }

        // Prepare default source
        if (usePlayersAudioSources && defaultSourceFromPlayer)
        {
            defaultSource = defaultSourceFromPlayer;
            defaultSource.loop = true; defaultSource.playOnAwake = false;
        }
        else
        {
            defaultSource = gameObject.GetComponent<AudioSource>();
            if (!defaultSource) defaultSource = gameObject.AddComponent<AudioSource>();
            defaultSource.loop = true; defaultSource.playOnAwake = false;
            defaultSource.spatialBlend = 1f; // set 0 for 2D ambience
            defaultSource.volume = 1f; defaultSource.clip = defaultClip;
        }

        // Create mixer root for room sources
        var mixerGO = new GameObject("RoomMixer");
        mixerGO.transform.SetParent(transform, false);
        roomMixerRoot = mixerGO.transform;

        // If using player's first room source, add it into our pool; others will be created as needed
        if (usePlayersAudioSources && roomSourceFromPlayer)
        {
            PrepareSource(roomSourceFromPlayer, defaultSource.spatialBlend);
            roomSources.Add(roomSourceFromPlayer);
        }
    }

    void Start()
    {
        if (defaultClip && defaultSource.clip != defaultClip) defaultSource.clip = defaultClip;
        if (defaultSource.clip && !defaultSource.isPlaying) defaultSource.Play();

        roomBus = 0f;
        roomBusTarget = 0f;

        defaultSource.volume = Mathf.Clamp01(defaultVolume); // ตั้งเลเวลเริ่มต้น
    }


    void Update()
    {
        // cross-fade bus value
        string cur = CurrentRoomId;
        if (string.IsNullOrEmpty(cur))
        {
            // เฟดกลับ default
            roomBus = Mathf.MoveTowards(roomBus, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
        }
        else
        {
            roomBus = Mathf.MoveTowards(roomBus, roomBusTarget, Time.deltaTime / Mathf.Max(0.01f, fadeToRoomTime));
        }

        // apply to room tracks
        ApplyBusToRoomTracks(roomBus);

        // default volume เป็น 1 - roomBus เพื่อไม่ให้ดังเกินรวม 1
        defaultSource.volume = Mathf.Clamp01(defaultVolume) * (1f - roomBus);


        // Safety: default always playing so there’s never silence
        if (defaultSource.clip && !defaultSource.isPlaying) defaultSource.Play();
    }

    // ====== เรียกจากโซน ======
    public void OnZoneEnter(GameObject zoneGO, Collider zoneCol)
    {
        if (!Player) return;

        string id = GetRoomId(zoneGO, zoneCol);
        if (string.IsNullOrEmpty(id)) return;

        zoneIdByCollider[zoneCol] = id;

        if (!activeRoomStack.Contains(id))
            activeRoomStack.Add(id);
        else
        {
            activeRoomStack.Remove(id);
            activeRoomStack.Add(id);
        }

        ApplyRoom(CurrentRoomId);
    }

    public void OnZoneExit(GameObject zoneGO, Collider zoneCol)
    {
        if (zoneCol && zoneIdByCollider.TryGetValue(zoneCol, out var id))
        {
            zoneIdByCollider.Remove(zoneCol);

            bool stillAny = false;
            foreach (var kv in zoneIdByCollider)
            {
                if (kv.Value == id) { stillAny = true; break; }
            }
            if (!stillAny) activeRoomStack.Remove(id);

            ApplyRoom(CurrentRoomId);
        }
    }

    // ====== ภายใน ======
    void ApplyRoom(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            // no room => fade back to default
            roomBusTarget = 0f;
            currentRoomConfig = null;
            // ไม่หยุดซอร์สทันที ปล่อยให้เฟดลงด้วย bus (แล้วหยุดเองเมื่อเบา)
            return;
        }

        if (TryGetRoomConfig(id, out var cfg))
        {
            currentRoomConfig = cfg;
            roomBusTarget = Mathf.Clamp01(cfg.busVolume);
            RebuildRoomTracks(cfg);
        }
        else
        {
            // ไม่พบ mapping → กลับ default
            roomBusTarget = 0f;
            currentRoomConfig = null;
        }
    }

    void RebuildRoomTracks(RoomMap cfg)
    {
        int need = Mathf.Max(0, cfg.clips?.Count ?? 0);

        // เพิ่ม/ลดจำนวน AudioSource ให้เท่ากับจำนวนคลิป
        // กรณีมี roomSourceFromPlayer จะถูกใช้เป็นช่องแรกอยู่แล้ว
        while (roomSources.Count < need)
        {
            var go = new GameObject("RoomTrack_" + roomSources.Count);
            go.transform.SetParent(roomMixerRoot, false);
            var src = go.AddComponent<AudioSource>();
            PrepareSource(src, defaultSource ? defaultSource.spatialBlend : 1f);
            roomSources.Add(src);
        }
        while (roomSources.Count > need)
        {
            var last = roomSources[roomSources.Count - 1];
            if (last) Destroy(last.gameObject);
            roomSources.RemoveAt(roomSources.Count - 1);
        }

        // เซ็ตคลิป/loop ให้แต่ละแทร็ก (ยังไม่ตั้ง volume ตรงนี้ ปล่อยให้ bus จัดการ)
        for (int i = 0; i < need; i++)
        {
            var sc = cfg.clips[i];
            var s = roomSources[i];

            if (s.clip != sc.clip)
            {
                s.clip = sc.clip;
                // ถ้า bus > 0 ให้เริ่มเล่นทันทีเพื่อความต่อเนื่อง
                if (roomBusTarget > 0f && sc.clip) s.Play();
            }
            s.loop = sc.loop;
        }
    }

    void ApplyBusToRoomTracks(float bus)
    {
        // ปรับโวลลูมแยกแต่ละแทร็ก = bus * subTrack.volume
        if (currentRoomConfig != null && currentRoomConfig.clips != null)
        {
            for (int i = 0; i < roomSources.Count; i++)
            {
                var s = roomSources[i];
                var sc = i < currentRoomConfig.clips.Count ? currentRoomConfig.clips[i] : null;

                if (s == null || sc == null)
                    continue;

                float target = Mathf.Clamp01(bus) * Mathf.Clamp01(sc.volume);

                // start/stop ตามระดับเสียง
                if (target > 0.01f)
                {
                    if (sc.clip && !s.isPlaying) s.Play();
                    s.volume = target;
                }
                else
                {
                    // ค่อย ๆ ลดเสียงลง
                    s.volume = Mathf.MoveTowards(s.volume, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
                    if (s.isPlaying && s.volume <= 0.01f) s.Stop();
                }
            }
        }
        else
        {
            // ไม่มีห้อง → ลดทุกแทร็กลง
            foreach (var s in roomSources)
            {
                if (!s) continue;
                s.volume = Mathf.MoveTowards(s.volume, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
                if (s.isPlaying && s.volume <= 0.01f) s.Stop();
            }
        }
    }

    void PrepareSource(AudioSource src, float spatial)
    {
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = spatial; // 1 = 3D, 0 = 2D
        src.volume = 0f;
    }

    string GetRoomId(GameObject zone, Collider col)
    {
        if (idSource == IdSource.Tag) return zone ? zone.tag : null;
        var pm = col ? col.sharedMaterial : null;
        return pm ? pm.name : null;
    }

    bool TryGetRoomConfig(string id, out RoomMap cfg)
    {
        if (roomClips != null)
        {
            foreach (var rm in roomClips)
            {
                if (rm != null && rm.id == id)
                {
                    cfg = rm;
                    return true;
                }
            }
        }
        cfg = null;
        return false;
    }
}
