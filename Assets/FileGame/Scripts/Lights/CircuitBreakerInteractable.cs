using UnityEngine;

[DisallowMultipleComponent]
public class CircuitBreakerInteractable : MonoBehaviour
{
    [Header("Refs")]
    public CircuitBreaker breaker;      // ถ้าเว้นไว้ จะหาในพาเรนต์/ตัวเองให้อัตโนมัติ

    [Header("UI Prompt")]
    [TextArea] public string promptText = "Press E Restore Power";

    void Reset()
    {
        if (!breaker) breaker = GetComponentInParent<CircuitBreaker>();
    }

    void Awake()
    {
        if (!breaker) breaker = GetComponentInParent<CircuitBreaker>();
    }

    /// <summary>
    /// เรียกจาก PlayerAimPickup เมื่อผู้เล่นกด Interact
    /// </summary>
    public void TryInteract(GameObject playerGO)
    {
        if (!breaker)
        {
            breaker = GetComponentInParent<CircuitBreaker>();
            if (!breaker)
            {
                Debug.LogError("[CircuitBreakerInteractable] ไม่พบ CircuitBreaker", this);
                return;
            }
        }
        breaker.TryInteract(playerGO);
    }
}
