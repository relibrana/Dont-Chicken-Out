# ADR-0001 — Selección de netcode online

**Proyecto:** Don't Chicken Out! · Raymi Games
**Fecha:** 4 de junio de 2026 · **Última actualización:** 15 de julio de 2026
**Estado:** ✅ **Aceptado** — modelo de autoridad: host-authority. Framework: **Photon Fusion 2** (lock 15 jul 2026). Alcance ampliado a **reconstrucción del core** (20 jul 2026, ver §14 e `fusion2-integracion.md`).
**Ámbito:** Arquitectura de red para M4 Beta (Online) y posterior.
**Documentos relacionados:** [fusion2-integracion.md](fusion2-integracion.md) · [fusion2-primer.md](fusion2-primer.md) · [overview.md](overview.md) · [Roadmap.md](Roadmap.md) · [sprintplan.md](sprintplan.md) · [Netcode-Analisis-y-Recomendacion.docx](Netcode-Analisis-y-Recomendacion.docx)

> Este registro captura **todas las decisiones y el análisis** realizados durante la evaluación de netcode, para poder retomarlos en cualquier sesión futura. El `.docx` es la versión presentable; este markdown es el registro técnico versionado en git.

---

## 1. Contexto

Don't Chicken Out! es un party game 2D de acción para 2–4 jugadores (posible 8 a futuro), con combate online por salas, matchmaking previsto, contenido variado (mapas, hazards, items, modos) y lanzamiento en PC + consola. El framework de netcode aún no está elegido; su selección está hard-locked para el **Sprint A-1**, y el online se integra en **M4 Beta (B-1 → B-4, sep–nov 2026)**, con beta cerrada en **feb–mar 2027**.

**Pregunta original que disparó el análisis:** ¿es Photon ("Photon X") la mejor opción, o hay algo que se le equipare?

---

## 2. Qué tiene el proyecto hoy (verificado en código, jun 2026)

- **Engine/stack:** Unity 6 (6000.3.9f1), URP, Input System 1.18, Cinemachine 3.1.5, 2D Feature. `com.unity.multiplayer.center` 1.0.1 instalado (selector presente, framework sin elegir).
- **Sin código de red todavía:** 0 referencias a Photon/Mirror/NGO/FishNet/Fusion en `Assets/Scripts` (55 scripts C#).
- **Local funcional 2–4P:** join por teclado split + gamepad vía `PlayerInputManager` (`PlayersManager.cs`).
- **Flujo de partida:** `GameManager` con state machine (Menu/Prepare/Game/Win) y rondas First-to-5.
- **Arquitectura "preparada para red":** `PlayerMovement` consume un `InputPayload` por tick y produce un `StatePayload` (patrón command/predicción); comentarios explícitos de *network-ready* (`PlayerPayloads.cs`, `PlayerMovement.cs`). ⚠️ **Corregido (auditoría jul 2026):** el patrón está *nombrado, no implementado* — `StatePayload` nunca se lee, los payloads no llevan tick, y la simulación corre en `Update()` con `Time.deltaTime`. **No es la ventaja de cronograma que este ADR asumió.** Ver `fusion2-integracion.md` §6–7. Este hallazgo motivó la decisión de reconstruir el core (§14, 20 jul 2026).
- **Física (restricción dura):** `Rigidbody2D` + `Physics2D` (raycasts, `SmoothDamp`, impulsos para kicks/bombas/springs). **No determinista entre plataformas** → descarta el lockstep determinista (Photon Quantum) salvo reescritura completa de la física.
- **Contenido a sincronizar:** jugadores, bloques con pooling escalado por rank, items/hazards (Bomb, Spring Disc, Item Capsules, Horizontal Spawner).

---

## 3. Qué necesita para funcionar online

| Requisito | Implicación para el netcode |
|---|---|
| Salas (rooms) online | Modelo de sesión por rooms: nativo en Photon, UGS Lobby, manual en FishNet |
| Matchmaking (a futuro) | Integrado en Photon; UGS Matchmaker; en FishNet externo (Edgegap) o propio |
| Predicción + reconciliación | Integrada en Fusion y FishNet; en NGO se implementa a mano |
| Sincronización de objetos | NetworkObject/Behaviour en las tres; estándar |
| Local + Online mismo código | Soportado por las tres; convivencia con el join local a planificar |
| 2–4 → 8 por sala | Sesión pequeña: no requiere servidores dedicados |
| Consola + cross-play | Relay con NAT traversal y transporte certificado por plataforma |
| Presupuesto / equipo | ~$125k total, ~$4,750 tools; 2 devs; ventana ~6 meses (B-1→B-4) → favorece "todo incluido" o gratis con predicción integrada |

---

## 4. Cómo funciona la conexión (conceptos clave acordados)

Resumen del modelo entendido en la sesión, en lenguaje simple:

1. **El jefe (host):** una de las PCs es la autoridad y tiene la **única verdad** del mundo. Los demás (clientes) obedecen. *Esto es el modelo de conexión.*
2. **El mensajero lento (lag/ping):** los mensajes entre PCs tardan (~ida y vuelta). Es el enemigo a ocultar.
3. **Predicción (client-side prediction):** tu PC mueve tu personaje **al instante** al apretar, adivinando que el jefe estará de acuerdo. Hace que cada jugador sienta su propio movimiento instantáneo.
4. **Reconciliación:** cuando llega la verdad del jefe, tu PC **corrige suavemente** solo si la adivinanza fue errónea (p. ej. te patearon o una bomba te empujó). Es "volver a estar de acuerdo con el jefe".
5. **Relay:** solo es el "camino" que conecta las PCs a través de internet/NAT. Es un cartero, **no** decide nada.

`InputPayload` = la nota "apreté tal botón en este tick"; `StatePayload` = "quedé en esta posición". Guardar ese historial es lo que permite re-jugar y corregir en la reconciliación.

---

## 5. Modelo de autoridad — DECISIÓN (Aceptada)

**Decisión: `host-authority` (un jugador hostea la sala), sin servidores dedicados.**

Razones:
- Sesiones pequeñas (2–8): no requieren infraestructura de servidor dedicada.
- Monetización solo cosmética, sin ranked: incentivo de cheating bajo → no se necesita autoridad estricta de servidor.
- Costo mínimo: el relay basta para NAT traversal; no hay que pagar/operar flotas de servidores.

Las tres opciones de framework soportan host mode. Se pueden reservar servidores dedicados para Live Ops si más adelante aparece un modo competitivo o torneos (M6).

---

## 6. Equidad y "ventaja del anfitrión" (host advantage) — análisis y DECISIÓN

**Riesgo identificado:** en host-authority el host tiene ~0 ping y los demás tienen lag. Como el juego es rápido y de interacción física (kicks, head-stomps, empujones), el host puede ganar los **choques disputados** ("¿quién pateó primero?"). La predicción hace que *todos* sientan su propio movimiento instantáneo; la ventaja del host se concentra en las interacciones disputadas, que es justo de lo que vive este juego.

**Opciones para emparejarlo:**

| Cómo | Quién es el "jefe" | Equidad | Costo / dificultad |
|---|---|---|---|
| Solo host-authority | un jugador | el host gana los choques | barato, fácil |
| + Lag compensation | un jugador, pero "rebobina el tiempo" | casi parejo en golpes | ⚠️ **no aplica en 2D** — la lag comp de Fusion solo cubre hitboxes 3D (ver corrección abajo) |
| + Host justo (input en la misma cola) | un jugador | quita el "frame gratis" del host | fácil |
| Servidor dedicado | PC neutral en la nube | el más parejo (nadie tiene 0) | caro + operación |
| Delay igual para todos (rollback estilo peleas) | todos esperan lo mismo | muy parejo | complejo con física y 8P; mal encaje aquí |

**Visión de producto confirmada (16 jun 2026):** el online es **"caos entre amigos" al estilo Party Animals**, NO competitivo ni ranked. En un brawler de caos físico, el desorden es parte del chiste y la "injusticia" del host advantage se diluye en la comedia. La vara real es "¿se siente divertido y responsivo?", no "justo de esports".

**Decisión:** `host-authority + predicción + interpolación + lag compensation ligero en los golpes + host justo (input en la misma cola)`. **Servidores dedicados descartados**, probablemente nunca necesarios; solo reconsiderar si aparece un modo competitivo serio en Live Ops. Esta decisión **no condiciona** la elección de framework (las tres pueden empezar host-authority y migrar modos puntuales a dedicado después).

> ⚠️ **Corrección (jul 2026):** la lag compensation de Fusion 2 solo cubre hitboxes **3D** — **no funciona con los `Collider2D`** del juego (verificado en doc oficial). La parte "lag compensation ligero en los golpes" de esta decisión no es implementable tal cual. La resolución de kicks disputados pasa a ser una **decisión de diseño** (autoritativo en host / emular hitbox 3D en plano fijo / sin compensar); recomendación y opciones en `fusion2-integracion.md` §11. **El resto de la decisión se mantiene** (host-authority + predicción + interpolación + host justo).

---

## 7. Opciones de framework analizadas (las 3 más maduras)

### 7.1 Photon Fusion 2
- **Pros:** salas y matchmaking nativos (room = núcleo de su API); predicción + reconciliación integradas (mapean sobre `InputPayload`/`StatePayload`); relay global incluido; consola certificada (PS4/5, Switch 1/2, Xbox One/Series); tier gratis 100 CCU cubre la beta; docs/soporte sólidos → menor riesgo de cronograma.
- **Contras:** costo por CCU recurrente que escala con el éxito (no baja a $0); menor control de la infraestructura; tooling server-authoritative algo excesivo para 2–8P.
- **Encaje:** muy alto. Apuesta segura #1.

### 7.2 Unity NGO + Unity Gaming Services (Lobby + Relay + Matchmaker)
- **Pros:** first-party (ya tienen Multiplayer Center); salas/relay/matchmaking gestionados por UGS; consola oficial y mejor historia de cross-play a largo plazo; Relay con tier gratis (50 CCU) y precio por egress (barato a baja escala); soporte de Unity.
- **Contras:** predicción NO integrada (se construye a mano, o host-authority + interpolación); más servicios separados que orquestar.
- **Encaje:** alto. Es el que **realmente iguala** a Photon; el costo se paga en trabajo de predicción en vez de licencia.

### 7.3 FishNet (+ relay/matchmaking externos)
- **Pros:** gratuito y open source ($0 framework, sin lock-in); única solución gratuita con predicción de cliente integrada (v4); probado en juegos de física; comunidad activa; soporta consola (según transporte).
- **Contras:** matchmaking y relay NO incluidos (Edgegap/Unity Relay/self-host); soporte comunitario (sin SLA); más trabajo de integración y operación.
- **Encaje:** alto si el presupuesto es la restricción dura. Iguala lo técnico gratis, a cambio de más trabajo propio.

> Nota: **FishNet faltaba** en la lista original del Sprint A-1 (NGO/Mirror/Fusion). Se recomienda añadirlo.

---

## 8. Opciones descartadas

- **Photon Quantum 3:** lockstep determinista; exigiría reescribir toda la física (Physics2D no determinista). Desproporcionado para un party game casual.
- **Photon PUN 2:** legacy, fin de vida para proyectos nuevos.
- **Mirror (puro):** predicción aún inmadura y hay que autohospedar todo; menor encaje que FishNet para acción con física.
- **Steamworks / Facepunch P2P (solo):** sin ruta de consola; no es solución completa.

---

## 9. Modelo de costos (resumen)

**Supuestos:** CCU = jugadores concurrentes en pico; host-authority sin dedicados; ~2 GB egress/CCU/mes (party game de bajo ancho de banda; Photon incluye 3 GB/CCU/mes). Cifras de Photon = tarifa pública; UGS/Edgegap = tarifas verificadas con supuestos (confirmar con el UGS Pricing Estimator al decidir). Los costos de UGS y FishNet+Edgegap **no** incluyen las horas de dev de integración/operación.

| Escenario | CCU | Photon Fusion 2 | NGO + UGS (est.) | FishNet + Edgegap (est.) |
|---|---|---|---|---|
| Beta cerrada | 100 | $0 (tier gratis) | ~$0–50/mes | ~$0–25/mes |
| Lanzamiento modesto | 500 | ~$125/mes (incl. 1.5 TB) | ~$120–180/mes | ~$200/mes |
| Lanzamiento exitoso | 2,000 | ~$500/mes (incl. 6 TB) | ~$500–700/mes | ~$800/mes |
| Hit | 5,000 | ~$2,500/mes ($0.50/CCU) | ~$1,200–1,800/mes | ~$1,400/mes |

> ⚠️ **Matiz (ago 2026):** el supuesto "~2 GB egress/CCU/mes cabe en los 3 GB incluidos" descansa en una **hipótesis de utilización que no está escrita** (CCU promedio ÷ CCU pico). Con una tasa de 8 KB/s por peer y utilización normal de picos tarde/finde, no cabe, y el overage queda en el mismo orden que el plan base. Sensibilidad, consumo por sesión y escenarios comerciales en [fusion2-integracion.md](fusion2-integracion.md) §9.6. **El número está sin medir** — pendiente de FusionStats en el prototipo de Fase 1. La conclusión central de este §9 (el costo de infra no es el factor decisivo) **se mantiene**.

**Lectura:** la beta es gratis/casi gratis en las tres (el tier gratis de Photon la cubre entera). Hasta ~2,000 CCU las tres caben en cientos de USD/mes. A escala "hit" el modelo CCU plano de Photon es el más caro; los modelos por egress escalan más barato (pero ya con ingresos). **Conclusión central:** a esta escala, el costo de infraestructura NO es el factor decisivo; lo es el **tiempo de desarrollo y el riesgo de cronograma** frente al costo recurrente de licencia.

---

## 10. Recomendación

Por orden de preferencia según la restricción que pese más:

1. **Photon Fusion 2** — si prima la velocidad a producción y la mínima fricción técnica (salas + matchmaking nativos, predicción y consola resueltas). Apuesta segura.
2. **NGO + UGS** — si prima la integración first-party y la economía de infraestructura, aceptando construir la predicción.
3. **FishNet** — si prima el presupuesto, aceptando construir/operar matchmaking + relay.

El **modelo de autoridad** (host-authority, sin dedicados) ya está resuelto y es independiente de esta elección.

---

## 11. Plan de validación (Sprint A-1, 15–26 jun 2026)

1. Prototipo de 1 día por opción (**Fusion 2, NGO+UGS, FishNet** — añadir FishNet a la lista): portar 1 jugador + 1 kick + 1 item spawneado a red.
2. Medir: RTT/feel a <150 ms, esfuerzo de integración real, convivencia con el join local.
3. Correr el modelo de costos con la concurrencia objetivo del publisher (sustituir supuestos por cifras reales).
4. Cerrar la decisión de framework con la versión final de este ADR (DoD del Sprint A-1).

---

## 12. Estado de las decisiones

| Decisión | Estado |
|---|---|
| Modelo de autoridad = host-authority (sin dedicados) | ✅ Aceptada |
| Equidad: prediction + interpolation + lag comp + host justo | ✅ Aceptada |
| Servidores dedicados descartados (salvo competitivo futuro) | ✅ Aceptada |
| Quantum / PUN2 / Mirror puro / Steamworks-solo descartados | ✅ Aceptada |
| FishNet añadido a los candidatos de A-1 | ✅ Aceptada |
| Framework final = **Photon Fusion 2** | ✅ Aceptada (15 jul 2026) |
| Alcance: **reconstrucción del core** (simulación única offline/couch/online) | ✅ Aceptada (20 jul 2026) |
| Pollo → **character controller cinemático** (bloques siguen dinámicos/interpolados) | 🔄 Aceptada a nivel técnico; **feel pendiente de validar con design leads** |

---

## 13. Fuentes

- Photon Fusion 2 — Pricing: https://www.photonengine.com/fusion/pricing
- Photon — Multiplayer Pricing Made Simple: https://blog.photonengine.com/multiplayer-pricing-made-simple/
- Photon Fusion 2 — Consoles: https://doc.photonengine.com/fusion/current/consoles/overview
- Unity — Relay Service Pricing: https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced
- Unity — UGS Pricing Estimator: https://unity-player-services-pricing-estimator.ds.unity3d.com/
- FishNet — Features: https://fish-networking.gitbook.io/docs/overview/readme/features
- FishNet — Client-Side Prediction: https://fish-networking.gitbook.io/docs/manual/guides/client-side-prediction
- Edgegap — Pricing: https://edgegap.com/resources/pricing
- KinematicSoup — Unity Multiplayer Comparison: https://www.kinematicsoup.com/blog/reactor-vs-coherence-unity-multiplayer

---

## 14. Historial

- **2026-06-04** — v1.0: análisis inicial, comparativa de frameworks y modelo de costos.
- **2026-06-16** — v1.1: confirmada visión casual "caos entre amigos" (ref. Party Animals); añadido modelo de autoridad (host-authority) y análisis de equidad / host advantage; servidores dedicados descartados.
- **2026-07-15** — v1.2: **DECISIÓN FINAL: Photon Fusion 2** (host mode). Próximos pasos: onboarding del equipo en Fusion 2 (`fusion2-primer.md`), reunión con Photon (pricing/consolas/soporte), dummy session de 2 clientes como validación.
- **2026-07-20** — v1.3: **DECISIÓN DE ALCANCE: reconstruir el core.** La auditoría de código (jul 2026) encontró que la arquitectura "network-ready" del §2 estaba *nombrada, no implementada*. Ante eso, el equipo acepta reconstruir la simulación como **una sola** (offline/couch/online = mismo código), con el pollo pasando a **controller cinemático** y los bloques manteniéndose dinámicos/interpolados. Framework y modelo host-authority **sin cambios**. Detalle técnico y plan por capas en `fusion2-integracion.md` (v2.0). El **GDD no se modifica** (propiedad de los design leads; el online ya se delega a producción en `gdd.md:80`). Correcciones factuales propagadas a `fusion2-primer.md`.
