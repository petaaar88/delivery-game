# NPC Ambient Traffic — Setup Guide (RCC V4)

**Goal:** ~5–10 AI vehicles driving endless road loops in `MapV1Scene`, ignoring the player.
**Two phases:** (A) turn each FBX into an AI vehicle prefab, (B) build the world (NavMesh + waypoints) and place them.

---

## Phase A — Make one NPC vehicle (do this once, then repeat)

Start with **one** model (e.g. `Blue Car`) and get it fully working before doing the rest.

### A1. Check the model first
- Drag `Assets/Models/NPC Vehicles/Cars/Blue Car.fbx` into the scene.
- Set the Scene view gizmo to **Pivot** + **Local** (top toolbar).
- Select the root, confirm axes: **X = Right, Y = Up, Z = Forward**, and the car is roughly real-world sized (compare to `MainVan`). Wrong axes must be fixed in the modeling tool; a wrong pivot the wizard can fix.

### A2. Add the RCC main controller
- With the vehicle root selected: **Tools → BoneCracker Games → Realistic Car Controller → Quick Vehicle Setup Wizard**.
- If it asks to fix the pivot → **Yes**.
- This adds `RCC_CarControllerV4` + a `Rigidbody` (mass auto ~1250–1500). For **Bus / Fire Truck** later, raise mass and wheel spring/damper.

### A3. Create wheel colliders
- In the `RCC_CarControllerV4` inspector open the **Wheel** tab.
- Select the 4 wheel mesh objects in the hierarchy → click **Create Wheel Colliders**.
- Verify each collider's radius/position matches its wheel in the Scene view.

### A4. Body collider + Center of Mass
- Make sure a **Box Collider** (or Mesh Collider) covers the body. The inspector's **Overall Check** panel flags anything missing (stray rigidbodies, missing colliders).
- Move the **COM** child low and roughly centered.
- Skip lights/exhaust/engine sound — not needed for NPCs (better performance).

### A5. Add the AI controller
- Vehicle still selected: **Tools → BoneCracker Games → Realistic Car Controller → AI → Add AI Controller To Vehicle**.
- This adds `RCC_AICarController` + a `NavMeshAgent`.
- In the `RCC_AICarController` inspector set:
  - **Navigation Mode** = `Follow Waypoints`
  - **Stop After Lap** = **off** (loops forever)
  - **Use Raycasts** = on, **Smoothed Steer** = on
  - (optional) **Limit Speed** = on + a calm **Maximum Speed** for ambient traffic
  - **Waypoints Container** — leave empty for now, you assign it in Phase B.

### A6. Save as prefab
- Drag the finished vehicle into `Assets/Prefabs/NPCVehicles/` (create the folder).
- Delete the instance from the scene.
- ✅ First NPC done. **Repeat A1–A6** for each other model you want (aim for 3–5 distinct ones to start).

> Tip: `Assets/Prefabs/MainVan.prefab` is already a fully-configured RCC vehicle — open it as a reference for what a "done" vehicle looks like.

---

## Phase B — Build the traffic world in MapV1Scene

### B1. Bake a NavMesh over the roads *(Unity 6 way — not the old PDF way)*
Your project has the **AI Navigation** package (`com.unity.ai.navigation 2.0.12`), so use the component, **not** the legacy Navigation window in the RCC PDF.
- Create an empty GameObject named `NavMesh`.
- **Add Component → Nav Mesh Surface**.
- Make sure roads/ground are included: set **Collect Objects = All** (or use **Include Layers** limited to your road/ground layer). For fine control, add a **Nav Mesh Modifier** to road objects.
- Click **Bake**. Confirm the blue mesh covers **all drivable roads** with no gaps.

### B2. Add a waypoints container
- **Tools → BoneCracker Games → Realistic Car Controller → AI → Add Waypoints Container To Scene** (adds `RCC_AIWaypointsContainer`).

### B3. Lay the loop
- Select the container in the Hierarchy.
- **Hold Shift + Left-click on the road** to drop waypoints **in order**, following a lane, all the way around back to the start = a closed loop.
- Set each waypoint's **Target Speed** (lower on corners).
- ⚠️ **Never `Ctrl+D`** to duplicate a waypoint — it breaks them.
- Want multiple routes? Add more containers and repeat.

### B4. Place the NPC vehicles
- Drag ~5–10 of your Phase-A prefabs into the scene, spaced out **along the loop**, each sitting **on the NavMesh** (close to it, or pathfinding fails).
- On each vehicle's `RCC_AICarController`: set **Waypoints Container** = the container from B2.
- Give each a different **Current Waypoint Index** so they don't all start stacked.

### B5. (Optional) Brake zones
- For tight intersections: **… → AI → Add BrakeZones Container To Scene**, then place zones where AI should slow. See the **BrakeZones** section below.

---

## Verify

1. Open `MapV1Scene`, press **Play**.
2. Toggle the Navigation overlay — blue mesh should cover the roads.
3. Watch cars follow the waypoint loop (gizmo lines), slow for corners, not fall through the ground or stall.
4. They should loop forever and steer around each other via raycasts.
5. They ignore the player; your driving feels unchanged.

---

## Common gotchas
- **Car falls through the map** → missing body collider, or a stray Rigidbody on a child (see Overall Check).
- **Car won't move / spins** → wheel colliders wrong radius/position, or COM too high.
- **AI just sits there** → not on the NavMesh, no waypoints container assigned, or NavMesh didn't bake over that spot.
- **Bus/Fire Truck unstable** → bump mass + wheel spring/damper (they're heavy).

---

# BrakeZones

A **BrakeZone** is a box-shaped volume you drop in the world that tells any nearby AI vehicle
*"slow down to X km/h here."* It's for spots where cars would otherwise take a corner or
junction too fast — a sharp turn, a tight intersection, a downhill.

**How it works (from the code):**
- `RCC_AIBrakeZone` has two fields:
  - **Target Speed** (`targetSpeed`, default `50`) — the km/h the AI slows to inside the zone.
  - **Distance** (`distance`, default `100`) — the range in meters at which cars *start* braking toward that target speed.
- Each zone is a GameObject with a **trigger BoxCollider** + the `RCC_AIBrakeZone` component. The `RCC_AIBrakeZonesContainer` holds them all and draws the red semi-transparent gizmo cubes.
- Every AI within its `detectorRadius` (~200 m) picks the **closest** brake zone. Then: *if the car is within `distance` meters of it **and** currently faster than `targetSpeed` → it brakes* until it's at or below that speed (`RCC_AICarController.cs:494`).
- At runtime the container auto-moves all zones to the **Ignore Raycast** layer so they don't confuse the AI's obstacle-avoidance raycasts.

**BrakeZone vs. waypoint Target Speed** — both cap speed, but:
- *Waypoint target speed* is per-waypoint, tied to your route. You'd edit many waypoints to slow one corner.
- *BrakeZone* is a shared, route-independent hazard. Place one at a nasty corner and **every** AI vehicle on **any** route slows there — no per-waypoint editing. That's the reason to use them.

For a simple ambient loop, per-waypoint target speed is usually enough, so BrakeZones are **optional**. Add them only where cars visibly overshoot.

## BrakeZone setup (Editor, do-it-yourself)

1. **Add the container:** `Tools → BoneCracker Games → Realistic Car Controller → AI → Add BrakeZones Container To Scene`. This adds an `RCC_AIBrakeZonesContainer` to the scene.
2. **Select the container** in the Hierarchy.
3. **Place zones:** just like waypoints — **hold Shift + Left-click on the road** at the hazard. Each click spawns a zone GameObject with a trigger `BoxCollider` (size 1×1×1) + `RCC_AIBrakeZone`. Put one right before the corner/junction.
4. **Size the box:** with the zone selected, scale its `BoxCollider` **Size** to roughly cover the slowdown area (the red gizmo cube shows it). It only needs to be near the road, not perfectly aligned — the effect is range-based, not collision-based.
5. **Tune each zone** in the `RCC_AIBrakeZone` inspector:
   - **Target Speed** = how slow (e.g. `30` for a sharp corner, `50` for a gentle bend).
   - **Distance** = how far out braking begins (e.g. `40–60` m so it slows *before* the corner, not in it).
6. Done — no wiring to the vehicles. AI cars detect zones automatically at play time (they must be within ~200 m and the zone active in the hierarchy).

**Quick check:** enter Play mode, watch a car approach the corner — it should ease down to the
target speed as it enters the red cube's range, then accelerate away after. If it doesn't slow:
the zone is too far from the car's path, `distance` is too small, or the car was already slower
than `targetSpeed`.
