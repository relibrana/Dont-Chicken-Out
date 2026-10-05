# Lista de assets — Arte y Audio (Items + Progresión)

**Última actualización:** 2026-09-25 · **Status:** en construcción, sesión de trabajo con Mateo.

Este doc junta lo que falta producir en Arte y Audio para el catálogo de items (`Docs/items-implementacion.md`) y el sistema de progresión (`Docs/progression-system.md`). Ahora mismo casi todo el arte de items es el sprite de la bomba tintado por código — este doc es el pendiente real para reemplazar eso.

Leyenda: ✅ repasado con Mateo · ⏳ propuesto, falta confirmar con diseño/arte.

Tipo: **Sprite** (imagen estática) · **Animación** (secuencia) · **Código** (resuelto en programación, sin asset) · **Audio**.

Estado: **✅ Hecho** o **⬜ Pendiente**. Lo que existe pero hay que rehacer cuenta como pendiente.

---

## Compartido entre items

No van en la tabla de un item porque aplican a varios.

1. **Lanzamiento** *(Audio)* — golpe de aire corto al arrojar un objeto. Lo comparten todos los proyectiles lanzables (moco, bomba, y los que se agreguen). ⬜ Pendiente
2. **Rotura de bloques** *(Animación + Audio)* — el bloque se parte y sus pedazos salen volando, con su golpe correspondiente. Lo reutiliza todo item que rompa bloques (yunque, súper patada, y los que se agreguen). ⬜ Pendiente

---

## Items

### Bomba (`BombItem`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Código | Idle de respiración | La bomba se infla y desinfla en bucle. El ritmo se acelera de forma progresiva a medida que se acerca la explosión, marcando cuánto tiempo queda. Se resuelve por programación deformando el sprite, sin asset de Arte. | ✅ Hecho (sep 2026) — `BombItem.TickBreathing`, de 1.5 a 9 respiraciones/s |
| 2 | Sprite | Sprite de la bomba | Redibujo del sprite base bajo la nueva dirección artística. | ⬜ Pendiente |
| 3 | Animación | Explosión | Estallido de pocos frames: expansión brusca y disipación inmediata. Debe leerse violento y contundente sin quedarse en pantalla. | ⬜ Pendiente |
| 4 | Código | Mecha y chispa | La chispa recorre la mecha mientras esta se acorta y se consume. Ambas avanzan al mismo ritmo que la respiración y llegan a cero justo al estallar. | ✅ Hecho (sep 2026) — **placeholder por LineRenderer** (`BombItem.TickFuse`). Al llegar el sprite de mecha, desmarcar `Use Procedural Fuse` |
| 5 | Animación | Quema de bloques | Cada bloque alcanzado por la explosión se prende, se consume y desaparece por separado, en vez de esfumarse de golpe. | ⬜ Pendiente |
| 6 | Audio | Mecha encendida | Chisporroteo continuo de mecha, en bucle desde que se enciende hasta el estallido. | ✅ Hecho |
| 7 | Audio | Explosión | Estallido grave y seco, de ataque inmediato y cola corta. | ✅ Hecho |
| 8 | Audio | Quemazón de bloques | Crepitar de fuego de duración muy corta y volumen bajo: suenan varios a la vez cuando la explosión alcanza a muchos bloques. | ⬜ Pendiente |
| 9 | Código | Sacudón de pantalla | Golpe de cámara corto y fuerte al estallar. | ✅ Hecho (sep 2026) — `ScreenShake.Punch`, 0.25 s / 0.55 u |
| 10 | Código | Explosión paneada | El estallido sale por el altavoz del lado en el que ocurre. | ✅ Hecho (sep 2026) — `AudioManager.PlaySoundAt`, paneo por posición en pantalla |

---

### Moco (`MocoProjectile` / `MocoStuckState`) ✅

El sonido de lanzarlo no se repite acá: está en *Audio compartido entre items*.

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Proyectil de moco | Bola de moco viscosa, tal como se ve en la mano del jugador y durante el vuelo. | ⬜ Pendiente |
| 2 | Animación | Estela de vuelo | Rastro que acompaña al moco mientras viaja, estirado en sentido contrario al desplazamiento para que se lea hacia dónde va. | ⬜ Pendiente |
| 3 | Sprite | Splat | Mancha de moco aplastada y pegada, tal como aparece al impactar contra pared o bloque. | ⬜ Pendiente |
| 4 | Animación | Idle del splat | Burbujas que suben y revientan en la mancha mientras sigue pegada, para que se lea viscosa y sucia. | ⬜ Pendiente |
| 5 | Animación | Jugador atrapado | El pollo queda adherido al moco y mueve el cuerpo intentando despegarse, sin lograrlo. | ⬜ Pendiente |
| 6 | Código | Forcejeo por movimiento | Sacudida corta del pollo hacia el lado al que empuja el jugador. Se resuelve por programación moviendo el sprite, sin asset de Arte. | ✅ Hecho (sep 2026) — 0.08 u, `MocoStuckState` |
| 7 | Código | Forcejeo por patada | Misma sacudida pero más amplia y marcada que la de movimiento. También por programación. | ✅ Hecho (sep 2026) — 0.18 u |
| 8 | Animación | Escape | El moco se agrieta, se desprende del pollo y desaparece, liberándolo. | ⬜ Pendiente |
| 9 | Audio | Impacto contra superficie | Splat húmedo al reventar contra pared o bloque. | ⬜ Pendiente |
| 10 | Audio | Impacto contra jugador | El mismo splat combinado con un chillido de gallina, en un solo golpe. | ⬜ Pendiente |
| 11 | Audio | Graznido de forcejeo | Graznido corto del pollo en cada intento de zafarse, tanto al moverse como al patear, para que se entienda que está luchando por salir. | ⬜ Pendiente |
| 12 | Audio | Rotura al escapar | Chasquido de la mancha rompiéndose y despegándose en el momento en que el pollo se libera. | ⬜ Pendiente |

---

### Llanta (`SpringDisc`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Llanta | Existe, pero hay que revisarlo y rehacerlo bajo la nueva dirección artística. | ⬜ Pendiente |
| 2 | Código | Deformación al rebotar | La llanta se aplasta y retrocede cuando un jugador rebota en ella. | ✅ Hecho |
| 3 | Audio | Rebote | Golpe elástico de goma cuando un jugador rebota encima. | ✅ Hecho |
| 4 | Audio | Impacto al quedar fija | Golpe seco al clavarse en la superficie donde cae. | ⬜ Pendiente |

---

### Doble salto (`DoubleJumpPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Animación | Impulso del salto aéreo | Variación de la animación de salto, para el segundo salto hecho en el aire. | ⬜ Pendiente |
| 2 | Animación | Indicador de poder activo | Elemento que flota sobre el pollo mientras dura el efecto, mostrando que tiene el salto extra disponible. | ⬜ Pendiente |
| 3 | Código | Aviso de expiración | El indicador flotante parpadea durante el último segundo, avisando que el efecto se acaba. | ⬜ Pendiente |
| 4 | Audio | Salto aéreo | Aleteo corto en el momento del segundo salto. | ⬜ Pendiente |
| 5 | Audio | Pérdida del efecto | Sonido corto que marca el momento en que se acaba el doble salto. | ⬜ Pendiente |

---

### POW (`PowPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Proyectil POW | El POW tal como se ve en la mano del jugador y durante el vuelo. | ⬜ Pendiente |
| 2 | Animación | Impacto y activación | El POW choca contra la superficie con un golpe fuerte y queda activado, arrancando la cuenta atrás. | ⬜ Pendiente (la lógica ya está: se clava al impactar) |
| 3 | Sprite | Cuenta atrás y cartel | Arte de los números 3-2-1 y del cartel final, que dice `JUMP!`. **Van centrados en pantalla**: el POW es un efecto global, así que el aviso no cuelga del proyectil (decisión de sep 2026, se probó colgado y se descartó). | ⬜ Pendiente (placeholder de texto ya funcionando) |
| 4 | Código | Detonación | Sacudida de cámara en el momento en que la cuenta llega a cero. | ✅ Hecho (sep 2026) — corta y fuerte, 0.22 s / 0.9 u |
| 5 | Animación | Jugador aturdido | Pollo mareado y sin control mientras dura el aturdimiento (2.5 s, quieto en el sitio). | ⬜ Pendiente |
| 9 | Código | Plumas al recibir el POW | Estallido de plumas en los aturdidos, como al recibir una patada. | ✅ Hecho (sep 2026) |
| 10 | Código | Vibración de mando | Leve al esquivarlo saltando, fuerte al recibirlo. Sin efecto en teclado. | ✅ Hecho (sep 2026) — `PlayerController.Rumble` |
| 6 | Audio | Cuenta atrás | Pitido corto por cada número, ganando intensidad y profundidad a medida que la cuenta baja. | ⬜ Pendiente |
| 7 | Audio | Detonación | Golpe grave al llegar a cero. | ⬜ Pendiente |
| 8 | Audio | Aturdimiento | Zumbido mareado mientras el jugador está aturdido. | ⬜ Pendiente |

---

### Teleporte (`TeleportPickup`) ⏳

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Item de teleporte | Cómo se ve el item en la mano del jugador mientras lo lleva encima. | ⬜ Pendiente |
| 2 | Animación | Desaparición | El pollo se esfuma del sitio donde estaba al activar el item. Podría resolverse como un VFX; el traslado en sí lo hace el código. | ⬜ Pendiente |
| 3 | Animación | Aparición | El pollo se materializa encima del rival que va más alto. Podría resolverse como un VFX. | ⬜ Pendiente |
| 4 | Audio | Desaparición | Golpe de succión corto al desvanecerse. | ⬜ Pendiente |
| 5 | Audio | Aparición | Golpe seco al materializarse en el destino. | ⬜ Pendiente |

---

### Metálico (`MetalChickenPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Aspecto metálico | El pollo recubierto de metal mientras dura el efecto. Podría resolverse como un VFX; el cambio en sí lo hace el código. | ⬜ Pendiente |
| 2 | Animación | Aterrizaje pesado | Aterrizaje distinto al normal: el choque contra el suelo es más contundente porque la caída es mucho más pesada. | ⬜ Pendiente |
| 3 | Código | Aviso de expiración | Parpadeo durante el último segundo, avisando que el efecto se acaba. | ⬜ Pendiente |
| 4 | Audio | Transformación | Golpe metálico al activarse. | ⬜ Pendiente |
| 5 | Audio | Aterrizaje pesado | Impacto grave y pesado al tocar suelo. | ⬜ Pendiente |
| 6 | Audio | Patada reforzada | Patada con más cuerpo que la normal, mientras dura el efecto. | ⬜ Pendiente |
| 7 | Audio | Pérdida del efecto | Sonido corto que marca el momento en que se acaba. | ⬜ Pendiente |

---

### Yunque (`AnvilPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Yunque | El yunque tal como se ve durante la caída. | ⬜ Pendiente |
| 2 | Sprite | Icono de aviso | Icono que aparece en la parte superior de la pantalla, alineado con la vertical por donde va a caer el yunque. | ⬜ Pendiente |
| 3 | Código | Parpadeo del aviso | El icono parpadea antes de que el yunque empiece a caer. | ⬜ Pendiente |
| 4 | Audio | Aviso | Sonido de alerta mientras el icono parpadea. | ⬜ Pendiente |
| 5 | Audio | Caída | Silbido que acompaña la caída. | ⬜ Pendiente |

La rotura de los bloques que atraviesa está en *Compartido entre items*.

---

### Papa caliente (`HotPotatoPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Elemento | El objeto en sí, tal como se ve. | ⬜ Pendiente |
| 2 | Animación | Idle del elemento flotante | El elemento que va sobre la cabeza del portador, en bucle mientras lo lleva. | ⬜ Pendiente |
| 3 | Animación | Crecimiento del elemento | El elemento flotante va aumentando de tamaño a medida que se acaba el tiempo. | ⬜ Pendiente |
| 4 | Animación | Activación final | Lo que le cae encima al jugador que lo tenga cuando el tiempo llega a cero. | ⬜ Pendiente |
| 5 | Audio | Urgencia | Sonido que acompaña el crecimiento del elemento, ganando intensidad conforme queda menos tiempo. | ⬜ Pendiente |
| 6 | Audio | Traspaso | Golpe corto en el momento en que cambia de dueño. | ⬜ Pendiente |
| 7 | Audio | Activación final | Golpe al acabarse el tiempo. | ⬜ Pendiente |

Falta definir qué es el elemento: una papa caliente, una nube que se va cargando y suelta un rayo sobre quien la lleva, una bomba de tiempo u otra cosa. Los audios dependen de esa decisión de dirección artística, así que se definen después.

---

### Súper patada (`SuperKickPickup`) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Indicador de poder activo | Elemento que flota sobre el pollo mientras dura el efecto, mostrando que tiene la súper patada. | ⬜ Pendiente |
| 2 | Animación | Patada reforzada | Patada más amplia y contundente que la normal. Falta confirmar si realmente cambia respecto a la patada de siempre. | ⬜ Pendiente |
| 3 | Código | Aviso de expiración | El indicador flotante parpadea durante el último segundo, avisando que el efecto se acaba. | ⬜ Pendiente |
| 4 | Audio | Activación | Golpe corto al recibir el efecto. | ⬜ Pendiente |
| 5 | Audio | Patada reforzada | Patada con mucho más cuerpo que la normal. | ⬜ Pendiente |
| 6 | Audio | Muerte por patada | Sonido particular para el momento en que la patada mata a un jugador. | ⬜ Pendiente |
| 7 | Audio | Pérdida del efecto | Sonido corto que marca el momento en que se acaba. | ⬜ Pendiente |

La rotura de bloques está en *Compartido entre items*.

---

## Etiquetas de jugador ✅

Referencia de diseño: las etiquetas de Smash (hilo de Liliana, 10-14 sep 2026). De momento el texto es `Player 1..4`; está preparado para nombres personalizados (`PlayerController.SetDisplayName`).

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Código | Etiqueta flotante | Texto en el mundo sobre cada pollo, con el color de ese jugador, que se oculta al morir. | ✅ Hecho (sep 2026) — `PlayerNameTag` |
| 2 | Sprite | Fondo / flechita | Cartelito y puntero bajo el nombre, al estilo de la referencia. Hoy hay un **recuadro negro al 55%** generado por código, que se ajusta solo al ancho del nombre. | ⬜ Pendiente |
| 3 | — | Colores por jugador | **Resuelto sin trabajo de Arte:** la etiqueta lee `_ReplacementColor1` del material del propio pollo (el color de cuerpo del shader de color-swap), así que no se pueden desincronizar. Se puede forzar otro color por jugador en `MaterialsSO.labelColor` (alfa 0 = usar el del shader). | ✅ Hecho (sep 2026) |

---

## Progresión (listón) ✅

| # | Tipo | Asset | Descripción | Estado |
|---|---|---|---|---|
| 1 | Sprite | Listón | El listón que cruza la pantalla y hay que tocar para pasar a la fase siguiente. Hoy es un placeholder amarillo. | ⬜ Pendiente |
| 2 | Animación | Idle del listón | El listón en bucle mientras espera a que alguien lo alcance. | ⬜ Pendiente |
| 3 | Animación | Rotura del listón | El listón se parte en el momento en que el primer jugador lo toca. | ⬜ Pendiente |
| 4 | Sprite | Aviso "Faster!" | Cartel que aparece en pantalla al entrar en la fase nueva. | ⬜ Pendiente |
| 5 | Audio | Rotura del listón | Golpe en el momento en que el listón se parte. | ⬜ Pendiente |
| 6 | Audio | Cambio de fase | Sonido que marca la entrada a la fase nueva. | ⬜ Pendiente |
| 7 | Audio | Escalado de tempo | La música acelera su tempo en cada fase. Falta decidir si se resuelve subiendo el pitch o con un parámetro de tempo real. | ⬜ Pendiente |

---

## Notas generales

- Todos los sprites de items hoy son el sprite de la bomba tintado (moco verde, llanta oscura, yunque gris, teleport morado, POW amarillo) — por eso cerrar bien el sprite de la bomba primero condiciona la lectura del resto.
- La cápsula (`ItemCapsule`) usa sprites de daño genéricos compartidos por todos los items (intacto/dañado1/dañado2) — no hace falta arte específico por item ahí, la sorpresa se revela al romperse.
