# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) and other agentic tools when working with code in this repository.

## Project Overview

Arcade-style 3D third-person package delivery game built in Unity 6 (6000.3.x) using the Universal Render Pipeline (URP). Think Crazy Taxi, but for delivering postal packages — fast, fun, chaotic driving with delivery objectives. Custom game scripts live under `Assets/Scripts/` (`Audio/`, `Game/`, `Navigation/`, `Package/`, `UI/`, `Vehicles/`, `World/`).

## Unity Setup

- **Unity version:** 6000.3.x (project last opened with 6000.3.10f1)
- **Render pipeline:** URP (`com.unity.render-pipelines.universal` 17.3.0)
- **Input system:** New Input System (`com.unity.inputsystem` 1.19.0) — do **not** use the legacy `Input` class
- **Scenes:** `Assets/Scenes/MainMenu.unity` (build index 0) → `Assets/Scenes/GameScene.unity` (main gameplay, build index 1). Other scenes (`MapV1Scene`, `BlockoutMapScene`, `PickupMechanicScene`, `NavigationMechanicsScene`) are dev/test scenes.
- **MCP integration:** `com.coplaydev.unity-mcp` is installed for Unity Editor automation via Claude Code. Note: the Unity Editor window must be focused/foreground for play mode and frame-dependent captures to run; modal dialogs (e.g. RCC platform warnings) block the whole editor loop.

## Building & Running

This is a Unity project with no CLI build scripts. Open and run via Unity Editor. The Unity Test Framework (`com.unity.test-framework` 1.6.0) is available for edit-mode and play-mode tests. An Android build profile exists at `Assets/Settings/Build Profiles/Custom Android.asset`.

## UI (UI Toolkit)

All game UI is UI Toolkit (no uGUI for game screens; the RCC uGUI canvas in GameScene is intentionally disabled).

| Piece | Location |
|---|---|
| UXML layouts | `Assets/UI/Uxml/` — `Hud.uxml` (HUD + pause overlay + mobile touch controls), `MainMenu.uxml` |
| Stylesheet/theme | `Assets/UI/Uss/Theme.uss` — palette vars on `.theme` class, component classes (`.btn`, `.pill`, `.menu-card`…) |
| PanelSettings | `Assets/UI/Settings/GamePanelSettings.asset` — ScaleWithScreenSize 1920×1080, match=1 (height) |
| Fonts | `Assets/UI/Fonts/` — Baloo 2 (display/buttons), Nunito (body); OFL licensed |
| Controllers | `Assets/Scripts/UI/` — `HudController`, `PauseMenuController`, `MobileControlsController`, `SafeAreaPadding`, `MainMenuController` |
| Game state | `Assets/Scripts/Game/GameSession.cs` — coins/deliveries/delivery timer; subscribes to `DeliveryManager` static events |

Key conventions:
- `GameUI` GameObject in GameScene holds the UIDocument + HUD/pause/mobile/safe-area controllers; `MenuUI` in MainMenu holds the menu.
- Mobile touch controls write into `RCC_MobileButtons.mobileInputs` (static) each frame; `MobileControlsController` enables `RCC_Settings.Instance.mobileControllerEnabled` at runtime on mobile (`Mode.Auto`) and restores the previous value on disable in the editor — a leftover `true` triggers RCC's blocking platform-warning dialog on play start (suppressed via `RCC_IgnorePlatformWarnings` EditorPrefs).
- UI text is English; earnings are displayed as coins.
- New USS should reuse the `--vars` defined on `.theme` and the existing component classes.

## Realistic Car Controller V4 (RCC)

The vehicle system is the **Realistic Car Controller V4** asset, located at `Assets/RealisticCarControllerV4/`. Do not modify files in this folder.

### Key RCC classes

| Class | Role |
|---|---|
| `RCC_CarControllerV4` | Main vehicle MonoBehaviour (requires `Rigidbody`). Attach to the car root. |
| `RCC_Core` | Base class for all RCC components; exposes `RCC_Settings` and `RCC_GroundMaterials` singletons. |
| `RCC_InputManager` | Singleton that gathers player input and feeds it to the active car. Reads `RCC_MobileButtons.mobileInputs` instead of the InputActions asset when `RCC_Settings.mobileControllerEnabled` is true. |
| `RCC_Inputs` | Struct holding throttle, brake, steer, handbrake, etc. values. |
| `RCC_Camera` | Follow camera with multiple modes (TPS, hood, cinematic, fixed, wheel). |
| `RCC_Settings` | ScriptableObject singleton at `Assets/RealisticCarControllerV4/Resources/RCC Assets/`. Controls global physics behavior. |
| `RCC_AICarController` | AI driver that follows `RCC_Waypoint` nodes inside an `RCC_AIWaypointsContainer`. |
| `RCC_SceneManager` | Scene singleton; `activePlayerVehicle` is the player's car (`speed` is km/h). |

### Controlling the car from game code

To drive the car programmatically (e.g. from a mission script), set `canControl = false` on `RCC_CarControllerV4` and call `carController.overrideInputs = true`, then supply values via `carController.OverrideInputs(throttle, brake, steer, handbrake, clutch, nosBoost)`. To restore player control, set both flags back.

### Input

RCC uses the **New Input System**. Input bindings live in `RCC_InputActions` (auto-generated). Do not create additional `InputActionAsset` files for vehicle input — extend or react to `RCC_InputManager` events instead. Mobile/touch input goes through `RCC_MobileButtons.mobileInputs` (see UI section).

### RCC demo UI

`RCC_Canvas` (uGUI demo dashboard) exists in GameScene but is **disabled** — the UI Toolkit HUD replaces it. Don't re-enable it; if something needs telemetry, read it from `RCC_SceneManager.Instance.activePlayerVehicle`.

## Known issues

- The player van's spawn spot in GameScene is wedged between scenery (garage/bench); it can fail to move from a standstill there.
