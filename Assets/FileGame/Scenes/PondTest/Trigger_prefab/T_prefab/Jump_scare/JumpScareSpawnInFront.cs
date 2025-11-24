using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class JumpScareSpawnInFront : MonoBehaviour
{
    public static JumpScareSpawnInFront Instance;

    [Header("Spawn Settings")]
    public Transform EventRoot;
    public GameObject monsterPrefab;
    public float heightOffset = -1.5f;
    public float forwardOffset = 0f;
    public float autoDestroyAfter = 3f;

    //public GameObject player;
    public SanityApplier sanityApplier;
    [SerializeField] float damage = 10f;

    [Header("Animation")]
    public string scareTriggerName = "Scare";

    [Header("Sound Settings")]
    public AudioSource scareAudioSource;      // ใช้เสียงจาก manager
    public AudioClip scareClip;               // คลิปเสียง jumpscare
    public float scareVolume = 1f;

    [Header("Lighting Boost")]
    public Volume globalVolume;
    public float brighterIntensity = 1.5f;
    public float effectDuration = 1.0f;

    private ColorAdjustments colorAdjust;
    private float originalIntensity;
    private GameObject spawnedMonster;

    private void Awake()
    {
        sanityApplier = FindAnyObjectByType<SanityApplier>();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out colorAdjust);
            if (colorAdjust != null)
                originalIntensity = colorAdjust.postExposure.value;
        }
        globalVolume = FindAnyObjectByType<Volume>();
        if (EventRoot == null)
        {
            EventRoot = GameObject.FindGameObjectWithTag("EventRoot")?.transform;

        }
    }

    public void PlayScare()
    {
        if (EventRoot == null || monsterPrefab == null)
        {
            Debug.LogWarning("JumpScareSpawnInFront: settings missing!");
            return;
        }

        // 1) spawn ตำแหน่ง
        Vector3 spawnPos =
            EventRoot.position +
            EventRoot.up * heightOffset +
            EventRoot.forward * forwardOffset;

        Quaternion spawnRot = EventRoot.rotation;

        // 2) spawn ผี
        spawnedMonster = Instantiate(
            monsterPrefab,
            spawnPos,
            spawnRot,
            EventRoot
        );

        // 3) animation
        Animator anim = spawnedMonster.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger(scareTriggerName);

        // 4) เล่นเสียง jumpscare 🔊
        PlayScareSound();
        sanityApplier.AddSanity(damage);


        // 5) เอฟเฟกต์สว่างขึ้น
        if (colorAdjust != null)
        {
            StopAllCoroutines();
            StartCoroutine(LightBoostRoutine());
        }

        // 6) ลบผีหลัง X วินาที
        if (autoDestroyAfter > 0f)
            Destroy(spawnedMonster, autoDestroyAfter);
    }

    private void PlayScareSound()
    {
        // PRIORITY 1 = ใช้ scareAudioSource จาก Manager
        if (scareAudioSource != null && scareClip != null)
        {
            scareAudioSource.volume = scareVolume;
            scareAudioSource.PlayOneShot(scareClip);
            return;
        }

        // PRIORITY 2 = ถ้า Manager ไม่มีเสียง → ใช้เสียงจาก prefab
        AudioSource prefabAudio = spawnedMonster.GetComponent<AudioSource>();
        if (prefabAudio != null)
        {
            prefabAudio.volume = scareVolume;
            if (scareClip != null)
                prefabAudio.PlayOneShot(scareClip);
            else
                prefabAudio.Play(); // ถ้าใน prefab มี clip อยู่แล้ว
        }
    }

    private System.Collections.IEnumerator LightBoostRoutine()
    {
        colorAdjust.postExposure.value = brighterIntensity;

        yield return new WaitForSeconds(effectDuration);

        colorAdjust.postExposure.value = originalIntensity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            JumpScareSpawnInFront.Instance.PlayScare();
        }
    }
}
