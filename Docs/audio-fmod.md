# Audio — migración a FMOD

> Estado (10 oct 2026): **solo FMOD**. El sistema viejo de AudioSource se eliminó: lo que no tiene
> evento **no suena**.
> Proyecto de Studio: `FMOD/DCO_FMOD/` · Bancos construidos: `FMOD/Desktop/` · Dueño: Juan (Cuaac).

---

## 1. Cómo funciona ahora

```
gameplay  →  AudioManager.PlaySound("player_jump")  →  AudioEventTable  →  FMOD
                                                            ↓ (sin evento)
                                                     no suena + aviso en consola
```

**`AudioManager` sigue siendo la única puerta.** Las llamadas de audio de UI y menús no cambiaron;
las de gameplay pasan por `SimEvents` (§4.1).

**`Assets/SOs/AudioEventTable.asset`** traduce cada id a su evento de FMOD. Es un ScriptableObject:
se edita en el inspector, sin tocar código.

**Un id sin evento no suena.** No hay backend de respaldo: era la última parte del código que
decidía volumen, loop y variación por clip, y eso lo controla Audio en FMOD (ver la regla de abajo).
Al arrancar, el AudioManager lista por consola los ids sin evento, y avisa una vez por id la primera
vez que alguien lo pide.

Con el sistema viejo se fueron los mixers de Unity, las listas de clips del prefab `AudioManager`,
el volumen por clip, la rotación aleatoria de pasos, muertes y joins, el `explosionSfx` suelto de
`BombItem` y la reproducción de melodías.

### Por qué un mapa y no un `EventReference` en cada script

Porque el netcode viene después. Cuando llegue Fusion 2 hay que meter guardas de rollback (que un
salto re-simulado no suene tres veces) y filtrado de "esto solo lo oye quien lo ve". Con el mapa eso
se toca **en un sitio**. Con `EventReference` repartido, en dieciocho.

---

## 2. Qué está mapeado

32 eventos existen en el proyecto de Studio (11 oct 2026). 29 ya están conectados; los 3 restantes son
versiones viejas duplicadas (§3.4).

> ⚠️ **Rehacer un evento le cambia el GUID.** La tabla guarda el GUID, no el nombre: si en Studio se
> borra un evento y se crea otro igual, la fila deja de sonar (pasó con `Gameplay`, `Button Hover` y
> `Button Press` el 11 oct). Renombrar o mover un evento NO rompe nada. Si se rehace, hay que volver a
> asignarlo en `AudioEventTable.asset`.

> ⚠️ Hasta el 10 oct 2026 la tabla **no estaba asignada** en `Assets/Prefabs/AudioManager.prefab` (campo `eventTable`): todo sonaba por el sistema viejo y FMOD nunca llegó a sonar en el juego. Si el audio deja de sonar entero, mirar eso primero: sin tabla, el `AudioManager` lo avisa con un error al arrancar.

| id del juego | evento de FMOD |
|---|---|
| `player_jump` / `player_kick` / `player_land` | `event:/SFX/Player/{Jump,Kick,Land}` |
| `player_steps` | `event:/SFX/Player/Steps` |
| `player_death` | `event:/SFX/Player/Death` |
| `player_spawn` | `event:/SFX/Player/Spawn` |
| `player_gliding` | `event:/SFX/Player/Gliding` — **sostenido**, un loop por pollo mientras planea (`PlayerGlideStarted/Ended`; se corta también al morir) |
| `block_placement` / `block_invalid` | `event:/SFX/Blocks/{Placement,Invalid}` |
| `block_pushing` | `event:/SFX/Blocks/Pushing` — **sostenido, sin usar todavía** |
| `egg_break` | `event:/SFX/Items/Obtain Item` (reemplaza a `Easter Egg`, oct 2026) |
| `tire_bounce` | `event:/SFX/Items/Tire` |
| `bomb_explosion` | `event:/SFX/Items/Bomb Explosion` |
| `bomb_lighter` | `event:/SFX/Items/Bomb Fuse` — **sostenido**, uno por bomba |
| `game_countdown` / `game_start` / `win_round` | `event:/SFX/UI/{Countdown,Start Round,Win Round}` |
| `button_selected` / `select` | `event:/SFX/UI/Button Press` |
| `button_hover` | `event:/SFX/UI/Button Hover` |
| `Title` | `event:/SFX/UI/Title Animation` — evento de animación `PlaySFX("Title")` en el primer cuadro de `Animation/Title/Appear.anim` |
| `scene_transitionIn` / `scene_transitionOut` | `event:/SFX/UI/Transition In` / `Transition Out` |
| `super_kick` | `event:/SFX/Items/Super Kick` — sustituye a la patada normal mientras dura la súper patada |
| `pow` | `event:/SFX/Items/Pow` — **un solo disparo** al empezar la cuenta: cubre todo el 3-2-1-JUMP! |
| `teleport` | `event:/SFX/Items/Teleport` — al llegar al destino |
| `anvil_fall` / `anvil_hit` | `event:/SFX/Items/{AnvilFall,AnvilHit}` — al empezar a caer / al romper cada bloque y al aplastar a un pollo |
| `Game` | `event:/BGM/Gameplay` |
| `BGM_Menu_A1` / `A2` / `B` | `event:/BGM/Menu` |
| *(victoria)* | `event:/BGM/Menu` arrancado con `GameState = 1`: entra por "Victory C" en vez de desde el principio. Lo pide `GameManager.DoWin` al ganar la partida con `PlayMusic("BGM_Menu_B", gameState: 1)` |

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

Sin esto la migración no puede cerrarse. Ojo: desde que se quitó el sistema viejo, **lo que falta aquí no
suena** (no hay respaldo).

### 3.1 VCAs — ✅ hecho (10 oct 2026)

> ⚠️ **Pendiente (Juan, 11 oct):** 15 eventos no están en ningún VCA, así que los sliders de ajustes no les afectarán. **Música → `vca:/MUSIC`:** `Gameplay` (se rehízo y perdió el VCA). **Efectos → `vca:/SFX`:** `Bomb Explosion`, `Bomb Fuse`, `Button Hover`, `Button Press`, `Obtain Item`, `Title Animation`, `Transition In`, `Transition Out`, `Gliding` (el nuevo), `AnvilFall`, `AnvilHit`, `Pow`, `Super Kick`, `Teleport`. Suenan bien; solo falta asignarlos y reconstruir.

Juan creó **`vca:/MUSIC`** (Gameplay, Menu) y **`vca:/SFX`** (los otros 19 eventos), guardó y
reconstruyó bancos. El `AudioEventTable.asset` ya apunta a esas dos rutas. `UI.bank` desapareció: sus
eventos viven ahora en `SFX.bank`, y como Unity carga todos los bancos (`BankLoadType: All`), no hay
que tocar nada.

El código acepta indistintamente un VCA (`vca:/...`) o un bus (`bus:/...`) por el prefijo de la ruta.
Si se renombran, basta con corregir los dos campos del `AudioEventTable.asset`.

> Ojo con la confusión habitual: un bank (`BGM.bank`, `SFX.bank`) es una unidad de carga, no un punto
> del mixer. No sirve para atenuar grupos.

### 3.2 Ids sin evento — ✅ ninguno (11 oct 2026)

Las transiciones ya tienen evento propio (`Transition In` / `Transition Out`). Solo quedan mudos los del
listón (`ribbon_break`, `phase_change`), que aún no están autorados (§3.5).

### 3.3 Parámetros que el código ya manda y faltan en FMOD 🟠

El código ya entrega estos datos; falta que Audio cree el parámetro y lo ponga en la tabla. Mientras
no exista, el evento suena tal cual, sin variación.

| Parámetro | Rango | Dónde | Qué era antes en código |
|---|---|---|---|
| Lado de pantalla de la explosión | −1 izq … 1 der | Local en `event:/SFX/Items/Bomb Explosion` → `panParameter` de `bomb_explosion` | Paneo recortado al 80% |
| Celda de la pieza colocada | 0, 1, 2… (una por celda de la pieza) | Local en `event:/SFX/Blocks/Placement` → `valueParameter` de `block_placement` | Escala de pitch: +0.08 por celda, tope 1.5 |
| Fase de progresión | 1, 2, 3… | **Global** → `progressionPhaseParameter` | Pitch de la música: +3% por fase, tope 1.25 |

> Mientras no existan, **la escala de colocación y la aceleración de la música no suenan**: el código
> ya no las fabrica. Es el cambio de comportamiento que se nota.

### 3.4 Eventos que existen y nadie usa

- **Duplicados viejos, para borrar en Studio:** `event:/SFX/Player/Gliding` (el que no está en ningún
  banco; el bueno es el otro con el mismo nombre), `event:/SFX/UI/Title` (sustituido por `Title
  Animation`) y `event:/SFX/UI/Transitions` (sustituido por `Transition In/Out`). Dos eventos con el
  mismo nombre confunden a cualquiera que los busque por ruta.
- `event:/SFX/Blocks/Pushing` está construido y mapeado (`block_pushing`) pero el juego no lo dispara:
  es un loop de estado y el hook existe (`SetTouchingBlock`). ¿Se quiere?

### 3.5 Lo que falta por autorar

Ya tienen evento (oct 2026): POW, teleporte, yunque y súper patada (un evento cada uno), y la papa
reutiliza la explosión de la bomba. Faltan **moco, metálico y doble salto**, y el resto de momentos de
cada ítem que lista el doc de assets. Tampoco el **listón de progresión** (`ribbon_break`, `phase_change`).
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

Decisión: **no pasan a FMOD** de momento, así que el botón de cluck no suena. La reproducción de
melodías se fue con el sistema viejo; `CluckSystem` solo lleva la cuenta de pulsaciones (índice de
nota por jugador). Para traerlas de vuelta: un `SimEvent` con ese índice y un evento de FMOD que lo
lea, sin clips en código. `MelodySO` y sus assets quedan sin uso.

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
