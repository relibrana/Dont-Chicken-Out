# Audio — migración a FMOD

> Estado: **fase 1 hecha** (oct 2026). El backend de audio ya es FMOD; lo que no tiene evento
> todavía sigue sonando por el sistema viejo sin que se note.
> Proyecto de Studio: `FMOD/DCO_FMOD/` · Bancos construidos: `FMOD/Desktop/` · Dueño: Juan (Cuaac).

---

## 1. Cómo funciona ahora

```
gameplay  →  AudioManager.PlaySound("player_jump")  →  AudioEventTable  →  FMOD
                                                            ↓ (sin evento)
                                                       AudioSource viejo
```

**`AudioManager` sigue siendo la única puerta.** Las ~45 llamadas de audio del juego no cambiaron
ni una línea. Lo que cambió es lo que hay detrás.

**`Assets/SOs/AudioEventTable.asset`** traduce cada id a su evento de FMOD. Es un ScriptableObject:
se edita en el inspector, sin tocar código.

**Un id sin evento asignado no es un error.** Cae al backend viejo de AudioSource, que se conserva a
propósito. Eso es lo que permite migrar evento a evento según Audio los vaya entregando sin que nada
se quede mudo por el camino. Al arrancar, el AudioManager lista por consola los ids que siguen en el
sistema viejo.

### Por qué un mapa y no un `EventReference` en cada script

Porque el netcode viene después. Cuando llegue Fusion 2 hay que meter guardas de rollback (que un
salto re-simulado no suene tres veces) y filtrado de "esto solo lo oye quien lo ve". Con el mapa eso
se toca **en un sitio**. Con `EventReference` repartido, en dieciocho.

---

## 2. Qué está mapeado

21 eventos existen en el proyecto de Studio. 18 ya están conectados:

| id del juego | evento de FMOD |
|---|---|
| `player_jump` / `player_kick` / `player_land` | `event:/SFX/Player/{Jump,Kick,Land}` |
| `player_steps` | `event:/SFX/Player/Steps` |
| `player_death` | `event:/SFX/Player/Death` |
| `player_spawn` | `event:/SFX/Player/Spawn` |
| `player_gliding` | `event:/SFX/Player/Gliding` — **sostenido, sin usar todavía** |
| `block_placement` / `block_invalid` | `event:/SFX/Blocks/{Placement,Invalid}` |
| `block_pushing` | `event:/SFX/Blocks/Pushing` — **sostenido, sin usar todavía** |
| `egg_break` | `event:/SFX/Items/Easter Egg` |
| `tire_bounce` | `event:/SFX/Items/Tire` |
| `bomb_explosion` | `event:/SFX/Items/Bomb` |
| `game_countdown` / `game_start` / `win_round` | `event:/SFX/UI/{Countdown,Start Round,Win Round}` |
| `button_selected` / `select` | `event:/SFX/UI/Button` |
| `Game` | `event:/BGM/Gameplay` |
| `BGM_Menu_A1` / `A2` / `B` | `event:/BGM/Menu` |

Los tres ids de menú apuntan al **mismo** evento a propósito: en FMOD el intro y el loop viven dentro
de un único evento con transition markers, así que `PlayMusicWithIntro` ya no necesita encadenar dos
clips. Si el evento pedido ya está sonando, no se reinicia.

### Lo que el código decidía y ahora decide FMOD

- **Pasos, muertes y joins**: antes el C# elegía un clip al azar sin repetir el anterior. Ahora es un
  solo disparo del evento y el multi-instrument de FMOD hace la variación. Audio puede iterarlo sin
  pedir builds.
- **Intro → loop de música**: era una corrutina esperando a que acabara el clip. Ahora es cosa del
  evento.
- **Tempo por fase de progresión**: `SetMusicPitch` manda a un parámetro del evento si está
  configurado en `musicTempoParameter`. Mientras no lo esté usa el pitch de la instancia, que sube
  también el tono — justo lo que `Docs/progression-system.md` §5.9 quería evitar.

---

## 3. Encargos para Audio (Juan)

Sin esto la migración no puede cerrarse. Ninguno bloquea al resto del equipo: el juego suena.

### 3.1 VCAs — bloquea los sliders de ajustes 🔴

El proyecto de Studio **no tiene ningún VCA ni bus** más allá del master. Los sliders de Música y
Efectos del menú no tienen a qué engancharse, así que hoy siguen moviendo el audio viejo.

Hacen falta dos: **`vca:/Music`** y **`vca:/SFX`**. Si se llaman de otra forma, basta con corregir
los dos campos del `AudioEventTable.asset`, no hay que tocar código.

### 3.2 Cuatro ids sin evento

| id | qué es | qué hace falta |
|---|---|---|
| `bomb_lighter` | La mecha encendida, en bucle desde que prende hasta que estalla. Se **para** por código al explotar. | ¿Es un evento aparte, o es el mismo `event:/SFX/Items/Bomb` con un parámetro? Si es el mismo, dime el nombre del parámetro. |
| `button_hover` | Pasar por encima de un botón. Hoy `event:/SFX/UI/Button` está mapeado al click. | ¿Mismo evento con parámetro, o uno propio? |
| `scene_transitionIn` / `scene_transitionOut` | Entrada y salida de transición de escena. Existe `event:/SFX/UI/Transitions`, uno solo para los dos. | ¿Un parámetro que distinga in/out, o dos eventos? Si los mapeo los dos al mismo, sonarán idénticos. |

### 3.3 Paneo de la explosión

Los eventos son 2D, así que FMOD no panea solo. La explosión de la bomba ya calcula de qué lado de
la pantalla ocurre y lo manda a un parámetro del evento — pero hay que **crear ese parámetro**
(rango −1 izquierda … 1 derecha) y apuntarlo en el campo `panParameter` de la fila. Mientras no
exista, la explosión suena centrada: no es un fallo, es que el evento no sabe panearse.

### 3.4 Eventos que existen y nadie usa

`event:/SFX/Player/Gliding`, `event:/SFX/Blocks/Pushing` y `event:/SFX/UI/Title` están construidos
pero el juego no los dispara. Los dos primeros son loops de estado y el código ya tiene el hook
(`OnGlideStateChanged`, `SetTouchingBlock`); enchufarlos es media hora. ¿Se quieren?

### 3.5 Lo que falta por autorar

Nada de los **ítems nuevos** tiene evento: moco, POW, teleporte, metálico, papa caliente, yunque,
súper patada, doble salto. Tampoco el **listón de progresión** (`ribbon_break`, `phase_change`).
La lista detallada, con la descripción de cada sonido, está en
`Docs/assets-arte-audio-items-progresion.md`.

---

## 4. Lo que falta por código

### 4.1 Bus de eventos de presentación — es lo que prepara Fusion 2 🟠

Hoy el gameplay llama al audio directamente. Para el netcode eso no vale: `PlayerMovement` se
re-simula en el rollback de Fusion, y cada re-simulación volvería a disparar el sonido de salto.

El plan acordado es que la **simulación levante eventos** (`OnJumped`, `OnKickLanded`,
`OnBlockPlaced`) y una capa de presentación los traduzca a audio. Beneficios:

- La guarda anti-rollback vive en un sitio.
- Sale gratis el "solo suena lo que ve mi cámara".
- El audio deja de ser una dependencia del código de simulación, que es la parte que hay que
  networkear.

Sólo hay que mover las llamadas que están **dentro de la simulación** (jugador, bloques, ítems:
unas 15). Las de UI y menús pueden seguir llamando directo — ahí no hay rollback que valga.

No se hizo en la misma pasada a propósito: primero conviene confirmar que el audio sale por FMOD, y
después mover la frontera. Mover las dos cosas a la vez deja sin saber cuál de los dos cambios rompió
qué.

### 4.2 Sistemas de audio zombis

`MusicManager` no lo referencia **nadie** y se puede borrar. `SoundManager` ya no lo usa nadie desde
que `StartGame.cs` pasó al AudioManager (oct 2026), pero sigue vivo en alguna escena — hay que
comprobarlo antes de borrarlo.

### 4.3 Melodías del cluck

`CluckSystem` reproduce notas sueltas desde `MelodySO` (arrays de `AudioClip`). No tiene equivalente
en FMOD todavía y sigue entero en el sistema viejo. Decidir si se migra o se queda así.

---

## 5. Notas para cuando llegue Fusion 2

- **El audio es presentación, nunca se replica.** No mandar RPCs de sonido: cada cliente dispara el
  suyo a partir del estado replicado. Un RPC por sonido es ancho de banda tirado y llega tarde.
- **Guarda de rollback.** Un evento one-shot disparado desde código que se re-simula necesita
  recordar en qué tick sonó. El sitio para eso es el `AudioManager`, no cada llamante.
- **Instancias sostenidas y reconciliación.** Los loops (`sustained` en la tabla) se paran por id.
  Si el estado que los mantenía se deshace en una reconciliación, hay que pararlos explícitamente o
  quedan sonando para siempre.
- **La ventana de la papa caliente** ya es un reloj absoluto que viaja con el ítem; su audio de
  urgencia debería leer ese valor, no un contador local de cada cliente.
