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
    [Min(0.5f)] public float maxPickupDistance = 3f;
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
    private Keyboard kb => Keyboard.current;
#endif

    [Header("Debug")]
    public bool drawRay = false;

    void OnEnable() => interactAction?.action.Enable();
    void OnDisable() => interactAction?.action.Disable();

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
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        // ตรวจหา target ตามลำดับ
        ItemPickup3D itemTarget = null;
        RadioInteractable radioTarget = null;
        CircuitBreakerInteractable breakerTarget = null;
        ShelfInteractable shelfTarget = null;
        DoorExitInteractable doorTarget = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxPickupDistance, hitMask, qti))
        {
            var tr = hit.collider.transform;
            itemTarget = tr.GetComponentInParent<ItemPickup3D>();
            if (!itemTarget) radioTarget = tr.GetComponentInParent<RadioInteractable>();
            if (!itemTarget && !radioTarget) breakerTarget = tr.GetComponentInParent<CircuitBreakerInteractable>();
            if (!itemTarget && !radioTarget && !breakerTarget) shelfTarget = tr.GetComponentInParent<ShelfInteractable>();
            if (!itemTarget && !radioTarget && !breakerTarget && !shelfTarget)
                doorTarget = tr.GetComponentInParent<DoorExitInteractable>();
        }

        bool hasTarget = itemTarget || radioTarget || breakerTarget || shelfTarget || doorTarget;

        // UI Prompt
        if (promptRoot) promptRoot.SetActive(hasTarget);
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
        if (promptText)
        {
            if (itemTarget) promptText.text = $"Press E to pick up {itemTarget.itemId} x{itemTarget.amount}";
            else if (radioTarget) promptText.text = radioTarget.promptText;
            else if (breakerTarget) promptText.text = breakerTarget.promptText;
            else if (shelfTarget) promptText.text = shelfTarget.promptText;
            else if (doorTarget) promptText.text = doorTarget.promptText;
            else promptText.text = "";
        }
#endif

        if (drawRay)
            Debug.DrawRay(ray.origin, ray.direction * maxPickupDistance, hasTarget ? Color.green : Color.red);

        // ตรวจปุ่ม interact
        if (hasTarget && PressedInteract())
        {
            if (itemTarget) itemTarget.TryPickup(gameObject);
            else if (radioTarget) radioTarget.TryInteract(gameObject);
            else if (breakerTarget) breakerTarget.TryInteract(gameObject);
            else if (shelfTarget) shelfTarget.TryInteract(gameObject);
            else if (doorTarget) doorTarget.TryInteract(gameObject);
        }
    }

    bool PressedInteract()
    {
        if (interactAction?.action.enabled == true)
            return interactAction.action.WasPressedThisFrame();
#if ENABLE_INPUT_SYSTEM
        if (kb != null) return kb[fallbackInteractKeyIS].wasPressedThisFrame;
#else
        if (Input.anyKeyDown) return Input.GetKeyDown(interactKeyLegacy);
#endif
        return false;
    }
}
