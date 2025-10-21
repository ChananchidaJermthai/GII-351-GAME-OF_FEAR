using UnityEngine;
using System.Collections.Generic;

public class SpawnEnemyManager : MonoBehaviour
{
    [Header("Enemy Prefabs (Project Assets)")]
    public GameObject[] enemies; // prefab จาก Project
    private List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    [Header("Spawn Settings")]
    public float spawnInterval = 3f;
    public float enemyLifetime = 10f; // เวลาให้ enemy อยู่ใน scene ก่อนลบ

    private float timer;

    void Awake()
    {
        enemyPrefabs.Clear();
        foreach (GameObject prefab in enemies)
        {
            if (prefab != null)
                enemyPrefabs.Add(prefab);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnRandomEnemy();
            timer = 0f;
        }
    }

    public void SpawnRandomEnemy()
    {
        if (enemyPrefabs.Count == 0 || spawnPoints.Length == 0) return;

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Count)];
        if (prefab == null) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Instantiate Enemy
        GameObject enemyInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // ตรวจสอบว่ามี EnemyMove อยู่บน prefab
        EnemyMove em = enemyInstance.GetComponent<EnemyMove>();
        if (em == null)
        {
            em = enemyInstance.AddComponent<EnemyMove>();
        }

        // ตั้งเวลาลบตัวเองหลัง enemyLifetime วินาที
        Destroy(enemyInstance, enemyLifetime);
    }
}
