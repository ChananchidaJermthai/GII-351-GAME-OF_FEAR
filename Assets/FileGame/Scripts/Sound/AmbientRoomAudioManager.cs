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
    [Range(0f, 1f)] public float duckTarget = 0.25f;
    [Range(0.01f, 2f)] public float duckAttack = 0.06f;
    [Range(0f, 3f)] public float duckHold = 0.8f;
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

        // setup defaultSource
        if (usePlayersAudioSources && defaultSourceFromPlayer)
        {
            defaultSource = defaultSourceFromPlayer;
            defaultSource.loop = true;
            defaultSource.playOnAwake = false;
        }
        else
        {
            defaultSource = gameObject.GetComponent<AudioSource>();
            if (!defaultSource) defaultSource = gameObject.AddComponent<AudioSource>();
            defaultSource.loop = true;
            defaultSource.playOnAwake = false;
            defaultSource.spatialBlend = 1f;
            defaultSource.volume = 1f;
            defaultSource.clip = defaultClip;
        }

        // room mixer
        var mixerGO = new GameObject("RoomMixer");
        mixerGO.transform.SetParent(transform, false);
        roomMixerRoot = mixerGO.transform;

        // preallocate first roomSource
        if (usePlayersAudioSources && roomSourceFromPlayer)
        {
            PrepareSource(roomSourceFromPlayer, defaultSource.spatialBlend);
            roomSources.Add(roomSourceFromPlayer);
        }

        // preallocate max possible roomSources
        int maxClips = 0;
        if (roomClips != null)
            foreach (var rm in roomClips)
                if (rm?.clips != null)
                    maxClips = Mathf.Max(maxClips, rm.clips.Count);

        for (int i = roomSources.Count; i < maxClips; i++)
        {
            var go = new GameObject("RoomTrack_" + i);
            go.transform.SetParent(roomMixerRoot, false);
            var src = go.AddComponent<AudioSource>();
            PrepareSource(src, defaultSource.spatialBlend);
            roomSources.Add(src);
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
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // smooth roomBus
        string cur = CurrentRoomId;
        float targetBus = string.IsNullOrEmpty(cur) ? 0f : roomBusTarget;
        roomBus = Mathf.Lerp(roomBus, targetBus, 1f - Mathf.Exp(-fadeToRoomTime * dt));

        float duckMultiplier = _duckActive ? SmoothDuck(dt) : 1f;

        ApplyBusToRoomTracks(roomBus, duckMultiplier);

        float baseDefault = Mathf.Clamp01(defaultVolume) * (1f - roomBus) * duckMultiplier;
        defaultSource.volume = Mathf.Lerp(defaultSource.volume, baseDefault, 1f - Mathf.Exp(-fadeToDefaultTime * dt));
        if (defaultSource.clip && !defaultSource.isPlaying) defaultSource.Play();
    }

    float SmoothDuck(float dt)
    {
        if (_duckTimerAttack > 0f)
        {
            _duck = Mathf.Lerp(_duck, _duckGoal, 1f - Mathf.Exp(-_duckTimerAttack * dt));
            _duckTimerAttack -= dt;
        }
        else if (_duckTimerHold > 0f)
        {
            _duckTimerHold -= dt;
            _duck = _duckGoal;
        }
        else if (_duckTimerRelease > 0f)
        {
            _duck = Mathf.Lerp(_duck, 1f, 1f - Mathf.Exp(-_duckTimerRelease * dt));
            _duckTimerRelease -= dt;
            if (_duckTimerRelease <= 0f) _duckActive = false;
        }
        return _duck;
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
            foreach (var kv in zoneIdByCollider)
                if (kv.Value == id) { stillAny = true; break; }

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
        }
        else
        {
            roomBusTarget = 0f;
            currentRoomConfig = null;
        }
    }

    void ApplyBusToRoomTracks(float bus, float duckMultiplier)
    {
        if (currentRoomConfig?.clips == null) return;

        for (int i = 0; i < currentRoomConfig.clips.Count; i++)
        {
            var s = roomSources[i];
            var sc = currentRoomConfig.clips[i];
            if (!s || sc == null) continue;

            float target = Mathf.Clamp01(bus) * Mathf.Clamp01(sc.volume) * duckMultiplier;
            s.volume = Mathf.Lerp(s.volume, target, 1f - Mathf.Exp(-fadeToRoomTime * Time.deltaTime));

            // hysteresis
            if (target > 0.02f && !s.isPlaying) s.Play();
            if (target < 0.005f && s.isPlaying) s.Stop();
        }
    }

    void PrepareSource(AudioSource src, float spatial)
    {
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = spatial;
        src.volume = 0f;
    }

    string GetRoomId(GameObject zone, Collider col)
    {
        if (idSource == IdSource.Tag) return zone ? zone.tag : null;
        return col?.sharedMaterial?.name;
    }

    bool TryGetRoomConfig(string id, out RoomMap cfg)
    {
        if (roomClips != null)
            foreach (var rm in roomClips)
                if (rm != null && rm.id == id)
                {
                    cfg = rm;
                    return true;
                }
        cfg = null;
        return false;
    }
}
