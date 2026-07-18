# Don't Chicken Out! — Raymi Games

Party game 2D (Unity 6, URP) para 2–4 jugadores: pollos trepan bloques tipo Tetris mientras la cámara sube; el último en pie gana la ronda, First-to-N gana el match. Target: PC + consola, julio 2027.

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

## Estado actual (actualizar al cambiar de fase)

- **Milestone:** M3 Alpha (target sep 2026). Sprint activo aprox: A-3 (difficulty pooling + settings).
- **Foco del equipo:** cuadrante 1 del backlog — items nuevos, Lobby de Pollos, difficulty progression, controles, FMOD, Steam Remote.
- **Netcode:** **Photon Fusion 2 (host mode)** — decidido jul 2026. Ver `Docs/ADR-0001-Netcode-Online.md` y `Docs/fusion2-primer.md`.

## Mapa del código (`Assets/Scripts`, ~55 scripts)

- `Manager/` — `GameManager` (state machine Menu/Prepare/Game/Win + rondas), `PlayersManager` (join local 2–4P teclado split + gamepad), `PoolingManager` (bloques por rank Winning/Neutral/Losing), `AudioManager`/`MusicManager`/`SoundManager`, `PauseManager`, `UIManager`
- `Player/` — `PlayerMovement` (jump buffer, coyote, glide, fall multiplier; consume `InputPayload`→`StatePayload` por tick, patrón network-ready en `PlayerPayloads.cs`), `PlayerInputHandler`, `PlayerBlockHandler`, `CluckSystem`, `HeadCollider`
- `Controllers/` — `PlayerController`, `PlayerAnimController`, `KickCollider`/`KickResponse`, `FeatherVFXController`, `CameraController`
- `Camera/` — `CinemachineVerticalRig2D` (auto-rise con curva de aceleración)
- `Items/` — `BombItem`, `SpringDisc`, `HorizontalSpawner`; `Objects/ItemCapsule`, `HoldableItem`
- `Objects/` — `BlockScript`, `BlockDamageable` (vida de bloques), `BlockOverlapCheck`, `PlayerKiller`, interfaces `IDamageable`/`IKickable`
- `SOs/` — config en ScriptableObjects: `PlatformerValuesSO`, `BlocksPoolSO`, `BlocksValuesSO`, `FeatherVFXConfigSO`, `MelodySO`, `SoundData`
- `SceneChange/` — transiciones, `MainMenuController`, `MenuInputRouter`, `SessionData`
- `UI/` — `PlayerUI`, `UIButtonSFX`

## Stack y convenciones

- Unity **6000.3.9f1** (Unity 6 LTS), URP, Input System 1.18, Cinemachine 3.1.5, DOTween, 2D Feature set.
- Física: `Rigidbody2D`/`Physics2D` (no determinista — restricción clave para netcode).
- Docs y comunicación del equipo en español; código/identificadores en inglés.
- **El GDD tiene contradicciones conocidas y puede estar desactualizado.** Antes de asumir o proponer cualquier cambio de diseño (GDD o mecánicas), **preguntar al usuario** si es realmente lo planeado — él no lleva diseño y lo valida con los design leads.
- Commits estilo actual: cortos, descriptivos (`fix(...)`, nombre del sistema). No commitear sin que lo pidan.
