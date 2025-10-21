using UnityEngine;
public class TriggerProbe : MonoBehaviour
{
    void OnTriggerEnter(Collider other) { Debug.Log("Probe Enter: " + other.name); }
    void OnTriggerStay(Collider other) { Debug.Log("Probe Stay: " + other.name); }
    void OnTriggerExit(Collider other) { Debug.Log("Probe Exit: " + other.name); }
}
