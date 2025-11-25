using UnityEngine;
using UnityEngine.AI;

public class GhostMove : MonoBehaviour
{
    [Header("Points")]
    public Transform point2Spawn;
    public Transform point3Destination;  // ถ้า null → ผีอยู่กับที่

    [Header("Ghost Prefabs")]
    public GameObject[] ghostPrefabs;  // ใส่ Prefab หลายตัว

    [Header("Ghost Settings")]
    public float moveSpeed = 3.5f;
    public float destroyTime = 5f;

    [Header("Random Rate (%)")]
    [Range(0,100)]
    public float rate = 30f;  // % โอกาสสุ่ม Spawn

    [Header("Options")]
    public bool spawnOnce = false;       // ถ้าติ๊กถูก จะ Spawn ครั้งเดียว
    public AudioClip spawnSound;         // เสียงตอน Spawn (Optional)
    [Range(0f, 1f)]
    public float spawnVolume = 1f;       // ปรับความดังของเสียง

    private GameObject currentGhost;
    private bool hasSpawnedOnce = false;

    public float sanityAmount = 3.5f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (spawnOnce && hasSpawnedOnce) return;
        if (currentGhost != null) return;

        float effectiveRate = spawnOnce ? 100f : rate;
        if (Random.Range(0f, 100f) > effectiveRate) return;

        if (ghostPrefabs.Length == 0)
        {
            Debug.LogWarning("ไม่มี Ghost Prefab ใส่ใน Array!");
            return;
        }

        GameObject prefab = ghostPrefabs[Random.Range(0, ghostPrefabs.Length)];

        NavMeshHit spawnHit;
        Vector3 spawnPos = point2Spawn.position;
        if (NavMesh.SamplePosition(spawnPos, out spawnHit, 1f, NavMesh.AllAreas))
            spawnPos = spawnHit.position;

        currentGhost = Instantiate(prefab, spawnPos, Quaternion.identity);

        // เล่นเสียงในหูผู้เล่น (2D, ไม่ขึ้นอยู่กับตำแหน่ง)
        if (spawnSound != null)
        {
            PlaySoundInPlayerEar(spawnSound, spawnVolume);
        }

        PlayerController3D playerController = other.GetComponent<PlayerController3D>();
        if (playerController != null)
        {
            playerController.AddSanity(sanityAmount);
        }

        if (point3Destination != null)
        {
            NavMeshHit destHit;
            Vector3 destPos = point3Destination.position;
            if (NavMesh.SamplePosition(destPos, out destHit, 1f, NavMesh.AllAreas))
                destPos = destHit.position;

            Vector3 lookPos = destPos;
            lookPos.y = currentGhost.transform.position.y;
            currentGhost.transform.LookAt(lookPos);

            NavMeshAgent agent = currentGhost.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = moveSpeed;
                agent.updateRotation = true;
                agent.updatePosition = true;
                agent.SetDestination(destPos);
            }
            else
            {
                Debug.LogWarning("Ghost Prefab ต้องมี NavMeshAgent!");
            }
        }

        Destroy(currentGhost, destroyTime);
        Invoke(nameof(ClearCurrentGhost), destroyTime);

        if (spawnOnce)
            hasSpawnedOnce = true;
    }

    private void PlaySoundInPlayerEar(AudioClip clip, float volume)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            AudioSource audioSource = mainCam.gameObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.spatialBlend = 0f; // 0 = 2D (อยู่ในหูผู้เล่น)
            audioSource.Play();
            Destroy(audioSource, clip.length); // ลบหลังเล่นจบ
        }
    }

    private void ClearCurrentGhost()
    {
        currentGhost = null;
    }
}
