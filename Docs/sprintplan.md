# 06 — Sprint Plan (Vertical Slice → Launch)

**Versión:** 1.0 · **Última actualización:** 2026-05-16 · **Status:** vigente

Plan biweekly desde el cierre de **Vertical Slice** (hoy) hasta **Launch en July 2027**. ~30 sprints distribuidos en 4 milestones (M2 wrap, M3 Alpha, M4 Beta, M5 Launch) + Live Ops post-launch.

---

## 📋 Resumen ejecutivo

| Variable | Valor |
| --- | --- |
| Inicio plan | Lunes 18 mayo 2026 |
| 🎯 Hito Alpha | Vie 18 sep 2026 (fin Sprint A-7) |
| 🎯 Hito Beta (Closed MP Beta) | Mar 2027 (fin Sprint B-12) |
| 🚀 Launch | Jul 2027 (fin Sprint L-9, target Jul 22-30) |
| Duración total | ~60 semanas calendar / ~30 sprints biweekly |
| Equipo | **[TODO: confirmar]** — asumido 2 devs + 1 design + arte/audio freelance |
| Capacidad efectiva (asunción 2 devs) | ~56h equipo/semana / ~112h por sprint biweekly |
| Estrategia | Vertical Slice polish → Alpha publisher tour → Online netcode → Content + cert |

> ⚠️ Capacidad y carga por dev son **placeholders**. Recalcular cuando se confirme equipo. Las decisiones de scope dentro de cada milestone YA están alineadas con `Roadmap.md`.

---

## 📊 Estado de avance

**Snapshot:** 2026-05-16 · Vertical Slice en cierre, pre-Alpha.

### Leyenda de estado

| Marca | Significado |
| --- | --- |
| ✅ Listo | Implementado y verificado |
| 🟢 En curso | Sprint activo |
| 🔄 Parcial | Parcialmente implementado — falta cerrar alcance |
| ⬜ Pendiente | No iniciado |

### Rollup por milestone

| Milestone | Estado | Detalle |
| --- | --- | --- |
| M1 — Prototype | ✅ Listo | Oct 2025 – Apr 2026. Loop core jugable, plataformero, sabotage primitives |
| **M2 — Vertical Slice** | 🟢 En curso | 1 mapa pulido, 2–4 local, item rotation, demo de 5 min publisher-ready |
| M3 — Alpha | ⬜ Pendiente | Feature-complete local, build tour para showcases |
| M4 — Beta (Closed MP Beta) | ⬜ Pendiente | Online + Local 4P, Skins, Skin Shop, closed beta Steam |
| M5 — Launch | ⬜ Pendiente | Custom Settings, Multiple Maps, 3 Game Modes, certs |
| M6 — Live Ops | ⬜ Pendiente | Post-Launch (Aug 2027+) |

### Sistemas ya en código (verificado a 2026-05-16)

- ✅ `GameManager` con state machine (Menu / Prepare / Game / Win) y round flow
- ✅ `PlayersManager` con join por teclado split + gamepad (2–4P local)
- ✅ `PlayerMovement` pulido (jump buffer, coyote, glide, head-stomp, fall multiplier)
- ✅ `CinemachineVerticalRig2D` con auto-rise + acceleration curve
- ✅ Block pooling escalado por rank (Winning/Neutral/Losing)
- ✅ Items: Bomb (radio explosión), Spring Disc, Item Capsules, Horizontal Spawner
- ✅ VFX feather particle system, Cluck system, audio manager
- ✅ Main menu, scene transitions, pause flow

---

## ⚖️ Escala de esfuerzo

| Nivel | Tiempo | Avg horas |
| --- | --- | --- |
| 1 | menos de 1 hora | 0.6h |
| 2 | 1 a 2 horas | 1.5h |
| 3 | 2 a 4 horas | 3.0h |
| 4 | 4 a 6 horas | 5.0h |
| 5 | día laboral completo | 7.0h |
| XL | 2-3 días | 16-20h |

---

## 📅 Calendario macro (30 sprints biweekly)

> Sprints lunes-viernes, 2 semanas cada uno. Holidays Perú a confirmar por año.

### M2 Vertical Slice (wrap) — 2 sprints

| Sprint | Fechas | Goal | Hito |
| --- | --- | --- | --- |
| VS-1 | 18-29 may 2026 | Polish VS, fix feedback de últimos playtests, build publisher-ready | — |
| VS-2 | 1-12 jun 2026 | Sizzle trailer + fact sheet + final VS build firmado | 🎯 **M2 Done** |

### M3 Alpha — 7 sprints

| Sprint | Fechas | Goal | Hito |
| --- | --- | --- | --- |
| A-1 | 15-26 jun 2026 | Netcode tech research + decisión locked + modular rig design | 🚨 Netcode lock |
| A-2 | 29 jun - 10 jul 2026 | Item roster expansion (≥ 4 items balanced) | — |
| A-3 | 13-24 jul 2026 | Difficulty pooling refinement + Settings menu (audio/video/controls) | — |
| A-4 | 27 jul - 7 ago 2026 | Localization scaffolding (EN/ES-LATAM) + telemetry hooks | — |
| A-5 | 10-21 ago 2026 | Closed demo build polish + press kit v1 | — |
| A-6 | 24 ago - 4 sep 2026 | Bug fixing intensivo + stream-safe build pass | — |
| A-7 | 7-18 sep 2026 | Alpha gate + demo tour build firmado | 🎯 **M3 Alpha Done** |

### M4 Beta (Closed Multiplayer Beta) — 12 sprints

| Sprint | Fechas | Goal | Hito |
| --- | --- | --- | --- |
| B-1 | 21 sep - 2 oct 2026 | Netcode integration: foundation (transport, conexión, sync básico) | — |
| B-2 | 5-16 oct 2026 | Lobby system + matchmaking básico | — |
| B-3 | 19-30 oct 2026 | Online round flow + sync de items + reconciliación | — |
| B-4 | 2-13 nov 2026 | Online 4P estable + lag compensation | — |
| B-5 | 16-27 nov 2026 | Modular character rig (foundation para skins + IP collabs) | — |
| B-6 | 30 nov - 11 dic 2026 | Skins system (data + apply + persistence) | — |
| B-7 | 14-25 dic 2026 | Account / profile system (reduced — holidays) | ⚠️ Holidays |
| B-8 | 28 dic - 8 ene 2027 | Skin Shop UI + catalog + pricing rails (reduced — holidays) | ⚠️ Holidays |
| B-9 | 11-22 ene 2027 | Backend services (matchmaking, persistence, telemetry) | — |
| B-10 | 25 ene - 5 feb 2027 | Telemetría dashboards + closed beta deployment infra | — |
| B-11 | 8-19 feb 2027 | **Steam Next Fest reveal** + closed beta cohort onboarding | 🎯 Beta reveal (Next Fest) |
| B-12 | 22 feb - 5 mar 2027 | Closed beta corriendo, feedback + balance pass via Remote Config | 🎯 **M4 Beta Done** |

### M5 Launch — 9 sprints

| Sprint | Fechas | Goal | Hito |
| --- | --- | --- | --- |
| L-1 | 8-19 mar 2027 | Custom Gameplay Settings (item pool, modifiers, rotation) | — |
| L-2 | 22 mar - 2 abr 2027 | Multiple Game Maps — mapa 2 implementado | — |
| L-3 | 5-16 abr 2027 | Multiple Game Maps — mapas 3 y 4 implementados | — |
| L-4 | 19-30 abr 2027 | Game Mode 2 implementado | — |
| L-5 | 3-14 may 2027 | Game Mode 3 implementado | — |
| L-6 | 17-28 may 2027 | Game Mode 4 implementado + balance pass entre modos | — |
| L-7 | 31 may - 11 jun 2027 | Console platform adaptation + first cert submission | 🚨 Console cert |
| L-8 | 14-25 jun 2027 | Full localization push + **Steam Next Fest pre-launch** | 🎯 Pre-Launch Next Fest |
| L-9 | 28 jun - 16 jul 2027 (3-week) | Launch QA, gold master, storefront pages, launch trailer | 🚀 **LAUNCH** |

### M6 Live Ops — ongoing

| Cadencia | Fechas | Goal |
| --- | --- | --- |
| LO sprints biweekly | Aug 2027 → | Modos adicionales, IP collabs (cosmetic drops), seasonal events, cross-play expansion |

---

## 🛠️ Convenciones del plan

### Tracks paralelos *(asumido — confirmar con equipo final)*

| Track | Owner | Foco |
| --- | --- | --- |
| Track A | Dev 1 (lead) | Sistemas core, arquitectura, netcode, backend integration |
| Track B | Dev 2 | Features de gameplay, UI, contenido, integración |

Ambos integran en `develop` semanalmente, branches por feature/sprint.

### Definition of Done por sprint

- [ ] Todos los tasks del sprint cerrados (PRs mergeados a `develop`)
- [ ] Tests críticos pasando (input handling, netcode roundtrip cuando aplique)
- [ ] Sin regresiones en features previas (smoke test manual en 4P local)
- [ ] Build instalable en PC (y consoles desde M5 L-7)
- [ ] Tag git por hito mayor (v0.X.Y)
- [ ] Notas de sprint actualizadas

### Daily standup

- 15 minutos máx
- Ayer / hoy / bloqueos
- 1 sesión semanal de code review pareado

### Code review

- PRs ≤ 400 líneas idealmente
- Review en 24h máx
- No merge a `develop` sin aprobación del otro dev

---

## 🏁 Sprint VS-1 — Vertical Slice polish

**Fechas:** Lun 18 – Vie 29 may 2026 · **Días:** 10 · **Capacidad:** ~112h equipo (placeholder)

> 🎯 **Goal:** Cerrar el Vertical Slice a calidad de publisher demo. Eliminar P0/P1 bugs detectados en los últimos playtests internos. Build firmado y reproducible.

### Track A (Dev 1)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| A1 | Audit completo de bugs abiertos en VS + triage P0/P1/P2 | 3 | ⬜ |
| A2 | Fix P0 bugs identificados en audit | XL | ⬜ |
| A3 | Estabilizar round flow → win condition edge cases (ties, simultaneous deaths) | 4 | ⬜ |
| A4 | Performance pass: locked 60 FPS en laptops gama media | 4 | ⬜ |
| A5 | Pulir item rotation balance (Bomb, Spring Disc, Item Capsules) | 3 | ⬜ |
| A6 | Documentar arquitectura actual en `architecture.md` (foundation para Alpha) | 3 | ⬜ |

### Track B (Dev 2)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| B1 | Fix P1 bugs identificados en audit | XL | ⬜ |
| B2 | UI polish: round results panel, pause menu, main menu | 4 | ⬜ |
| B3 | Audio polish: mixing, volume balance, miss SFX coverage | 3 | ⬜ |
| B4 | Feather VFX polish + screen-shake pass | 2 | ⬜ |
| B5 | Capture build de gameplay clips para sizzle trailer (VS-2) | 2 | ⬜ |
| B6 | Press kit draft v0 (fact sheet ya existe del Follow-up Pitch) | 2 | ⬜ |

### Definition of Done

- [ ] Tag `v0.2.0-VS-RC1`
- [ ] 0 P0 bugs abiertos
- [ ] ≤ 3 P1 bugs abiertos (con plan de fix para VS-2)
- [ ] 60 FPS estable en laptop gama media de referencia
- [ ] Build firmado entregable a stakeholders
- [ ] Notas de sprint cerradas

### Entregables externos esperados

- **Arte:** revisión final del mapa hero + sprites de items
- **Audio:** pase de mixing si hay tracks pendientes

---

## 🎬 Sprint VS-2 — Vertical Slice ship

**Fechas:** Lun 1 – Vie 12 jun 2026 · **Días:** 10 · **Capacidad:** ~112h equipo

> 🎯 **Goal:** Sizzle trailer + press kit completos. Build VS firmado v1.0. Demo lista para enviar a publishers.

### Track A (Dev 1)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| A1 | Cierre de P1 bugs remanentes de VS-1 | 5 | ⬜ |
| A2 | Telemetry hooks básicos (session length, item usage, round duration) — foundation para Alpha | 4 | ⬜ |
| A3 | Build script reproducible (CI ready) — fundacional para Alpha+ | 3 | ⬜ |
| A4 | Iniciar evaluación de netcode frameworks (lectura, prototipos iniciales — full lock en A-1) | 4 | ⬜ |
| A5 | Documentar onboarding técnico para nuevo dev (preparación para escalar equipo en Alpha) | 2 | ⬜ |

### Track B (Dev 2)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| B1 | Sizzle trailer 60s — corte, música, motion | XL | ⬜ |
| B2 | Press kit v1: screenshots, gifs, logos, fact sheet, build instructions | 4 | ⬜ |
| B3 | Storefront page placeholder Steam (no live aún) | 2 | ⬜ |
| B4 | UX review interno con tester externo no-dev | 2 | ⬜ |
| B5 | Build firmado Windows + smoke test en 3+ máquinas distintas | 2 | ⬜ |

### Definition of Done — **🎯 M2 Vertical Slice Done**

- [ ] Tag `v0.2.0-VS-FINAL`
- [ ] Build VS firmado entregable a publishers
- [ ] Sizzle trailer subido (YouTube + portfolio)
- [ ] Press kit zip listo para distribuir
- [ ] KPIs validados: ≥ 80% rematch rate en playtests, sesión ≥ 8 min, 60 FPS gama media
- [ ] Aprobación interna para iniciar M3 Alpha

---

## 🚀 Sprint A-1 — Alpha kickoff: Netcode lock

**Fechas:** Lun 15 – Vie 26 jun 2026 · **Días:** 10 · **Capacidad:** ~112h equipo

> 🎯 **Goal:** **DECISIÓN LOCK** del netcode framework. Modular rig design aprobado. Bases puestas para todo M4 Beta.

> 🚨 **Decisión crítica del proyecto.** Este sprint es el más importante del año desde el punto de vista técnico.

### Track A (Dev 1)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| A1 | Prototipos comparativos: Netcode for GameObjects vs. Mirror vs. Photon Fusion (1 día c/u con scene de prueba) | XL | ⬜ |
| A2 | Matriz de evaluación: cost, latency, console support, community, learning curve | 3 | ⬜ |
| A3 | **DECISIÓN LOCK + ADR escrito** | 2 | ⬜ |
| A4 | Setup del framework elegido en `develop` con dummy session | 4 | ⬜ |

### Track B (Dev 2)

| # | Tarea | Effort | Estado |
| --- | --- | --- | --- |
| B1 | Modular character rig design — separar mesh/material/animation para soportar skins futuras | 5 | ⬜ |
| B2 | Item roster planning: 4+ items balanceados para Alpha (kick variations, defense items) | 3 | ⬜ |
| B3 | Telemetry hooks v2: round outcomes, item usage breakdown | 3 | ⬜ |
| B4 | Public showcase research — slots disponibles entre A-2 y A-7 | 2 | ⬜ |

### Definition of Done

- [ ] Tag `v0.3.0-alpha-kickoff`
- [ ] ADR de netcode framework merged a `docs/`
- [ ] Modular rig design doc aprobado
- [ ] Dummy online session conectando 2 clientes (mismo machine OK)

---

## 📦 Sprints A-2 → A-7 — Alpha build (outline)

> Detalle se completa al inicio de cada sprint. Goals predefinidos a continuación.

### A-2 (29 jun – 10 jul 2026) · Item roster expansion

**Goal:** ≥ 4 items balanceados con feel, FX, audio. Sabotage vs defense rotation.

**Track A:** netcode foundation continuation, server-authoritative item spawning prep
**Track B:** 2 nuevos items (sabotage + defense), balance pass con playtest

### A-3 (13 – 24 jul 2026) · Difficulty pooling + Settings menu

**Goal:** Refinar el block pooling escalado por rank. Settings menu completo (audio/video/controls/accessibility).

**Track A:** difficulty curve tuning, perf profiling
**Track B:** Settings UI, control remapping, accessibility (subtitles, color blind, etc.)

### A-4 (27 jul – 7 ago 2026) · Localization + telemetry

**Goal:** Localization scaffolding (EN base, ES-LATAM, indirección i18n lista para añadir idiomas en M5). Telemetry hooks v3.

**Track A:** i18n architecture, key-based string system, ES-LATAM pass
**Track B:** telemetry v3 (funnel, retention proxies, item usage)

### A-5 (10 – 21 ago 2026) · Closed demo build polish

**Goal:** Build "tour-ready". Press kit v2. Demo distribuible sin asistencia.

**Track A:** signed build pipeline, demo configuration (lock to demo map, disable spoilers)
**Track B:** press kit v2 (updated screenshots, trailer with real Alpha footage)

### A-6 (24 ago – 4 sep 2026) · Bug fixing intensivo

**Goal:** Crash rate < 1 / 30 sesiones en QA interno. Stream-safe build.

**Track A:** systematic P1 fix pass
**Track B:** UX review, debug overlay cleanup, streamer-safe audit

### A-7 (7 – 18 sep 2026) · 🎯 Alpha gate

**Goal:** **M3 Alpha Done.** Build firmado para demo tour publishers + showcases.

**Track A:** final integration pass, telemetry dashboard for showcase tracking
**Track B:** showcase materials (booth deck, looping trailer, demo flow card)

### Definition of Done — **🎯 M3 Alpha Done**

- [ ] Tag `v0.3.0-alpha-final`
- [ ] Build tour-ready firmado
- [ ] Crash rate < 1 per 30 sessions
- [ ] Localization EN + ES-LATAM funcional
- [ ] Stream-safe (sin overlays, sin spoilers)
- [ ] Press kit v2 distribuido

---

## 🌐 Sprints B-1 → B-12 — Beta (Closed Multiplayer Beta)

> Detalle de cada sprint se cierra al inicio. Goals macro:

### Netcode foundation (B-1 → B-4)

- **B-1 · 21 sep – 2 oct 2026** · Foundation: transport, connection management, basic sync
- **B-2 · 5 – 16 oct 2026** · Lobby system + matchmaking básico
- **B-3 · 19 – 30 oct 2026** · Online round flow + item sync + reconciliación
- **B-4 · 2 – 13 nov 2026** · Online 4P estable + lag compensation + edge cases

### Skins & Shop (B-5 → B-8)

- **B-5 · 16 – 27 nov 2026** · Modular character rig implementation (built on A-1 design)
- **B-6 · 30 nov – 11 dic 2026** · Skins system: data, apply, persistence, preview
- **B-7 · 14 – 25 dic 2026** · ⚠️ Holidays · Account / profile system (capacidad reducida)
- **B-8 · 28 dic – 8 ene 2027** · ⚠️ Holidays · Skin Shop UI + catalog (capacidad reducida)

### Backend & deployment (B-9 → B-12)

- **B-9 · 11 – 22 ene 2027** · Backend services: matchmaking infra, persistence, telemetry
- **B-10 · 25 ene – 5 feb 2027** · Telemetría dashboards + closed beta deployment infra
- **B-11 · 8 – 19 feb 2027** · 🎯 **Steam Next Fest reveal** + closed beta cohort onboarding
- **B-12 · 22 feb – 5 mar 2027** · 🎯 **M4 Done**: closed beta corriendo, feedback + balance pass

### Definition of Done — **🎯 M4 Beta Done**

- [ ] Tag `v0.4.0-beta-final`
- [ ] < 150 ms median input-to-display 4P online
- [ ] D1 retention ≥ 40% en cohort closed beta
- [ ] Skin Shop con telemetría de conversión válida
- [ ] Crash rate < 1 / 50 sesiones en beta cohort

---

## 🎨 Sprints L-1 → L-9 — Launch

### Content expansion (L-1 → L-6)

- **L-1 · 8 – 19 mar 2027** · Custom Gameplay Settings (item pool, modifiers, rotation)
- **L-2 · 22 mar – 2 abr 2027** · Map 2 (full implementation + balance)
- **L-3 · 5 – 16 abr 2027** · Maps 3 + 4
- **L-4 · 19 – 30 abr 2027** · Game Mode 2 implementation
- **L-5 · 3 – 14 may 2027** · Game Mode 3 implementation
- **L-6 · 17 – 28 may 2027** · Game Mode 4 implementation + cross-mode balance

### Pre-Launch (L-7 → L-9)

- **L-7 · 31 may – 11 jun 2027** · 🚨 Console platform adaptation + **first cert submission**
- **L-8 · 14 – 25 jun 2027** · 🎯 **Steam Next Fest pre-launch** + full localization push
- **L-9 · 28 jun – 16 jul 2027** *(3-week sprint)* · Launch QA, gold master, storefront pages, launch trailer
  - **Lun 28 jun – Vie 9 jul:** Gold master candidate, P0/P1 fixes
  - **Lun 12 – Vie 16 jul:** 🚀 **LAUNCH WEEK** (target dates a coordinar con publisher)

### Definition of Done — **🚀 Launch**

- [ ] Tag `v1.0.0`
- [ ] Live en Steam (PC) + plataformas console acordadas
- [ ] 0 P0 issues, < 5 P1 abiertos
- [ ] Cert pass en 1ra o 2da submission por plataforma
- [ ] Launch trailer live
- [ ] Storefront pages aprobadas
- [ ] Equipo monitoreando 24h primer día
- [ ] Hotfix build pre-listo

---

## 🔁 M6 Live Ops — Aug 2027 →

**Cadencia:** Sprints biweekly continuos.

**Streams de contenido (cadencia objetivo):**

- **Modos adicionales:** 1 nuevo modo cada 2–3 meses
- **Cosmetic drops:** quincenal o mensual via Skin Shop
- **IP collaborations:** trimestral
- **Seasonal events:** 4 al año (Halloween, December, Spring, Summer)
- **Cross-play expansion:** según prioridad de plataformas con publisher
- **Community tournament tooling:** presets, observer mode, replays

---

## 🏆 Hitos clave

| Hito | Sprint | Fecha objetivo | Estado |
| --- | --- | --- | --- |
| Prototype done | — | Apr 2026 | ✅ Listo |
| Vertical Slice publisher-ready | VS-2 | Vie 12 jun 2026 | 🟢 En curso |
| **🚨 Netcode decision lock** | A-1 | Vie 26 jun 2026 | ⬜ Pendiente |
| Modular rig design aprobado | A-1 | Vie 26 jun 2026 | ⬜ Pendiente |
| 🎯 **M3 Alpha Done** | A-7 | Vie 18 sep 2026 | ⬜ Pendiente |
| Online 4P estable | B-4 | Vie 13 nov 2026 | ⬜ Pendiente |
| Skin Shop funcional | B-8 | Vie 8 ene 2027 | ⬜ Pendiente |
| 🎯 **Steam Next Fest reveal** | B-11 | Feb 2027 | ⬜ Pendiente |
| 🎯 **M4 Beta Done** | B-12 | Vie 5 mar 2027 | ⬜ Pendiente |
| Console first cert submission | L-7 | Vie 11 jun 2027 | ⬜ Pendiente |
| 🎯 Steam Next Fest pre-launch | L-8 | Jun 2027 | ⬜ Pendiente |
| 🚀 **LAUNCH** | L-9 | Jul 2027 (target Jul 22-30) | ⬜ Pendiente |

---

## 📊 Carga por dev (placeholder, recalcular con equipo final)

> ⚠️ Cálculos basados en 2 devs full time 5d/7h @ 80% eficiencia → 28h efectivas/dev/semana → 56h efectivas/sprint/dev (biweekly).
> Sobrecarga aceptable: ±10% por sprint. Sobrecarga > 15% → revisar scope.

| Sprint | Dev 1 carga | Dev 2 carga | Capacidad / dev | Status |
| --- | --- | --- | --- | --- |
| VS-1 | TBD | TBD | 56h | Pendiente estimación |
| VS-2 | TBD | TBD | 56h | Pendiente estimación |
| A-1 | TBD | TBD | 56h | Pendiente estimación |
| A-2..A-7 | TBD | TBD | 56h | Outline only |
| B-1..B-12 | TBD | TBD | 56h | Outline only |
| L-1..L-9 | TBD | TBD | 56h | Outline only |

---

## ⚠️ Riesgos del plan

> Detalle completo cuando exista `risks-log.md`.

| Riesgo | Sprint afectado | Mitigación |
| --- | --- | --- |
| 🚨 Netcode lock tardío | A-1 → todo M4 Beta | Investigación arranca en VS-2 (A4), decisión hard-locked al cierre de A-1 |
| Arte de skins no escala con catalog del Shop | B-6 → B-8 | Modular rig en B-5 estandariza pipeline de drops |
| Console kit access tarda (publisher-driven) | L-7 cert | Coordinar con publisher en M3 Alpha, no esperar a L-7 |
| Localization no cabe en L-8 | L-8 | Indirección i18n implementada en A-4, traducciones acumuladas durante Beta |
| Feriados Perú reducen capacidad | B-7, B-8 | Holiday sprints planeados con scope reducido (10-15% menos) |
| Bugs late-stage descubiertos en Beta | B-11, B-12 | Triage P0/P1 con remote config para hotfix de balance |
| Steam Next Fest slot no asignado | B-11 / L-8 | Aplicar 4 meses antes, coordinar con publisher |
| 🚨 Equipo insuficiente para scope | Todo | TODO: confirmar headcount, escalar si flag de overload se sostiene 2 sprints |

---

## 📚 Documentos relacionados

- [overview.md](overview.md) — visión y scope del proyecto
- [Roadmap.md](Roadmap.md) — roadmap publisher-facing
- [Roadmap.pptx](Roadmap.pptx) — versión deck del roadmap
- *(pendientes)* `systems.md`, `architecture.md`, `risks-log.md`, `decisions-log.md`, `progress-tracking.md`

---

## TODOs principales (no de scope, de plan)

- [ ] Confirmar tamaño de equipo y recalcular capacidad por sprint
- [ ] Confirmar feriados Perú 2026 y 2027 que afecten cada sprint
- [ ] Validar fechas Steam Next Fest 2027 (Feb y Jun) al abrir aplicaciones
- [ ] Confirmar plataformas console exactas (afecta L-7 cert window)
- [ ] Setup `progress-tracking.md` con plantilla semanal
- [ ] Revisar plan al cierre de cada milestone (VS-2, A-7, B-12, L-9)
