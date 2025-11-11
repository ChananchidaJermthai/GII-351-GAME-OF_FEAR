using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AmbientRoomAudioManager : MonoBehaviour
{
    public static AmbientRoomAudioManager Instance { get; private set; }

    [Header("References")]
    public Transform Player;

    [Header("Default Settings")]
    [Tooltip("เสียง ambient พื้นฐานเมื่ออยู่นอกทุกห้อง")]
    public AudioClip defaultClip;
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Header("Follow / Output Mode")]
    [Tooltip("ให้ตัว Manager ตามผู้เล่นเพื่อให้เสียง 3D ติดตัวผู้เล่น")]
    public bool attachToPlayer = true;

    [Tooltip("ใช้ AudioSource ที่อยู่บน Player แทนการสร้างใหม่")]
    public bool usePlayersAudioSources = false;
    [Tooltip("กำหนด AudioSource บน Player สำหรับ Default (ปล่อยว่างถ้าไม่ใช้)")]
    public AudioSource defaultSourceFromPlayer;
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
        [Range(0f, 1f)] public float volume = 1f;
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
    readonly List<AudioSource> roomSources = new List<AudioSource>();
    Transform roomMixerRoot;

    readonly List<string> activeRoomStack = new List<string>();
    readonly Dictionary<Collider, string> zoneIdByCollider = new Dictionary<Collider, string>();

    string CurrentRoomId => activeRoomStack.Count > 0 ? activeRoomStack[activeRoomStack.Count - 1] : null;

    float roomBus;
    float roomBusTarget;
    RoomMap currentRoomConfig;

    // === Global Ducking (Focus Event) ===
    [Header("Global Ducking (Focus Event)")]
    public bool enableGlobalDucking = true;
    [Tooltip("ระดับลดเสียงของ ambience ระหว่างโฟกัสอีเวนต์ (0 = เงียบ, 1 = ปกติ)")]
    [Range(0f, 1f)] public float duckTarget = 0.25f;
    [Tooltip("เวลาลดเสียงให้ถึง duckTarget")]
    [Range(0.01f, 2f)] public float duckAttack = 0.06f;
    [Tooltip("เวลาค้างโฟกัส")]
    [Range(0f, 3f)] public float duckHold = 0.8f;
    [Tooltip("เวลาคลายกลับปกติ")]
    [Range(0.01f, 2f)] public float duckRelease = 0.6f;

    float _duck = 1f;
    float _duckGoal = 1f;
    float _duckTimerAttack, _duckTimerHold, _duckTimerRelease;
    bool _duckActive = false;

    public static void FocusDuck()
    {
        if (Instance) Instance.BeginFocusDuck(Instance.duckTarget, Instance.duckAttack, Instance.duckHold, Instance.duckRelease);
    }
    public static void FocusDuck(float target, float attack, float hold, float release)
    {
        if (Instance) Instance.BeginFocusDuck(target, attack, hold, release);
    }
    public void BeginFocusDuck(float target, float attack, float hold, float release)
    {
        if (!enableGlobalDucking) return;

        target = Mathf.Clamp01(target);
        attack = Mathf.Max(0.01f, attack);
        release = Mathf.Max(0.01f, release);
        hold = Mathf.Max(0f, hold);

        _duckActive = true;
        _duckGoal = target;
        _duckTimerAttack = attack;
        _duckTimerHold = hold;
        _duckTimerRelease = release;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (attachToPlayer && Player)
        {
            transform.SetParent(Player, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
        }

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

        var mixerGO = new GameObject("RoomMixer");
        mixerGO.transform.SetParent(transform, false);
        roomMixerRoot = mixerGO.transform;

        if (usePlayersAudioSources && roomSourceFromPlayer)
        {
            PrepareSource(roomSourceFromPlayer, defaultSource ? defaultSource.spatialBlend : 1f);
            roomSources.Add(roomSourceFromPlayer);
        }
    }

    void Start()
    {
        if (defaultClip && defaultSource.clip != defaultClip) defaultSource.clip = defaultClip;
        if (defaultSource.clip && !defaultSource.isPlaying) defaultSource.Play();

        roomBus = 0f;
        roomBusTarget = 0f;

        defaultSource.volume = Mathf.Clamp01(defaultVolume);
    }

    void Update()
    {
        string cur = CurrentRoomId;

        if (string.IsNullOrEmpty(cur))
            roomBus = Mathf.MoveTowards(roomBus, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
        else
            roomBus = Mathf.MoveTowards(roomBus, roomBusTarget, Time.deltaTime / Mathf.Max(0.01f, fadeToRoomTime));

        float duckMultiplier = 1f;
        if (_duckActive)
        {
            if (_duckTimerAttack > 0f)
            {
                _duck = Mathf.MoveTowards(_duck, _duckGoal, Time.deltaTime / _duckTimerAttack);
                _duckTimerAttack -= Time.deltaTime;
                if (_duckTimerAttack <= 0f) _duck = _duckGoal;
            }
            else if (_duckTimerHold > 0f)
            {
                _duckTimerHold -= Time.deltaTime;
                _duck = _duckGoal;
            }
            else if (_duckTimerRelease > 0f)
            {
                _duck = Mathf.MoveTowards(_duck, 1f, Time.deltaTime / _duckTimerRelease);
                _duckTimerRelease -= Time.deltaTime;
                if (_duckTimerRelease <= 0f) { _duck = 1f; _duckActive = false; }
            }
            duckMultiplier = _duck;
        }
        else
        {
            _duck = 1f;
            duckMultiplier = 1f;
        }

        ApplyBusToRoomTracks(roomBus, duckMultiplier);

        float baseDefault = Mathf.Clamp01(defaultVolume) * (1f - roomBus);
        defaultSource.volume = baseDefault * duckMultiplier;

        if (defaultSource.clip && !defaultSource.isPlaying) defaultSource.Play();
    }

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
            foreach (var kv in zoneIdByCollider) { if (kv.Value == id) { stillAny = true; break; } }
            if (!stillAny) activeRoomStack.Remove(id);

            ApplyRoom(CurrentRoomId);
        }
    }

    void ApplyRoom(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            roomBusTarget = 0f;
            currentRoomConfig = null;
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
            roomBusTarget = 0f;
            currentRoomConfig = null;
        }
    }

    void RebuildRoomTracks(RoomMap cfg)
    {
        int need = Mathf.Max(0, cfg.clips?.Count ?? 0);

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

        for (int i = 0; i < need; i++)
        {
            var sc = cfg.clips[i];
            var s = roomSources[i];

            if (s.clip != sc.clip)
            {
                s.clip = sc.clip;
                if (roomBusTarget > 0f && sc.clip) s.Play();
            }
            s.loop = sc.loop;
        }
    }

    void ApplyBusToRoomTracks(float bus, float duckMultiplier)
    {
        if (currentRoomConfig != null && currentRoomConfig.clips != null)
        {
            for (int i = 0; i < roomSources.Count; i++)
            {
                var s = roomSources[i];
                var sc = i < currentRoomConfig.clips.Count ? currentRoomConfig.clips[i] : null;
                if (s == null || sc == null) continue;

                float target = Mathf.Clamp01(bus) * Mathf.Clamp01(sc.volume);
                target *= duckMultiplier;

                if (target > 0.01f)
                {
                    if (sc.clip && !s.isPlaying) s.Play();
                    s.volume = target;
                }
                else
                {
                    s.volume = Mathf.MoveTowards(s.volume, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeToDefaultTime));
                    if (s.isPlaying && s.volume <= 0.01f) s.Stop();
                }
            }
        }
        else
        {
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
