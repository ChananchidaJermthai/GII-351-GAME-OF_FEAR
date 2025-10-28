using UnityEngine;

[RequireComponent(typeof(Collider))]
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
        if (mgr && mgr.Player && (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player)))
        {
            mgr.EnterRoom(gameObject, col);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var mgr = AmbientRoomAudioManager.Instance;
        if (mgr && mgr.Player && (other.transform == mgr.Player || other.transform.IsChildOf(mgr.Player)))
        {
            mgr.ExitRoom(gameObject, col);
        }
    }
}
