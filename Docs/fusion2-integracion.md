# Arquitectura objetivo — Don't Chicken Out! con Fusion 2

**Proyecto:** Don't Chicken Out! · Raymi Games
**Fecha:** 20 de julio de 2026 · **Última actualización:** 4 de agosto de 2026
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

### 4.1 Lobby unificado y loadout (ago 2026) 📊

**Decisión: un solo diseño de lobby para local y online.** Se evaluó un wireframe alternativo, minimalista, exclusivo para online (solo tu propio carrusel + lista de ready/waiting/disconnected), bajo la hipótesis de que mostrar a todos los jugadores encarecía el online. **La hipótesis es falsa en los dos escenarios analizados:**

| Escenario | Wireframe minimalista | Lobby unificado |
|---|---|---|
| 4 jugadores en 2 PCs (couch mixto) | Imposible: fuerza 1 jugador = 1 peer → **4 CCU** | **2 CCU** |
| 4 jugadores en 4 PCs | 4 CCU | 4 CCU (idéntico) |

- **CCU:** Photon factura **peers, no jugadores**. El wireframe "barato" es el que maximiza CCU, porque prohíbe compartir máquina. El unificado es un descuento por cada jugador que comparte peer.
- **Egress:** a igual número de peers, lo único que el unificado manda de más es el loadout de los otros jugadores. Techo teórico patológico (4 jugadores scrolleando sin parar 90 s a 30 Hz) ≈ **100 KB/lobby**; caso real ≈ **3–8 KB**. Una ronda de gameplay son ~1–3 MB. El lobby entero de una sesión de 2 h es **<1 % del consumo** (§9.6).
- ⚠️ **Requisito duro:** un solo `NetworkRunner` por máquina. Abrir un runner por jugador local convierte 2 CCU en 4 y anula toda la ventaja.

**Los cosméticos no escalan el costo de red.** Un loadout viaja como **IDs de ancho fijo**, nunca como assets: tipo de pollo (`byte`) + gorro/ropa/accesorio (`ushort` c/u) + variante de color (`byte`) ≈ **8 bytes**. El costo lo fija el número de *slots*, no el tamaño del catálogo: pasar de 20 a 500 gorros cuesta **cero bytes**. Cada slot nuevo (mochila, calzado) suma ~2 bytes.

**Reglas de implementación:**
- **Commit-on-confirm:** la selección en curso vive en estado **local, no networked**; se escribe a la propiedad `[Networked]` solo al confirmar. Reduce el tráfico de loadout al mínimo teórico (una escritura por visita al ropero) y es mejor que debounce, que sigue mandando cada combinación en la que el jugador se detiene.
- **La cortina no ahorra tráfico** — ocultar visualmente no impide enviar. Ahorra el commit; la cortina es el contrato de UI que evita que el commit se sienta como lag. Se justifica por experiencia (mata el estroboscopio de un jugador scrolleando 200 gorros, tapa el pop-in de carga de assets, da un punto único de validación de entitlements, y genera el momento de reveal), **no por costo**.
- **Estado `[Networked]`, no RPCs por cambio:** Fusion deltea el estado y resuelve el late-join gratis — quien entre a mitad de lobby ve los loadouts actuales sin replay de eventos.
- **Sincronizar el loadout en el lobby es una ventaja técnica**, no un gasto: da una ventana de precarga de 30–90 s para los assets cosméticos ajenos. Diferirlo al arranque de la ronda mete la carga en el peor momento posible (hitch/pop-in en el primer segundo). Relevante sobre todo en Switch.
- El cap total de pollos (4) **no** lo da `MaxPlayers` de Fusion, que es máximo de *peers*: 4 peers × 4 locales serían 16. Se impone en lógica propia, junto con la UX de "quiero sumar un local pero la sala está llena".
- Si cae un peer con 2+ locales, **desaparecen varios pollos a la vez**: la lógica de rondas / last-man-standing tiene que aguantarlo.

**El costo real del lobby unificado no es red: es render local.** Cuatro previews de pollo animadas con capas de cosméticos en menú, en la plataforma más floja del target. Presupuestarlo como riesgo de cliente, no de ancho de banda.

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

### 9.6 Consumo real y sensibilidad del egress 📊 (ago 2026)

Todo lo de esta sección son **estimaciones propias sin medir**. Existe para dimensionar y para saber qué medir, no como cifra de presupuesto.

**Sesión típica de 2 h** (4 jugadores, First-to-5, ~5–6 matches, ~75 % en simulación activa):

| Fase | Tiempo | Tasa estimada | Total por peer |
|---|---|---|---|
| Lobby inicial | ~3 min | ~0,5–1 KB/s | ~0,2 MB |
| Gameplay activo | ~90 min | 5–15 KB/s | **27–81 MB** |
| Entre rondas / resultados / re-lobby | ~27 min | ~0,5–1 KB/s | ~1–1,6 MB |
| **Total** | 120 min | | **~30–85 MB** (central ≈ 50 MB) |

→ La cuota incluida (3 GB/peak-CCU/mes) da **~60 sesiones de 2 h**, o ~120 h de juego, por CCU. **El lobby es <1 % del total; el 99 % son ticks de gameplay.**

**La variable oculta es la utilización** (CCU promedio ÷ CCU pico): se factura la cuota por el **pico**, pero se consume por el promedio. GB por peak-CCU/mes, contra los 3 GB incluidos:

| Tasa \ Utilización | 15 % | 25 % | 35 % |
|---|---|---|---|
| **5 KB/s** | 2,0 ✅ | 3,3 ⚠️ | 4,6 🚩 |
| **8 KB/s** | 3,1 ⚠️ | 5,3 🚩 | 7,4 🚩 |
| **15 KB/s** | 5,9 🚩 | 9,9 🚩 | 13,8 🚩 |

⚠️ **Esto matiza el supuesto del `ADR-0001` §9** ("~2 GB egress/CCU/mes, cabe en los 3 GB incluidos"): ese 2 GB descansa en un supuesto de utilización que no está escrito. A 8 KB/s con utilización normal (picos de tarde/finde), **no cabe**, y el overage queda en el mismo orden de magnitud que el plan base — +45 % a 8 KB/s @ 25 %, +215 % a 15 KB/s @ 35 %. Desde `sa` el overage es $0.10/GB, el doble (§9.3). No es que el ADR esté mal; es que su conclusión es más frágil de lo que aparenta.

**Riesgo #1 que puede duplicar la tasa: bloques dinámicos que no se duermen.** Los bloques siguen siendo `Rigidbody2D` dinámicos (§3). Un bloque asentado tiene que dejar de emitir deltas por completo — si 40 bloques apilados micro-tiemblan, se paga esa vibración a 30 Hz para siempre sin que se vea nada en pantalla, y se pasa de ~8 a 30+ KB/s. Forzar sleep / congelar bloques asentados y **verificarlo con FusionStats**, no asumirlo.

**Palancas si la medición sale alta:** (1) tick 30 en vez de 60 Hz → mitad directa; (2) **culling vertical** — el juego sube, los bloques que salen por debajo de la cámara no necesitan replicarse (ahorro específico de un climber que el Area of Interest genérico no da solo); (3) cuantización de posiciones.

**Cuatro escenarios comerciales.** CCU = peers online concurrentes, no ventas: buena parte de la audiencia de un party game juega solo en couch (0 CCU) y el couch-en-online comprime jugadores por peer (§4.1). Sumar 15–30 % de colchón porque **los picos se suman por región** (§9.2). Proyecciones de copias/CCU = reglas de industria, **no datos del publisher** — sustituir por la concurrencia objetivo real (`ADR-0001` §11 paso 3).

| Escenario | Copias año 1 | Peak CCU online | Plan | Base | Overage est. | Total/mes |
|---|---|---|---|---|---|---|
| **Pesimista** | 5–20k | 60–120 | Free 100 → 200 CCU Plus | ~$8 | $0–15 | **~$10–25** |
| **Realista** | 50–150k | 300–700 pico · 100–250 estable | 500 CCU en lanzamiento → bajar a 200 | $125 → $8 | $30–80 | **$155 lanz. · ~$40 estable** |
| **Optimista** | 300–500k | 1.200–2.500 | 2.000 CCU o Gaming Circle STARTER | $500 | $150–400 | **$650–900** |
| **Muy optimista** | 1M+ | 5.000–12.000 | 5.000 CCU ($0.50/CCU) o Gaming Circle | $2.500–6.000 | $500–1.500 | **$3.000–7.500** |

En el escenario optimista la factura anual de Photon es ~0,3 % de ingresos. **En ningún escenario el monto compromete la viabilidad.** Los problemas son otros dos, y son los accionables:

1. 🚩 **Burst obligatorio el mes de lanzamiento.** Sin burst por debajo de 500 CCU, un pico de lanzamiento por encima del tier **desconecta jugadores** — el día exacto en que se escriben las reviews de Steam. El pico de lanzamiento es un spike que decae 80–90 % en un mes y no representa el estado estable. **No forecastear ese mes: comprar headroom.** Subir a 500 CCU ($125) para el mes de lanzamiento y bajar después. Es seguro barato contra un desastre de día 1.
2. 📊 **Gaming Circle puede mover el techo 5x.** STARTER $500/mes **"sin CCU"** (§9.4): si eso es literalmente sin tope, el escenario muy optimista cuesta $500–1.000/mes en vez de $3.000–7.500. Es la diferencia más grande de toda la tabla y sale de una línea sin confirmar → §12.

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
3. **¿Cuánto pesa `CombinedPlayerInputs` con 4 locales?** — FusionStats. Ojo: el struct de `INetworkInput` es de **tamaño fijo**, así que 4 slots declarados se envían cada tick aunque haya un solo jugador local. Bit-packing (dirección 2 bits, jump 1, kick 1…) lo baja a ~1 byte por slot.
4. **¿Cuál es la tasa real en KB/s por peer?** — 10 min de gameplay 4P con FusionStats. Todo el modelo de costos (§9.6) cuelga de un número hoy estimado entre 5 y 15 KB/s, un rango de 3x que mueve la factura entre "cabe en la cuota" y "el overage duplica el plan". Medirlo **antes** de la reunión con Photon para negociar con cifra propia. Verificar en la misma corrida que los bloques asentados dejan de emitir deltas.

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
| **¿Se permiten pollos duplicados?** (§4.1) | Permitir · bloquear el ya elegido · resolver al arrancar | Bloquear en vivo: en 4 pollos la legibilidad de quién es quién es funcional, no estética |
| **Reparto de la cortina del ropero** (§4.1) | Todo tras cortina · nada · solo cosméticos | **Tipo de pollo visible y en vivo** (identidad, hay que ver quién lo tomó) + **cosméticos tras cortina** (sabor; ahí está el estroboscopio y el reveal). Con todo oculto vuelve el arbitraje de duplicados en el reveal |
| **Presupuesto de render del lobby** (§4.1) | 4 previews animadas con capas cosméticas · versiones estáticas/simplificadas | Medir en la plataforma más floja del target antes de comprometer el arte del lobby |

---

## 12. Preguntas para la reunión con Photon

Las de `fusion2-primer.md` §4 siguen vigentes. Nuevas de esta investigación:

**Pricing:** ¿peak CCU se suma por región? · ¿tier intermedio entre 200 y 500 CCU o burst en 200? · ¿sigue el descuento anual "2 meses gratis"? · ¿`sa` confirmado en overage $0.10/GB? · ¿compromiso de estabilidad de precios (lanzamos jul 2027)? · 🚩 **¿Gaming Circle STARTER es realmente sin tope de CCU, y qué pasa con el egress bajo ese plan?** (§9.6 — mueve el escenario muy optimista de $3.000–7.500/mes a $500–1.000) · ¿se puede subir de tier solo el mes de lanzamiento y bajar después?
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
| **Bloques asentados que no se duermen** → deltas constantes, tasa 3–4x | Media | 🔴 Alto | Forzar sleep de bloques asentados; verificar con FusionStats en Fase 1 (§9.6) |
| **Overage subestimado** — el supuesto de 2 GB/CCU del ADR asume utilización no escrita | Media | 🟠 Medio | Medir KB/s reales antes de la reunión con Photon; palancas de tick/culling/cuantización (§9.6) |
| **Desconexiones el día de lanzamiento** por pico sobre el tier sin burst | Media | 🔴 Alto | Subir a 500 CCU el mes de lanzamiento sin forecastear; bajar después (§9.6) |
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
- **2026-08-04 · v2.1** — análisis de **lobby, cosméticos y consumo real**. Añade §4.1 (lobby unificado local+online, loadout como IDs, commit-on-confirm, la cortina es UX y no ahorro) y §9.6 (consumo de una sesión de 2 h, sensibilidad del egress a la utilización, cuatro escenarios comerciales). Matiza el supuesto de egress del `ADR-0001` §9. Nuevos riesgos: bloques que no duermen, overage subestimado, desconexiones de día 1 sin burst. Nuevas decisiones para design leads: duplicados, reparto de la cortina, presupuesto de render del lobby. **A tener muy en cuenta durante el traspaso del gameplay actual a online** (capas 3–6, B-1→B-4).
- **2026-07-20 · v2.0** — **reencuadre a arquitectura objetivo** tras la decisión de reconstruir el core. Añade: una sola simulación (§4), controller cinemático + reparto predecir/interpolar (§3), seis capas (§5), secuencia por capas (§10), decisiones para design leads (§11). La auditoría pasa a "estado de partida" (§6). El GDD **no se modifica** (decisión del equipo; el online ya se delega a docs de producción en `gdd.md:80`).
