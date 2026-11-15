# Robot explorador en Marte (nombre de trabajo)

Juego original de supervivencia y terraformación en primera persona: un robot explorador prepara
Marte para la llegada humana.

> **Sobre el nombre:** "Terraforming Mars" es el título de un juego de mesa comercial (y de su
> versión digital). Úsalo como mucho como nombre interno de trabajo y elige otro nombre antes de
> publicar nada. El código usa el espacio de nombres neutro `MarsRobot`.

## Paso 1 (hecho): arquitectura base

| Sistema | Carpeta | Archivos |
|---|---|---|
| Núcleo | `Assets/Scripts/Core` | `GameServices.cs` (registro de servicios), `MarsTime.cs` (reloj marciano compartido) |
| Terreno | `Assets/Scripts/World/Terrain` | `TerrainManager.cs`, `TerrainChunk.cs`, `ChunkMeshBuilder.cs`, `HeightField.cs`, `TerrainMapSettings.cs` |
| Clima | `Assets/Scripts/World/Environment` | `MarsEnvironmentSystem.cs` (día/noche, tormentas, ráfagas) |
| Zonas | `Assets/Scripts/Spawns` | `LandingZoneData.cs`, `LandingZoneCatalog.cs`, `LandingZoneSpawner.cs` |
| Robot | `Assets/Scripts/Robot` | `RobotHealthSystem.cs`, `RobotModule.cs`, `RobotHealthConfig.cs`, `RobotController.cs`, `RobotRepairController.cs`, `RobotInput.cs`, `SurfaceHazard.cs` |
| Energía/polvo | `Assets/Scripts/Energy/Dust` | `EnergyInterfaces.cs`, `BatterySystem.cs`, `DustingSystem.cs` |
| UI | `Assets/Scripts/UI` | `RobotDamageHUD.cs` (fallos de HUD/minimapa por cables) |
| Editor | `Assets/Scripts/Editor` | `LandingZonePresetsCreator.cs` (menú **Marte ▸ Crear zonas de aterrizaje por defecto**) |
| Pendiente | `Assets/Scripts/Base/Rocket`, `Assets/Scripts/Agriculture` | vacías a propósito |

## Cómo montar la escena de prueba (cuando abras Unity)

**Ya está preparado:** el proyecto (Unity 6000.6.9f1, Built-in, solo módulos integrados: no
necesita internet), las 6 zonas y el catálogo en `Assets/ScriptableObjects/LandingZones`, y la
escena `Assets/Scenes/Prueba.unity` (también en Build Settings).

1. Cierra los otros Unity abiertos (falta RAM). En Unity Hub: **Add ▸ Add project from disk** →
   `Documents\JUEGO`. La primera apertura tarda (crea `Library/`).
2. Abre `Assets/Scenes/Prueba.unity` y pulsa Play. `TestSceneBootstrap` monta por código el sol, el
   clima, el terreno y el robot con cámara, y muestra un panel de depuración.
3. Controles: WASD mover · Q/E girar · ratón mirar (clic en la vista Game) · V primera/tercera persona · rueda zoom.
   Base: F usar máquina o estructura · B construir (Tab categoría, 1–9 elegir, R girar, clic colocar) ·
   L foco · Z escáner · X taladro de muestras (parado; se descarga solo en la base).
   Teclas de prueba: F1–F6 cambiar de zona · F7 +10 de cada recurso · F8 golpe · F9 reparar todo · F10 tormenta.

Si alguna vez las zonas se pierden, el menú **Marte ▸ Crear zonas de aterrizaje por defecto** las
vuelve a crear.

### Montaje manual (alternativa, para la escena definitiva)

1. Escena nueva en `Assets/Scenes/Juego.unity`:
   - `Terreno`: objeto vacío con **TerrainManager** (asigna un material; deja `initializeOnStart` desactivado).
   - `Clima`: objeto vacío con **MarsEnvironmentSystem** (asigna la luz direccional como `sunLight`).
   - `Robot`: cápsula o caja con **Rigidbody** (masa 300), **RobotHealthSystem**, **RobotController**,
     **BatterySystem**, **DustingSystem** y **RobotRepairController**. Mete la Main Camera como hija
     a ~1,2 m de altura y asígnala como `cameraPivot`.
   - `Arranque`: objeto vacío con **LandingZoneSpawner**; arrastra las referencias y elige una
     zona en `fallbackZone`.
2. Pulsa Play. Controles: WASD, ratón, R (mantener) reparar, T herramienta, G módulo, C limpiar paneles.

## Notas de memoria/rendimiento

- El campo de alturas por defecto (1025²) ocupa unos 4 MB.
- Los chunks lejanos usan mallas de 8×8; los cercanos, 64×64. Solo los chunks a menos de 1,5
  chunks del robot tienen colisionador.
- Pool con tope (`maxPooledChunks`), y la malla se vacía al volver al pool.
- Las mallas se calculan en hilos de fondo y se suben de 2 en 2 por frame (ajustable).

## Sistemas añadidos (paso 5)

| Sistema | Carpeta | Teclas / uso |
|---|---|---|
| Inventario físico (masa kg + volumen L, sublimación, corrosión) | `Assets/Scripts/Inventory` | **I** inventario · objetos de ejemplo en `Assets/Resources/Items` |
| Módulos del rover (brazo, cubierta, bahía; a escala Curiosity) | `Assets/Scripts/Robot` (`RoverConfig`, `RobotModuleSystem`, `RoverToolController`) | **X** herramienta del brazo · se cambian en el **Taller de ensamblaje** |
| Interacción por punto de mira + rocas minables | `Assets/Scripts/Interaction`, `Spawns/RockScatterSpawner` | **F** (pulsar o mantener) sobre lo que apuntas |
| Cohete nodriza: bodega, terminal, fundición, refinería Sabatier, berma | `Assets/Scripts/Rocket` | Vuelca regolito en la berma con **F** |
| Diálogos, avisos y bitácora | `Assets/Scripts/Dialogue` · ejemplos en `Assets/Resources/Dialogue` | **Intro/Espacio** avanzar · **1–4** responder · **J** bitácora |
| Efectos de cámara (inercia, sacudidas, visión dañada) | `Assets/Scripts/Camera` · shader `Assets/Shaders/DamageVision.shader` | automático |
| Clima espacial con NASA DONKI (FLR, CME, SEP, GST) | `Assets/Scripts/Environment/SpaceWeather` | **F11** lanza el siguiente evento (prueba) |
| Minimapa táctico con niebla y marcadores | `Assets/Scripts/UI/Minimap` | arriba a la izquierda; las balizas (B ▸ Ciencia) revelan más mapa |

Notas:
- La tecla de usar es **F** (la E gira el rover). La interfaz es IMGUI hecha por código (no hay paquete uGUI ni prefabs de Canvas).
- DONKI usa la clave `DEMO_KEY` (pocas peticiones por hora). Sin internet, el clima espacial se simula.
- En una build, añade `Hidden/MarsRobot/DamageVision` a *Graphics ▸ Always Included Shaders*.

## Menú principal (paso 6)

Escena de arranque: `Assets/Scenes/MenuPrincipal.unity` (escena 0 en Build Settings; si se descoloca,
menú **Marte ▸ Configurar menú principal en Build Settings**).

| Pantalla | Qué hace |
|---|---|
| Inicio | Fondo de Marte con la base, título y botones **JUGAR / OPCIONES / SALIR** |
| Modo | **Creativo** (sin daño, energía y recursos ilimitados, sin tormentas) o **Supervivencia** (Fácil / Normal / Difícil) |
| Crear mundo | Nombre, semilla, modo, dificultad y punto de aparición |
| Mapa de Marte | Mapa completo: clic en cualquier punto. Los rombos dorados son las 6 zonas con datos NASA; el resto es terreno procedural según latitud, altura y rugosidad |
| Opciones | Usa los Ajustes del juego (`Core/GameSettings`): volúmenes, brillo, sensibilidad, calidad, FPS y nombre del comandante |

Archivos: `UI/Menu/MainMenu.cs`, `UI/Menu/MenuArt.cs`, `UI/Menu/MenuAudio.cs`, `World/GlobalMap/MarsGlobalMap.cs`,
`Spawns/SpawnZoneFactory.cs`, `Spawns/GameModeRuntime.cs`, `Core/GameModeState.cs`, `Editor/MainMenuSceneSetup.cs`.

En la partida, con el ratón libre (**Esc**) aparece abajo a la derecha el botón **Menú principal**.
Imágenes opcionales: `Assets/Resources/MenuBackground.png` (fondo) y `Assets/Resources/MarsGlobalMap.png`
(mapa equirectangular real, p. ej. el mosaico MOLA coloreado de la NASA).
