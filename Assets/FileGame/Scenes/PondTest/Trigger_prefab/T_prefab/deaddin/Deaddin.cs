using System.Collections;
using UnityEngine;

public class Deaddin : MonoBehaviour
{
    [Header("Animation")]
    public Animator animator;
    [Tooltip("ชื่อ parameter ที่ใช้เริ่มขยับ")]
    public string moveParam = "isMoving";

    [Tooltip("ชื่อ parameter ที่ใช้เริ่มอนิเมชั่นตาย")]
    public string deathParam = "isDying";

    public SanityApplier sanityApplier;
    [SerializeField] float damage = 5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip moveSFX;
    public AudioClip deathSFX;

    [Header("Timing")]
    public float delayBeforeDeathSFX = 1.5f;   // ระหว่างแกว่งก่อนเสียงตาย
    public float delayBeforeStop = 3.0f;       // เวลาก่อนกลับมา Idle

    [Header("Option")]
    public bool triggerOnce = true;
    private bool hasTriggered = false;

    private void Awake()
    {
        sanityApplier = FindAnyObjectByType<SanityApplier>();
    }
    private void Start()
    {
        // เริ่มต้นใน Idle
        if (animator != null)
        {
            animator.SetBool(moveParam, false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;

        StartCoroutine(HangingSequence());
    }

    private IEnumerator HangingSequence()
    {
        // 1) เริ่มขยับ
        if (animator != null)
        {
            animator.SetBool(moveParam, true);
        }

        // เสียงขยับ
        if (audioSource != null && moveSFX != null)
        {
            audioSource.PlayOneShot(moveSFX);
        }
        sanityApplier.AddSanity(damage);

        // 2) รอจนถึงช่วงเสียงตาย
        yield return new WaitForSeconds(delayBeforeDeathSFX);

        // เล่นเสียงตาย
        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        // 3) เปลี่ยนไป Death Animation
        if (animator != null)
        {
            animator.SetTrigger(deathParam);
        }

        // 4) รอจนถึงเวลาหยุดขยับ
        yield return new WaitForSeconds(delayBeforeStop);

        // 5) กลับไป Idle
        if (animator != null)
        {
            animator.SetBool(moveParam, false);
        }

        // ถ้าให้ Trigger ซ้ำได้
        if (!triggerOnce)
        {
            hasTriggered = false;
        }
    }
}
