using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemHighlighter : MonoBehaviour
{
    [Header("Camera / Ray Settings")]
    [Tooltip("กล้องที่ใช้ยิง Ray (ถ้าเว้นว่าง จะใช้ Camera.main)")]
    public Camera playerCamera;

    [Tooltip("ระยะตรวจไฮไลท์")]
    public float maxDistance = 4f;

    [Tooltip("LayerMask ของวัตถุที่ให้ตรวจ (ตั้งเป็น Item / Interactable เป็นต้น)")]
    public LayerMask interactLayer = ~0;

    [Header("Debug")]
    public bool showDebugRay = false;

    private HighlightItem _currentHighlight;
    private Ray _ray;
    private RaycastHit _hit;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (playerCamera == null) return;

        // เตรียม Ray
        _ray.origin = playerCamera.transform.position;
        _ray.direction = playerCamera.transform.forward;

        bool hitSomething = Physics.Raycast(_ray, out _hit, maxDistance, interactLayer, QueryTriggerInteraction.Ignore);

        if (showDebugRay)
        {
            Color rayColor = hitSomething ? Color.green : Color.red;
            Vector3 endPoint = hitSomething ? _hit.point : _ray.origin + _ray.direction * maxDistance;
            Debug.DrawLine(_ray.origin, endPoint, rayColor);
        }

        if (hitSomething)
        {
            var highlight = _hit.collider.GetComponentInParent<HighlightItem>();
            if (highlight != null)
            {
                if (_currentHighlight != highlight)
                {
                    ClearCurrent();
                    _currentHighlight = highlight;
                    _currentHighlight.SetHighlight(true);
                }
                return; // เจอ highlight แล้ว ไม่ต้อง ClearCurrent
            }
        }

        ClearCurrent(); // ถ้าไม่ได้ Raycast เจอ highlight
    }

    void ClearCurrent()
    {
        if (_currentHighlight != null)
        {
            _currentHighlight.SetHighlight(false);
            _currentHighlight = null;
        }
    }
}
