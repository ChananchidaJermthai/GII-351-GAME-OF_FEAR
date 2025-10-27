using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostChase : MonoBehaviour
{
    [Header("Chase Settings")]
    [SerializeField] private float moveSpeed = 3f;       // ความเร็วผี
    [SerializeField] private float stopDistance = 1.5f;  // ระยะที่หยุดเมื่อใกล้พอ
    [SerializeField] private float detectRadius = 20f;   // รัศมีตรวจจับผู้เล่น

    private Transform targetPlayer;
    private NavMeshAgent agent;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation = true;
        agent.autoBraking = true;
    }

    private void Update()
    {
        if (targetPlayer == null)
        {
            FindPlayerInRange();
            return;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        // ถ้า player ออกนอกระยะตรวจจับ — หยุดและค้นหาใหม่
        if (distance > detectRadius)
        {
            targetPlayer = null;
            agent.ResetPath();
            return;
        }

        // ถ้าอยู่ใกล้เกิน stopDistance ให้หยุด
        if (distance <= stopDistance)
        {
            agent.ResetPath();
            return;
        }

        // ให้ NavMeshAgent ไล่ตามผู้เล่น
        if (agent.enabled && targetPlayer != null)
        {
            agent.SetDestination(targetPlayer.position);
        }
    }

    private void FindPlayerInRange()
    {
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
