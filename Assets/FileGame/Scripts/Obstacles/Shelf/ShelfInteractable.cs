using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ShelfInteractable : MonoBehaviour
{
    [Header("Refs")]
    public Shelf shelf;   // ถ้าเว้นไว้จะหาในพาเรนต์/ตัวเองให้

    [Header("UI Prompt")]
    [TextArea] public string promptText = "Press E Open";

    void Reset()
    {
        // หา Shelf ใน parent/ตัวเอง ถ้าไม่ได้ใส่ reference
        if (!shelf) shelf = GetComponentInParent<Shelf>();

        // ตั้งค่า Collider เริ่มต้น
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;
    }

    void Awake()
    {
        // หา Shelf ใน parent/ตัวเอง ถ้าไม่ได้ใส่ reference
        if (!shelf) shelf = GetComponentInParent<Shelf>();

        if (!shelf)
        {
            Debug.LogWarning("[ShelfInteractable] Shelf not assigned or found in parent.", this);
        }
    }

    /// <summary>
    /// เรียกเพื่อ Interact กับ Shelf
    /// </summary>
    public void TryInteract(GameObject playerGO)
    {
        if (!shelf)
        {
            // Fallback: หา Shelf อีกครั้ง
            shelf = GetComponentInParent<Shelf>();
            if (!shelf)
            {
                Debug.LogError("[ShelfInteractable] Shelf not found.", this);
                return;
            }
        }

        shelf.TryInteract(playerGO);
    }
}
