using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DoorExitInteractable : MonoBehaviour
{
    [Header("Refs")]
    public DoorExit door;

    [Header("UI Prompt")]
    [TextArea] public string promptText = "Hold [E] to open";

    void Reset()
    {
        if (!door) door = GetComponentInParent<DoorExit>();
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }

    void Awake()
    {
        if (!door) door = GetComponentInParent<DoorExit>();
    }

    public void TryInteract(GameObject playerGO)
    {
        if (!door)
        {
            door = GetComponentInParent<DoorExit>();
            if (!door)
            {
                Debug.LogError("[DoorExitInteractable] DoorExit not found.", this);
                return;
            }
        }
        door.TryBeginInteract(playerGO);
    }
}