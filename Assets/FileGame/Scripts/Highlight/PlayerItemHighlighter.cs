using UnityEngine;

public class PlayerItemHighlighter : MonoBehaviour
{
    [Header("Camera / Ray Settings")]
    [Tooltip("กล้องที่ใช้ยิง Ray (ถ้าเว้นว่าง จะใช้ Camera.main)")]
    public Camera playerCamera;

    [Tooltip("ระยะตรวจไฮไลท์")]
    public float maxDistance = 4f;

    [Tooltip("LayerMask ของวัตถุที่ให้ตรวจ (ตั้งเป็น Item / Interactable เป็นต้น)")]
    public LayerMask interactLayer = ~0; // ค่า default = ทุกเลเยอร์

    [Header("Debug")]
    public bool showDebugRay = false;

    HighlightItem _currentHighlight;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, interactLayer, QueryTriggerInteraction.Ignore))
        {
            if (showDebugRay) Debug.DrawLine(ray.origin, hit.point, Color.green);

            var highlight = hit.collider.GetComponentInParent<HighlightItem>();
            if (highlight != null)
            {
                // ถ้าตัวใหม่ไม่เหมือนตัวเดิม ให้สลับ highlight
                if (_currentHighlight != highlight)
                {
                    ClearCurrent();
                    _currentHighlight = highlight;
                    _currentHighlight.SetHighlight(true);
                }
                return;
            }
        }
        else
        {
            if (showDebugRay)
                Debug.DrawLine(ray.origin, ray.origin + ray.direction * maxDistance, Color.red);
        }

        
        ClearCurrent();
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
