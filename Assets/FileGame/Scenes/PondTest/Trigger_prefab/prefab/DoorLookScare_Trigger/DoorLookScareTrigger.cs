using UnityEngine;

public class DoorLookScareTrigger : MonoBehaviour
{
    public DoorLookScare doorLookScare;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (doorLookScare == null) return;

        doorLookScare.Activate();

        // ถ้าอยากให้ Trigger ใช้ได้ครั้งเดียว
        Destroy(gameObject);
    }
}
