using System.Collections;
using UnityEngine;

public class SlowRevealChase : MonoBehaviour
{
    [Header("Player & Vision")]
    public string playerTag = "Player";
    public float detectionRange = 10f;
    public float stopDistance = 1.5f;
    public float chaseSpeed = 5f;
    public bool useOnce = true;

    [Header("Sound Before Chase")]
    public AudioSource audioSource;      // มีแค่ 1 แทร็ก
    public AudioClip preChaseSound;      // เสียงเตือนก่อนเริ่มวิ่ง
    [Range(0f, 1f)] public float soundVolume = 1f;

    [Header("Sanity Damage")]
    public SanityApplier sanityApplier;
    [SerializeField] float damage = -20f;

    private Transform player;
    private bool sequenceStarted = false;
    private bool isChasing = false;

    private void Awake()
    {
        // หา Player จาก Tag
        GameObject obj = GameObject.FindGameObjectWithTag(playerTag);
        if (obj != null)
            player = obj.transform;

        // หา SanityApplier อัตโนมัติ
        if (sanityApplier == null)
            sanityApplier = FindAnyObjectByType<SanityApplier>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (sequenceStarted && useOnce) return;

        sequenceStarted = true;
        StartCoroutine(PreChaseRoutine());
    }

    private IEnumerator PreChaseRoutine()
    {
        // ถ้ามีเสียงก่อนไล่
        if (audioSource != null && preChaseSound != null)
        {
            audioSource.volume = soundVolume;
            audioSource.PlayOneShot(preChaseSound);

            // รอจนเสียงแทร็กเดียวนี้เล่นจนจบ
            yield return new WaitForSeconds(preChaseSound.length);
        }
        else
        {
            Debug.LogWarning("SlowRevealChase: ไม่มี audioSource หรือ preChaseSound ให้เล่น");
        }

        // หลังเสียงจบ → ถ้าผู้เล่นอยู่ในระยะ → เริ่มไล่
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= detectionRange)
                isChasing = true;
        }
    }


    private void Update()
    {
        if (!isChasing || player == null) return;

        // ไล่ผู้เล่น
        Vector3 dir = (player.position - transform.position);
        dir.y = 0;

        if (dir.sqrMagnitude > 0.01f)
        {
            dir = dir.normalized;
            transform.position += dir * chaseSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        // ถึงตัวผู้เล่น
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= stopDistance)
        {
            ReachPlayer();
        }
    }

    private void ReachPlayer()
    {
        isChasing = false;

        // ผีหายจากฉาก
        gameObject.SetActive(false);

        // เรียก jumpscare กลาง
        if (JumpScareSpawnInFront.Instance != null)
            JumpScareSpawnInFront.Instance.PlayScare();

        // ลด sanity
        if (sanityApplier != null)
            sanityApplier.AddSanity(damage);
    }
}
