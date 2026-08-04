# Photon Fusion 2 — Primer para el equipo

**Objetivo:** base común de conocimiento antes de la reunión con Photon y del primer prototipo online. Framework decidido el 15 jul 2026 (ver `ADR-0001-Netcode-Online.md`). Integración online planificada para B-1→B-4 (sep–nov 2026).

> ⚠️ **Estado (20 jul 2026):** este primer es correcto en sus **conceptos de multiplayer** (§1), pero predata la decisión de **reconstruir el core** (jul 2026). La arquitectura objetivo, el controller cinemático y el plan por capas viven ahora en `fusion2-integracion.md` — usar ese doc como fuente de verdad. Las notas técnicas concretas de este primer se corrigieron abajo.

---

## 1. Conceptos de multiplayer online (aplican a cualquier framework)

| Concepto | Qué es | En nuestro juego |
|---|---|---|
| **Autoridad (host)** | Una máquina tiene la "verdad" del mundo; el resto obedece | Host-authority sin dedicados (decidido en ADR) |
| **RTT / ping** | Tiempo ida y vuelta de un mensaje | Meta: < 150 ms input-to-display |
| **Tick** | La simulación avanza en pasos fijos numerados; todo se referencia a un tick | Ya trabajamos así: `InputPayload` por tick |
| **Client-side prediction** | Tu PC mueve tu personaje al instante, "adivinando" que el host estará de acuerdo | Clave para que el movimiento se sienta local |
| **Reconciliación / resimulación** | Al llegar la verdad del host, si tu predicción falló, se corrige re-simulando desde ese tick | Nuestro `StatePayload` + historial existe para esto |
| **Interpolación** | Los personajes *ajenos* se muestran con un pequeño delay, suavizados entre estados recibidos | Los rivales no se predicen, se interpolan |
| **Lag compensation** | Para resolver "¿quién pateó primero?", el host rebobina el mundo al momento en que el cliente actuó | Crítico para kicks/head-stomps disputados |
| **State transfer vs lockstep** | Fusion sincroniza *estado* (eventual consistency); lockstep sincroniza solo inputs y exige determinismo | Physics2D no es determinista → state transfer (por eso se descartó Quantum) |

## 2. Cómo funciona Fusion 2 (lo esencial)

- **NetworkRunner:** el corazón — corre la simulación de red, gestiona la sesión (room), el tick y la conexión al Photon Cloud.
- **Topologías:** *Host Mode* (un jugador es server+client — **la nuestra**), *Server Mode* (dedicado — descartado), *Shared Mode* (autoridad repartida por objeto — no encaja con física competitiva).
- **NetworkObject / NetworkBehaviour:** componentes que marcan qué GameObjects se sincronizan. Las propiedades `[Networked]` se replican solas.
- **Input:** defines un struct de input (`INetworkInput`) que el runner recolecta cada tick y envía al host. Mapea casi 1:1 con nuestro `InputPayload` (`PlayerPayloads.cs`) — nuestra arquitectura ya está preparada.
- **Predicción + resimulación integradas:** los objetos con autoridad de input se predicen localmente; si el estado del host difiere, Fusion re-simula automáticamente. Es nuestro patrón `StatePayload` pero provisto por el framework.
- **NetworkTransform / NetworkRigidbody:** sync de posición/física con interpolación. ⚠️ **Corrección:** en Fusion 2.1 el componente se unificó en `NetworkRigidbody` (ya no existe `NetworkRigidbody2D`). Además, con el controller cinemático (ver `fusion2-integracion.md` §3), el **pollo NO usa `NetworkRigidbody`** — se predice como estado propio; solo los **bloques dinámicos** lo usan y se interpolan.
- **Lag compensation:** ⚠️ **Corrección:** la lag compensation de Fusion solo acepta hitboxes **3D** (sphere/box); los `Collider2D` **no participan**. NO es aplicable directamente al `KickCollider`. Cómo resolver los kicks disputados es una **decisión de diseño** (ver `fusion2-integracion.md` §11), no un detalle de framework.
- **Rooms + matchmaking nativos:** sesiones = rooms del Photon Cloud (crear/unirse por nombre, propiedades de sesión, lobbies, regiones). El relay/NAT traversal viene incluido — no operamos servidores.
- **Object pooling:** hook `INetworkObjectProvider` para integrar nuestro `PoolingManager` con el spawn de red.
- **Dashboard + AppId:** el proyecto se registra en dashboard.photonengine.com; el AppId conecta el cliente al cloud. Ahí se ven CCU, regiones y plan.

## 3. Qué significa para nuestro código

1. `PlayerInputHandler` → llenará el struct `INetworkInput` (el join local con `PlayerInputManager` debe convivir con el spawn de red — punto abierto del ADR: couch + online mixto es trabajo custom).
2. `PlayerMovement` → migra su tick loop al `FixedUpdateNetwork()` de Fusion; el historial manual de payloads probablemente se elimina (Fusion lo hace).
3. `GameManager` (state machine + rondas) → el host es autoridad de estados de ronda; clientes reciben `[Networked]` state.
4. Bloques con pooling + items (Bomb, Spring Disc, capsules) → spawn vía runner con pooling provider; física autoritativa en host.
5. `CinemachineVerticalRig2D` → local por cliente (la cámara no se replica), pero la altura/velocidad de subida la dicta el host.

## 4. Preguntas para la reunión con Photon

**Pricing / plan**
1. ¿El tier gratuito de 100 CCU cubre nuestra closed beta (feb–mar 2027, Steam)? ¿Cómo se factura el paso a 500–2,000 CCU?
2. ¿El egress incluido (3 GB/CCU/mes) es realista para un party game 4P con física? ¿Costos por exceso?
3. ¿Descuentos indie / programas para estudios pequeños con publisher?

**Consolas**
4. Proceso y requisitos para acceder a los SDKs de consola (Switch/PS5/Xbox) — ¿qué necesitamos tener firmado con cada plataforma?
5. ¿Cross-play PC↔consola con Fusion 2: qué implica a nivel de matchmaking/regiones?

**Técnico**
6. Recomendaciones para juegos de física 2D competitiva en host mode: límites prácticos de la resimulación con Rigidbody2D.
7. Lag compensation en interacciones melee disputadas (kicks simultáneos) — best practices.
8. Cobertura de regiones del cloud en Sudamérica (jugadores en Perú) — latencias esperadas.
9. Host migration en Fusion 2: ¿qué pasa si el host se desconecta a mitad de ronda?

**Soporte**
10. Opciones de soporte (Discord/Circle/premium), SLAs y costo; ¿revisión de arquitectura temprana disponible?

## 5. Ruta de aprendizaje (antes de B-1)

1. **Fusion 2 Tutorial oficial** (doc.photonengine.com/fusion/current/tutorials/host-mode-basics) — host mode paso a paso.
2. **Manual:** secciones *Network Input*, *Prediction & Resimulation*, *Lag Compensation*, *NetworkRigidbody*.
3. **Samples oficiales** de Fusion 2 (descargables del dashboard) — ver el platformer/brawler más cercano a nuestro caso.
4. **Prototipo interno (DoD de A-1, aún pendiente):** dummy session con 2 clientes en la misma máquina — 1 jugador movible + 1 kick + 1 item spawneado en red.
5. Discord oficial de Photon para dudas puntuales.

> ⚠️ Cifras de pricing citadas según el ADR (jun 2026) — verificar en photonengine.com antes de la reunión.
