using UnityEngine;

public class JumpScareSpawnInFront : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Transform jumpScareRoot;     // Empty ที่อยู่หน้ากล้อง
    public GameObject monsterPrefab;    // Prefab ผี
    public bool useOnce = true;         // ใช้ครั้งเดียวไหม
    public float autoDestroyMonsterAfter = 3f; // ให้ผีอยู่กี่วินาที (0 = ไม่ลบ)

    [Header("Animation (optional)")]
    public string scareTriggerName = "Scare"; // ชื่อ Trigger ใน Animator

    private bool hasTriggered = false;
    private GameObject spawnedMonster;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && useOnce) return;
        if (!other.CompareTag("Player")) return;  // ให้ Player มี Tag = "Player"

        hasTriggered = true;

        // 1) เช็กว่ามี root และ prefab ครบไหม
        if (jumpScareRoot == null || monsterPrefab == null)
        {
            Debug.LogWarning("JumpScare: ยังไม่ได้เซ็ต jumpScareRoot หรือ monsterPrefab");
            return;
        }

        // 2) สร้างผีที่หน้ากล้อง (เป็นลูกของ root)
        spawnedMonster = Instantiate(
            monsterPrefab,
            jumpScareRoot.position,
            jumpScareRoot.rotation,
            jumpScareRoot   // ทำเป็นลูกของ root จะได้ตามกล้อง
        );

        // 3) สั่งเล่นอนิเมชั่น (ถ้ามี)
        Animator anim = spawnedMonster.GetComponent<Animator>();
        if (anim != null && !string.IsNullOrEmpty(scareTriggerName))
        {
            anim.SetTrigger(scareTriggerName);
        }

        // 4) ลบผีอัตโนมัติหลังจาก X วินาที (ถ้ากำหนด)
        if (autoDestroyMonsterAfter > 0f)
        {
            Destroy(spawnedMonster, autoDestroyMonsterAfter);
        }

        // 5) ถ้า Trigger ใช้ครั้งเดียว ให้ทำลายตัวเอง
        if (useOnce)
        {
            Destroy(gameObject, 1f);
        }
    }
}
