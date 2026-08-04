# Arquitectura objetivo — Don't Chicken Out! con Fusion 2

**Proyecto:** Don't Chicken Out! · Raymi Games
**Fecha:** 20 de julio de 2026
**Estado:** dirección técnica — insumo para B-1→B-4 y para la reunión con Photon
**Relacionados:** [ADR-0001-Netcode-Online.md](ADR-0001-Netcode-Online.md) · [fusion2-primer.md](fusion2-primer.md) · [fusion2-puntos-clave.html](fusion2-puntos-clave.html) · [sprintplan.md](sprintplan.md)

> **Premisa (decidida jul 2026):** el equipo está dispuesto a **reconstruir el core** si eso lleva a un mejor resultado a largo plazo. Este documento deja de ser una auditoría de "qué está mal" y pasa a ser la **arquitectura objetivo** y el orden para llegar a ella. La auditoría del código sigue presente (§6), pero como **estado de partida del que migramos**, no como diagnóstico.
>
> La versión visual de esto es [fusion2-puntos-clave.html](fusion2-puntos-clave.html). Este `.md` es el registro técnico completo.
>
> Marcas: ✅ verificado en fuente oficial · ⚠️ no confirmado, no asumir · 📊 estimación propia.

---

## 1. Resumen ejecutivo

Fusion 2 sigue siendo la elección correcta y el modelo host-authority del `ADR-0001` se mantiene. Lo que cambia es el **alcance**.

Hoy tenemos un juego local al que habría que añadirle red. La decisión de reconstruir permite apuntar a algo mejor: **una sola simulación donde offline, couch y online son el mismo código** — cambia cuántos peers hay conectados, nada más. Local pasa a ser "el host sin nadie conectado".

Esto no es rehacer el juego. Todo lo que define cómo se siente y cómo se ve se conserva (§2). Se reconstruye la capa que decide **cuándo** pasan las cosas y **quién** tiene autoridad. Y habilita una decisión que con el core cerrado no estaba disponible: **terminar de sacar al pollo de la física de Unity** (§3), que ataca de raíz el que era el riesgo #1 de la integración.

### Por qué reconstruir y no parchear

El `ADR-0001` §2 cuenta la arquitectura "network-ready" como **ventaja de cronograma**. La auditoría (§6) muestra que ese patrón está *nombrado en comentarios, no implementado*: `StatePayload` nunca se lee, los payloads no tienen tick, la simulación corre en `Update()` con `Time.deltaTime` variable. Es decir: la base sobre la que se planificó B-1→B-4 no existe. Ante eso, parchear cuesta casi lo mismo que reconstruir bien, y deja deuda; reconstruir una vez deja una simulación única que sirve cuatro años.

### Las cinco decisiones de esta reconstrucción

| # | Decisión | Sección |
|---|---|---|
| 1 | Una sola simulación en tick fijo; offline/couch/online = el mismo código | §4, §5 |
| 2 | El pollo pasa a **character controller cinemático** (sale de la física dinámica) | §3 |
| 3 | Identidad de jugador sobre `(PlayerRef, localIndex)` desde el día 1 | §4 |
| 4 | Autoridad al host; la UI solo **proyecta** estado, no decide | §5 |
| 5 | Arbitraje explícito de kicks, stomps y muertes | §5, §11 |

---

## 2. Alcance — qué se conserva, qué se reconstruye, qué es nuevo

"Descartar el core" no es descartar el juego.

**Se conserva (el juego):**
- Valores de feel: jump buffer, coyote time, glide, fall multiplier — ya viven en `ScriptableObjects` (`PlatformerValuesSO`).
- Arte, sprites, animaciones, rig del pollo.
- Audio, melodías, integración FMOD.
- Diseño de nivel y curva de subida de cámara.
- Aspecto de UI y VFX de plumas.
- Las mecánicas de diseño: kick, colocación de bloques, block life, items. (El GDD que las define **no se toca** — es propiedad de los design leads.)

**Se reconstruye (el core):**
- El loop de simulación → tick fijo, sin `Time.deltaTime` en gameplay.
- El movimiento del pollo → controller cinemático explícito (§3).
- La identidad de jugador → `(PlayerRef, localIndex)` (§4).
- La autoridad → host decide, UI dibuja (§5).
- El arbitraje de interacciones disputadas (§5, §11).
- Pooling y spawn → autoritativos, tras el `ObjectProvider` de Fusion.
- El RNG → sembrado y replicable.

**Es nuevo (no existía):**
- Capa de sesión: crear, unirse, códigos de invitación.
- Lobby online y flujo de reconexión.
- Invitaciones de Steam — Photon da autenticación, el flujo lo construimos nosotros (§9.5).
- Pausa que funciona con varios jugadores remotos (§11).
- Qué pasa si se cae el host (§8.7, §11).

---

## 3. La decisión de fondo — terminar de salir de la física de Unity

Es la decisión con más impacto a largo plazo, y **solo está disponible porque reconstruimos**.

El dato clave sale de la auditoría: el pollo **ya casi no usa la física dinámica de Unity**. Tiene `gravityScale = 0f` ([PlayerMovement.cs:72](../Assets/Scripts/Player/PlayerMovement.cs)), integra la gravedad a mano, congela la rotación, y escribe la velocidad en un único punto ([:236](../Assets/Scripts/Player/PlayerMovement.cs)). El `Rigidbody2D` hoy solo sirve para dos cosas: resolver colisiones y transportar velocidad. Estamos a mitad de camino de un character controller cinemático.

### Los dos caminos

**Camino A — seguir dinámico (Rigidbody2D + Physics Addon).** El pollo sigue siendo cuerpo dinámico y Fusion sincroniza/resimula la física completa.
- **Costo:** solo los clientes resimulan ✅; cada corrección re-simula pollos *y* torre de bloques. El costo **crece con la altura de la torre**, justo cuando la partida se pone interesante.
- **Agravante:** el orden del solver de `Physics2D` no es determinista entre máquinas → la predicción falla más y corrige más seguido.

**Camino B — cinemático (character controller propio). Recomendado.** El pollo pasa a kinematic con colisión por barrido (`Cast` + depenetración manual). Su estado completo cabe en un struct pequeño.
- **Gana:** resimular un pollo pasa a ser aritmética barata; el estado es capturable al 100 %, así que la predicción acierta casi siempre y las correcciones se vuelven raras.
- **Cuesta:** escribir el controller y **re-tunear el feel**. Los valores del SO se conservan, pero hay que volver a sentirlos.

### El reparto que habilita el camino B

**Predecir lo barato, interpolar lo caro:**
- **Pollos cinemáticos → se predicen.** Estado pequeño y completo; resimular es barato; respuesta instantánea al input local.
- **Bloques dinámicos → se interpolan.** Necesitan física real para apilarse y tambalearse, pero nadie los controla con un mando: no hace falta predecirlos. El cliente **nunca los resimula**, así que el costo deja de crecer con la altura de la torre.

### El tradeoff honesto (la única incógnita seria)

Si los bloques solo se interpolan, un cliente los ve unos ms por detrás de donde el host los tiene. Como aquí los bloques **matan y se pisan**, en teoría podrías morir por un bloque que en tu pantalla aún no había llegado. Juega a favor que los bloques son lentos y pesados frente a un pollo, así que el desfase visible es pequeño. **Hay que medirlo en el prototipo (§10, Fase 1)**, no darlo por bueno.

> ⚠️ **Toca diseño:** el paso a controller cinemático cae sobre "Física y collider del personaje" ([gdd.md:61](gdd.md)). Es un cambio de **implementación**, no de intención — el objetivo es preservar exactamente el comportamiento documentado (el de abajo no arrastra al de arriba, colisión y push entre personajes, glide, etc.). Aun así, el **feel** resultante debe validarse con los design leads antes de darse por cerrado. El GDD no se modifica aquí (decisión 20 jul 2026).

---

## 4. Una sola simulación — identidad y modos

Fusion cuenta **peers** (máquinas), no jugadores: asigna un `PlayerRef` por peer ✅. Como tenemos couch co-op, hasta 4 pollos pueden compartir uno. Si la identidad se diseña una vez sobre `(PlayerRef, localIndex)`, los tres modos caen solos:

| Modo | Peers | Locales por peer | Qué es |
|---|---|---|---|
| Offline | 1 | 1 | host sin nadie conectado |
| Couch | 1 | 2–4 | host sin nadie conectado |
| Online | 2–4 | 1–4 c/u | host + clientes |

> **Doc oficial · PlayerRef:** *"If the game allows for more than one local physical player on a single NetworkRunner (for example, couch co-op combined with online players), then game specific logic will be needed to differentiate the local players, independent of Fusion's 'Player' concept."*

**Patrón oficial "Multiple Players Per Peer":** consolidar los inputs locales en un struct único con slots + indexer.

```csharp
public struct PlayerInputs : INetworkStruct {
    public float moveDirection;
    public NetworkButtons buttons;   // jump, place, kick, cluck
}
public struct CombinedPlayerInputs : INetworkInput {
    public PlayerInputs PlayerA, PlayerB, PlayerC, PlayerD;
    public PlayerInputs this[int i] { get { /* switch */ } set { /* switch */ } }
}
```

**Consecuencias:**
- Los pollos de un mismo peer **comparten `PlayerRef` e InputAuthority**.
- `SetPlayerObject` / `TryGetPlayerObject` son 1:1 con `PlayerRef` → no sirven tal cual; hace falta un mapa propio `(PlayerRef, localIndex) → NetworkObject`.
- `OnPlayerJoined` / `OnPlayerLeft` disparan **por peer, no por jugador**: los eventos de "entró/salió un pollo lógico" los emitimos y sincronizamos nosotros.
- El `playerIndex` que hoy asigna `GameManager` por hueco libre ([GameManager.cs:259-270](../Assets/Scripts/Manager/GameManager.cs)), el contador de slots de `PlayersManager`, los colores por slot y el spawn vía `PlayerInput.Instantiate` (device pairing local) se rehacen sobre este modelo.
- ⚠️ El **tamaño máximo del struct `INetworkInput` no está documentado** — con 4 locales crece linealmente; medir con FusionStats.

El beneficio de fondo: **desaparece la categoría de bug "funciona en local pero no en online"**, porque es literalmente el mismo código.

---

## 5. Arquitectura objetivo — seis capas

Cada capa solo se apoya en las de abajo. Es a la vez el diseño y el orden de trabajo: **ninguna capa se empieza hasta que la anterior aguanta peso.**

| # | Capa | Qué contiene | Autoridad |
|---|---|---|---|
| 6 | **Presentación** | UI, VFX de plumas, audio, cámara, screen shake. Solo lee estado y lo dibuja; nunca decide. Aquí vive la guardia de resimulación. | Local · cada cliente |
| 5 | **Reglas de partida** | Rondas, First-to-N, victoria, ranking, rank de dificultad. Estado replicado; los clientes lo reciben, no lo calculan. | Host |
| 4 | **Mundo** | Bloques dinámicos, items, cápsulas, spawner. Spawn/despawn autoritativos con el pool tras el `ObjectProvider`. | Host · cliente interpola |
| 3 | **Interacciones** | Kicks, head-stomps, daño a bloques, muerte. Arbitraje: quién, a quién, en qué tick, una sola resolución por evento. | Host arbitra |
| 2 | **Movimiento del pollo** | Controller cinemático: gravedad, salto, coyote, glide, colisión por barrido. Función pura `(estado, input) → estado`. | Predicho en cliente |
| 1 | **Tick y estado** | Todo avanza en `FixedUpdateNetwork()` con paso fijo. Estado en propiedades replicadas, RNG sembrado, nada de gameplay depende de DOTween/corrutinas/`Time.deltaTime`. | Host |

**La regla que hay que defender en code review:** la presentación **nunca decide**. Si una capa de arriba toma una decisión de juego, la arquitectura ya se rompió — porque en online esa capa corre en una máquina sin autoridad.

### Qué se mueve de sitio respecto a hoy

La auditoría encontró decisiones de juego viviendo en la capa de presentación. Con esta arquitectura vuelven al host:

| Qué decide | Dónde vive hoy | Capa destino |
|---|---|---|
| Umbral First-to-3 | [UIManager.cs:202](../Assets/Scripts/Manager/UIManager.cs) | 5 (host) |
| Transición Prepare→Game | [UIManager.cs:359](../Assets/Scripts/Manager/UIManager.cs) *(tween DOTween)* | 5 (host) |
| Ranking final | [UIManager.cs:264-285](../Assets/Scripts/Manager/UIManager.cs) | 5 (host) |
| Qué bloque recibe cada jugador | [PoolingManager.cs:109](../Assets/Scripts/Manager/PoolingManager.cs) *(Random sin semilla)* | 1 + 4 (host, RNG sembrado) |
| Pausa de partida | [PauseManager.cs:64](../Assets/Scripts/Manager/PauseManager.cs) *(`Time.timeScale = 0`)* | Rediseño (§11) |

### Efectos no repetibles y la guardia de resimulación

Los clientes ejecutan `FixedUpdateNetwork()` varias veces por frame (resimulan el mismo tick) ✅. Todo efecto no repetible va en la capa 6 tras la guardia:

```csharp
if (Runner.IsForward && !Runner.IsResimulation) {
    // spawn, audio, VFX, screen shake
}
```

Nos afecta en `FeatherVFXController`, `CluckSystem`, `AudioManager` (hoy invocado desde dentro de la simulación en [PlayerMovement.cs:193](../Assets/Scripts/Player/PlayerMovement.cs)), `CameraEffects` y el spawn de bombas/items.
⚠️ **FMOD** se integró en el último commit y no entró en la auditoría; cualquier evento FMOD disparado desde simulación necesita la misma guardia. Revisarlo antes de B-1.

---

## 6. Estado de partida (auditoría — de dónde migramos)

Verificado a 20 jul 2026 sobre 55 scripts. Esto **no es un diagnóstico de errores**, es el inventario de lo que la reconstrucción sustituye. Se conserva aquí porque cada punto mapea a una capa de §5.

### Entorno confirmado (directo del proyecto)
- Unity **6000.3.9f1** → dentro de 6.3.x, soportado por Fusion 2.1 ✅.
- `Fixed Timestep = 0.02` (50 Hz) en `TimeManager.asset`.
- `com.unity.multiplayer.center 1.0.1` instalado; **cero paquetes de netcode**.
- FMOD ya integrado en `Assets/Plugins/FMOD`.

### Lo que ayuda a la migración
- Input aislado en `InputPayload`; `ProcessInput` como función de entrada.
- Gravedad manual → media migración al controller cinemático ya hecha (§3).
- Un único punto de escritura al Rigidbody del pollo; `AddImpulse` como puerta única de kicks/springs/explosiones.
- Solo **dos** `FixedUpdate` en todo el proyecto.

### Lo que la reconstrucción reemplaza (mapeo a capas)

**Capa 1–2 (simulación):**
- Gravedad integrada con `Time.deltaTime` en `Update` ([PlayerMovement.cs:207,224,230](../Assets/Scripts/Player/PlayerMovement.cs)); `SmoothDamp` con estado interno no capturado ([:163-168](../Assets/Scripts/Player/PlayerMovement.cs)); `DOVirtual.DelayedCall` como reloj de gameplay ([:191](../Assets/Scripts/Player/PlayerMovement.cs)).
- `StatePayload` captura 3 de 12 campos de estado ([:50-64](../Assets/Scripts/Player/PlayerMovement.cs)) y **nunca se lee** ([:121](../Assets/Scripts/Player/PlayerMovement.cs)); los payloads no tienen tick.
- `ProcessInput` consume 3 de 6 campos; kick/place/cluck van por eventos C# paralelos ([PlayerInputHandler.cs:186-198](../Assets/Scripts/Player/PlayerInputHandler.cs)).
- RNG sin sembrar decide gameplay: bloque por jugador ([PoolingManager.cs:109](../Assets/Scripts/Manager/PoolingManager.cs)), item de cápsula ([ItemCapsule.cs:45](../Assets/Scripts/Objects/ItemCapsule.cs)), todo `HorizontalSpawner`.

**Capa 3 (interacciones) — hoy sin arbitraje:**
- `KickCollider` ([:8-32](../Assets/Scripts/Controllers/KickCollider.cs)): sin autoridad, timestamp, cooldown ni dedup por víctima; dos jugadores que se patean ejecutan ambos golpes. El impulso lo aplica el receptor, no el atacante ([KickResponse.cs:14](../Assets/Scripts/Controllers/KickResponse.cs)).
- `BlockDamageable.TakeDamage` recibe el atacante y lo descarta — sin atribución de daño/kill.
- `ItemCapsule` sin guard `life <= 0` ([:27](../Assets/Scripts/Objects/ItemCapsule.cs)): dos golpes en el mismo frame entregan el item dos veces.

**Capa 5–6 (autoridad) — ver tabla en §5.**

### Bugs latentes ya presentes en local (la red los amplifica)
Estos aparecen aun sin online; conviene barrerlos al reconstruir la capa correspondiente:
1. **NRE en el ranking:** `playersAlive` se escribe por `playerIndex` ([GameManager.cs:288](../Assets/Scripts/Manager/GameManager.cs)) y se lee por índice denso ([:363](../Assets/Scripts/Manager/GameManager.cs)); muere el jugador de índice bajo → excepción.
2. **Doble entrega de item:** `ItemCapsule` sin guard de vida.
3. **Pool de bombas se degrada:** `BombItem` sale del pool pero hace `Destroy` ([:109](../Assets/Scripts/Items/Bomb/BombItem.cs)).
4. **Input legacy:** `Input.GetKeyDown(KeyCode.K)` en `BombItem` ([:57-61](../Assets/Scripts/Items/Bomb/BombItem.cs)).

### Código muerto a limpiar
`StartGame.cs` (Input Manager antiguo), `MusicManager.cs` (sin referencias), `SoundManager.cs` (solo lo usa `StartGame`), `LoadingScreenController`/`MinigameDefinition` (el menú hardcodea `"Main_Level"`).

---

## 7. Correcciones a documentos existentes

Tres afirmaciones vigentes en `ADR-0001` y `fusion2-primer.md` son incorrectas a día de hoy. Son **correcciones de hecho** (no cambios de diseño), y siguen aplicando bajo la premisa de reconstrucción.

### 7.1 "Arquitectura ya preparada para red (ventaja clave)" — `ADR-0001` §2
Falso: el patrón está nombrado, no implementado (ver §6). Bajo la reconstrucción esto deja de ser un problema y pasa a ser el punto de partida — pero la afirmación del ADR debe corregirse para que nadie vuelva a planificar sobre una base inexistente.

### 7.2 "Lag compensation integrada — aplicable al KickCollider" — `fusion2-primer.md` §2
Doc oficial ✅: *"Currently, a Hitbox can only be described as a 3D shape: sphere or box."* Los `Collider2D` **no participan** en la lag compensation de Fusion. Es una **decisión de diseño** cómo resolver los kicks (§11), no un detalle de implementación.

### 7.3 "usaremos NetworkRigidbody2D" — `fusion2-primer.md` §2
En Fusion 2.1 ese componente **ya no existe con ese nombre** ✅: se unificó en `NetworkRigidbody`, que sincroniza `Rigidbody` o `Rigidbody2D` indistintamente. Cualquier tutorial que lo mencione es de 2.0. (Con el camino B de §3, el pollo ni siquiera usa `NetworkRigidbody` — solo los bloques.)

> Estas correcciones **no** implican editar el GDD. El `fusion2-primer.md` es doc de onboarding técnico y puede corregirse; queda como follow-up (§ nota final).

---

## 8. Fusion 2 — datos técnicos verificados ✅

### 8.1 Versión e instalación
| Dato | Valor |
|---|---|
| SDK actual | **Fusion 2.1.1**, Build 2177, 30 jun 2026 |
| Unity soportado | 2021.3.45 · 2022.3.45 · **6.0.x · 6.3.x** |
| Nuestro Unity | 6000.3.9f1 → dentro de 6.3.x ✅ |
| Instalación | `.unitypackage` del dashboard → `Assets/Photon/`. Sin UPM |
| Requisito | *Asset Serialization* en **modo texto** (crítico para git) |

Entrar directo en 2.1.x (2.1 requiere Realtime 5 y no es compatible con 2.0).

### 8.2 Topología y runner
`GameMode`: `Single`, `Shared`, `Host`, `Server`, `Client`, `AutoHostOrClient`. Nuestra topología: **Client-Host** (`GameMode.Host` / `AutoHostOrClient`), única con predicción + host migration. NAT punch-through incorporado; ~1 de cada 10 conecta vía relay ✅. Regla: un `NetworkRunner` se usa una sola vez. Tick 8–256 Hz configurable (hoy 50 Hz).

### 8.3 Física (mapea a §3)
- **Forecast Physics** (2.1): soporta `Rigidbody2D`, por extrapolación; lógica en `FixedUpdate()`. Limitación ✅: *no considera obstáculos del mundo* — mal para bloques/kicks.
- **Physics Addon:** predicción + resimulación reales; componente `NetworkRigidbody`; modos `Disabled`/`SyncTransforms`/`SimulateForward`/`SimulateAlways`. Solo los clientes resimulan ✅.
- Con el camino B, esto aplica **solo a los bloques**, no al pollo. Palanca si pesa: **Input Delay** (2.1+, solo Server/Host).
- ⚠️ La enum "Physics Simulation Mode: None/Sync/Multi/Independent" **no existe** en la doc de Fusion 2. No planificar sobre ella.

### 8.4 Spawn y pooling
Solo el host spawnea ✅; los clientes piden por RPC. Integración del pool: derivar de `NetworkObjectProviderDefault` y sobrescribir `InstantiatePrefab` / `DestroyPrefabInstance`; asignar por `StartGameArgs.ObjectProvider`. El default no hace pooling.

### 8.5 Lag compensation — no sirve en 2D
`HitboxRoot` + `Hitbox` (máx 31 por root), queries `Runner.LagCompensation.Raycast/Overlap`. Limitación ✅: solo formas 3D (sphere/box). Para 2D real hay que usar `Physics2D` sin compensar. → decisión de §11.

### 8.6 Multi-peer testing
`PeerMode = Multiple` corre host + N clientes en un editor sin builds. Requiere `RunnerEnableVisibility` y `EnableOnSingleRunner`. La doc advierte: **evitar estáticos/singletons que afecten la simulación** — hoy 8 singletons con 91 accesos. La arquitectura de §5 (autoridad en el host, presentación local) lo resuelve por construcción.

### 8.7 Host migration
No es un flag: shutdown del runner, descarga de escena, nuevo runner con `HostMigrationToken` + `HostMigrationResume`, iterar `GetResumeSnapshotNetworkObjects()` + `CopyStateFrom()`. 📊 Para rondas cortas, evaluar abortar al lobby (§11). `StartGameArgs.PlayerUniqueId` (2.1) da `PlayerRef` consistentes entre reconexiones.

### 8.8 Lo que NO necesitamos
Interest management / AOI (todos ven todo en una arena de 2–8); servidores dedicados (confirmado en ADR).

---

## 9. Costos y plan comercial

### 9.1 Pricing verificado ✅ (jul 2026)
| Plan | Precio | Tráfico | Burst |
|---|---|---|---|
| 100 CCU Free | $0 | 0.3 TB/mes | **No** |
| 200 CCU Plus | $95 / año | 0.3 TB/mes | No |
| 500 CCU | $125/mes | 1.5 TB/mes | Sí |
| 1,000 CCU | $250/mes | 3.0 TB/mes | Sí |
| 2,000 CCU | $500/mes | 6.0 TB/mes | Sí |

Egress incluido: 3 GB por peak CCU/mes. Overage $0.05/GB (EU/US/CA) · $0.10/GB (Asia, SA, etc.).

### 9.2 Cuatro riesgos comerciales (no documentados en el ADR)
1. 🚩 **Sin CCU Burst < 500 CCU** — hard cap; los excedentes se **desconectan**. Mitigación: $95/año del pack 200 CCU (0.08 % del presupuesto).
2. 🚩 **Peak CCU se suma por región** ✅, no global.
3. 🚩 **Salto de tier:** de $95/año (200) a $1,500/año (500), sin escalón intermedio.
4. 🚩 **Lock-in** ✅ — sin salida self-hosted; incluso con servidores propios se depende del Photon Cloud. Mitigación arquitectónica: aislar la red tras interfaces propias desde B-1 (encaja con la capa 1 de §5).

### 9.3 Regiones desde Perú 📊
São Paulo (`sa`, 78.8 ms) es la única región sudamericana; solo ~9 ms mejor que Washington (`us`, 87.6 ms) pero el doble de overage. 88 ms es jugable para este género. Medir con "best region" en la beta.

### 9.4 Soporte y consolas
Gaming Circle: STARTER $500/mes · PRO $1,000/mes (sin CCU); trial de 1 mes gratis. Consolas ✅: PS4/5, Switch 1/2, Xbox One/Series — requieren *certified developer* y **native socket library por consola**. ⚠️ Sin datos sobre NDA/fees de Photon en consola: pregunta obligatoria.

### 9.5 Matchmaking
Nativo ✅: lobbies, `SessionProperties`, `FillRoom`/`SerialMatching`/`RandomMatching`, `SessionName` (→ códigos de invitación). **No** trae skill-based, amigos ni party. Steam Auth existe ✅, pero las **invitaciones de amigos las construimos nosotros** — presupuestar ese sprint (capa 5, §5).

---

## 10. Secuencia de construcción

Las **capas 1 y 2 no necesitan Fusion** (refactor de simulación pura) y **mejoran el juego local igualmente** — hoy la altura de salto depende del framerate. Pueden empezar ya, en paralelo a A-4→A-7. Las capas 3–6 quieren la red presente.

| Bloque | Capas | ¿Fusion? | Cuándo |
|---|---|---|---|
| Simulación en tick fijo — controller cinemático, estado completo, RNG sembrado | 1 · 2 | No | Ya, en paralelo a A-4→A-7 |
| **Prototipo de riesgo** — medir resimulación y desfase de bloques con latencia | 1 · 2 · 4 | Sí | Antes de cerrar B-3 |
| Identidad y sesión — `(PlayerRef, localIndex)`, join, lobby, reconexión | 1 · 5 | Sí | B-1 → B-2 |
| Autoridad de partida — reglas y ranking al host | 5 · 6 | Sí | B-2 → B-3 |
| Mundo autoritativo — pool tras `ObjectProvider`, items | 4 | Sí | B-3 |
| Arbitraje de interacciones — kicks, stomps, muertes | 3 | Sí | B-4 |
| Invitaciones de Steam | 5 | Nuevo | No estaba presupuestado |

### Fase 1 — el prototipo de riesgo responde tres preguntas con código
1. **¿Cuánto cuesta la física de los bloques interpolados + resimulación?** 4 pollos + torre, latencia 80–150 ms.
2. **¿Cuánto desfase visible tienen los bloques?** — el tradeoff de §3. Fijar un umbral aceptable como criterio (§11).
3. **¿Cuánto pesa `CombinedPlayerInputs` con 4 locales?** — FusionStats.

Si el reparto cinemático/interpolado resultara inviable, es mucho mejor saberlo en B-1 que en B-4.

---

## 11. Decisiones que necesitan design leads (no código)

| Decisión | Opciones | Recomendación |
|---|---|---|
| **Feel del controller cinemático** (§3) | Preservar 1:1 el feel actual · ajustar aprovechando el rework | Preservar; validar en playtest |
| **Kicks disputados** (lag comp no va en 2D) | Emular hitboxes 3D · autoritativo en host · sin compensar | Autoritativo en host — encaja con "caos entre amigos", más barato; cambia el feel de choques simultáneos |
| **Caída del host** | Host migration · abortar al lobby | Abortar al lobby en rondas cortas |
| **Pausa en online** | Solo local con pollo en auto · votación · sin pausa | Sin decidir — necesita criterio de diseño |
| **Umbral de desfase de bloques** | Se mide en Fase 1 y se fija | Definirlo como criterio de aceptación antes de la capa 4 |

---

## 12. Preguntas para la reunión con Photon

Las de `fusion2-primer.md` §4 siguen vigentes. Nuevas de esta investigación:

**Pricing:** ¿peak CCU se suma por región? · ¿tier intermedio entre 200 y 500 CCU o burst en 200? · ¿sigue el descuento anual "2 meses gratis"? · ¿`sa` confirmado en overage $0.10/GB? · ¿compromiso de estabilidad de precios (lanzamos jul 2027)?
**Técnico:** ¿Forecast o Physics Addon para bloques `Rigidbody2D` + personajes cinemáticos? · ¿best practices de kicks 2D sin lag comp? · ¿límite práctico de `INetworkInput`? · ¿host migration realista en rondas cortas?
**Consolas:** ¿cambia el CCU en consola? ¿fee/NDA del lado de Photon?
**Soporte:** ¿programa para estudios pequeños más allá del trial?

---

## 13. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| Desfase de bloques interpolados rompe el feel de muertes | Media | 🔴 Alto | Medir en Fase 1; fijar umbral (§11); si falla, revisar reparto |
| Costo de resimulación de bloques aún alto | Baja | 🟠 Medio | El pollo ya no resimula; palanca Input Delay |
| Las capas 1–2 no caben antes de B-1 | Alta | 🟠 Medio | Solapar con A-4→A-7; mejoran el juego local igual |
| Re-tuneo del feel del controller tarda | Media | 🟠 Medio | Conservar valores del SO; playtest temprano con design leads |
| Beta desconecta jugadores por falta de burst | Baja | 🟠 Medio | Pack 200 CCU ($95/año) |
| Lock-in de Photon | Baja | 🔴 Alto | Aislar la red tras interfaces propias (capa 1) |
| Acceso a SDK de consola tarda | Media | 🟠 Medio | Iniciar trámite con plataformas en M3, no en L-7 |

---

## 14. Fuentes

**Fusion 2 — doc oficial:** [SDK & Download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) · [What's New 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1) · [Network Runner](https://doc.photonengine.com/fusion/current/manual/network-runner) · [Topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies) · [Player Input](https://doc.photonengine.com/fusion/current/manual/data-transfer/player-input) · [PlayerRef](https://doc.photonengine.com/fusion/current/manual/playerref) · [Physics](https://doc.photonengine.com/fusion/current/manual/physics) · [Physics Addon 2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1) · [Lag Compensation](https://doc.photonengine.com/fusion/current/manual/advanced/lag-compensation) · [Simulation Loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop) · [Spawning](https://doc.photonengine.com/fusion/current/manual/spawning) · [NetworkObjectProvider](https://doc.photonengine.com/fusion/current/manual/advanced/network-object-provider) · [Host Migration](https://doc.photonengine.com/fusion/current/manual/advanced/host-migration) · [Multi-Peer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer) · [Matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking) · [Steam Auth](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/authentication/steam-auth) · [Consoles](https://doc.photonengine.com/fusion/current/consoles/overview) · [Dedicated Servers](https://doc.photonengine.com/fusion/current/concepts-and-patterns/dedicated-server-overview)

**Pricing:** [Fusion Pricing](https://www.photonengine.com/fusion/pricing) · [Photon Pricing (docs)](https://doc.photonengine.com/photon/current/pricing) · [Pricing Made Simple (jul 2025)](https://blog.photonengine.com/multiplayer-pricing-made-simple/) · [200 CCU Plus](https://blog.photonengine.com/new-200-ccu-plus-package-100-paid-100-free/) · [Gaming Circle](https://www.photonengine.com/gaming/pricing) · [Regions](https://doc.photonengine.com/realtime/current/connection-and-authentication/regions)

**Comparación:** [UGS Pricing](https://unity.com/products/gaming-services/pricing) · [Edgegap](https://edgegap.com/pricing) · [WonderNetwork — Lima](https://wondernetwork.com/pings/Lima)

---

## 15. Historial

- **2026-07-20 · v1.0** — investigación inicial (auditoría 55 scripts + doc Fusion 2.1). Encuadre: qué corregir para ir online.
- **2026-07-20 · v2.0** — **reencuadre a arquitectura objetivo** tras la decisión de reconstruir el core. Añade: una sola simulación (§4), controller cinemático + reparto predecir/interpolar (§3), seis capas (§5), secuencia por capas (§10), decisiones para design leads (§11). La auditoría pasa a "estado de partida" (§6). El GDD **no se modifica** (decisión del equipo; el online ya se delega a docs de producción en `gdd.md:80`).
