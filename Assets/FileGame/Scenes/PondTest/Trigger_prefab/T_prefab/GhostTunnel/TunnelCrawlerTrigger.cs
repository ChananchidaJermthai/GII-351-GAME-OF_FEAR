using UnityEngine;

public class TunnelCrawlerTrigger : MonoBehaviour
{
    public GhostCrawlerTunnel crawler;
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag("Player")) return;

        hasTriggered = true;
        crawler.StartChase();
        //
    }
}
