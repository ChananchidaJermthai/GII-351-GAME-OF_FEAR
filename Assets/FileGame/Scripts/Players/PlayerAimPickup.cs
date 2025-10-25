using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerAimPickup : MonoBehaviour
{
    [Header("Input (Input System)")]
    public InputActionReference interactAction; // Button

    [Header("References")]
    public Camera playerCamera;
    public InventoryLite inventory;

    [Header("Aim / Raycast")]
    [Min(0.5f)] public float maxPickupDistance = 3.0f;
    public LayerMask hitMask = ~0;
    public bool includeTriggers = false;

    [Header("UI (optional)")]
    public GameObject promptRoot;
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
    public TMPro.TMP_Text promptText;
#endif

    [Header("Fallback Key (if no action)")]
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

        var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        var hit = default(RaycastHit);
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // ลองหาเป้าหมายตามลำดับที่ต้องการ
        ItemPickup3D itemTarget = null;
        RadioInteractable radioTarget = null;
        CircuitBreakerInteractable breakerTarget = null;
        ShelfInteractable shelfTarget = null;

        if (Physics.Raycast(ray, out hit, maxPickupDistance, hitMask, qti))
        {
            var tr = hit.collider.transform;

            itemTarget = tr.GetComponentInParent<ItemPickup3D>();
            if (itemTarget == null) radioTarget = tr.GetComponentInParent<RadioInteractable>();
            if (itemTarget == null && radioTarget == null) breakerTarget = tr.GetComponentInParent<CircuitBreakerInteractable>();
            if (itemTarget == null && radioTarget == null && breakerTarget == null) shelfTarget = tr.GetComponentInParent<ShelfInteractable>();
        }

        bool hasTarget = itemTarget || radioTarget || breakerTarget || shelfTarget;

        if (promptRoot) promptRoot.SetActive(hasTarget);
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
        if (promptText)
        {
            if (itemTarget) promptText.text = $" Press E Get {itemTarget.itemId} x{itemTarget.amount} ";
            else if (radioTarget) promptText.text = radioTarget.promptText;
            else if (breakerTarget) promptText.text = breakerTarget.promptText;
            else if (shelfTarget) promptText.text = shelfTarget.promptText;
            else promptText.text = "";
        }
#endif

        if (drawRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * maxPickupDistance, hasTarget ? Color.green : Color.red);
        }

        if (hasTarget && PressedInteract())
        {
            if (itemTarget) itemTarget.TryPickup(gameObject);
            else if (radioTarget) radioTarget.TryInteract(gameObject);
            else if (breakerTarget) breakerTarget.TryInteract(gameObject);
            else if (shelfTarget) shelfTarget.TryInteract(gameObject);
        }
    }

    bool PressedInteract()
    {
        if (interactAction && interactAction.action.enabled)
            return interactAction.action.WasPressedThisFrame();
#if ENABLE_INPUT_SYSTEM
        if (kb != null) return kb[fallbackInteractKeyIS].wasPressedThisFrame;
#else
        if (Input.anyKeyDown) return Input.GetKeyDown(interactKeyLegacy);
#endif
        return false;
    }
}
