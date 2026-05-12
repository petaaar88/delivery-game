# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) and other agentic tools when working with code in this repository.

## Project Overview

Arcade-style 3D third-person package delivery game built in Unity 6 (6000.3.13f1) using the Universal Render Pipeline (URP). Think Crazy Taxi, but for delivering postal packages — fast, fun, chaotic driving with delivery objectives. All custom game scripts go under `Assets/` (no subdirectory exists yet — create one, e.g. `Assets/Scripts/`).

## Unity Setup

- **Unity version:** 6000.3.13f1
- **Render pipeline:** URP (`com.unity.render-pipelines.universal` 17.3.0)
- **Input system:** New Input System (`com.unity.inputsystem` 1.19.0) — do **not** use the legacy `Input` class
- **Scene:** `Assets/Scenes/TestScene.unity`
- **MCP integration:** `com.coplaydev.unity-mcp` is installed for Unity Editor automation via Claude Code

## Building & Running

This is a Unity project with no CLI build scripts. Open and run via Unity Editor. The Unity Test Framework (`com.unity.test-framework` 1.6.0) is available for edit-mode and play-mode tests.

## Realistic Car Controller V4 (RCC)

The vehicle system is the **Realistic Car Controller V4** asset, located at `Assets/3rd Party - RealisticCarControllerV4/`. Do not modify files in this folder.

### Key RCC classes

| Class | Role |
|---|---|
| `RCC_CarControllerV4` | Main vehicle MonoBehaviour (requires `Rigidbody`). Attach to the car root. |
| `RCC_Core` | Base class for all RCC components; exposes `RCC_Settings` and `RCC_GroundMaterials` singletons. |
| `RCC_InputManager` | Singleton that gathers player input and feeds it to the active car. |
| `RCC_Inputs` | Struct holding throttle, brake, steer, handbrake, etc. values. |
| `RCC_Camera` | Follow camera with multiple modes (TPS, hood, cinematic, fixed, wheel). |
| `RCC_Settings` | ScriptableObject singleton at `Assets/3rd Party - RealisticCarControllerV4/Resources/`. Controls global physics behavior. |
| `RCC_AICarController` | AI driver that follows `RCC_Waypoint` nodes inside an `RCC_AIWaypointsContainer`. |

### Controlling the car from game code

To drive the car programmatically (e.g. from a mission script), set `canControl = false` on `RCC_CarControllerV4` and call `carController.overrideInputs = true`, then supply values via `carController.OverrideInputs(throttle, brake, steer, handbrake, clutch, nosBoost)`. To restore player control, set both flags back.

### Input

RCC uses the **New Input System**. Input bindings live in `RCC_InputActions` (auto-generated). Do not create additional `InputActionAsset` files for vehicle input — extend or react to `RCC_InputManager` events instead.
