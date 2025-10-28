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

    [Header("Sound Settings")]
    public AudioClip[] spawnSounds; // ✅ เสียงสุ่มหลายแบบ
    private AudioSource audioSource;

    private float timer;

    void Awake()
    {
        // ดึง AudioSource จาก GameObject นี้
        audioSource = GetComponent<AudioSource>();

        // เก็บ prefab ศัตรูที่ไม่ null
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

        // ✅ เล่นเสียงแบบสุ่มก่อน Spawn
        PlayRandomSpawnSound();

        // 👾 สร้างศัตรู
        GameObject enemyInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // ตรวจสอบว่ามี EnemyMove อยู่บน prefab หรือไม่
        EnemyMove em = enemyInstance.GetComponent<EnemyMove>();
        if (em == null)
        {
            em = enemyInstance.AddComponent<EnemyMove>();
        }

        // ลบศัตรูหลังครบเวลา
        Destroy(enemyInstance, enemyLifetime);
    }

    private void PlayRandomSpawnSound()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("⚠️ ไม่มี AudioSource อยู่ใน GameObject ที่มี SpawnEnemyManager!");
            return;
        }

        if (spawnSounds == null || spawnSounds.Length == 0)
        {
            Debug.LogWarning("⚠️ ไม่มีเสียงใน spawnSounds Array!");
            return;
        }

        // ✅ สุ่มเสียงจาก Array
        AudioClip randomClip = spawnSounds[Random.Range(0, spawnSounds.Length)];

        // เล่นเสียงแบบ OneShot (ไม่ขัดกับเสียงอื่น)
        audioSource.PlayOneShot(randomClip);
    }
}
