// ============================================================================
// VILL4GE — EnemySpawner.cs
// Configurable encounter/wave spawner.
// Attach to a GameObject in the scene and configure spawn points + waves.
//
// This complements GameManager's internal wave system with a per-encounter,
// designer-friendly component you can drop into any area.
// ============================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    // ── Wave configuration ────────────────────────────────────────────────
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("Number of Villagers in this wave.")]
        public int villagers = 3;
        [Tooltip("Number of Pitchfork Ganados in this wave.")]
        public int pitchforks = 0;
        [Tooltip("Number of Heavy (axe) Ganados in this wave.")]
        public int heavies = 0;
        [Tooltip("Number of Chainsaw (Dr. Salvador) enemies in this wave.")]
        public int chainsaws = 0;
        [Tooltip("Delay (seconds) before this wave starts spawning.")]
        public float startDelay = 0f;
        [Tooltip("Interval (seconds) between each enemy spawn in this wave.")]
        public float spawnInterval = 0.5f;
    }

    [Header("Spawner Settings")]
    [Tooltip("Spawn points available for this encounter. If empty, uses this object's position.")]
    public Transform[] spawnPoints;

    [Tooltip("Waves to spawn in sequence. All enemies in wave N must die before wave N+1 starts.")]
    public WaveConfig[] waves;

    [Tooltip("Radius around each spawn point for randomizing exact enemy position.")]
    public float spawnRadius = 2f;

    [Tooltip("If true, starts spawning immediately on Start(). Otherwise, call Activate().")]
    public bool autoStart = false;

    [Tooltip("If true, respawns all waves on loop once the last wave is cleared.")]
    public bool looping = false;

    // ── State ─────────────────────────────────────────────────────────────
    public bool IsActive  { get; private set; }
    public bool IsCleared { get; private set; }
    public int  CurrentWave { get; private set; }

    List<Enemy> _aliveEnemies = new List<Enemy>();
    int _waveIndex = 0;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (autoStart) Activate();
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Begin spawning waves.</summary>
    public void Activate()
    {
        if (IsActive) return;
        IsActive  = true;
        IsCleared = false;
        _waveIndex = 0;
        _aliveEnemies.Clear();
        StartCoroutine(SpawnLoop());
    }

    // ─────────────────────────────────────────────────────────────────────
    IEnumerator SpawnLoop()
    {
        while (_waveIndex < waves.Length)
        {
            var cfg = waves[_waveIndex];
            CurrentWave = _waveIndex + 1;

            Debug.Log($"[EnemySpawner] Wave {CurrentWave} starting (delay {cfg.startDelay}s)");
            yield return new WaitForSeconds(cfg.startDelay);

            yield return StartCoroutine(SpawnWave(cfg));

            // Wait until all enemies are dead
            yield return new WaitUntil(() => AllDead());

            Debug.Log($"[EnemySpawner] Wave {CurrentWave} cleared.");
            _waveIndex++;
        }

        IsCleared = true;
        Debug.Log("[EnemySpawner] All waves cleared!");
        OnEncounterCleared();

        if (looping)
        {
            _waveIndex = 0;
            IsCleared  = false;
            StartCoroutine(SpawnLoop());
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    IEnumerator SpawnWave(WaveConfig cfg)
    {
        var toSpawn = BuildSpawnList(cfg);
        foreach (var type in toSpawn)
        {
            SpawnEnemy(type);
            yield return new WaitForSeconds(cfg.spawnInterval);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    List<Enemy.EType> BuildSpawnList(WaveConfig cfg)
    {
        var list = new List<Enemy.EType>();
        for (int i = 0; i < cfg.villagers;   i++) list.Add(Enemy.EType.Villager);
        for (int i = 0; i < cfg.pitchforks;  i++) list.Add(Enemy.EType.Pitchfork);
        for (int i = 0; i < cfg.heavies;     i++) list.Add(Enemy.EType.Heavy);
        for (int i = 0; i < cfg.chainsaws;   i++) list.Add(Enemy.EType.Chainsaw);

        // Shuffle for variety
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // ─────────────────────────────────────────────────────────────────────
    void SpawnEnemy(Enemy.EType type)
    {
        Vector3 pos = ChooseSpawnPoint();

        var go = new GameObject($"Enemy_{type}");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        var enemy = go.AddComponent<Enemy>();
        enemy.Init(type);

        _aliveEnemies.Add(enemy);
        if (GameManager.I) GameManager.I.enemiesAlive++;
    }

    // ─────────────────────────────────────────────────────────────────────
    Vector3 ChooseSpawnPoint()
    {
        Vector3 origin;

        if (spawnPoints != null && spawnPoints.Length > 0)
            origin = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
        else
            origin = transform.position;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        return origin + new Vector3(offset.x, 0, offset.y);
    }

    // ─────────────────────────────────────────────────────────────────────
    bool AllDead()
    {
        _aliveEnemies.RemoveAll(e => !e || !e.gameObject.activeInHierarchy);
        return _aliveEnemies.Count == 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Override to react when the entire encounter is cleared.</summary>
    protected virtual void OnEncounterCleared()
    {
        if (GameUI.I) GameUI.I.ShowEncounterClearedMessage();
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (spawnPoints != null)
            foreach (var sp in spawnPoints)
                if (sp) Gizmos.DrawWireSphere(sp.position, spawnRadius);
        else
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
