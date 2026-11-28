# Don't Chicken Out! — Raymi Games

Party game 2D (Unity 6, Built-in RP) para 2–4 jugadores: pollos trepan bloques tipo Tetris mientras la cámara sube; el último en pie gana la ronda, First-to-N gana el match. Target: PC + consola, julio 2027.

## Reglas para ahorrar tokens (importante)

- **NUNCA leer el PDF del GDD** (`E:\GameDev\Raymi Games\GDD - Don't Chicken Out.docx.pdf`, 7 MB). Su contenido está condensado en `Docs/gdd.md`.
- **NUNCA explorar** `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, ni los `.csproj`/`.slnx` — es todo generado.
- Los `.meta` de Unity no aportan contexto; ignorarlos salvo que el problema sea de GUIDs/referencias.
- Las escenas (`.unity`) y prefabs son YAML enorme: buscar con Grep dirigido, no leerlos enteros.
- El estado del proyecto, backlog y sprint ya están documentados en `Docs/` — leer el doc relevante en vez de re-explorar el repo.

## Docs (leer bajo demanda, no todos)

| Doc | Qué contiene |
|---|---|
| `Docs/gdd.md` | GDD condensado: concepto, loop, mecánicas (movimiento, bloques, kick, block life) |
| `Docs/backlog.md` | Matriz de prioridades (pizarra), desglose del cuadrante 1 ordenado, tareas de la semana, responsables |
| `Docs/overview.md` | Visión, scope por milestone, stack, métricas de éxito |
| `Docs/sprintplan.md` | Plan sprint a sprint (VS→Launch) + estado de sistemas en código |
| `Docs/Roadmap.md` | Roadmap publisher-facing |
| `Docs/ADR-0001-Netcode-Online.md` | Decisión de netcode: host-authority aceptado; framework (Fusion 2 / NGO+UGS / FishNet) pendiente de lock |
| `Docs/items-implementacion.md` | Catálogo de ítems: estado por ítem, valores serializados, plan de pruebas y decisiones de diseño abiertas |
| `Docs/progression-system.md` | Sistema del listón: variables, montaje en escena, cómo tunear y dónde vive cada valor |
| `Docs/assets-arte-audio-items-progresion.md` | Lista de assets de Arte y Audio pendientes por ítem (fuente: sesión con Mateo) |
| `Docs/deuda-tecnica.md` | Cosas ya rotas o frágiles en el código, ordenadas por cuánto duelen. Leer antes de tocar muerte de jugadores, pooling o sorting layers |
| `Docs/audio-fmod.md` | Migración del audio a FMOD: arquitectura, mapa id→evento, encargos pendientes para Audio y notas de netcode |
| `Docs/steam-upload.md` | Subir builds a Steam con SteamPipe: depot/build/branch/package, setup único, pasos por versión (Tools > Don't Chicken Out > Steam), Remote Play Together |
| `Docs/lobby.md` | Lobby de Pollos: flujo Modo→Mapa→Reglas→Gallinero, controles por jugador, qué pasa al llegar al mapa, cómo añadir un mapa, pendientes |

## Estado actual (actualizar al cambiar de fase)

- **Milestone:** M3 Alpha (target sep 2026). Sprint activo aprox: A-3 (difficulty pooling + settings).
- **Foco del equipo:** cuadrante 1 del backlog — items nuevos, Lobby de Pollos, difficulty progression, controles, FMOD, Steam Remote.
- **Netcode:** **Photon Fusion 2 (host mode)** — decidido jul 2026. Ver `Docs/ADR-0001-Netcode-Online.md` y `Docs/fusion2-primer.md`.

## Mapa del código (`Assets/Scripts`, ~55 scripts)

- `Lobby/` + `Match/` — flujo previo a la partida en la escena `Lobby` (ver `Docs/lobby.md`): `LobbyFlow` (Modo → Mapa → Reglas → Gallinero), `CoopLobby` (un cuarto por jugador, input por jugador, P1 inicia), `MatchSetup` (estático: modo, mapa, rondas para ganar, jugadores + `PlayerLoadout`). Mapas = `MapDefinitionSO` + `MapCatalogSO`. La escena se genera con Tools > Don't Chicken Out > Crear escena Lobby. `Main_Level` es la plantilla base de nivel (solo escenario jugable); Tools > Limpiar nivel quita restos del lobby antiguo
- Piezas e ítems se reparten por **tercio de la cámara** (`GameplayCamera.ViewThird`): pools `SOs/Blocks Pool` (inferior/medio/superior) e `ItemsPool.asset` con pesos por tercio
- `Manager/` — `GameManager` (state machine Menu/Prepare/Game/Win + rondas; `RoundsToWin` sale del lobby), `PlayersManager` (sienta a los jugadores del lobby en su cuarto y lanza la primera cuenta atrás; un nivel abierto directo manda al Lobby — ya no hay join en la arena), `UIManager` (solo la cuenta 3-2-1-GO!; el marcador de huevos entre rondas es `UI/RoundScoreboard`, que muestra/oculta `GameManager`), `PoolingManager` (bloques por rank Winning/Neutral/Losing), `AudioManager`, `PauseManager`, `UIManager`, `DisplaySettings` (monitor/modo/resolución/frecuencia/VSync/límite FPS, en `PlayerPrefs`. Por defecto: pantalla completa exclusiva en el mejor modo del monitor, 60 Hz antes que tamaño. Resolución y frecuencia solo en exclusiva, con "¿Mantener?" que revierte en 10 s; el panel avisa si la pantalla va a < 48 Hz)
- `Player/` — `PlayerItemSlot` (hueco de un ítem manual + icono sobre la cabeza, botón `UseItem`), `PlayerMovement` (jump buffer, coyote, planeo a velocidad fija con retardo, corrección de esquinas, viaja sobre piezas empujadas; `IsOnPlayer`; consume `InputPayload`→`StatePayload` por tick, patrón network-ready en `PlayerPayloads.cs`), `PlayerInputHandler`, `PlayerBlockHandler`, `CluckSystem`, `HeadCollider`
- `Controllers/` — `PlayerController`, `PlayerAnimController` (estados `Anim_*` del rig nuevo con respaldo a los viejos; Stun sin salida, lo mantiene `StunState`), `ChickenSkin` (pollo nuevo `Personaje_Chiken.psb`: color por etiqueta de Sprite Library P1-P4 = Yellow/Blue/Green/Pink, piezas Metal, gorro solo en Stun, copia con `Anim_Die` al morir), `KickCollider`/`KickResponse`, `FeatherVFXController`. El rig vive en `Prefabs/Chicken/Chicken.prefab` (variante del PSB, montada por Arte en `Scenes/ChickenRig.unity`) bajo el hijo `Rig` de `PlayerPrefab`, que es el `visualRoot`; se monta con Tools > Don't Chicken Out > Arte nuevo > Montar pollo nuevo en PlayerPrefab
- `Camera/` — `CinemachineVerticalRig2D` (auto-rise con curva de aceleración + shake), `ScreenShake` (puerta estática: `ScreenShake.Punch(dur, amp)` desde cualquier sitio)
- `Items/` — catálogo completo implementado (ver `Docs/items-implementacion.md`, sección nov 2026: hueco de ítem, tercios, bomba/llanta/moco/metálico según el GDD; `Throwing/ExplosionBlast`, `Moco/MocoPiece`): `PlayerItemState` (base de estados con timer/tinte/cleanup; `OnTick` = tick, `OnRender` = visual), `Throwing/ThrowableItem` (lanzables), por carpeta: `SuperKick`, `Pow`, `Moco`, `HotPotato`, `Teleport`, `Anvil`, `MetalChicken`, `DoubleJump`, más `Bomb` y `Tire` (= la llanta lanzable del catálogo); `Objects/ItemCapsule` (pool con pesos + `IInstantItem` vs `HoldableItem`)
- `Objects/` — `BlockScript` (vida por pieza: 3 golpes y se rompe entera; empuje: X congelada hasta que la empujan, `PushGroup` en cadena y con lo de encima, opuestos se anulan), `BlockDamageable` (reenvía el daño a su pieza), `BlockOverlapCheck`, `PlayerKiller`, interfaces `IDamageable`/`IKickable`
- `SOs/` — config en ScriptableObjects: `PlatformerValuesSO`, `BlocksPoolSO`, `BlocksValuesSO`, `FeatherVFXConfigSO`, `MelodySO`, `SoundData`
- `SceneChange/` — transiciones, `MainMenuController`, `MenuInputRouter`, `SessionData`
- `UI/` — `ChickenPortraitStudio` (retratos en vivo del pollo nuevo para la UI, los usa `RoundScoreboard`), `UIButtonSFX`, `PlayerNameTag` (etiqueta "Player N" en el mundo, color = el del pollo), `SettingsPanel` (ajustes del menú con pestañas; cada pestaña es un `SettingsSection`: `AudioSettingsPanel`, `DisplaySettingsPanel`; `SelectionHighlight` marca la opción con foco; se monta con Tools > Don't Chicken Out > Crear panel de ajustes)
- Estilo de UI (nov 2026): tablas de madera `Sprites/NewArt/UIButton_*` (9-slice, `_0` liso / `_1` con plumas = foco) y fuente `DontChickenOutFont SDF` (sin tildes ni ñ: CalSans de respaldo). Helpers en `Editor/MenuPanelBuilderKit` (`PlankImage`, `PlankButton`, `GameFont`); la fuente se aplica a escenas, prefabs y default de TMP con Tools > Don't Chicken Out > Arte nuevo > Aplicar fuente del juego
- `Utility/` — `SpriteFlipbook` (tiras de frames sin Animator: splash del moco, poof del teleporte), `DebugOverlay` (F3 o L3+R3 en mando, también en builds: FPS, peor frame, Hz del monitor, VSync, tick), `CursorAutoHide` (esconde el cursor en pantalla completa tras 2 s quieto; solo builds), `ItemTestFilter` (solo editor: panel en Reglas del lobby para elegir qué ítems salen de las cajas)

## Stack y convenciones

- Unity **6000.3.9f1** (Unity 6 LTS), **Built-in Render Pipeline** (no hay paquete URP instalado), Input System 1.18, Cinemachine 3.1.5, DOTween, 2D Feature set.
- Física: `Rigidbody2D`/`Physics2D` (no determinista — restricción clave para netcode).
- **Simulación vs presentación:** la lógica de gameplay corre en `FixedUpdate` (tick, 50 Hz) y lee el `Rigidbody2D`; el `Transform` es la posición dibujada, interpolada entre ticks. Reposicionar un pollo solo con `PlayerController.TeleportTo` (escribir `transform.position` lo pisa la interpolación). Ver `Docs/deuda-tecnica.md` (tabla de arreglados, oct 2026).
- Docs y comunicación del equipo en español; código/identificadores en inglés.
- **El GDD tiene contradicciones conocidas y puede estar desactualizado.** Antes de asumir o proponer cualquier cambio de diseño (GDD o mecánicas), **preguntar al usuario** si es realmente lo planeado — él no lleva diseño y lo valida con los design leads.
- Commits estilo actual: cortos, descriptivos (`fix(...)`, nombre del sistema). No commitear sin que lo pidan.
