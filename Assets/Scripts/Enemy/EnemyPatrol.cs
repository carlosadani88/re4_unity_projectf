// ============================================================================
// VILL4GE — EnemyPatrol.cs
// Waypoint-based patrol component for enemies.
// Attach alongside Enemy.cs to give a Ganado a patrol route.
//
// States: Idle → Patrol → Alert → Chase (handled by Enemy.cs)
// This script drives movement only when Enemy is in Idle/Patrol state.
// ============================================================================
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Enemy))]
public class EnemyPatrol : MonoBehaviour
{
    // ── Patrol Mode ───────────────────────────────────────────────────────
    public enum PatrolMode { Waypoints, Random, Stationary }

    [Header("Patrol")]
    public PatrolMode mode = PatrolMode.Stationary;

    [Tooltip("Ordered waypoints for Waypoints mode. Loop when reaching the last.")]
    public Transform[] waypoints;

    [Tooltip("Radius for random wandering (Random mode).")]
    public float wanderRadius = 8f;

    [Tooltip("How long to idle at each waypoint before moving on.")]
    public float idleTimeAtWaypoint = 2f;

    [Tooltip("Patrol speed (fraction of base enemy speed).")]
    [Range(0.2f, 1f)] public float patrolSpeedFraction = 0.45f;

    // ── Detection ─────────────────────────────────────────────────────────
    [Header("Detection")]
    [Tooltip("Radius within which the enemy can HEAR the player (works in any direction).")]
    public float hearRadius = 6f;

    [Tooltip("Radius within which the enemy can SEE the player.")]
    public float sightRadius = 14f;

    [Tooltip("Field-of-view half-angle for sight (degrees, e.g. 60 = 120° total FOV).")]
    [Range(10f, 90f)] public float sightAngleHalf = 55f;

    [Tooltip("Layer mask for sight line-of-sight raycasts.")]
    public LayerMask obstructionMask = ~0;

    // ── Constants ─────────────────────────────────────────────────────────
    /// <summary>Vertical offset for sight raycasts (eye level).</summary>
    const float EYE_HEIGHT_OFFSET = 1.5f;

    // ── State ─────────────────────────────────────────────────────────────
    public enum PatrolState { Idle, MovingToWaypoint, Alerted }
    public PatrolState CurrentState { get; private set; } = PatrolState.Idle;

    // ─────────────────────────────────────────────────────────────────────
    Enemy _enemy;
    CharacterController _cc;
    int  _waypointIdx     = 0;
    float _idleTimer      = 0f;
    Vector3 _targetPos;
    bool _patrolInitialized = false;
    Vector3 _spawnPos;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        _enemy    = GetComponent<Enemy>();
        _cc       = GetComponent<CharacterController>();
        _spawnPos = transform.position;

        if (mode == PatrolMode.Waypoints && (waypoints == null || waypoints.Length == 0))
        {
            Debug.LogWarning($"[EnemyPatrol] {name}: Waypoints mode but no waypoints assigned. Switching to Stationary.");
            mode = PatrolMode.Stationary;
        }

        NextPatrolTarget();
        _patrolInitialized = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!_patrolInitialized) return;

        // Only patrol when Enemy is alive and not already chasing
        if (_enemy == null || _enemy.IsDead) return;
        if (_enemy.IsChasing) return;    // Enemy.cs handles movement when chasing

        DetectPlayer();
        if (CurrentState == PatrolState.Alerted) return; // enemy.cs takes over

        switch (mode)
        {
            case PatrolMode.Stationary:   IdleLoop();      break;
            case PatrolMode.Waypoints:    WaypointLoop();  break;
            case PatrolMode.Random:       RandomWander();  break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void IdleLoop()
    {
        // Just stand still and scan (detection is in DetectPlayer)
    }

    // ─────────────────────────────────────────────────────────────────────
    void WaypointLoop()
    {
        float distToTarget = Vector3.Distance(transform.position, _targetPos);

        if (CurrentState == PatrolState.Idle)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                _waypointIdx = (_waypointIdx + 1) % waypoints.Length;
                _targetPos   = waypoints[_waypointIdx].position;
                CurrentState = PatrolState.MovingToWaypoint;
            }
        }
        else if (CurrentState == PatrolState.MovingToWaypoint)
        {
            MoveTowards(_targetPos, _enemy.speed * patrolSpeedFraction);

            if (distToTarget < 0.6f)
            {
                CurrentState = PatrolState.Idle;
                _idleTimer   = idleTimeAtWaypoint;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void RandomWander()
    {
        float distToTarget = Vector3.Distance(transform.position, _targetPos);

        if (CurrentState == PatrolState.Idle)
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                Vector2 rnd = Random.insideUnitCircle * wanderRadius;
                _targetPos   = _spawnPos + new Vector3(rnd.x, 0, rnd.y);
                CurrentState = PatrolState.MovingToWaypoint;
            }
        }
        else if (CurrentState == PatrolState.MovingToWaypoint)
        {
            MoveTowards(_targetPos, _enemy.speed * patrolSpeedFraction);

            if (distToTarget < 0.6f)
            {
                CurrentState = PatrolState.Idle;
                _idleTimer   = idleTimeAtWaypoint + Random.Range(-0.5f, 1f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void MoveTowards(Vector3 target, float speed)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) return;

        dir.Normalize();
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            Time.deltaTime * 4f);

        if (_cc)
            _cc.Move((dir * speed + Physics.gravity) * Time.deltaTime);
        else
            transform.position += dir * speed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────────
    void DetectPlayer()
    {
        if (!GameManager.I?.player) return;
        Transform playerT = GameManager.I.player.transform;
        Vector3   toPlayer = playerT.position - transform.position;
        float     dist     = toPlayer.magnitude;

        bool detected = false;

        // Hear check (no direction requirement)
        if (dist <= hearRadius) detected = true;

        // Sight check (cone + LOS)
        if (!detected && dist <= sightRadius)
        {
            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle < sightAngleHalf)
            {
                // Line-of-sight raycast
                if (!Physics.Raycast(
                        transform.position + Vector3.up * EYE_HEIGHT_OFFSET,
                        toPlayer.normalized, dist,
                        obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    detected = true;
                }
            }
        }

        if (detected)
        {
            CurrentState = PatrolState.Alerted;
            _enemy.Alert(playerT); // Tell Enemy.cs to start chasing
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    void NextPatrolTarget()
    {
        if (mode == PatrolMode.Waypoints && waypoints.Length > 0)
            _targetPos = waypoints[_waypointIdx].position;
        else
            _targetPos = transform.position;
    }

    // ─────────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // Hear radius
        Gizmos.color = new Color(1, 1, 0, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hearRadius);

        // Sight cone
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, sightRadius);
        Vector3 fwd   = transform.forward * sightRadius;
        Vector3 right = Quaternion.Euler(0, sightAngleHalf, 0)  * transform.forward * sightRadius;
        Vector3 left  = Quaternion.Euler(0, -sightAngleHalf, 0) * transform.forward * sightRadius;
        Gizmos.DrawLine(transform.position, transform.position + fwd);
        Gizmos.DrawLine(transform.position, transform.position + right);
        Gizmos.DrawLine(transform.position, transform.position + left);

        // Waypoints
        if (waypoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.3f);
                if (i + 1 < waypoints.Length && waypoints[i + 1])
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
            if (waypoints.Length > 1 && waypoints[0] && waypoints[waypoints.Length - 1])
                Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
