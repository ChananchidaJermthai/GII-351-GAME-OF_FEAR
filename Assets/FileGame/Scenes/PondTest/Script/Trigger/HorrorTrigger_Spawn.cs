using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HorrorTrigger_Spawn : MonoBehaviour
{
    [Header("Trigger Settings")]
    public string playerTag = "Player";
    public bool singleUse = true;
    public float spawnDelay = 0f;

    [Header("Spawn")]
    public Transform spawnPoint;     // Empty จุดเกิดผี (วางตรงที่ “จะเห็นยืนจ้อง”)
    public GameObject ghostPrefab;
    public bool lookAtPlayer = true;
    public bool matchSpawnPointRotationIfNotLookAtPlayer = true;

    [Header("Lifetime (optional)")]
    public float autoDestroyAfter = 0f; // 0 = ไม่ทำลาย

    [Header("SFX (optional)")]
    public AudioClip spawnSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    bool _used;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_used && singleUse) return;
        if (!other.CompareTag(playerTag)) return;
        if (!ghostPrefab || !spawnPoint) return;

        StartCoroutine(DoSpawn(other.transform));
        if (singleUse) _used = true;
    }

    System.Collections.IEnumerator DoSpawn(Transform player)
    {
        if (spawnDelay > 0f) yield return new WaitForSeconds(spawnDelay);

        var go = Instantiate(ghostPrefab, spawnPoint.position, spawnPoint.rotation);
        if (lookAtPlayer && player)
        {
            Vector3 dir = (player.position - go.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else if (matchSpawnPointRotationIfNotLookAtPlayer)
        {
            go.transform.rotation = spawnPoint.rotation;
        }

        if (spawnSfx)
            AudioSource.PlayClipAtPoint(spawnSfx, spawnPoint.position, sfxVolume);

        if (autoDestroyAfter > 0f)
            Destroy(go, autoDestroyAfter);
    }
}
