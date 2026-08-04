# Sistema de progresión — implementación

Implementación del documento de diseño *Sistema de progresión — Don't Chicken Out!*
Estado: **código completo y compilando**. Falta montaje en escena (5 min), assets de arte y clips de
audio.

---

## 1. Qué hace

Un cronómetro corre durante la ronda. Al llegar a cero aparece un **listón** anclado al mundo a
una distancia fija por encima de la vista; el cronómetro se congela. El **primer jugador que lo
toca** lo rompe, y la rotura es lo que aplica la fase siguiente: se recalculan los escalares, la
cámara cae a 0 y vuelve a subir progresivamente hasta la nueva velocidad, y el cronómetro reinicia
con un intervalo más corto.

Separar *cuándo aparece* (tiempo) de *cuándo se activa* (contacto) es lo que convierte al listón en
un punto disputado, y es la parte del diseño que el código respeta literalmente.

---

## 2. Archivos

**Nuevos**

| Archivo | Qué es |
|---|---|
| `Assets/Scripts/Progression/ProgressionManager.cs` | Servicio: cronómetro, fases, spawn/rotura del listón, escalares |
| `Assets/Scripts/Progression/ProgressionRibbon.cs` | El listón: anclaje, detección de contacto, rotura, placeholder runtime |
| `Assets/Scripts/Progression/ProgressionVar.cs` | Enum de las variables |
| `Assets/Scripts/SOs/ProgressionValuesSO.cs` | Toda la configuración. Ni un número mágico en código |
| `Assets/Scripts/UI/ProgressionUI.cs` | Aviso "FASTER!" + lectura de fase/cronómetro para balanceo |
| `Assets/SOs/ProgressionValues.asset` | Asset ya creado con los valores exactos del doc |

**Modificados**

| Archivo | Cambio |
|---|---|
| `GameManager.cs` | Tickea el sistema, escala la cámara, aplica la recuperación, **rampa vieja eliminada** |
| `PlayerMovement.cs` | Velocidad lateral escalada |
| `PlayerBlockHandler.cs` | Cadencia y generación escaladas, arreglo del cooldown de colocación |
| `PlayerController.cs` | Deja de pisar el cooldown de colocación cada frame |
| `PoolingManager.cs` | Gravedad del bloque escalada al salir del pool |
| `PlatformerValuesSO.cs` | Campo nuevo `blockGenerationDelay`, y `blockPlacementCD` con valor real |
| `AudioManager.cs` | `SetMusicPitch()` para el escalado de tempo |
| `Assets/SOs/BasePlatformerValues.asset` | Valores base de la cadencia |

---

## 3. Las variables

| Variable | Tasa | Piso | Dónde se aplica |
|---|---|---|---|
| Cámara | +6 | — | `GameManager.TickCameraAutoMove` |
| Vel. lateral | +5 | — | `PlayerMovement.HandleHorizontalMovement` |
| Caída de bloque | +9 | — | `PoolingManager.SetBlock` (gravityScale) |
| Push | +7 | — | **No se consume.** Ver §5.2 |
| Generación de bloque | −6 | 50% | `PlayerBlockHandler.GenerationDelay` |
| Cadencia de colocación | −6 | 50% | `PlayerBlockHandler.PlacementCooldown` |
| Pausa de cámara | −8 | 40% | `GameManager.TickCameraAutoMove` (recuperación) |

Todas se consumen igual: `valor base * escalar`. Fase 1 = 100%. Incrementos aditivos, mitad de tasa
desde la fase 7. Intervalos 100 / 85 / 70 / 55 / 40 (tope 40).

Consumo seguro: cualquier script llama a `ProgressionManager.Get(ProgressionVar.X)`, que devuelve
**1.0 si no hay sistema en la escena**. Sin el ProgressionManager montado, el juego se comporta
exactamente como antes.

---

## 4. Montaje en Unity (5 minutos)

1. Abrir Unity y dejar que reimporte los scripts nuevos.
2. En `Main_Level`, crear un GameObject vacío **ProgressionManager** en el origen.
   *No debe ser hijo de la cámara ni de nada que se mueva.*
3. Añadirle el componente `ProgressionManager` y asignar **Values** → `Assets/SOs/ProgressionValues.asset`.
   Dejar **Ribbon Prefab** vacío: mientras no haya arte, genera un listón placeholder amarillo funcional.
4. En el componente **GameManager**, sección *Progression*, arrastrar ese objeto al campo **Progression**.
5. *(Opcional)* Añadir `ProgressionUI` al canvas de gameplay y asignarle un TMP para el "FASTER!"
   y otro para la lectura de fase/tiempo durante los tests.
6. *(Audio)* Añadir clips con los ids `ribbon_break` y `phase_change` a las listas de SFX del
   AudioManager. Hasta entonces sale un `Debug.Log` por fase, sin errores.

### Cómo testearlo

- **P** hace aparecer el listón, **O** lo rompe (solo en editor, igual que las teclas Z/X de la cámara).
- Click derecho en el componente ProgressionManager → *Force ribbon now* / *Force break ribbon* / *Log current scalars*.
- Click derecho en `ProgressionValues.asset` → **Log safety check (§4.3)**: imprime la tabla
  cámara vs bloques/seg de las 12 primeras fases y avisa si la cámara adelanta a la construcción.
- Para no esperar 100 s, bajar `firstInterval` a 10 en el asset.

### Notas de montaje aprendidas en el primer test

**Cámara.** Ninguna cámara del proyecto lleva el tag `MainCamera` — la Camera vive dentro de
`CameraRig.prefab` y está `Untagged` —, así que `Camera.main` devuelve null. En la primera versión eso
hacía que el listón naciera siempre en el origen del mundo, por debajo de todos y sin romperse nunca,
congelando el cronómetro el resto de la ronda.
Ahora el ProgressionManager resuelve la cámara por su cuenta: campo **View Camera** si se asigna,
luego `Camera.main`, y por último la única Camera de la escena. Funciona sin tocar nada, pero tagear
esa cámara como MainCamera sigue siendo buena práctica: hay fallbacks en `CinemachineVerticalRig2D`
que hoy petarían si `cineCam` quedara sin asignar.

**Ancho del listón.** `ribbonWidth = 0` significa automático: cubre todo el ancho visible más un
margen, sea cual sea el aspect ratio. Poner un número fijo solo si el arte lo exige.

**Colisión.** El placeholder nace en layer Default, y la matriz 2D del proyecto tiene Default × Player
activa (solo están desactivadas BlockStart × BlockStart y BlockStart × Kick), así que el trigger
dispara sin configurar nada. Cuando llegue el prefab de arte con layer propia, hay que comprobar esa
casilla de la matriz.

---

## 5. Estado de las decisiones de diseño

### 5.1 Velocidad de cámara — `cameraBaseSpeed = 0.65` en la escena

La rampa continua vieja queda eliminada, como se decidió. Pero esa rampa **era el motor de ritmo del
juego**, así que la velocidad base pasa a definir el ritmo de toda la ronda:

| | Antes | Ahora |
|---|---|---|
| Arranque | 0.15 u/s | 0.65 u/s |
| Aceleración | +0.01 u/s por segundo | solo +6% por fase |
| A los 100 s (aparece L1) | 1.15 u/s | 0.65 u/s |
| Tope | 1.5 u/s a los 135 s | sin tope, pero 0.85 u/s en fase 6 |

0.65 está elegido para que **la cámara recorra en los primeros 100 segundos exactamente la misma
distancia que hoy** (65 unidades), así que la fase 1 conserva el ritmo global actual.

**Matiz importante para el primer test:** eso iguala el total de la fase, no el arranque. Hoy la
ronda empieza muy lenta (0.15) y se va acelerando; ahora empieza directamente a 0.65 y se mantiene.
Mismo terreno recorrido, pero repartido de otra forma: **el principio se nota más apretado y el
final más suelto**. Es una consecuencia directa del modelo del documento, que solo acelera *entre*
fases y no *dentro* de una fase.
*Si el arranque resulta brusco, la solución barata es una rampa de entrada de ronda reutilizando la
misma recuperación de §5.3 (0 → base en N segundos). No está implementada.*

**Y una observación para diseño:** la curva del documento es mucho más suave que la rampa que tenía
el juego. Con +6% por fase, en fase 6 la cámara va a 0.85 u/s; para llegar al 1.5 u/s que hoy
alcanza a los 135 segundos harían falta más de treinta fases. Es decir: **las rondas van a durar
bastante más que ahora**, y la presión final va a venir de la densidad del escenario y no de la
velocidad de la cámara. Si eso no es lo buscado, la palanca no es +6→+7 (apenas cambia nada) sino
subir la velocidad base o replantear la tasa por fase.

### 5.2 Push — cerrado, no se implementa
El push es fakeo de moverse contra un objeto, así que ya escala indirectamente a través de la
velocidad lateral. No hay fuerza de empuje en el código.
El campo `push` se mantiene en el SO para no perder el valor del documento, pero **no lo consume
nadie**, y está etiquetado como tal en el inspector.
*Efecto secundario a tener presente: el push real escala al ritmo del lateral (+5), no al +7 que
figura en el documento.*

### 5.3 Pausa de cámara — cerrado
No es un congelado: al romperse el listón **la velocidad de cámara cae a 0 y vuelve a subir
progresivamente** (smoothstep) hasta la velocidad de la fase nueva. Ese es el diente de sierra.
La variable "pausa de cámara" es la duración de esa recuperación: base **1.2 s**, escalando
100% → 92% → … con piso 40%, así que en fases altas la cámara se recompone casi al instante.
El seguimiento a jugadores no se ve afectado, para no dejar fuera de plano a quien vaya arriba.
*Para cambiar la forma de la curva de recuperación: `ProgressionManager.CameraSpeedFactor`, una línea.*

### 5.4 Rampa continua de cámara — eliminada
Campos `cameraAcceleration` y `cameraMaxSpeed` fuera. `cameraInitialSpeed` pasa a llamarse
`cameraBaseSpeed` (con `FormerlySerializedAs`, así que la escena no pierde el valor).

### 5.5 Velocidad lateral — cerrado
Escalada sobre `maxSpeed` del jugador. El arco del salto se alarga en la misma proporción
(+25% en fase 6) y es intencionado.

### 5.6 Cadencia de colocación — arreglada, ritmo actual preservado
Dos bugs acumulados hacían que el cooldown de colocación no existiera:
1. `PlayerController.Update` ponía `IsAvailable = true` **cada frame**, cancelando el cooldown al
   frame siguiente de arrancarlo.
2. `blockPlacementCD` ni siquiera estaba en `BasePlatformerValues.asset` → valía **0**.

El resultado es que hoy el ritmo real lo marca solo el delay de 0.3 s de bloque nuevo, y "cadencia
de colocación" habría sido una de las variables del doc escalando un cero.

Arreglado: el cooldown ahora lo posee `PlayerBlockHandler` y nadie de fuera lo pisa. Para **no
cambiar el ritmo actual**, los 0.3 s se reparten entre las dos variables del documento:
`blockPlacementCD = 0.15` + `blockGenerationDelay = 0.15`. Total idéntico a hoy, pero ahora las dos
mitades son reales y escalan.

### 5.7 Caída de bloque — a corroborar en test
Escalada sobre la gravedad del bloque, aplicada al salir del pool. Los bloques ya colocados
conservan la suya: cambiarla bajo una torre asentada daría un tirón a todo el nivel.
Recordatorio: la gravedad también cambia cómo se asienta y rebota la torre, no solo la velocidad de
caída.

### 5.8 Animaciones a velocidades altas (§6.1 del doc)
Verificado en `Assets/Animation/ChickenRig/Player.controller`: **todos los estados tienen
`m_SpeedParameterActive: 0` y `m_Speed: 1`**. Los clips van a ritmo fijo y el float `Speed` solo se
usa como condición de transición, no como multiplicador. Conclusión y propuesta en §6.

### 5.9 Detalles resueltos por defecto
- **Rotura:** cualquier jugador **vivo**; disparo único aunque dos toquen el mismo frame.
- **Red de seguridad:** si el listón queda por debajo de la vista sin que nadie lo toque, se rompe
  solo. El doc no contempla ese caso y sin esto el cronómetro se congelaría el resto de la ronda.
- **Reset por ronda:** el match es first-to-N, así que la progresión vuelve a fase 1 en cada ronda.
- **Pooling por rank:** las fases no tocan la dificultad de las piezas (Winning/Neutral/Losing).
- **Tempo de música:** pitch del AudioSource (+3% por fase, tope 1.25). El pitch sube también el
  tono; lo correcto es un parámetro de tempo en FMOD — solo cambia el cuerpo de `SetMusicPitch`.

---

## 6. Propuesta de timing de animación

Un `animator.speed` global **no** sirve: escalaría también el salto, la caída y el planeo, que están
atados a un salto que por diseño no cambia (§4.3), y la animación se desincronizaría del arco real.
Lo que tiene sentido es por estado:

| Estado | ¿Escalar? | Motivo |
|---|---|---|
| Run | **Sí**, por vel. lateral | Con +25% de velocidad y el ciclo a ritmo fijo, los pies patinan visiblemente |
| Place | **Sí**, por cadencia | En fase 6 el cooldown es el 70%; si el clip dura más que su propio cooldown se corta o se solapa |
| Jump / Fall / Glide | No | El salto es fijo por diseño; acelerar el clip lo desincroniza del arco |
| Kick / Hit | No | La fuerza de patada no escala |

Implementación mínima: añadir un float `SpeedMul` al Animator, activar *Speed → Multiplier* en los
estados Run y Place, y alimentarlo desde `PlayerAnimController` con `|velocityX| / maxSpeed` para Run
y con el escalar de cadencia para Place. Lo de Run se autocorrige solo: cubre tanto el escalado por
fase como la aceleración y frenada normales.

**No implementado todavía** — requiere tocar el Animator y conviene decidirlo con arte.

---

## 7. Nota sobre 2 vs 4 jugadores

El razonamiento de §4.3 del documento habla de "ambos competidores". Con cuatro pollos hay cuatro
efectos que adelantan el caos respecto a dos:

1. **Se coloca el doble de bloques por segundo**, así que el escenario se llena antes: más
   colocaciones bloqueadas por solape y más interferencia física.
2. **`_isPlayerOnHead` bloquea el salto** cuando otro jugador está encima. Con cuatro pollos en una
   columna estrecha eso pasa muchísimo más.
3. **La cámara sigue al jugador vivo más alto.** Con cuatro, la probabilidad de que alguien vaya
   destacado es mucho mayor, así que la cámara tira hacia arriba más a menudo y aprieta al resto.
4. **El listón se rompe antes.** Como la rotura es por contacto, con cuatro cuerpos cruzando la
   banda alguien lo toca casi de inmediato; con dos puede quedarse un rato sin romper. Es decir, con
   4 jugadores las fases avanzan más pegadas al cronómetro puro y **la ronda se comprime más rápido**.

**Recomendación acordada: la curva se tunea a 4 jugadores**, que es el caso que llega antes al
límite. Lo que se sienta bien a cuatro será holgado a dos; al revés, no.

Nada en el código depende del número de jugadores: el sistema aplica las mismas fases y los mismos
tiempos a 2 y a 4. Que los intervalos o las tasas varíen según cuántos haya sería un cambio de
diseño y tendría que decidirlo diseño, no el código.

---

## 8. Lo que falta de fuera

**Arte** — asset del listón con dimensiones, idle, animación de rotura, aviso "Faster!".
El prefab solo necesita: raíz con `BoxCollider2D` + `ProgressionRibbon`, y un hijo con el arte
asignado a `visualRoot`. El código lo escala al ancho configurado. Si hay Animator, se le manda el
trigger `Break`. Conviene ponerlo en una layer propia, fuera de las máscaras de suelo y bloques.

**Audio** — SFX `ribbon_break` y `phase_change`, y decidir cómo se escala el tempo (pitch vs FMOD).

---

## 9. Netcode

Pensado para Fusion 2 host mode sin rehacerlo: toda la mutación de estado pasa por
`ApplyPhase(int)` y `Tick(float)`, hay un flag `isAuthority` que decide quién puede spawnear y
romper el listón, y el tick tiene un único punto de llamada por frame desde el GameManager.
El wrapper de red solo necesita replicar `CurrentPhase` y el cronómetro, y llamar a `ApplyPhase` en
los clientes.

Los escalares se leen del SO en vivo y **nunca se escriben**: mutar el ScriptableObject habría
guardado los valores escalados a disco en el editor y corrompido los valores base entre sesiones.
