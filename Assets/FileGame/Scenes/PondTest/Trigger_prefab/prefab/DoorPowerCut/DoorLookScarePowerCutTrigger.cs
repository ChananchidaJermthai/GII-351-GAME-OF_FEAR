using UnityEngine;

public class DoorLookScarePowerCutTrigger : MonoBehaviour
{
    public DoorLookScarePowerCut scare;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!scare) return;

        scare.Activate();
        Destroy(gameObject); // ใช้ครั้งเดียว
    }
}
