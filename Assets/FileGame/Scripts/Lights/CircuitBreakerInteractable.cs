using UnityEngine;

[DisallowMultipleComponent]
public class CircuitBreakerInteractable : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("ถ้าเว้นไว้ จะหา CircuitBreaker ในพาเรนต์หรือ object ตัวเองให้อัตโนมัติ")]
    public CircuitBreaker breaker;

    [Header("UI Prompt")]
    [TextArea] public string promptText = "Press E Restore Power";

    void Reset() => FindBreaker();

    void Awake() => FindBreaker();

    void FindBreaker()
    {
        if (!breaker)
            breaker = GetComponentInParent<CircuitBreaker>();
    }

    /// <summary>
    /// เรียกจาก PlayerAimPickup / PlayerInteraction เมื่อผู้เล่นกดปุ่ม
    /// </summary>
    public void TryInteract(GameObject playerGO)
    {
        if (!breaker)
        {
            FindBreaker();
            if (!breaker)
            {
                Debug.LogError("[CircuitBreakerInteractable] ไม่พบ CircuitBreaker ใน parent หรือตัวเอง", this);
                return;
            }
        }

        breaker.TryInteract(playerGO);
    }
}
