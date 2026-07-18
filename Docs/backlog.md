# Backlog priorizado — matriz Player Value × Production Feasibility

**Fuente:** pizarra de priorización del equipo (jul 2026). Este archivo reemplaza las capturas de la pizarra en sesiones futuras — actualizarlo aquí cuando la pizarra cambie.

**Leyenda de áreas (color de nota):** 🟩 verde = Programación · 🟨 amarillo = Diseño · 🟪 morado = UX · 🌸 rosado = Arte · 🟦 cyan = Sonido
**Iniciales de responsables:** M, A, R, P, Q, C (M = Mateo, design lead; resto por confirmar).

---

## Matriz (resumen)

### Cuadrante 1 — Alto valor + alta factibilidad → EN EJECUCIÓN
| Item | Área |
|---|---|
| Steam Remote | 🟩 Progra |
| Lobby de Pollos | 🟩 Progra |
| Más items (7–10) | 🟨 Diseño |
| Difficulty progression + landmarks | 🟨 Diseño |
| Dirección de arte | 🌸 Arte |
| Controles | 🟩 Progra |
| Personalización de pollitos | 🌸 Arte |
| Implementación de FMOD | 🟦 Sonido |

### Cuadrante 2 — Alto valor, baja factibilidad (grandes apuestas, después)
🟨 2 Mapas + eventos · 🟨 3 modos de juego · 🟨 Reevaluar sistema de construcción · 🟨 Mecánica de colocar items · 🟨 Movement set más completo (wall jump, high jump, head break, etc.) · 🟨 Post-death

### Cuadrante 3 — Menor valor, alta factibilidad (quick wins de relleno)
🟨 Block effects · 🌸 Animaciones · 🌸 Accesorización básica (sombreros/accesorios) · 🟩 Settings de partidas · 🟩 Settings del juego en juego

### Cuadrante 4 — Menor valor, baja factibilidad (última prioridad)
🟨 Patadas en diferentes direcciones · 🟨 Patada cargada* · 🟪 Accesibilidad

> *Nota: "Patada cargada" ya existe en el GDD (kick con power bar); en la pizarra aparece como pendiente de implementación/iteración.

---

## Cuadrante 1 — desglose ordenado (cada paso desbloquea el siguiente)

### Más items (7–10) 🟨
1. Diseño de ítems 🟨
2. Implementación de items 🟩
3. Testeo de ítems 🟨
4. SFX item 🟦
5. Asset del ítem 🌸 → Implementación dentro del juego 🟩

### Lobby de Pollos 🟩
1. PANTALLA (diseño de pantalla) 🟪
2. Sistema de selección 🟩
3. Sistema de personalización de player 🟩
4. PANTALLA (segunda pantalla) 🟪

### Difficulty progression + landmarks 🟨
1. Diseñar 🟨
2. Implementar 🟩
3. Testear 🟨

### Dirección de arte 🌸
1. Investigación de juegos similares 🌸
2. Art bible 🌸
3. Concept art 🌸
4. Participación de diseño 🟨

### Controles 🟩
1. Controles base (botones) 🟪 → Control 🟪 + Teclado 🟪
2. Pantalla para personalizar los controles 🟪
3. Sistema de personalización de los controles 🟩

### Personalización de pollitos 🌸
5. Assets de cabecita de pollo 🌸
6. Animación de la cabecita del pollo 🌸
7. Implementación 🟩

### Sin desglose (tarea única)
- Implementación de FMOD 🟦
- Steam Remote 🟩

---

## Pendientes de ESTA semana (semana del 13 jul 2026)

| Tarea | Responsable | Área |
|---|---|---|
| Diseño de ítems | M + A | 🟨 Diseño |
| PANTALLA de Lobby | R | 🟪 UX |
| Diseñar Difficulty | M + A | 🟨 Diseño |
| Investigación de juegos similares | P | 🌸 Arte |
| Controles base (botones): Control + Teclado | R | 🟪 UX |
| Implementación de FMOD | A + Q | 🟦 Sonido |
| Steam Remote | A | 🟩 Progra |
| Estudiar Photon | C | 🟩 Progra/Online |

> ✅ **Actualización 15 jul 2026:** el equipo decidió **Photon Fusion 2** como framework de netcode. "Estudiar Photon" pasa a ser onboarding del equipo en Fusion 2 (ver `fusion2-primer.md`). ADR-0001 actualizado.
