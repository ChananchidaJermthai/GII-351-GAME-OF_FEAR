using System.Collections.Generic;
using UnityEngine;

/// Trigger ที่เมื่อผู้เล่นมีไอเท็มครบตามกำหนด จะเปิด GameObject ที่กำหนดไว้ แล้วทำลายตัวทริกเกอร์
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class ShowQuestTrigger : MonoBehaviour
{
    [Header("Requirement (from InventoryLite)")]
    [Tooltip("Item ID to check, e.g., \"Diary\"")]
    public string requiredItemId = "Diary";

    [Tooltip("Minimum count required, e.g., 2 means must have at least 2 pieces")]
    public int requiredCount = 2;

    [Tooltip("Try to consume the items after activation")]
    public bool consumeOnSuccess = false;

    [Header("Target(s) to Activate")]
    [Tooltip("All objects here will be SetActive(true) when requirement is met")]
    public List<GameObject> objectsToActivate = new List<GameObject>();

    [Header("Player filter")]
    [Tooltip("Check tag before attempting inventory lookup")]
    public bool requirePlayerTag = true;

    [Tooltip("Tag that the player uses")]
    public string playerTag = "Player";

    [Header("Trigger cleanup")]
    [Tooltip("Destroy this trigger GameObject after success (recommended)")]
    public bool destroySelfOnSuccess = true;

    [Tooltip("If not destroying self, at least disable the collider")]
    public bool disableColliderOnSuccess = false;

    [Header("Status")]
    [Tooltip("True ถ้าผู้เล่นมีไอเท็มที่ต้องการครบ")]
    public bool isKeyExit = false;

    private bool _done;
    private Collider _col;

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col && !_col.isTrigger) _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_done) return;

        // Optional tag check
        if (requirePlayerTag && !other.CompareTag(playerTag))
            return;

        // Find InventoryLite on the object or its parents
        var inventory = other.GetComponentInParent<InventoryLite>();
        if (!inventory) return;

        // Check requirement
        if (string.IsNullOrEmpty(requiredItemId) || requiredCount <= 0) return;

        int have = inventory.GetCount(requiredItemId);

        // ✅ ถ้ามีไอเท็มครบ ให้ตั้งค่า isKeyExit = true
        if (have >= requiredCount)
        {
            isKeyExit = true;
        }
        else
        {
            isKeyExit = false;
            return; // ยังไม่ครบ -> หยุดที่นี่
        }

        // Optionally consume the items
        if (consumeOnSuccess)
        {
            bool ok = inventory.Consume(requiredItemId, requiredCount);
            if (!ok) return;
        }

        // Activate targets
        foreach (var go in objectsToActivate)
        {
            if (go) go.SetActive(true);
        }

        _done = true;

        // Cleanup trigger
        if (destroySelfOnSuccess)
        {
            Destroy(gameObject);
        }
        else if (disableColliderOnSuccess && _col)
        {
            _col.enabled = false;
        }
    }
}
