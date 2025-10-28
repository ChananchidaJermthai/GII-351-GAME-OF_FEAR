using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AmbientRoomAudioManager : MonoBehaviour
{
    public static AmbientRoomAudioManager Instance { get; private set; }

    [Header("References")]
    public Transform Player;

    [Tooltip("เสียง ambient พื้นฐานที่จะเล่นเมื่ออยู่นอกห้อง")]
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

    AudioSource defaultSource, roomSource;
    string currentRoomId;
    float roomTargetVol;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Default source
        defaultSource = gameObject.AddComponent<AudioSource>();
        defaultSource.loop = true;
        defaultSource.playOnAwake = false;
        defaultSource.spatialBlend = 1f;
        defaultSource.volume = 1f;
        defaultSource.clip = defaultClip;

        // Room source
        roomSource = gameObject.AddComponent<AudioSource>();
        roomSource.loop = true;
        roomSource.playOnAwake = false;
        roomSource.spatialBlend = 1f;
        roomSource.volume = 0f;
    }

    void Start()
    {
        if (defaultSource.clip) defaultSource.Play();
    }

    void Update()
    {
        // cross-fade volumes
        if (string.IsNullOrEmpty(currentRoomId))
        {
            // fade to default
            roomSource.volume = Mathf.MoveTowards(roomSource.volume, 0f, Time.deltaTime / fadeToDefaultTime);
            defaultSource.volume = 1f - roomSource.volume;
        }
        else
        {
            // fade to room
            roomSource.volume = Mathf.MoveTowards(roomSource.volume, roomTargetVol, Time.deltaTime / fadeToRoomTime);
            defaultSource.volume = 1f - roomSource.volume;

            if (roomSource.volume > 0.01f && !roomSource.isPlaying) roomSource.Play();
            if (roomSource.volume <= 0.01f && roomSource.isPlaying) roomSource.Stop();
        }
    }

    // เรียกจาก AmbientRoomZone เมื่อผู้เล่นเข้า
    public void EnterRoom(GameObject zone, Collider col)
    {
        string id = GetRoomId(zone, col);
        if (TryGetRoomClip(id, out var clip, out var vol))
        {
            if (roomSource.clip != clip)
            {
                roomSource.clip = clip;
                roomSource.Play();
            }
            currentRoomId = id;
            roomTargetVol = vol;
        }
    }

    // เรียกจาก AmbientRoomZone เมื่อผู้เล่นออก
    public void ExitRoom(GameObject zone, Collider col)
    {
        string id = GetRoomId(zone, col);
        if (id == currentRoomId)
        {
            currentRoomId = null;
            roomTargetVol = 0f;
        }
    }

    string GetRoomId(GameObject zone, Collider col)
    {
        if (idSource == IdSource.Tag)
            return zone.tag;
        else
            return col && col.sharedMaterial ? col.sharedMaterial.name : null;
    }

    bool TryGetRoomClip(string id, out AudioClip clip, out float vol)
    {
        if (roomClips != null)
        {
            foreach (var rm in roomClips)
            {
                if (rm != null && rm.id == id)
                {
                    clip = rm.clip;
                    vol = rm.volume;
                    return true;
                }
            }
        }
        clip = null; vol = 0f;
        return false;
    }
}
