using UnityEngine;

public class SpawnEnemyManager : MonoBehaviour
{
    [Header("Enemy Prefabs")]
    public GameObject[] enemies; // เก็บ prefab ของศัตรู

    [Header("Spawn Points")]
    public Transform[] spawnPoints; // จุดเกิดที่สามารถตั้งในฉาก

    [Header("Spawn Settings")]
    public float spawnInterval = 3f; // เวลาระหว่างการ spawn

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnRandomEnemy();
            timer = 0f;
        }
    }

    void SpawnRandomEnemy()
    {
        if (enemies.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("No enemies or spawn points assigned!");
            return;
        }

        // สุ่ม prefab ศัตรู
        GameObject randomEnemy = enemies[Random.Range(0, enemies.Length)];

        // สุ่มตำแหน่ง spawn point
        Transform randomSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // สร้างศัตรู
        Instantiate(randomEnemy, randomSpawn.position, randomSpawn.rotation);
    }
}
