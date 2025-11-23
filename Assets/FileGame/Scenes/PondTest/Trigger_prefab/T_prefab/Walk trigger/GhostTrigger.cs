using UnityEngine;

public class GhostTrigger : MonoBehaviour
{
    [SerializeField] public GhostController ghostController;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;
            ghostController.StartSequence(); // สั่งเริ่ม sequence ผี
        }
    }
}
