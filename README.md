# Terraforming Mars

> **Working title.** "Terraforming Mars" is the name of an existing commercial board game (and its digital edition), so it is only used as an internal working name. Pick a final title before publishing anything. In code, the root namespace is the neutral `MarsRobot`.

A first-person survival and terraforming game for Unity. You control the explorer rover **XR-1** on Mars and prepare a landing zone for the first human crew. Your home base is a 52.5 m mothership that landed with you.

This is **version 1**: a playable prototype. Every model, icon and UI element is generated in code, so the project needs no external art assets.

---

> Download in the releases.

## Table of contents

1. [Requirements](#requirements)
2. [Getting started](#getting-started)
3. [Gameplay overview](#gameplay-overview)
4. [Controls](#controls)
5. [Systems](#systems)
6. [Project structure](#project-structure)
7. [Real Mars data (NASA)](#real-mars-data-nasa)
8. [Performance notes](#performance-notes)
9. [Known limitations](#known-limitations)
10. [Roadmap](#roadmap)
11. [Credits and disclaimers](#credits-and-disclaimers)

---

## Requirements

| Item | Version / note |
|---|---|
| Unity | **6000.6.9f1** (Unity 6), Built-in Render Pipeline |
| Packages | Built-in modules only (no internet needed to open the project) |
| Input | Legacy Input Manager (`Active Input Handling` = *Input Manager* or *Both*) |
| OS | Developed on Windows 11 |
| RAM | 8 GB minimum; close other Unity instances (the editor needs about 1 GB just to start) |
| Optional | Python 3.12 for the map processor in `Tools/MapProcessor` |

## Getting started

1. In **Unity Hub**, choose **Add ▸ Add project from disk** and select this folder.
2. Open the scene **`Assets/Scenes/MenuPrincipal.unity`** (main menu, build scene 0). To skip the menu, open `Assets/Scenes/Prueba.unity` directly.
3. Press **Play** and click inside the **Game** view so it captures the keyboard and mouse.

### Main menu

`UI/Menu/MainMenu` draws the start screen in IMGUI (Mars landscape, orange title, steel **JUGAR / OPCIONES / SALIR** buttons). Flow:

1. **JUGAR** ▸ choose **Creativo** (no damage, unlimited energy and resources, no storms) or **Supervivencia** (normal game with Easy / Normal / Hard).
2. **Crear mundo**: name, seed, mode, difficulty.
3. **Punto de aparición**: a full map of Mars (`World/GlobalMap/MarsGlobalMap`, generated on a background thread with real feature coordinates). Click anywhere; gold diamonds are the 6 preset zones with real NASA data.
4. `Spawns/SpawnZoneFactory` turns the point into a runtime copy of a `LandingZoneData` (the preset zone, or a procedural zone whose climate, ice, light and relief come from latitude and elevation) and loads `Prueba`. `Spawns/GameModeRuntime` applies the mode in-game; with the mouse released (**Esc**), a **Menú principal** button appears bottom-right.

Optional art: put `Assets/Resources/MenuBackground.png` (menu background) or `Assets/Resources/MarsGlobalMap.png` (equirectangular Mars map, e.g. NASA's colorized MOLA mosaic) to replace the procedural images.

`TestSceneBootstrap` builds the whole scene at runtime: sun, weather, terrain, rover, mothership base, MarteCar, rocks, minimap and HUD. The scene file contains only one object.

The 6 landing zones and their catalog are already in `Assets/ScriptableObjects/LandingZones`. If they get lost, recreate them with **Marte ▸ Crear zonas de aterrizaje por defecto**.

## Gameplay overview

1. **Landing.** The rover appears on the charging pad in front of the mothership, and the ship's AI briefs you.
2. **Explore and collect.** Scan the ground (**Z**), mine rocks (**hold F**) or drill the soil (**hold X**). Mind your cargo's mass and volume.
3. **Process.** Unload at the ship, then smelt ores, print bricks and refine propellant.
4. **Build.** Place solar panels, batteries, an oxygen extractor, ice drills, tanks, a lab, an antenna, beacons, a radiation shelter and a landing pad (**B**).
5. **Survive.** Manage dust, batteries, damaged modules, dust storms and space weather (flares, CMEs, solar particles, magnetic storms).
6. **Goal.** Complete the crew-readiness checklist: oxygen, water, stable power, shelter, landing pad and antenna.

The rover has **no health bar**. Damage is tracked **per module** (wheels, cables, chassis, solar panel). Damaged wheels reduce mobility and pull the rover to one side; cut cables glitch the HUD, minimap and sensors. Nothing heals by itself: you repair by hand with the right tool, or for free in the mothership's assembly bay.

## Controls

The full list is also in-game: **I ▸ Ajustes ▸ Controles**.

| Context | Keys |
|---|---|
| Movement | **W/S** forward/back · **A/D** strafe left/right (wheels turn 90°) · **Q/E** turn · **Mouse** look · **V** 1st/3rd person · **Wheel** zoom · **Esc** release mouse |
| Actions | **F** use / interact (hold to mine, build berm, download logs) · **hold X** front-arm tool (rover stopped) · **Z** scanner · **L** headlight · **C** clean panels · **hold R** manual repair · **T** repair tool · **G** module to repair |
| Panels | **I** management panel (inventories, status, settings) · **J** logbook · **K** inspect mothership · **B** build mode |
| Build mode | **Tab** category · **1–9** structure · **R** rotate · **Click / Enter** place · **B / Esc** exit |
| MarteCar | **F** (aiming at the car) get in · **W/S** throttle · **A/D** steer · **F** get out |
| Ship inspector | **LMB** rotate · **RMB** pan · **Wheel** zoom · **WASD/Q/E** move · **1–5** level · **Shift+1–5** isolate level · **0** all · **C** cutaway · **L** labels · **M** dimensions · **K/Esc** exit |
| Dialogue | **Enter / Space** continue · **1–4** choose an answer |
| Astronaut | **P** switch astronaut ↔ robot (robot must be in the hangar) · **WASD** walk · **Shift** run · **Space** jump · **F** use machine / lockers / lift · **I** personal inventory and hand crafting · **V** 1st/3rd person |
| Crafting window | **Click** select · **Right-click / Shift+click** transfer stack · **Drag** move between inventories · **Double-click** a blueprint to craft · **Esc / F / I** close |
| Elevator (robot) | **F** on the platform: up to the hangar / down to the ground · **F** at the hangar door or at the foot of the rails: call it |
| Debug | **F1–F6** landing zone · **F7** +10 of each resource · **F8** heavy hit · **F9** repair all · **F10** dust storm · **F11** next space-weather event · **H** hide debug panel |

## Systems

| System | Folder | Highlights |
|---|---|---|
| Terrain streaming | `World/Terrain` | Static height map, chunk grid with load/unload hysteresis, 4 LOD levels, chunk pool, meshes built on background threads, colliders only near the rover, procedural Mars ground material |
| Real terrain data | `World/Terrain` + `Tools/MapProcessor` | Optional preprocessed NASA chunks (elevation and minerals) loaded from `StreamingAssets/MarsMap` |
| Weather | `World/Environment` | Day/night sol cycle, dust storms, cleaning gusts, temperature |
| Space weather | `Environment/SpaceWeather` | NASA **DONKI** API (FLR, CME, SEP, GST) with offline simulation; flares cause interference, CMEs trigger a global storm, SEPs degrade panels, GSTs cause power and motor glitches |
| Rover health | `Robot` | Per-module integrity, impact and terrain wear, steering pull, events for UI |
| Rover control | `Robot` | Rigidbody first-person controller, 90° crab steering, camera rig (1st/3rd person), movement modifiers |
| Rover modules | `Robot` | Curiosity-class scale (about 3 × 2.7 × 2.2 m, 899 kg). Slots: front arm (drill / shovel / dust blower), top deck (antenna / extra solar panels), internal bay ×3 (storage, aux battery, cryo, sealed, dual-arm adapter) |
| Rover model | `Robot/Visual` | Detailed procedural rover (rocker-bogie, mast, arm, RTG); animated wheels, steering, mast and arm |
| Energy and dust | `Energy/Dust` | Battery, dust on panels, solar charging |
| Inventory | `Inventory` | Items with real mass (kg) and volume (L), dual limits, overload effects, volatile sublimation, perchlorate corrosion, storage containers, drop and pick up |
| Interaction | `Interaction` | Crosshair raycast, hold interactions, mineable rocks driven by CRISM-style mineral data |
| Mothership | `Rocket`, `Rocket/Ship` | 52.5 × Ø 9 m ship with 5 interior levels, tanks and engines; inspector with cutaway, labels and dimensions; cargo hold, terminal, smelter, ISRU Sabatier refinery, radiation berm |
| Base building | `Base`, `Base/Building`, `Base/Stations` | Warehouse, power grid (no wires), 17 structures, research, crew-readiness objectives, fabricator, assembly bay with upgrades |
| Dialogue | `Dialogue` | ScriptableObject dialogue nodes, typewriter effect, choices, events; HUD notifications; logbook; discovery points |
| Camera effects | `Camera` | Head bob and chassis inertia, Perlin camera shake, damaged-vision shader (chromatic aberration, scanlines, VHS) |
| Minimap | `UI/Minimap` | Tactical top-left minimap, manual-render orthographic camera, fog of war, markers, space-weather alerts |
| Management UI | `Inventory`, `UI`, `UI/Theme` | Tabbed panel: overview, ship inventory, robot inventory, robot status, settings and controls; procedural item icons; live 3D rover preview |
| Settings | `Core` | Quality, volume, brightness, mouse sensitivity, language (Spanish only for now), FPS counter; saved with PlayerPrefs |
| Vehicles | `Vehicles` | MarteCar: separate drivable vehicle with lateral grip, parking brake and chase camera |

## Project structure

```
Assets/
  Resources/            Example ItemData and DialogueNode assets (loaded at runtime)
  Scenes/Prueba.unity   Test scene (one bootstrap object)
  ScriptableObjects/    Landing zones and catalog
  Scripts/
    Core/               Services, Mars time, settings
    World/              Terrain streaming, weather
    Environment/        Space weather (DONKI)
    Spawns/             Landing, rocks, test-scene bootstrap
    Robot/              Health, controller, modules, tools, camera rig, Visual/
    Energy/Dust/        Battery and dust
    Inventory/          Items, containers, management panel
    Interaction/        Raycast interaction and resource nodes
    Rocket/  Rocket/Ship/  Mothership systems and 3D model
    Base/               Warehouse, stations, building, research, objectives
    Dialogue/           Dialogue, notifications, logbook
    Camera/             Camera effects
    UI/                 Minimap, theme, icons, damage HUD
    Vehicles/           MarteCar
    Editor/             Editor menu tools
  Shaders/DamageVision.shader
Tools/MapProcessor/     Python script that prepares real Mars terrain chunks
LEEME.md                Spanish notes
```

Code conventions: comments and in-game text are in Spanish, identifiers are in English, and namespaces live under `MarsRobot.*`. There are no external dependencies.

## Real Mars data (NASA)

- **Terrain and minerals:** `Tools/MapProcessor/generate_mars_chunks.py` preprocesses elevation (MOLA / HiRISE / CTX) and mineral data (CRISM) **offline** into `Assets/StreamingAssets/MarsMap/<zone>/`. Zones without data use a procedural terrain. See `Tools/MapProcessor/README.md`. Close Unity before running it.
- **Space weather:** the game queries the public **NASA DONKI** API at runtime with `DEMO_KEY`, which allows only a few requests per hour. You can get a free key at api.nasa.gov. Only dates and the key are sent, never player data. Without internet, events are simulated.
- **Landing zones:** Jezero (medium), Gale (moderate-high), Elysium Planitia (easy), Utopia Planitia (high, with ice), Valles Marineris (extreme), Acidalia Planitia (low, with storms). Coordinates are approximate.

## Performance notes

The project is designed for machines with little free memory:
- Terrain chunks are pooled; mesh data is reused; colliders exist only within about 1.5 chunks of the rover.
- The height field takes about 4 MB; mesh building is spread across frames.
- Static parts of the rover and the ship are merged into a few meshes per material.
- The minimap camera renders a 256² texture every 4 frames; the rover preview renders only while the panel is open.
- Space-weather requests run asynchronously with a timeout.

## Known limitations

- The UI uses IMGUI drawn in code; there are no Canvas prefabs, because the uGUI package is not installed.
- Models and icons are procedural placeholders, ready to be swapped for real art.
- The astronaut walks only inside the ship (6 levels, lift between them); outside, you play as the rover. Crafting stations have no power or fuel requirement yet.
- The ship interior is a fictional conversion made for the game; it is not based on real ship plans.
- In-game, CMEs trigger global dust storms. That is a gameplay liberty: real Martian dust storms are not caused by CMEs.
- There is no music yet, and only Spanish is available.
- `DamageVision.shader` must be added to **Graphics ▸ Always Included Shaders** for builds.
- v1 has been compiled against Unity 6 assemblies, but the latest systems have not been fully play-tested.

## Roadmap

- Play-test pass and balancing (water in zones without ice, energy budget, progression).
- Ship rooms built progressively from collected materials (start with an empty ship).
- Greenhouse and agriculture.
- Exterior modules (habitats, medical bay, drone hangar), trailer and scout drone.
- Real art: rover, ship, structures and item icons; Canvas-based UI; music and English localisation.
- Save and load.

## Credits and disclaimers

- Game design by the project owner. Code generated with AI assistance.
- Mars science references: NASA (MOXIE, RAD, CRISM, DONKI, Mars Trek, HiRISE). Values are simplified for gameplay.
- The mothership is inspired by large reusable stainless-steel spacecraft. No third-party names, logos or trademarks are used in the game.
- Not affiliated with NASA, SpaceX or the publishers of any game mentioned in this file.

### Garden and new stations

Levels 3–6 were redesigned (machines against the wall, open centre; **Marte ▸ Comprobar espacio de la nave** reports walkable area per level). New crafting stations: 3D printer, electronics bench, two hydroponic gardens (3 trays each, growing in parallel with visible plant growth), laboratory, water plant, oxygen plant and kitchen. Crops: red lettuce, radish, dwarf tomato, potato, dwarf wheat, soybean (seeds, harvests, nutrients, flour, rations, salad). Details in LEEME.md.

### Visual system (Built-in pipeline)

Scene hierarchy `_Environment` / `_Structures` / `_Props` / `_PhysicsSystem` (built at runtime by `Visuals/SceneRoots`; **Marte ▸ Arquitectura de escena** creates it in the editor) · organic set dressing (±15° yaw, ±8 % scale, mm offsets, seeded) · mesh-fitted colliders (static: collider only; loose props: Rigidbody with mass from volume × density) · procedural PBR texture sets and a PBR material factory for the Standard shader (**Marte ▸ Materiales PBR**; maps: Albedo, Normal, MetallicSmoothness or Metallic + Roughness, AO) · Mars sky cubemap and realtime reflection probes. Details in LEEME.md.

### Optional NASA 3D models

The game can use 3D models from [NASA 3D Resources](https://science.nasa.gov/3d-resources/) (free to use under the [NASA Images and Media Usage Guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/); NASA logos and insignia must not suggest NASA endorses the game). The `.glb` files (about 35 MB) live in `Assets/Models/NASA/<Id>/` and the generated prefabs in `Assets/Resources/NASAModels/`; if those prefabs are removed, the procedural models are used again. The Curiosity file is a single static mesh, so with it the rover's wheels, mast and arm are not animated (the procedural rig is still built, hidden, for the camera and tools).

| Id | Model | Credit | Source | Used for |
|---|---|---|---|---|
| `CuriosityRover` | Curiosity Rover, 3D Model | NASA/JPL-Caltech | https://science.nasa.gov/resource/curiosity-rover-3d-model/ | Look of the XR-1 rover |
| `InSightLander` | InSight Lander, 3D Model | NASA/JPL-Caltech | https://science.nasa.gov/resource/insight-lander-3d-model/ | "Módulo de aterrizaje abandonado" discovery point |
| `Ingenuity` | Mars Ingenuity Helicopter, 3D Model | NASA/JPL-Caltech | https://science.nasa.gov/resource/mars-ingenuity-helicopter-3d-model/ | "Restos de una sonda antigua" discovery point |

Setup: the project uses the **glTFast** package (`com.unity.cloud.gltfast`) to import `.glb` files. Put each downloaded "glTF File (3D Model)" in `Assets/Models/NASA/<Id>/`, then run **Marte ▸ Configurar modelos NASA**. It scales each model to real size, puts it on the ground facing +Z and saves `Assets/Resources/NASAModels/<Id>.prefab` (`Visuals/NasaModelLibrary` loads it). **Marte ▸ Vista previa modelos NASA** renders front/side/top PNGs to `Logs/NasaPreview/`. The NASA models are visual only: rover physics, colliders, camera mount and arm stay the same.
