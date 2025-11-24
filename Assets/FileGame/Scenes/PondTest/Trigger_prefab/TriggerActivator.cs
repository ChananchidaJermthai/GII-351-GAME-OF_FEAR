using UnityEngine;

public class TriggerActivator : MonoBehaviour
{
    [Header("ตัวที่จะถูกเปิดใช้งาน")]
    public GameObject[] targetTriggers;   

    public bool useOnce = true;
    private bool used = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (used && useOnce) return;

        used = true;

        foreach (GameObject trg in targetTriggers)
        {
            if (trg != null)
                trg.SetActive(true);
        }
    }
}
