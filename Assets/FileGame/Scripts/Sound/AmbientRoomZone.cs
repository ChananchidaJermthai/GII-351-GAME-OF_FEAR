using UnityEngine;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class AmbientRoomZone : MonoBehaviour
{
    private Collider col;

    void Awake()
    {
        if (TryGetComponent(out Collider c))
        {
            col = c;
            col.isTrigger = true;
        }
        else
        {
            Debug.LogError($"AmbientRoomZone requires a Collider on {gameObject.name}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player || col == null) return;

        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
            mgr.OnZoneEnter(gameObject, col);
    }

    void OnTriggerExit(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (!mgr || !mgr.Player || col == null) return;

        if (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player))
            mgr.OnZoneExit(gameObject, col);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (TryGetComponent(out Collider c))
        {
            c.isTrigger = true;
        }
    }
#endif
}
