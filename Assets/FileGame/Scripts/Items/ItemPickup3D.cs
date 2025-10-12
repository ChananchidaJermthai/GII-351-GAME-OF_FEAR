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

    // เรียกจาก PlayerAimPickup เมื่อต้องการเก็บ (เล็งโดน + อยู่ในระยะ + กดปุ่มที่ฝั่งผู้เล่น)
    public void TryPickup(GameObject playerGO)
    {
        if (!playerGO) return;

        var inv = playerGO.GetComponentInParent<InventoryLite>();
        if (!inv)
        {
            Debug.LogWarning($"[ItemPickup3D] No InventoryLite on {playerGO.name} or its parents.");
            return;
        }
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[ItemPickup3D] itemId is empty.");
            return;
        }

        inv.AddItem(itemId, Mathf.Max(1, amount));

        if (pickupVfxPrefab) Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);
        onPicked?.Invoke();

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    void Reset()
    {
        // ใช้ Raycast จากฝั่งผู้เล่น ไม่จำเป็นต้องเป็น Trigger
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
