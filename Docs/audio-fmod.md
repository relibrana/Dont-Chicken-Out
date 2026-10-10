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

### Regla: el código no decide cómo suena nada (10 oct 2026)

Todo lo que tiene que ver con el sonido (volumen, pitch, paneo, fades, variación, tempo) lo controla
**Audio en FMOD Studio**. El código solo hace dos cosas:

1. **Dispara el evento** cuando algo pasa.
2. **Pasa datos del juego en crudo** a parámetros que Audio expone, nunca valores de audio ya
   calculados.

| Dato que manda el código | Campo en `AudioEventTable.asset` | Qué hace Audio con él |
|---|---|---|
| Celda de la pieza que se coloca (0, 1, 2…) | `valueParameter` de la fila `block_placement` | La escala ascendente, o lo que quiera |
| Lado de la pantalla (−1 … 1) | `panParameter` de la fila | La curva de paneo (cuánto, con qué forma) |
| Fase de progresión (1, 2, 3…) | `progressionPhaseParameter` (**global**) | Tempo, capas, intensidad |

Sin parámetro configurado, el dato se ignora y el evento suena tal cual se autoró: el código **nunca**
cae a `setPitch` ni `setVolume`.

La única excepción es el volumen que **elige el jugador** en ajustes: entra por los VCAs `MUSIC` y
`SFX` como un multiplicador encima de la mezcla de Audio. Arranca en 1, o sea, la mezcla tal cual.
Hoy no hay ninguna UI que lo cambie.

### Lo que el código decidía y ahora decide FMOD

- **Pasos, muertes y joins**: antes el C# elegía un clip al azar sin repetir el anterior. Ahora es un
  solo disparo del evento y el multi-instrument de FMOD hace la variación. Audio puede iterarlo sin
  pedir builds.
- **Intro → loop de música**: era una corrutina esperando a que acabara el clip. Ahora es cosa del
  evento.
- **Tempo por fase de progresión**: antes el código subía el pitch de la música (+3% por fase, tope
  1.25). Ahora `SetProgressionPhase` solo manda el número de fase al parámetro global; el tempo lo
  hace Audio.
- **Escala de colocación de bloques**: antes `BlockScript` calculaba un pitch por celda
  (`basePitch`/`pitchStep`/`maxPitch`). Ahora manda el índice de la celda.
- **Paneo**: antes el código lo recortaba al 80% (`maxStereoPan`). Ahora manda el lado de pantalla
  sin recortar.

---

## 3. Encargos para Audio (Juan)

Sin esto la migración no puede cerrarse. Ninguno bloquea al resto del equipo: el juego suena.

### 3.1 VCAs — ✅ hecho (10 oct 2026)

Juan creó **`vca:/MUSIC`** (Gameplay, Menu) y **`vca:/SFX`** (los otros 19 eventos), guardó y
reconstruyó bancos. El `AudioEventTable.asset` ya apunta a esas dos rutas. `UI.bank` desapareció: sus
eventos viven ahora en `SFX.bank`, y como Unity carga todos los bancos (`BankLoadType: All`), no hay
que tocar nada.

El código acepta indistintamente un VCA (`vca:/...`) o un bus (`bus:/...`) por el prefijo de la ruta.
Si se renombran, basta con corregir los dos campos del `AudioEventTable.asset`.

> Ojo con la confusión habitual: un bank (`BGM.bank`, `SFX.bank`) es una unidad de carga, no un punto
> del mixer. No sirve para atenuar grupos.

### 3.2 Cuatro ids sin evento

| id | qué es | qué hace falta |
|---|---|---|
| `bomb_lighter` | La mecha encendida, en bucle desde que prende hasta que estalla. Se **para** por código al explotar. | ¿Es un evento aparte, o es el mismo `event:/SFX/Items/Bomb` con un parámetro? Si es el mismo, dime el nombre del parámetro. |
| `button_hover` | Pasar por encima de un botón. Hoy `event:/SFX/UI/Button` está mapeado al click. | ¿Mismo evento con parámetro, o uno propio? |
| `scene_transitionIn` / `scene_transitionOut` | Entrada y salida de transición de escena. Existe `event:/SFX/UI/Transitions`, uno solo para los dos. | ¿Un parámetro que distinga in/out, o dos eventos? Si los mapeo los dos al mismo, sonarán idénticos. |

### 3.3 Parámetros que el código ya manda y faltan en FMOD 🟠

El código ya entrega estos datos; falta que Audio cree el parámetro y lo ponga en la tabla. Mientras
no exista, el evento suena tal cual, sin variación.

| Parámetro | Rango | Dónde | Qué era antes en código |
|---|---|---|---|
| Lado de pantalla de la explosión | −1 izq … 1 der | Local en `event:/SFX/Items/Bomb` → `panParameter` de `bomb_explosion` | Paneo recortado al 80% |
| Celda de la pieza colocada | 0, 1, 2… (una por celda de la pieza) | Local en `event:/SFX/Blocks/Placement` → `valueParameter` de `block_placement` | Escala de pitch: +0.08 por celda, tope 1.5 |
| Fase de progresión | 1, 2, 3… | **Global** → `progressionPhaseParameter` | Pitch de la música: +3% por fase, tope 1.25 |

> Mientras no existan, **la escala de colocación y la aceleración de la música no suenan**: el código
> ya no las fabrica. Es el cambio de comportamiento que se nota.

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

### 4.1 Bus de eventos de presentación — ✅ hecho (10 oct 2026)

Para el netcode, el gameplay no puede llamar al audio directamente: `PlayerMovement` se re-simula en
el rollback de Fusion, y cada re-simulación volvería a disparar el sonido de salto.

Ahora la frontera es `Assets/Scripts/Presentation/`:

```
simulación  →  SimEvents.Raise(SimEvent.PlayerJumped, pos, this)  →  GameplayAudio  →  AudioManager
                      ↑ guarda de resimulación (Suppressed)
```

- **`SimEvents`**: enum `SimEvent` + `SimEventData` (tipo, posición, origen y un dato del juego,
  como el índice de celda). La simulación solo llama a `Raise`. Con Fusion, el driver del tick pone
  `SimEvents.Suppressed = Runner.IsResimulation` y los repetidos se descartan aquí, en un solo sitio.
  Los eventos que **cierran** un loop (`BombFuseOut`) pasan siempre, para que nunca se quede uno
  sonando.
- **`GameplayAudio`**: traduce cada `SimEvent` a su id del `AudioManager`. Se suscribe solo al
  arrancar (`RuntimeInitializeOnLoadMethod`), no hay que montarlo en escena. Aquí iría el filtro
  "solo suena lo que ve mi cámara": el evento ya lleva posición y origen.

Movidas (14 llamadas): salto, aterrizaje, patada, colocación inválida, join, muerte, colocación de
bloque y de holdable, golpe a la cápsula, rebote de llanta, mecha (encender/apagar ×2) y explosión.

Siguen directas a propósito: UI y menús (no se re-simulan), los pasos y `PlayerAnimController`
(los disparan eventos de animación, ya son presentación), `ProgressionManager` (reglas: listón y
tempo) y `CluckSystem` (pendiente de §4.3). Cuando se rehaga el core para Fusion, `ProgressionManager`
y la música de `GameManager` deberían pasar también por aquí.

Para añadir un sonido de gameplay nuevo: un valor en `SimEvent`, un `Raise` en la simulación y un
`case` en `GameplayAudio`. VFX o screen shake pueden suscribirse a `SimEvents.Raised` igual.

> ⚠️ `SimEvents` es estático: con `PeerMode = Multiple` (varios runners en un editor) mezclaría los
> eventos de todos. Si llega a usarse, hacerlo por runner.

### 4.2 Sistemas de audio zombis

`MusicManager` y `SoundManager` no los referencia **ningún** script, escena ni prefab (verificado por
GUID, 10 oct 2026). Se pueden borrar sin más.

### 4.3 Melodías del cluck — ⏸️ desactivadas (10 oct 2026)

Decisión: **no pasan a FMOD** de momento. `CluckSystem` tiene `melodiesEnabled = false`, así que el
botón de cluck no suena. El código de melodías (`MelodySO`, `PlayMelodyNote`) sigue ahí; para
reactivarlas basta con marcar la casilla en el componente del pollo.

---

## 5. Notas para cuando llegue Fusion 2

- **El audio es presentación, nunca se replica.** No mandar RPCs de sonido: cada cliente dispara el
  suyo a partir del estado replicado. Un RPC por sonido es ancho de banda tirado y llega tarde.
- **Guarda de rollback.** Un evento one-shot disparado desde código que se re-simula no debe volver
  a sonar. El sitio para eso es `SimEvents` (§4.1), no cada llamante.
- **Instancias sostenidas y reconciliación.** Los loops (`sustained` en la tabla) van por
  `(id, dueño)`: `PlaySoundFor` / `StopSoundFor` en el `AudioManager`, así cada bomba enciende y apaga
  sólo su mecha. `StopSound(id)` sigue parando todas las copias. Si el estado que los mantenía se deshace en una reconciliación, hay que pararlos explícitamente o
  quedan sonando para siempre.
- **La ventana de la papa caliente** ya es un reloj absoluto que viaja con el ítem; su audio de
  urgencia debería leer ese valor, no un contador local de cada cliente.
