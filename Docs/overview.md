# 01 — Overview del proyecto

**Versión**: 1.0 | **Última actualización**: 2026-05-16 | **Status**: vigente

---

## El juego

**Don't Chicken Out!** — *Where hesitation means defeat.*

Un party game caótico 2D para **2–4 jugadores** donde la geometría inspirada en Tetris se convierte en el campo de batalla. La cámara sube sin parar; los jugadores escalan, sabotean y se empujan entre sí para no caer fuera de pantalla. Reflejos rápidos, física hilarante y momentos altamente compartibles diseñados desde día uno para streaming y energía de couch multiplayer.

**Estudio**: Raymi Games
**Plataformas**: PC + Console
**Género**: Party / Action / Casual
**Público**: 13–21 años principal · streamers y community-driven secundario
**Lanzamiento target**: **July 2027** (PC & Console)
**Milestone actual**: **Vertical Slice** (May 2026)

---

## Pilares de experiencia

1. **Caos & Sabotaje** — items powerful tipo Smash Bros. + sabotaje de plataformas tipo Ultimate Chicken Horse generan el "just one more game" loop
2. **Accesibilidad inmediata** — controles intuitivos, cualquiera entra y juega sin curva de aprendizaje, alto mastery ceiling
3. **Streamability nativa** — sesiones cortas, momentos virales, reacciones físicas exageradas (Party Animals as reference)
4. **Couch + Online multiplayer** — energía party sin importar si es presencial o remoto
5. **Identidad visual instantánea** — la geometría tipo Tetris hace al juego reconocible en 1 segundo

---

## Bucle de juego principal

```
       ┌─→ ITEM ROTATION (sabotage / boost) ─┐
       │                                      │
       │                                      ▼
CAMERA RISES → CLIMB BLOCKS → PLAYER INTERACT → SURVIVE / FALL
   ▲                                              │
   │                                              ▼
   └───────── ROUND END ←──── LAST PLAYER STANDING / FIRST OUT
                                              │
                                              ▼
                                    POINTS / NEW ROUND
                                              │
                                              ▼
                                  FIRST-TO-N WINS MATCH
```

1. **Climb**: la cámara sube; los jugadores trepan por bloques generados procedurally tipo Tetris
2. **Sabotage**: items aparecen (Bomb, Spring Disc, etc.) que cambian dramáticamente la situación
3. **Player interactions**: kicks, head-stomps, empujones, bloqueo de paths
4. **Eliminación**: caer debajo de cámara = fuera de la ronda
5. **Round-based**: el último en pie gana la ronda
6. **Match**: First-to-5 rounds gana la partida (locked at Beta)

---

## Equipo

> ⚠️ **[TODO: confirmar composición exacta del equipo y dedicación]**. Estructura asumida para planeación inicial:

| Rol | Cantidad | Notas |
|---|---|---|
| Programación | 2 (asumido) | Full time, ~5d/7h por dev. **Confirmar nombres y seniority** |
| Game Design | 1 (asumido) | Part time o full time según fase |
| Arte 2D | 1+ (asumido) | Spine animations + sprites + UI |
| Audio | freelance / contract | SFX + música por fases |
| QA | shared / part time | Sube intensidad en Beta y Launch |

**Budget de referencia (del pitch):** $125,000 USD total — 76.9% team salary, 19.2% contingency buffer, 3.8% tools & licenses.

---

## Capacidad y timeline

| Variable | Valor |
|---|---|
| Inicio Prototype | Oct 2025 |
| Cierre Prototype | Apr 2026 |
| **Vertical Slice (current)** | **May 2026** |
| 🎯 Hito Alpha | Aug – Sep 2026 (fin Sprint A-7) |
| 🎯 Hito Beta (Closed Multiplayer Beta) | Feb – Mar 2027 |
| **🚀 Launch** | **July 2027** (PC + Console) |
| Live Ops | Aug 2027 → ongoing |
| Total semanas calendar desde hoy a Launch | ~60 semanas (~14 meses) |
| Sprints biweekly desde VS hasta Launch | ~30 sprints |
| Feriados Perú a considerar | Multiples — ver `sprintplan.md` |

> Detalle de carga semanal por dev y feriados específicos en `sprintplan.md`.

---

## Scope al lanzamiento (M5 Launch — July 2027)

### Contenido jugable

- **Multiplayer Online**: hasta 4 jugadores, First-to-5 rounds
- **Multiplayer Local**: hasta 4 jugadores, First-to-5 rounds
- **Multiple Game Maps**: varios mapas con geometría y dinámicas distintivas
- **3 Game Modes adicionales** (más allá del core climb-to-survive) — diseñados como foundation para modos post-Launch
- **Custom Gameplay Settings**: house rules — team counts, item pool, modifiers, rotación de mapas, conteo de rondas
- **Skins customizables** + **Skin Shop** (cosméticos, sin impacto en gameplay)
- **Localización**: set de idiomas inicial definido con publisher

### Sistemas

- Plataformero 2D pulido: jump buffer, coyote time, glide-fall, head-stomp, kick, dash
- Round-based match flow con ranking live (Winning / Neutral / Losing)
- Generación de bloques con pooling escalado por rank
- Cámara auto-rise con curva de aceleración
- Item rotation system (Bomb, Spring Disc, Item Capsules + items por agregar)
- Cluck system (mecánica de identidad)
- VFX (feathers, screen-shake, particles)
- Online netcode: matchmaking + lobby + sync de estado + reconciliación
- Account / profile system (cosmetic persistence)
- Backend de tienda + telemetría
- Menu, settings, pause, scene transitions, accessibility options

### Backend & Plataformas

- Netcode framework: **[TODO: lock Q3 2026 — opciones: Netcode for GameObjects / Mirror / Photon Fusion]**
- Account services + cosmetic persistence: TBD con publisher
- Plataformas console específicas: TBD con publisher (Switch / PS5 / Xbox Series)
- Storefront: Steam (PC), platform stores (console)

### Monetización

- **Cosmetic-only Skin Shop** — sin pay-to-win, sin loot boxes
- Skins themed sets, pricing por validar en Beta
- Hooks para colaboraciones IP post-Launch (mismo modular rig)

---

## Out of scope para Launch (planeado para Live Ops)

| Sistema | Estado | Plan post-Launch |
|---|---|---|
| Game modes adicionales (más allá de los 3) | ⏸️ Cortado | Live Ops cadence (mensual / trimestral) |
| IP collaborations & crossovers | ⏸️ Cortado | Live Ops (rig modular ya soporta) |
| Cross-play expandido | ⏸️ Cortado | Live Ops (parity en plataformas adicionales) |
| Tournament tooling completo (replays, observer) | ⏸️ Cortado | Live Ops |
| Mobile port | ⏸️ Cortado | Evaluación post-Launch según tracción |
| Seasonal / event content | ⏸️ Cortado | Live Ops (calendario trimestral) |

---

## Hitos del proyecto

| Hito | Fecha objetivo | Definición |
|---|---|---|
| **M1 Prototype** | ✅ Oct 2025 – Apr 2026 | Loop core jugable: climb + camera rise + first sabotage primitives |
| **M2 Vertical Slice** | 🟢 May 2026 | 1 mapa pulido, 2–4 local, item rotation, demo de 5 min para publishers |
| **M3 Alpha** | ⏳ Aug – Sep 2026 | Feature-complete local, 4P estable, build tour para publishers & showcases |
| **M4 Beta — Closed MP Beta** | 🔜 Feb – Mar 2027 | Online + Local 4P (First-to-5), Skins, Skin Shop, closed beta en Steam |
| **🚀 M5 Launch** | 🎯 July 2027 | Custom Settings, Multiple Maps, 3 Game Modes, PC & Console release |
| **M6 Live Ops** | 🔁 Aug 2027 → | Modos adicionales, IP collabs, cross-play, eventos seasonal |

Detalle completo en [Roadmap.md](Roadmap.md).

---

## Métricas de éxito

### M2 Vertical Slice (publisher demo)
- ≥ 80% de first-time playtesters piden rematch
- Sesión promedio sin asistencia ≥ 8 minutos
- 60 FPS estable en laptops gama media

### M3 Alpha (publisher tour)
- Demos sin intervención del dev en showcases
- Crash rate < 1 por cada 30 sesiones en QA interno
- Build stream-safe (sin debug overlays, sin spoilers)

### M4 Beta (Closed Multiplayer Beta)
- < 150 ms median input-to-display en partidas 4P online
- D1 retention ≥ 40% en cohort closed beta
- Telemetría de shop valida assumptions de pricing

### 🚀 Launch (July 2027)
- 4P online estable concurrente en todas las plataformas soportadas
- Console cert pass en primera o segunda submission
- Day-1: zero P0 issues, < 5 P1

### Live Ops (post-Launch)
- MAU sostenido 3+ meses post-launch
- Repeat purchase rate ≥ benchmark de party-games
- Cross-play parity en latencia y matchmaking

---

## Stack tecnológico

| Capa | Tecnología | Versión confirmada |
|---|---|---|
| Engine | Unity | **6000.3.9f1** (Unity 6 LTS) |
| Lenguaje | C# | .NET Standard 2.1 |
| Render Pipeline | URP | incluido |
| Input | Unity Input System | 1.18.0 |
| Cámara | Cinemachine | 3.1.5 |
| 2D Pipeline | Unity 2D Feature (Animation, Aseprite, SpriteShape, PixelPerfect, Tilemap) | 2.0.2 |
| Multiplayer (decisión pendiente) | Multiplayer Center package | 1.0.1 (selector instalado, framework por elegir) |
| Tweening | DOTween | última estable |
| Source control | Git | — |

> ⚠️ **Decisión pendiente Q3 2026:** netcode framework (Netcode for GameObjects / Mirror / Photon Fusion). Determina arquitectura de M4 Beta.

### Backend a definir con publisher

- Account services (Steam-first vs. cross-platform account)
- Telemetría (GameAnalytics / Firebase Analytics / propio)
- Storefront del cosmetic shop (Steam Inventory vs. backend propio)
- Crash reporting (Sentry / Crashlytics / Bugsnag)

---

## Marketing & showcases

| Evento | Fecha | Estado |
|---|---|---|
| Pixel Play | Nov 22, 2025 | ✅ Participado (durante Prototype) |
| Neonet Fest | Feb 22, 2026 | ✅ Participado |
| Peru is Key! — CVA | Mar 10, 2026 | ✅ Participado |
| Steam Next Fest | Feb 2027 (target) | 🎯 Beta reveal window |
| Steam Next Fest | Jun 2027 (target) | 🎯 Pre-Launch window |
| Publisher demos | Vertical Slice / Alpha tour | 🟢 Ongoing |

---

## Documentos relacionados

- [Roadmap.md](Roadmap.md) — roadmap publisher-facing con milestones, critical path, touchpoints
- [Roadmap.pptx](Roadmap.pptx) — versión deck del roadmap
- [sprintplan.md](sprintplan.md) — plan biweekly sprint-by-sprint hasta Launch
- *(pendientes)* `systems.md`, `architecture.md`, `risks-log.md`, `decisions-log.md`

---

## Cambios pendientes / TODOs principales

- [ ] Confirmar composición exacta del equipo (programación, diseño, arte, audio, QA) y dedicación por persona
- [ ] Cerrar selección de netcode framework antes de Q3 2026
- [ ] Definir plataformas console exactas con publisher (Switch / PS5 / Xbox)
- [ ] Definir set de idiomas para localización
- [ ] Definir pricing tiers del cosmetic shop
- [ ] Confirmar feriados Perú 2026 y 2027 que afecten capacidad
