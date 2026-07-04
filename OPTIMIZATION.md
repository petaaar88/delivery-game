# Performance Optimization Guide — GameScene

> Audit performed 2026-07-04 via Unity MCP on `Assets/Scenes/GameScene.unity`.
> Baseline: ~20 FPS in editor play mode, Game view 998×476.

## Scene audit results

| Metric | Value | Note |
|---|---|---|
| MeshRenderers | 1938 | **0 marked Static** — no static batching, no occlusion culling |
| Shadow-casting renderers | 1926 | Nearly everything casts shadows |
| Total scene vertices | ~2.1M | 979 unique meshes, 100 materials |
| RCC vehicles | 39 (38 AI) | 156 WheelColliders, all fully simulated every physics tick |
| Lights | 1 realtime directional | Soft shadows, 4 cascades @ 2048 |
| Terrain | shadows *TwoSided*, Draw Instanced **off** | pixelError 5, basemap distance 1000 |
| Physics | fixedDeltaTime 0.02, solver iterations 6 | OK, don't lower fixedDeltaTime |
| URP asset (`PC_RPAsset`) | SRP Batcher on, HDR on, MSAA off, shadow distance 50 | |
| Waypoint containers | 14 | Gizmo drawing = editor overhead |

**Diagnosis:** at this resolution the game is almost certainly **CPU-bound**: vehicle physics + per-object culling/draw setup + editor overhead.

---

## 1. AI traffic — biggest cost (scales with every car added)

- **Distance-based simulation culling.** Write a manager that, for each AI car farther than ~100–150 m from the player:
  - disables `RCC_AICarController` + `RCC_CarControllerV4`
  - sets the `Rigidbody` to kinematic (or calls `Sleep()`)
  - disables its renderers
  - re-enables everything when the player gets close again
- **Better long-term: pool traffic around the player** (Crazy Taxi style) — keep only ~10–15 live cars; respawn/teleport far cars onto waypoint routes near the player instead of simulating the whole map.
- **Kinematic NPC option.** Each RCC car is expensive (wheel friction, suspension, alignment every FixedUpdate). NPC traffic doesn't need full RCC physics — a kinematic waypoint-follower (transform movement + simple box collider) costs ~1% of an RCC car.
- Keep `Time.fixedDeltaTime` at 0.02.

## 2. Mark the environment Static

Select all non-moving map objects (buildings, roads, props — everything except vehicles and pickups) and tick **Static** in the Inspector. Enables:

- **Static batching** — merges draw calls across the 1938 renderers
- **Occlusion culling** — then bake via *Window → Rendering → Occlusion Culling → Bake* so buildings stop rendering geometry hidden behind them

~10-minute change, large payoff.

## 3. Cut shadow work

- **Cast Shadows → Off** on small props (`MeshRenderer` setting). With 1926 casters, the shadow pass re-renders nearly the whole scene up to 4 extra times (once per cascade).
- **Reduce cascades 4 → 2** in `PC_RPAsset` — with a 50 m shadow distance, 4 cascades is waste.
- **Terrain shadow casting: TwoSided → Off** (a flat terrain casts nothing useful).

## 4. Terrain settings

On the Terrain component:

- enable **Draw Instanced**
- raise **Pixel Error** 5 → ~15–20
- lower **Basemap Distance** 1000 → ~150

## 5. Unity 6 freebie: GPU Resident Drawer

In `PC_RPAsset` → *Rendering* → set **GPU Resident Drawer** to *Instanced Drawing*. Unity 6 batch-renders repeated low-poly meshes on the GPU with much lower CPU cost — made exactly for "big map, lots of small objects." Optionally also enable **GPU Occlusion Culling** there.

## 6. Smaller wins

- 97 **non-convex MeshColliders** — fine for static map geometry, but replace with box/capsule colliders on simple props.
- **Camera layer cull distances** (`Camera.layerCullDistances`) — cull small props at e.g. 200 m instead of the 500 m far plane.
- If streetlights/headlights are added later: keep URP *Additional Lights* per-pixel count low, never give them shadows.

---

## Running better in the editor

Editor play mode is always 30–50% slower than a build — part of the 20 FPS is editor tax.

- **Profile first:** *Window → Analysis → Profiler* → play → check whether the top cost is `FixedUpdate.PhysicsFixedUpdate` (→ section 1) or `Camera.Render` / culling (→ sections 2–5).
- **Close or hide the Scene view** while playing (it renders the whole scene a second time). Use *Maximize on Play* on the Game view.
- **Disable Gizmos in the Game view** — 14 waypoint containers draw spheres + lines per waypoint, pure editor overhead.
- **Collapse/clear the Console** — spammed log entries kill editor FPS.
- Judge real performance with an occasional **Development Build** (*File → Build Profiles*); that's the honest number.
