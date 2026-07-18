# GDD condensado — Don't Chicken Out!

**Fuente:** `GDD - Don't Chicken Out.docx.pdf` v4.4.0 (14/06/2026) · Este resumen reemplaza la lectura del PDF en sesiones de trabajo. Solo consultar el PDF original para imágenes (control scheme, block catalogue, blueprint) o si esta versión quedó desactualizada.
**Editores autorizados del GDD:** Mateo Cayo y Liliana Bravo (design leads); otros requieren aprobación.

---

## Concepto

Juego de acción 2D / party game donde pollos esponjosos combaten apilando bloques tipo Tetris para atrapar al oponente y no caer fuera de pantalla. Se gana la partida ganando la mayoría de rondas. Al colocar bloques: preview de posición, rotación implícita por orientación, y push estratégico tras colocar.

**Banner aesthetic:** *"Building Bold!"* — combate audaz y juguetón basado en construcción rápida, no en pelea tradicional. Tono colorido, alegre, vibrante.

## Metas del proyecto

1. **Accesibilidad**: entrar a una partida al instante, sin tutoriales complejos.
2. **Rejugabilidad**: cada partida se siente fresca (combinaciones inesperadas, interacciones quirky).
3. **Diversión rápida**: partidas cortas, dinámicas, competitivas pero lighthearted, dignas de compartir.

## Audiencia (según GDD)

- **Demografía:** 25–35 años, profesionales con poco tiempo libre. ⚠️ *Discrepancia: `overview.md` (pitch) dice 13–21 + streamers. Resolver con diseño.*
- **Psicografía:** buscan experiencias lighthearted, quirky y caóticas; party games con humor y espontaneidad; valor social/compartible.
- **MDA:** Sensation 35% (feedback sensorial: sonidos satisfactorios, animaciones snappy, controles responsivos) · Challenge 65% (competencia y maestría).
- **Bartle:** predominantemente **Killers** (competencia directa, dominancia).

## Referencias / competencia

- **Ultimate Chicken Horse** — teasing entre jugadores, interacción dinámica multijugador, decisiones que afectan al resto.
- **Tricky Towers** — construcción libre con gravedad usando piezas tipo Tetris; reto vertical competitivo.
- **Duck Game** — aleatoriedad (cada match distinto) + competencia juguetona / trolleo estratégico.

## Valores core

**Experiencia:** accesibilidad y entrada fácil · partidas cortas/aleatorias/rejugables · engagement sensorial (visual expresivo + audio sincronizado).
**Gameplay:** interacciones juguetonas constantes (empujar, bloquear, trepar, sabotear) · impredecibilidad y partidas dinámicas · feedback expresivo, claro e inmediato.

---

## Core game loop

1. **Observar contexto** — leer terreno, rival, paths accesibles/bloqueados.
2. **Colocar bloque Tetris** — decidir dónde/cómo usar el bloque recibido.
3. **Trepar y evitar obstáculos** — subir anticipando al rival, no caer.

## Condiciones de victoria/derrota

| Nivel | Victoria | Derrota |
|---|---|---|
| Match | Mayoría de rondas (best-of-3 → 2; best-of-5 → 3) | Perder mayoría de rondas |
| Ronda | Sobrevivir sin caer al vacío | Caer al vacío |

## Cámara

2D, sigue a los jugadores subiendo. Si todos permanecen al mismo nivel, pausa breve y luego sigue ascendiendo tras unos segundos (urgencia constante).

## Sistema de movimiento

- **Lateral:** libre izq/der dentro de límites del área de juego. Genera dinámica de **Push** al empujar bloques.
- **Salto:** impulso vertical; con lateral → **salto direccional**.
- **Física y collider del personaje:** collider bloquea atravesar bloques, permite pararse sobre piezas y sobre otros jugadores (el de abajo NO arrastra al de arriba — decisión deliberada para que el de abajo pueda contrarrestar). Colisión y push entre personajes.
- **Glide:** al pasar el pico del salto, mantener botón de salto → caída lenta. Con lateral → **glide direccional**.
- **Cock-a-doodle-doo:** mecánica 100% expresiva (spamear cacareo para molestar). Sin efecto en gameplay.

## Sistema de combate

- **Block selection:** al inicio cada jugador recibe uno de los 5 tipos. Al colocar, se genera otro automáticamente. Generación NO uniforme: el área se divide en 3 tercios verticales — tercio inferior → mayor probabilidad de bloques altos (catch-up), tercio superior → bloques pequeños (freno al líder), medio → balanceado.
- **Block preview:** el bloque asignado aparece semi-transparente en la posición prevista. Válido = semi-transparente; inválido = se oscurece. Adapta orientación (espejado según dirección en que mira el jugador).
- **Catálogo de bloques:** O (2x2) · I (1x2) · T (3x2) · L/J (2x2) · Dot (1x1). L/J se espeja según facing.
- **Block placement:** botón dedicado confirma. No se puede colocar mientras se salta (evita colocar bloques sobre oponentes en el aire, sin contramedida). ⚠️ *La redacción del GDD ("cannot place blocks while moving sideways, but not while jumping") es contradictoria; la intención registrada es: se puede colocar en movimiento lateral, NO en salto. Confirmar con diseño.*
- **Física de bloques:** collider con forma de la pieza; colocados en el aire caen recto hasta superficie válida; NO rotan ni se inclinan; quedan fijos al aterrizar.
- **Push:** moverse lateralmente contra un bloque y mantener input → lo desplaza (también cadenas completas de bloques conectados, como unidad). Funciona incluso parado sobre el segmento bajo de una L. Dos jugadores empujando en direcciones opuestas → fuerzas se cancelan.
- **Kick:** siempre hacia donde mira el personaje. A jugadores: impulso en la dirección de la patada. A bloques/paredes estáticas: los daña pero no los mueve. **Carga modulable:** mantener input llena una power bar (máx 3 s = fuerza máxima); soltar ejecuta. Mientras carga, el personaje NO puede moverse (risk-reward). Dinámicas: empujar oponentes fuera de posiciones ventajosas; mover/romper bloques.
- **Block life:** todo bloque colocado tiene durabilidad. Cada kick en el área de impacto hace 1 de daño (independiente de la carga). **3 kicks → el bloque se destruye** y se elimina permanentemente.

---

## Lo que el GDD aún NO cubre (vive en código/docs de producción)

Items (Bomb, Spring Disc, Item Capsules), rondas First-to-N y ranking (Winning/Neutral/Losing), pooling de bloques por rank, head-stomp, jump buffer/coyote time, Cluck system, VFX, online. Ver `overview.md` y el código en `Assets/Scripts`.
