using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class ItemPickup3D : MonoBehaviour
{
    [Header("Item")]
    public string itemId = "Key";
    [Min(1)] public int amount = 1;

    [Header("FX & Behavior")]
    public bool destroyOnPickup = true;
    public GameObject pickupVfxPrefab;
    public AudioClip pickupSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    [Header("Events")]
    public UnityEvent onPicked;

    /// <summary>
    /// เรียกจาก PlayerAimPickup เมื่อผู้เล่นพยายามเก็บไอเท็ม
    /// </summary>
    public void TryPickup(GameObject playerGO)
    {
        if (!playerGO || string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[ItemPickup3D] Player or itemId is invalid.");
            return;
        }

        var inv = playerGO.GetComponentInParent<InventoryLite>();
        if (!inv)
        {
            Debug.LogWarning($"[ItemPickup3D] No InventoryLite found on {playerGO.name} or its parents.");
            return;
        }

        // เพิ่มไอเท็ม
        inv.AddItem(itemId, Mathf.Max(1, amount));

        // สร้าง VFX/SFX
        if (pickupVfxPrefab) Object.Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);

        // เรียก event
        onPicked?.Invoke();

        // ปิดหรือทำลายตัวเอง
        if (destroyOnPickup) Object.Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    void Reset()
    {
        // Collider ต้องไม่เป็น Trigger สำหรับ Raycast
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (amount < 1) amount = 1;
    }
#endif
}
