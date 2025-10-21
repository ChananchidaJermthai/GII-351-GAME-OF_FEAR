using UnityEngine;

public class GhostChase : MonoBehaviour
{
    [Header("Chase Settings")]
    [SerializeField] private float moveSpeed = 3f;       // ความเร็วผี
    [SerializeField] private float stopDistance = 1.5f;  // ระยะที่หยุดเมื่อใกล้พอ
    [SerializeField] private float detectRadius = 20f;   // รัศมีตรวจจับผู้เล่น

    private Transform targetPlayer;

    private void Update()
    {
        // ถ้ายังไม่มี target ให้ค้นหาใหม่
        if (targetPlayer == null)
        {
            FindPlayerInRange();
            return;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // ถ้าอยู่ใกล้เกิน stopDistance หยุด
        if (distance <= stopDistance)
            return;

        // ถ้ายังอยู่ในระยะ ตรวจให้แน่ใจว่า player ยังอยู่ใน detectRadius
        if (distance > detectRadius)
        {
            targetPlayer = null; // ผู้เล่นออกนอกระยะ — ยกเลิก target
            return;
        }

        // ผีหันหน้าและเคลื่อนที่เข้าหาผู้เล่น
        Vector3 direction = (targetPlayer.position - transform.position).normalized;
        direction.y = 0f; // ไม่ให้เงย/ก้ม
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void FindPlayerInRange()
    {
        // หาผู้เล่นทั้งหมดในฉาก (อาจมีหลายตัว)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        float closestDist = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (GameObject p in players)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < detectRadius && dist < closestDist)
            {
                closestDist = dist;
                closestPlayer = p.transform;
            }
        }

        if (closestPlayer != null)
        {
            targetPlayer = closestPlayer;
            Debug.Log($"👻 Ghost detected player: {targetPlayer.name} (distance: {closestDist:F1})");
        }
    }
}
