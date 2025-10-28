using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class AmbientRoomZone : MonoBehaviour
{
    Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player) return;

        // ผู้เล่นเข้าโซน?
        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
        {
            mgr.EnterRoom(gameObject, _col);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player) return;

        // ผู้เล่นออกโซน?
        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
        {
            mgr.ExitRoom(gameObject, _col);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }
#endif
}
