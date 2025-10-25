using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerAimPickup : MonoBehaviour
{
    [Header("Input (ใช้ Input Actions ได้)")]
    public InputActionReference interactAction;     // Button

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
        var hit = default(RaycastHit);

        // ลำดับตรวจ: Item > Radio > CircuitBreaker
        var itemTarget = FindItemTarget(ray, out hit);
        var radioTarget = itemTarget ? null : FindRadioTarget(ray, out hit);
        var breakerTarget = (itemTarget || radioTarget != null) ? null : FindBreakerTarget(ray, out hit);

        bool hasTarget = itemTarget != null || radioTarget != null || breakerTarget != null;

        if (promptRoot) promptRoot.SetActive(hasTarget);
#if TMP_PRESENT || UNITY_2021_1_OR_NEWER
        if (promptText)
        {
            if (itemTarget) promptText.text = $" Press E Get {itemTarget.itemId} x {itemTarget.amount} ";
            else if (radioTarget) promptText.text = radioTarget.promptText;
            else if (breakerTarget) promptText.text = breakerTarget.promptText;
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
        }
    }

    Ray CenterRay() => new Ray(playerCamera.transform.position, playerCamera.transform.forward);

    ItemPickup3D FindItemTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        if (Physics.Raycast(ray, out hit, maxPickupDistance, hitMask, qti))
            return hit.collider.GetComponentInParent<ItemPickup3D>();
        return null;
    }

    RadioInteractable FindRadioTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        if (Physics.Raycast(ray, out hit, maxPickupDistance, hitMask, qti))
            return hit.collider.GetComponentInParent<RadioInteractable>();
        return null;
    }

    CircuitBreakerInteractable FindBreakerTarget(Ray ray, out RaycastHit hit)
    {
        hit = default;
        var qti = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        if (Physics.Raycast(ray, out hit, maxPickupDistance, hitMask, qti))
            return hit.collider.GetComponentInParent<CircuitBreakerInteractable>();
        return null;
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
