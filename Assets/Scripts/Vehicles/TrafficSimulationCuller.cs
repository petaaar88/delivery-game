using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based simulation culling for RCC AI traffic.
///
/// Each AI car is expensive: the dominant per-tick cost is not the car
/// controller but the <see cref="RCC_WheelCollider"/> friction math that runs in
/// its own FixedUpdate (four per car), plus an idle NavMeshAgent on the AI's
/// "Navigator" child. Disabling only the controllers (as a naive cull would)
/// leaves all of that running.
///
/// So we cull the whole car with <c>GameObject.SetActive(false)</c>: one call
/// silences the AI controller, car controller, all four wheel colliders, the
/// NavMeshAgent, renderers and colliders at once. This is safe because RCC's
/// only reaction to an AI car enabling/disabling is idempotent registration in
/// <see cref="RCC_SceneManager"/> (a Contains-guarded add/remove on its vehicle
/// list — no camera retarget, no Destroy). Reactivating a car resumes it from
/// its serialized waypoint index, and Awake does not re-run so the Navigator
/// child persists.
///
/// Culling (deactivation) is cheap and applied immediately. Reactivation runs
/// OnEnable resets and lets physics settle, so a burst of revives (player
/// driving back into a cluster) can hitch — we therefore budget a limited
/// number of revives per frame.
///
/// Attach this to the "Traffic" GameObject (or any always-active object).
/// </summary>
[DisallowMultipleComponent]
public class TrafficSimulationCuller : MonoBehaviour
{
    [Header("Distances (hysteresis)")]
    [Tooltip("An active car is deactivated once it is farther than this (metres, XZ) from the player.")]
    [SerializeField] private float cullDistance = 120f;

    [Tooltip("A culled car is reactivated once it is closer than this (metres, XZ) to the player. " +
             "Keep it below Cull Distance so cars don't flicker on the boundary.")]
    [SerializeField] private float reviveDistance = 100f;

    [Header("Budget")]
    [Tooltip("Maximum number of cars reactivated per frame. Deactivation is always immediate.")]
    [SerializeField] private int maxRevivesPerFrame = 3;

    [Tooltip("Seconds between distance evaluations. 0 evaluates every frame; distance checks are cheap so a small interval is plenty.")]
    [SerializeField] private float evaluateInterval = 0.2f;

    [Header("Gizmos")]
    [Tooltip("Draw the cull/revive rings around the player and colour each tracked car by live/culled state.")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<RCC_AICarController> tracked = new List<RCC_AICarController>();
    private readonly Queue<RCC_AICarController> reviveQueue = new Queue<RCC_AICarController>();
    private readonly HashSet<RCC_AICarController> queued = new HashSet<RCC_AICarController>();

    private float cullSqr;
    private float reviveSqr;
    private float nextEvalTime;

    // Cached during Evaluate so gizmos can centre on the player without touching
    // RCC_SceneManager.Instance (whose getter would spawn a manager object if
    // called from the editor's OnDrawGizmos).
    private Transform playerForGizmos;

    private void OnEnable()
    {
        // Pick up cars spawned at runtime. SetActive(true) during a revive
        // re-fires this too, so HandleAISpawned dedups.
        RCC_AICarController.OnRCCAISpawned += HandleAISpawned;
    }

    private void OnDisable()
    {
        RCC_AICarController.OnRCCAISpawned -= HandleAISpawned;
    }

    private void Start()
    {
        // reviveDistance must stay below cullDistance for the hysteresis band to
        // exist; clamp defensively so a bad Inspector value can't cause flicker.
        reviveDistance = Mathf.Min(reviveDistance, cullDistance);
        cullSqr = cullDistance * cullDistance;
        reviveSqr = reviveDistance * reviveDistance;

        SeedFromSceneManager();
    }

    /// <summary>
    /// Seed the tracked list from RCC's own vehicle registry rather than scanning
    /// the scene. By Start, every scene-placed AI car has registered itself.
    /// </summary>
    private void SeedFromSceneManager()
    {
        RCC_SceneManager sceneManager = RCC_SceneManager.Instance;

        if (!sceneManager)
            return;

        foreach (RCC_CarControllerV4 vehicle in sceneManager.allVehicles)
        {
            if (!vehicle)
                continue;

            RCC_AICarController ai = vehicle.GetComponent<RCC_AICarController>();

            if (ai)
                HandleAISpawned(ai);
        }
    }

    private void HandleAISpawned(RCC_AICarController ai)
    {
        if (ai && !tracked.Contains(ai))
            tracked.Add(ai);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextEvalTime)
        {
            nextEvalTime = Time.unscaledTime + evaluateInterval;
            Evaluate();
        }

        ProcessReviveQueue();
    }

    /// <summary>
    /// Deactivate cars that drifted out of range, and queue in-range cars for
    /// reactivation. Also prunes genuinely destroyed cars from the list.
    /// </summary>
    private void Evaluate()
    {
        RCC_CarControllerV4 player = RCC_SceneManager.Instance
            ? RCC_SceneManager.Instance.activePlayerVehicle
            : null;

        // No player yet (not spawned) — leave everything as-is.
        if (!player)
        {
            playerForGizmos = null;
            return;
        }

        playerForGizmos = player.transform;
        Vector3 playerPos = player.transform.position;

        // Iterate backwards so we can drop destroyed entries in place.
        for (int i = tracked.Count - 1; i >= 0; i--)
        {
            RCC_AICarController ai = tracked[i];

            // Unity fake-null is true only for destroyed objects, not ones we
            // merely deactivated — so this prunes real removals without losing
            // cars we culled ourselves.
            if (ai == null)
            {
                tracked.RemoveAt(i);
                continue;
            }

            // Never manage the player's own vehicle.
            if (ai.CarController == player)
                continue;

            float sqr = SqrDistanceXZ(ai.transform.position, playerPos);
            bool active = ai.gameObject.activeSelf;

            if (active)
            {
                if (sqr > cullSqr)
                    ai.gameObject.SetActive(false);
            }
            else if (sqr < reviveSqr && !queued.Contains(ai))
            {
                reviveQueue.Enqueue(ai);
                queued.Add(ai);
            }
        }
    }

    /// <summary>
    /// Reactivate up to <see cref="maxRevivesPerFrame"/> queued cars, re-checking
    /// each one's state since it may have been destroyed or moved away again
    /// while it sat in the queue.
    /// </summary>
    private void ProcessReviveQueue()
    {
        int revived = 0;

        while (revived < maxRevivesPerFrame && reviveQueue.Count > 0)
        {
            RCC_AICarController ai = reviveQueue.Dequeue();

            if (ai != null)
                queued.Remove(ai);

            if (ai == null || ai.gameObject.activeSelf)
                continue;

            ai.gameObject.SetActive(true);
            revived++;
        }
    }

    private static float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static readonly Color ReviveColor = new Color(0.25f, 0.9f, 0.35f);
    private static readonly Color CullColor = new Color(0.95f, 0.35f, 0.2f);

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        // Centre on the player at runtime; fall back to this object in the editor
        // (before play, there is no player and the tracked list is empty).
        Vector3 center = playerForGizmos ? playerForGizmos.position : transform.position;

        // Rings, not spheres: culling compares XZ distance and ignores height,
        // so a horizontal circle is the honest shape of the boundary.
        DrawHorizontalCircle(center, reviveDistance, ReviveColor);
        DrawHorizontalCircle(center, cullDistance, CullColor);

        // Per-car state (populated only in play mode).
        for (int i = 0; i < tracked.Count; i++)
        {
            RCC_AICarController ai = tracked[i];

            if (ai == null)
                continue;

            bool live = ai.gameObject.activeSelf;
            Vector3 carPos = ai.transform.position;

            Gizmos.color = live ? ReviveColor : CullColor;
            Gizmos.DrawLine(center, carPos);
            Gizmos.DrawWireCube(carPos + Vector3.up * 0.5f, Vector3.one);
        }
    }

    private static void DrawHorizontalCircle(Vector3 center, float radius, Color color)
    {
        Gizmos.color = color;

        const int segments = 64;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
