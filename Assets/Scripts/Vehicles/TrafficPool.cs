using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Crazy-Taxi-style traffic pooling for RCC AI cars.
///
/// The scene ships ~38 AI cars, each expensive (four <see cref="RCC_WheelCollider"/>
/// friction solves per FixedUpdate plus an idle NavMeshAgent). Simulating all of
/// them at once is the dominant CPU cost. Instead we keep only a small live cap
/// (<see cref="liveCarCap"/>) simulating at any moment and treat the rest as a
/// reserve. As the player drives, cars that fall far behind and out of view are
/// deactivated back into the reserve, and reserve cars are teleported onto route
/// waypoints just around the player — so traffic stays dense wherever the player
/// goes, at a bounded cost.
///
/// Deactivation is <c>GameObject.SetActive(false)</c>: one call silences the AI
/// controller, car controller, all four wheel colliders, the NavMeshAgent,
/// renderers and colliders. Reactivation re-fires OnEnable (not Awake), so the
/// runtime "Navigator" child persists and RCC's only reaction is idempotent
/// registration in <see cref="RCC_SceneManager"/>.
///
/// Respawning uses RCC's own safe teleport (<see cref="RCC.Transport"/>, which
/// zeroes velocities and calls Physics.SyncTransforms), then rebinds the car to a
/// nearby route by writing <see cref="RCC_AICarController.waypointsContainer"/> and
/// <see cref="RCC_AICarController.currentWaypointIndex"/>. The AI picks the new
/// route up on its next FixedUpdate. Since every route is a large loop threading
/// the whole map, any car may be placed on any nearby route (a free pool).
///
/// Spawns are placed out of camera view and recycles only happen out of view, so
/// cars never visibly pop in or out. Teleport + activation is the costly op, so a
/// per-frame budget (<see cref="maxSpawnsPerFrame"/>) spreads bursts out.
///
/// Attach this to the "Traffic" GameObject (or any always-active object).
/// Adjust <see cref="liveCarCap"/> at runtime via the Inspector slider or
/// <see cref="SetDensity"/>.
/// </summary>
[DisallowMultipleComponent]
public class TrafficPool : MonoBehaviour
{
    [Header("Density")]
    [Tooltip("Maximum number of AI cars simulating at once (across all routes). The rest sit " +
             "deactivated as a reserve. Change at runtime via the slider or SetDensity().")]
    [SerializeField, Range(0, 40)] private int liveCarCap = 10;

    [Header("Per-route caps")]
    [Tooltip("Max cars allowed live on each route at once. Click 'Refresh Routes From Scene' (button " +
             "below the inspector, or right-click ▸ context menu) to fill this with every route, then set " +
             "each cap. Routes not listed here are unlimited (only the global cap applies).")]
    [SerializeField] private List<RouteQuota> routeQuotas = new List<RouteQuota>();

    [Header("Spawn ring")]
    [Tooltip("Recycled cars are teleported onto a route waypoint at least this far (metres, XZ) from the player.")]
    [SerializeField] private float spawnMinDistance = 60f;

    [Tooltip("...and at most this far. Keep below Despawn Distance so a freshly-spawned car isn't immediately recycled.")]
    [SerializeField] private float spawnMaxDistance = 140f;

    [Tooltip("A live car farther than this (metres, XZ) AND out of view is deactivated back into the reserve. " +
             "In-view cars are kept past this (up to Hard Despawn Distance) so they don't visibly pop out.")]
    [SerializeField] private float despawnDistance = 175f;

    [Tooltip("Absolute cap: a car farther than this (metres, XZ) is deactivated even if on-screen. " +
             "Keep above Despawn Distance and far enough that the pop is barely noticeable.")]
    [SerializeField] private float hardDespawnDistance = 250f;

    [Tooltip("Cars closer than this (metres, XZ) are never deactivated, so a quick camera turn can't pop nearby cars.")]
    [SerializeField] private float keepAliveDistance = 35f;

    [Tooltip("Never spawn a car closer than this (metres, XZ) to a car that is already live.")]
    [SerializeField] private float minSpawnSeparation = 12f;

    [Tooltip("Radius (metres, XZ) of a local 'area' used for the density check below.")]
    [SerializeField] private float spawnAreaRadius = 25f;

    [Tooltip("Only spawn where fewer than this many live cars are already within Spawn Area Radius — " +
             "keeps traffic from clustering at the same few spots.")]
    [SerializeField] private int maxCarsPerArea = 3;

    [Tooltip("Vertical offset (metres) added when teleporting a car onto a waypoint, so it drops in " +
             "instead of clipping into the road. Raise if cars spawn stuck in the ground.")]
    [SerializeField] private float spawnHeightOffset = 1.5f;

    [Header("View cone")]
    [Tooltip("Half-angle (degrees) from the camera forward beyond which a car counts as out of view. " +
             "Cars only recycle when out of view, and spawns prefer out-of-view spots.")]
    [SerializeField] private float cullViewAngle = 110f;

    [Header("Budget")]
    [Tooltip("Maximum number of cars spawned (teleported + activated) per frame. Deactivation is always immediate.")]
    [SerializeField] private int maxSpawnsPerFrame = 2;

    [Tooltip("Seconds between evaluations. 0 evaluates every frame; the checks are cheap so a small interval is plenty.")]
    [SerializeField] private float evaluateInterval = 0.2f;

    [Header("Gizmos")]
    [Tooltip("Draw the spawn/despawn rings, the view cone, and colour each pooled car by live/reserve state.")]
    [SerializeField] private bool drawGizmos = true;

    /// <summary>A spawnable point on a route: a waypoint position plus the facing toward its successor.</summary>
    private struct SpawnNode
    {
        public RCC_AIWaypointsContainer container;
        public int index;
        public Vector3 position;
        public Quaternion rotation;
    }

    /// <summary>Inspector row: a route and the maximum number of cars allowed live on it at once.</summary>
    [System.Serializable]
    public class RouteQuota
    {
        [Tooltip("Route's group name, filled by Refresh Routes From Scene for identification.")]
        public string label;
        public RCC_AIWaypointsContainer route;
        [Tooltip("Max cars that may be live on this route simultaneously.")]
        [Min(0)] public int maxLiveCars = 4;
    }

    private readonly List<RCC_AICarController> pool = new List<RCC_AICarController>();
    private SpawnNode[] spawnNodes = System.Array.Empty<SpawnNode>();

    // Positions of currently-live cars, rebuilt each Evaluate and used for the
    // spawn-separation test.
    private readonly List<Vector3> livePositions = new List<Vector3>();

    // Live car count per route and the configured cap per route, rebuilt each
    // Evaluate. A route absent from capPerRoute is unlimited.
    private readonly Dictionary<RCC_AIWaypointsContainer, int> livePerRoute = new Dictionary<RCC_AIWaypointsContainer, int>();
    private readonly Dictionary<RCC_AIWaypointsContainer, int> capPerRoute = new Dictionary<RCC_AIWaypointsContainer, int>();
    private readonly List<RCC_AIWaypointsContainer> overCapRoutes = new List<RCC_AIWaypointsContainer>();

    private float spawnMinSqr;
    private float spawnMaxSqr;
    private float despawnSqr;
    private float hardDespawnSqr;
    private float keepAliveSqr;
    private float minSpawnSepSqr;
    private float spawnAreaRadiusSqr;

    // Reusable scratch list of spawn nodes that pass every filter this frame; one is
    // then chosen at random so spawns don't repeat at the same spot.
    private readonly List<SpawnNode> candidateNodes = new List<SpawnNode>();

    // Cosine of the view half-angle. cosAngle (camera forward vs direction to car)
    // below cullDot means the car is behind the camera / out of view.
    private float cullDot;

    private float nextEvalTime;

    // Cached during Evaluate so gizmos can centre on the player and draw the view
    // cone without touching RCC_SceneManager.Instance (whose getter would spawn a
    // manager object if called from the editor's OnDrawGizmos).
    private Transform playerForGizmos;
    private bool hasCamForGizmos;
    private Vector3 camPosForGizmos;
    private Vector3 camForwardForGizmos;

    private void OnEnable()
    {
        // Pick up cars spawned at runtime. SetActive(true) during a respawn
        // re-fires this too, so HandleAISpawned dedups.
        RCC_AICarController.OnRCCAISpawned += HandleAISpawned;
    }

    private void OnDisable()
    {
        RCC_AICarController.OnRCCAISpawned -= HandleAISpawned;
    }

    private void Start()
    {
        // Same hysteresis intent throughout: the spawn ring sits inside the despawn
        // ring, the keep-alive bubble inside the spawn ring, and the "in view" cone
        // inside the "out of view" cone. Clamp defensively so bad Inspector values
        // can't cause flicker.
        hardDespawnDistance = Mathf.Max(hardDespawnDistance, despawnDistance);
        spawnMaxDistance = Mathf.Min(spawnMaxDistance, despawnDistance);
        spawnMinDistance = Mathf.Min(spawnMinDistance, spawnMaxDistance);
        keepAliveDistance = Mathf.Min(keepAliveDistance, spawnMinDistance);

        spawnMinSqr = spawnMinDistance * spawnMinDistance;
        spawnMaxSqr = spawnMaxDistance * spawnMaxDistance;
        despawnSqr = despawnDistance * despawnDistance;
        hardDespawnSqr = hardDespawnDistance * hardDespawnDistance;
        keepAliveSqr = keepAliveDistance * keepAliveDistance;
        minSpawnSepSqr = minSpawnSeparation * minSpawnSeparation;
        spawnAreaRadiusSqr = spawnAreaRadius * spawnAreaRadius;
        cullDot = Mathf.Cos(cullViewAngle * Mathf.Deg2Rad);

        SeedFromSceneManager();
        BuildSpawnNodes();

        // If the caps list was never filled in the editor, populate it at runtime so
        // per-route limits still apply (with the default cap).
        if (routeQuotas.Count == 0)
            RefreshRoutesFromScene();
    }

    /// <summary>
    /// Seed the pool from RCC's own vehicle registry rather than scanning the scene.
    /// By Start, every scene-placed AI car has registered itself.
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

    /// <summary>
    /// Flatten every route into a list of spawn points. Waypoints are static, so
    /// this is built once. Each node stores the facing toward its successor so a
    /// teleported car is aimed down the route.
    /// </summary>
    private void BuildSpawnNodes()
    {
        RCC_AIWaypointsContainer[] containers =
            FindObjectsByType<RCC_AIWaypointsContainer>(FindObjectsSortMode.None);

        List<SpawnNode> nodes = new List<SpawnNode>();

        foreach (RCC_AIWaypointsContainer container in containers)
        {
            if (!container || container.waypoints == null)
                continue;

            int count = container.waypoints.Count;

            for (int i = 0; i < count; i++)
            {
                RCC_Waypoint waypoint = container.waypoints[i];

                if (!waypoint)
                    continue;

                Vector3 pos = waypoint.transform.position;

                // Face the next waypoint (routes are closed loops, so wrap).
                RCC_Waypoint next = container.waypoints[(i + 1) % count];
                Quaternion rot = Quaternion.identity;

                if (next && next != waypoint)
                {
                    Vector3 forward = next.transform.position - pos;
                    forward.y = 0f;

                    if (forward.sqrMagnitude > 1e-4f)
                        rot = Quaternion.LookRotation(forward, Vector3.up);
                }

                nodes.Add(new SpawnNode
                {
                    container = container,
                    index = i,
                    position = pos,
                    rotation = rot,
                });
            }
        }

        spawnNodes = nodes.ToArray();
    }

    private void HandleAISpawned(RCC_AICarController ai)
    {
        if (ai && !pool.Contains(ai))
            pool.Add(ai);
    }

    /// <summary>Set the live-car cap (density) at runtime. Clamped to the pool size.</summary>
    public void SetDensity(int liveCars)
    {
        liveCarCap = Mathf.Clamp(liveCars, 0, pool.Count);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextEvalTime)
        {
            nextEvalTime = Time.unscaledTime + evaluateInterval;
            Evaluate();
        }
    }

    /// <summary>
    /// Reconcile the live set toward the cap: prune destroyed cars, deactivate cars
    /// that are over-cap or drifted out of range/view, and teleport reserve cars
    /// onto nearby routes to fill the cap.
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

        ResolveCamera(out bool haveCam, out Vector3 camPos, out Vector3 camForwardXZ);
        hasCamForGizmos = haveCam;
        camPosForGizmos = camPos;
        camForwardForGizmos = camForwardXZ;

        livePositions.Clear();
        livePerRoute.Clear();
        RebuildCapLookup();
        int liveCount = 0;

        // Pass 1: prune, recycle out-of-range cars, and tally the live set.
        // Iterate backwards so we can drop destroyed entries in place.
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            RCC_AICarController ai = pool[i];

            // Unity fake-null is true only for destroyed objects, not deactivated
            // ones — so this prunes real removals without losing reserve cars.
            if (ai == null)
            {
                pool.RemoveAt(i);
                continue;
            }

            // Never manage the player's own vehicle.
            if (ai.CarController == player)
                continue;

            if (!ai.gameObject.activeSelf)
                continue;

            Vector3 carPos = ai.transform.position;
            float sqr = SqrDistanceXZ(carPos, playerPos);

            // Recycle a live car when it is past the hard cap (regardless of view),
            // or past the soft despawn distance while out of view (so on-screen cars
            // don't visibly pop out). The keep-alive bubble is implicit here
            // (despawnDistance is always larger).
            bool tooFar = sqr > hardDespawnSqr;
            bool farAndHidden = sqr > despawnSqr && OutOfView(haveCam, camPos, camForwardXZ, carPos);

            if (tooFar || farAndHidden)
            {
                ai.gameObject.SetActive(false);
                continue;
            }

            livePositions.Add(carPos);
            AddRoute(ai.waypointsContainer, 1);
            liveCount++;
        }

        // Pass 2: enforce per-route caps (deactivate hidden/far excess first).
        liveCount -= EnforceRouteCaps(player, playerPos, haveCam, camPos, camForwardXZ);

        // Pass 3: if over the global cap, deactivate the farthest live cars (out-of-view first).
        if (liveCount > liveCarCap)
            liveCount -= DeactivateWorst(null, liveCount - liveCarCap, player, playerPos, haveCam, camPos, camForwardXZ);

        // Pass 4: if under the global cap, teleport reserve cars onto nearby routes.
        if (liveCount < liveCarCap)
            FillToCap(player, playerPos, haveCam, camPos, camForwardXZ, liveCarCap - liveCount);
    }

    /// <summary>
    /// Deactivate the <paramref name="excess"/> "worst" live cars (farthest, and
    /// preferring out-of-view ones so on-screen cars aren't yanked), optionally
    /// restricted to a single <paramref name="routeFilter"/>. Cars inside the
    /// keep-alive bubble are never dropped. Returns how many were deactivated and
    /// keeps <see cref="livePositions"/> / <see cref="livePerRoute"/> in sync.
    /// </summary>
    private int DeactivateWorst(RCC_AIWaypointsContainer routeFilter, int excess,
        RCC_CarControllerV4 player, Vector3 playerPos, bool haveCam, Vector3 camPos, Vector3 camForwardXZ)
    {
        int removed = 0;

        // Cheap repeated-max selection: excess is tiny, so an O(excess * live) scan
        // beats allocating and sorting.
        while (removed < excess)
        {
            RCC_AICarController best = null;
            Vector3 bestPos = default;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < pool.Count; i++)
            {
                RCC_AICarController ai = pool[i];

                if (ai == null || ai.CarController == player || !ai.gameObject.activeSelf)
                    continue;

                if (routeFilter != null && ai.waypointsContainer != routeFilter)
                    continue;

                Vector3 carPos = ai.transform.position;
                float sqr = SqrDistanceXZ(carPos, playerPos);

                if (sqr <= keepAliveSqr)
                    continue;

                // Rank by distance, with a large bonus for being out of view so
                // those are dropped before any on-screen car.
                float score = sqr;
                if (OutOfView(haveCam, camPos, camForwardXZ, carPos))
                    score += 1e9f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = ai;
                    bestPos = carPos;
                }
            }

            if (best == null)
                break;

            AddRoute(best.waypointsContainer, -1);
            best.gameObject.SetActive(false);
            livePositions.Remove(bestPos);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Deactivate cars on any route that currently exceeds its configured cap.
    /// Returns the total number deactivated.
    /// </summary>
    private int EnforceRouteCaps(RCC_CarControllerV4 player, Vector3 playerPos,
        bool haveCam, Vector3 camPos, Vector3 camForwardXZ)
    {
        // Snapshot over-cap routes first — DeactivateWorst mutates livePerRoute.
        overCapRoutes.Clear();

        foreach (KeyValuePair<RCC_AIWaypointsContainer, int> kv in livePerRoute)
        {
            if (kv.Value > RouteCap(kv.Key))
                overCapRoutes.Add(kv.Key);
        }

        int removed = 0;

        for (int i = 0; i < overCapRoutes.Count; i++)
        {
            RCC_AIWaypointsContainer route = overCapRoutes[i];
            int excess = LiveOnRoute(route) - RouteCap(route);

            if (excess > 0)
                removed += DeactivateWorst(route, excess, player, playerPos, haveCam, camPos, camForwardXZ);
        }

        return removed;
    }

    /// <summary>Rebuild the route→cap lookup from the Inspector list (supports live editing).</summary>
    private void RebuildCapLookup()
    {
        capPerRoute.Clear();

        for (int i = 0; i < routeQuotas.Count; i++)
        {
            RouteQuota q = routeQuotas[i];

            if (q != null && q.route != null)
                capPerRoute[q.route] = Mathf.Max(0, q.maxLiveCars);
        }
    }

    private void AddRoute(RCC_AIWaypointsContainer route, int delta)
    {
        if (route == null)
            return;

        livePerRoute.TryGetValue(route, out int count);
        count += delta;

        if (count <= 0)
            livePerRoute.Remove(route);
        else
            livePerRoute[route] = count;
    }

    private int RouteCap(RCC_AIWaypointsContainer route)
    {
        return route != null && capPerRoute.TryGetValue(route, out int cap) ? cap : int.MaxValue;
    }

    private int LiveOnRoute(RCC_AIWaypointsContainer route)
    {
        return route != null && livePerRoute.TryGetValue(route, out int count) ? count : 0;
    }

    /// <summary>
    /// Fill <see cref="routeQuotas"/> with every route in the scene, labelled by its
    /// group name, preserving caps already set. Runs from the Inspector button, the
    /// context menu, or automatically at runtime if the list is empty.
    /// </summary>
    [ContextMenu("Refresh Routes From Scene")]
    public void RefreshRoutesFromScene()
    {
        RCC_AIWaypointsContainer[] containers =
            FindObjectsByType<RCC_AIWaypointsContainer>(FindObjectsSortMode.None);

        // Preserve existing caps keyed by route.
        Dictionary<RCC_AIWaypointsContainer, int> existing = new Dictionary<RCC_AIWaypointsContainer, int>();

        foreach (RouteQuota q in routeQuotas)
        {
            if (q != null && q.route != null)
                existing[q.route] = q.maxLiveCars;
        }

        routeQuotas.Clear();

        foreach (RCC_AIWaypointsContainer container in containers)
        {
            if (!container)
                continue;

            int cap = existing.TryGetValue(container, out int v) ? v : 4;

            routeQuotas.Add(new RouteQuota
            {
                label = RouteLabel(container),
                route = container,
                maxLiveCars = cap,
            });
        }

        routeQuotas.Sort((a, b) => string.CompareOrdinal(a.label, b.label));
    }

    /// <summary>A route's readable name: its parent group's name (routes are all named "Waypoints Container").</summary>
    private static string RouteLabel(RCC_AIWaypointsContainer container)
    {
        if (!container)
            return "(missing)";

        Transform parent = container.transform.parent;
        return parent ? parent.name : container.name;
    }

    /// <summary>
    /// Teleport up to min(<paramref name="needed"/>, budget) reserve cars onto route
    /// waypoints in the spawn ring, preferring spots out of view and clear of other
    /// live cars.
    /// </summary>
    private void FillToCap(RCC_CarControllerV4 player, Vector3 playerPos,
        bool haveCam, Vector3 camPos, Vector3 camForwardXZ, int needed)
    {
        if (spawnNodes.Length == 0)
            return;

        int budget = Mathf.Min(needed, maxSpawnsPerFrame);
        int spawned = 0;

        for (int i = 0; i < pool.Count && spawned < budget; i++)
        {
            RCC_AICarController ai = pool[i];

            if (ai == null || ai.CarController == player || ai.gameObject.activeSelf)
                continue;

            if (!TryPickSpawnNode(playerPos, haveCam, camPos, camForwardXZ, out SpawnNode node))
                break; // no valid node available this frame — try again next tick

            // Activate first so the Rigidbody is live for Transport's MovePosition,
            // then rebind the route and teleport. FeedRCC resumes throttle next tick.
            // Lift the spawn slightly so the car drops onto the road instead of
            // clipping through it; velocities are zeroed by Transport so it settles.
            ai.gameObject.SetActive(true);
            ai.waypointsContainer = node.container;
            ai.currentWaypointIndex = node.index;
            RCC.Transport(ai.CarController, node.position + Vector3.up * spawnHeightOffset, node.rotation);

            livePositions.Add(node.position);
            AddRoute(node.container, 1);
            spawned++;
        }
    }

    /// <summary>
    /// Collect every spawn node that passes all filters — in the ring, under the
    /// route cap, clear of nearby live cars, in a not-too-crowded area, and (when a
    /// camera exists) out of view — then return one at random so spawns vary instead
    /// of repeating at the same spot. Falls back to allowing in-view nodes only if no
    /// out-of-view node qualifies.
    /// </summary>
    private bool TryPickSpawnNode(Vector3 playerPos, bool haveCam, Vector3 camPos, Vector3 camForwardXZ,
        out SpawnNode chosen)
    {
        chosen = default;

        // Strict pass requires out-of-view (no pops); if it yields nothing, relax it.
        if (CollectCandidates(playerPos, haveCam, camPos, camForwardXZ, requireOutOfView: haveCam) == 0)
        {
            if (!haveCam || CollectCandidates(playerPos, haveCam, camPos, camForwardXZ, requireOutOfView: false) == 0)
                return false;
        }

        chosen = candidateNodes[Random.Range(0, candidateNodes.Count)];
        return true;
    }

    /// <summary>Fill <see cref="candidateNodes"/> with qualifying nodes; returns the count.</summary>
    private int CollectCandidates(Vector3 playerPos, bool haveCam, Vector3 camPos, Vector3 camForwardXZ,
        bool requireOutOfView)
    {
        candidateNodes.Clear();

        for (int i = 0; i < spawnNodes.Length; i++)
        {
            SpawnNode node = spawnNodes[i];
            float sqr = SqrDistanceXZ(node.position, playerPos);

            if (sqr < spawnMinSqr || sqr > spawnMaxSqr)
                continue;

            // Respect the route's live-car cap.
            if (LiveOnRoute(node.container) >= RouteCap(node.container))
                continue;

            if (TooCloseToLive(node.position))
                continue;

            // Local density: skip areas that already have enough cars.
            if (LiveCountNear(node.position) >= maxCarsPerArea)
                continue;

            if (requireOutOfView && !OutOfView(haveCam, camPos, camForwardXZ, node.position))
                continue;

            candidateNodes.Add(node);
        }

        return candidateNodes.Count;
    }

    private int LiveCountNear(Vector3 pos)
    {
        int count = 0;

        for (int i = 0; i < livePositions.Count; i++)
        {
            if (SqrDistanceXZ(livePositions[i], pos) < spawnAreaRadiusSqr)
                count++;
        }

        return count;
    }

    private bool TooCloseToLive(Vector3 pos)
    {
        for (int i = 0; i < livePositions.Count; i++)
        {
            if (SqrDistanceXZ(livePositions[i], pos) < minSpawnSepSqr)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolve the active camera's position and XZ-flattened forward. Prefer RCC's
    /// active camera; fall back to Camera.main. haveCam is false if none exists or
    /// the camera looks straight up/down (degenerate flattened forward).
    /// </summary>
    private void ResolveCamera(out bool haveCam, out Vector3 camPos, out Vector3 camForwardXZ)
    {
        haveCam = false;
        camPos = default;
        camForwardXZ = default;

        Camera cam = RCC_SceneManager.Instance ? RCC_SceneManager.Instance.activeMainCamera : null;

        if (!cam)
            cam = Camera.main;

        if (!cam)
            return;

        camPos = cam.transform.position;
        camForwardXZ = cam.transform.forward;
        camForwardXZ.y = 0f;

        if (camForwardXZ.sqrMagnitude <= 1e-6f)
            return;

        camForwardXZ.Normalize();
        haveCam = true;
    }

    /// <summary>
    /// True when the car is clearly behind the camera. Uses XZ so the TPS camera's
    /// downward pitch doesn't misclassify level cars. Always false without a camera.
    /// </summary>
    private bool OutOfView(bool haveCam, Vector3 camPos, Vector3 camForwardXZ, Vector3 carPos)
    {
        if (!haveCam)
            return false;

        Vector3 toCar = carPos - camPos;
        toCar.y = 0f;
        float mag = toCar.magnitude;

        if (mag <= 1e-4f)
            return false;

        return Vector3.Dot(camForwardXZ, toCar / mag) < cullDot;
    }

    private static float SqrDistanceXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static readonly Color LiveColor = new Color(0.25f, 0.9f, 0.35f);
    private static readonly Color ReserveColor = new Color(0.95f, 0.35f, 0.2f);
    private static readonly Color SpawnRingColor = new Color(0.35f, 0.6f, 0.95f);

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        // Centre on the player at runtime; fall back to this object in the editor
        // (before play, there is no player and the pool is empty).
        Vector3 center = playerForGizmos ? playerForGizmos.position : transform.position;

        // Rings, not spheres: everything compares XZ distance and ignores height.
        DrawHorizontalCircle(center, keepAliveDistance, LiveColor);
        DrawHorizontalCircle(center, spawnMinDistance, SpawnRingColor);
        DrawHorizontalCircle(center, spawnMaxDistance, SpawnRingColor);
        DrawHorizontalCircle(center, despawnDistance, ReserveColor);
        DrawHorizontalCircle(center, hardDespawnDistance, ReserveColor);

        if (hasCamForGizmos)
            DrawViewCone(camPosForGizmos, camForwardForGizmos, cullViewAngle, despawnDistance, ReserveColor);

        // Per-car state (populated only in play mode).
        for (int i = 0; i < pool.Count; i++)
        {
            RCC_AICarController ai = pool[i];

            if (ai == null)
                continue;

            bool live = ai.gameObject.activeSelf;
            Vector3 carPos = ai.transform.position;

            Gizmos.color = live ? LiveColor : ReserveColor;

            if (live)
                Gizmos.DrawLine(center, carPos);

            Gizmos.DrawWireCube(carPos + Vector3.up * 0.5f, Vector3.one);
        }
    }

    /// <summary>
    /// Draw the two edges of a horizontal cone from <paramref name="apex"/> along
    /// <paramref name="forwardXZ"/>, spread by ±<paramref name="halfAngle"/> degrees.
    /// </summary>
    private static void DrawViewCone(Vector3 apex, Vector3 forwardXZ, float halfAngle, float length, Color color)
    {
        Gizmos.color = color;

        Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forwardXZ;
        Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * forwardXZ;

        Gizmos.DrawLine(apex, apex + left * length);
        Gizmos.DrawLine(apex, apex + right * length);
    }

    private static void DrawHorizontalCircle(Vector3 center, float radius, Color color)
    {
        Gizmos.color = color;

        const int segments = 64;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
