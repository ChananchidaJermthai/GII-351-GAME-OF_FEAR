using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class AmbientRoomZone : MonoBehaviour
{
    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player) return;

        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
            mgr.OnZoneEnter(gameObject, col);
    }

    void OnTriggerExit(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player) return;

        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
            mgr.OnZoneExit(gameObject, col);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }
#endif
}
