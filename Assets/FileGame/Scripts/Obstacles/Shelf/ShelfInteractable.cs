using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ShelfInteractable : MonoBehaviour
{
    [Header("Refs")]
    public Shelf shelf;   // ถ้าเว้นไว้จะหาในพาเรนต์/ตัวเองให้

    [Header("UI Prompt")]
    //public string promp;
    [TextArea] public string promptText = "Press E Open";

    void Reset()
    {
        if (!shelf) shelf = GetComponentInParent<Shelf>();
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false; // เริ่มต้นให้ไม่ trigger (ปรับได้ตามเกม)
    }

    void Awake()
    {
        if (!shelf) shelf = GetComponentInParent<Shelf>();
    }

    public void TryInteract(GameObject playerGO)
    {
        if (!shelf)
        {
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
