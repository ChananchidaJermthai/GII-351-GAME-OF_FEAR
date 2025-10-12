using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerAimPickup : MonoBehaviour
{
    [Header("Input (ใช้ Input Actions ได้)")]
    public InputActionReference interactAction;     // Button (เช่น "Interact")

    [Header("References")]
    public Camera playerCamera;                     // กล้องที่ "หมุนจริง" ตามผู้เล่น
    public InventoryLite inventory;                 // คลังของผู้เล่น (ไม่จำเป็นต้องใช้ที่นี่ แต่เผื่อไว้)

    [Header("Aim / Raycast")]
    [Min(0.5f)] public float maxPickupDistance = 3.0f;
    public LayerMask hitMask = ~0;                  // ต้องรวมเลเยอร์ของไอเท็ม
    public bool includeTriggers = false;            // true = ให้ Raycast ชน Trigger ด้วย

    [Header("UI (optional)")]
    public GameObject promptRoot;                   // กล่อง "กด Interact เพื่อเก็บ"
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
    public TMPro.TMP_Text promptText;
#endif

    [Header("Fallback (ถ้าไม่ได้ตั้ง Action)")]
    public KeyCode interactKeyLegacy = KeyCode.E;   
#if ENABLE_INPUT_SYSTEM
    public Key fallbackInteractKeyIS = Key.E;       
    Keyboard kb => Keyboard.current;
#endif

    [Header("Debug")]
    public bool drawRay = false;

    void OnEnable() { interactAction?.action.Enable(); }
    void OnDisable() { interactAction?.action.Disable(); }

    void Reset()
    {
        // หากล้องจากลูกหลานก่อน เพื่อเลี่ยงไปจับกล้องอื่นในซีน
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        if (!inventory) inventory = GetComponentInParent<InventoryLite>();
    }

    void Awake()
    {
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        if (!inventory) inventory = GetComponentInParent<InventoryLite>();
        if (promptRoot) promptRoot.SetActive(false);
    }

    
    void LateUpdate()
    {
        if (!playerCamera) return;

        var ray = CenterRay(); 
        var target = FindPickupTarget(ray, out var hit);

        // UI
        if (promptRoot) promptRoot.SetActive(target != null);
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
        if (promptText) promptText.text = target ? $"กด Interact เก็บ {target.itemId} x{target.amount}" : "";
#endif

        // Debug
        if (drawRay)
        {
            Color c = target ? Color.green : Color.red;
            Debug.DrawRay(ray.origin, ray.direction * maxPickupDistance, c);
        }

        // กดเก็บ
        if (target != null && PressedInteract())
        {
            // เรียกเมธอดของไอเท็ม (ตัวไอเท็มจะเพิ่มของ + เล่น FX เอง)
            target.TryPickup(gameObject);
        }
    }

    // ---------- Core ----------
    Ray CenterRay()
    {
        
        return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
    }

    ItemPickup3D FindPickupTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;
        var qti = includeTriggers ? QueryTriggerInteraction.Collide
                                  : QueryTriggerInteraction.Ignore;

        if (Physics.Raycast(ray, out hit, maxPickupDistance, hitMask, qti))
        {
            // รองรับกรณี Collider อยู่เป็นลูกของวัตถุไอเท็ม
            return hit.collider.GetComponentInParent<ItemPickup3D>();
        }
        return null;
    }

    bool PressedInteract()
    {
        // 1) ใช้ Input Action ถ้ามี
        if (interactAction && interactAction.action.enabled)
            return interactAction.action.WasPressedThisFrame();

        // 2) Fallback ระบบใหม่ (ถ้าไม่ได้ผูก Action)
#if ENABLE_INPUT_SYSTEM
        if (kb != null) return kb[fallbackInteractKeyIS].wasPressedThisFrame;
#else
        // 3) Fallback ระบบเก่า
        if (Input.anyKeyDown) return Input.GetKeyDown(interactKeyLegacy);
#endif
        return false;
    }
}
