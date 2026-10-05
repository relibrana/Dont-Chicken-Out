# Deuda técnica — cosas a arreglar en otra sesión

> Creado: 2026-09-25. Lista viva: al arreglar algo, borrarlo de aquí (o marcarlo ✅ con la fecha).
> Esto NO es el backlog de features — eso vive en `Docs/backlog.md`. Esto son cosas que ya están
> mal en el código y funcionan por suerte o a medias.

Orden: por cuánto duele, no por cuánto cuesta.

---

## 1. Los pollos no mueren, se teletransportan 🔴

**Qué pasa.** `GameManager.OnPlayersDeath` no desactiva al jugador: lo mueve a `deathPos` y lo deja
ahí, vivo y simulando, hasta que acaba la ronda.

```csharp
player.gameObject.transform.position = deathPos.position;
player.DropBlock();
playersAlive[player.playerIndex] = null;
```

**Por qué está así.** Al apagar el GameObject (`SetActive(false)`) se rompía el vínculo del
`PlayerInput` con su mando: el jugador muerto volvía sin controles en la ronda siguiente.

**Por qué importa.**
- El pollo muerto sigue corriendo física, raycasts de suelo, `Update` de todos sus componentes y
  del bloque en mano. Es coste puro y, peor, es **estado vivo** que puede volver a entrar en el
  juego por un colisionador perdido.
- Cualquier sistema que barra `FindObjectsByType<PlayerController>()` lo encuentra. Hoy todos
  filtran por `isOnGame`, pero es una convención, no una garantía: el día que alguien olvide el
  filtro, el POW aturde a un muerto o el teleporte manda a alguien a la zona de muertos.
- Con netcode (Fusion 2) esto se vuelve un problema de verdad: hay que replicar la posición de un
  objeto que conceptualmente no existe.

**Por dónde va el arreglo.** El vínculo de input no depende del GameObject estando activo, sino de
que el `PlayerInput` no se desempareje. Las dos vías:
1. Desactivar sólo los componentes de gameplay (`PlayerMovement`, colliders, renderers) y dejar
   vivos el GameObject y el `PlayerInput`. Es el cambio más pequeño y resuelve casi todo.
2. Apagar el GameObject y mover el emparejamiento de dispositivos a `PlayersManager`, que ya lleva
   los esquemas (`FreeKeyboardScheme`), re-emparejando en `OnPrepareState`.

La (1) es la que yo haría primero; la (2) es la correcta si de todas formas hay que tocar el join
para el Lobby de Pollos.

---

## 2. Sorting layers: el HUD está por debajo del juego 🟠

**Qué pasa.** En `ProjectSettings/TagManager.asset` el orden es:

```
Bg → Default → UI → Items → Characters → Pause → LoadingUI
```

**`UI` está por debajo de `Items` (bloques) y `Characters` (pollos)**, así que cualquier cosa del
HUD sale detrás de la torre.

**Estado.** Parcheado en runtime (sep 2026): `UIManager.SetOverlayOnTop` sube el canvas al layer
`Pause` mientras dura la pantalla de fin de ronda y lo devuelve al entrar a juego. Resuelve el
síntoma reportado (la capa oscura del victory detrás de los bloques).

**Lo correcto** es mover `UI` por encima de `Characters` en TagManager y quitar el parche. No se
hizo en la sesión porque reordenar sorting layers afecta a todos los `SpriteRenderer` del proyecto
(cada uno cachea además un índice `m_SortingLayer` que puede quedar desincronizado) y hay que
verificarlo con el editor abierto, no por YAML.

---

## 3. La bomba destruye un objeto del pool 🟠

`BombItem.FuseRoutine` acaba en `Destroy(gameObject)`. Pero la bomba sale de `PoolingManager`, así
que cada explosión **quema una plaza del pool**: el pool se queda con menos hijos y a partir de ahí
`GetPooledItem` instancia uno nuevo cada vez (`"pool is full, spawn new one"` en consola).

Arreglo: `gameObject.SetActive(false)` como el resto de items, y resetear `hasExploded` /
`fuseRoutine` en `OnDisable`. Hay que revisar que la animación de explosión no se corte antes de
tiempo (por eso existe `destroyDelay`).

---

## 4. `BlockDamageable` sólo inicializa la vida en `OnDisable` 🟠

`_currentLife` se pone a `maxLife` en `ResetState()`, y `ResetState()` sólo se llama desde
`OnDisable()`. Hoy funciona **de milagro**: `PoolingManager` hace `SetActive(false)` justo después
de instanciar y otra vez en cada entrega, así que el ciclo dispara `OnDisable`.

Un bloque que se activara sin haber pasado nunca por `OnDisable` tendría `_currentLife == 0` y
`TakeDamage` saldría por el early-return: **bloque indestructible**. Es una trampa esperando a que
alguien cambie el pooling.

Arreglo: inicializar también en `Awake`/`OnEnable`.

---

## 5. `Camera.main` es null en todo el proyecto 🟠

Ninguna cámara lleva el tag `MainCamera` — la del juego vive dentro de `CameraRig.prefab` y está
`Untagged`. Ya mordió una vez (el listón de progresión nacía en el origen del mundo, ver
`Docs/progression-system.md`).

Sitios que hoy lo esquivan con un fallback: `ProgressionManager`, `AudioManager.PlaySoundAt`,
`CinemachineVerticalRig2D.LensOrthoSize` (este **no** tiene fallback: peta si `cineCam` queda sin
asignar).

Arreglo: taguear esa cámara como `MainCamera` y quitar los fallbacks.

---

## 6. `BombItem.Update` tapa el `Update` de `HoldableItem` 🟡

`HoldableItem` tiene un `void Update()` privado que pinta el color de "en mano" y de "solapando".
`BombItem` declara su propio `private void Update()`, que lo **oculta** (method hiding, no override:
el de la base no es virtual). Resultado: la bomba nunca recibe ese feedback de color.

No es visible hoy porque la bomba tiene su propio degradado de mecha, pero es una trampa del mismo
tipo para el siguiente item que herede y declare `Update`.

Arreglo: convertir el `Update` de `HoldableItem` en `protected virtual` y que las subclases hagan
`base.Update()`.

---

## 7. Temporizadores de items en tiempo real, no en tick 🟡

Todos los `PlayerItemState` cuentan con `Time.deltaTime` en `Update`, el POW usa corrutinas y varios
items usan `DOVirtual.DelayedCall`. Al migrar al modelo por tick de Fusion 2 hay que mover todo eso
al tick simulado o los estados se desincronizarán entre host y clientes.

Lo bueno: los hooks ya están centralizados en `PlayerMovement`/`PlayerController`, que es justo la
frontera que se va a networkear. Ver `Docs/items-implementacion.md` §Pendientes técnicos.

---

## 8. El yunque vive fuera del pool 🟡

`AnvilPickup` instancia y destruye su `Anvil.prefab` con `Instantiate`/`Destroy` en vez de pasar por
`PoolingManager`. Si un yunque no termina su caída antes del fin de ronda, `ResetPool` no lo ve
(no está en `itemList`) y se queda en escena.

Es el mismo patrón de bug que tenían las cápsulas (arreglado en sep 2026: `GetCapsule` instanciaba
cápsulas nuevas sin añadirlas a `capsuleList`, así que los huevos sobrevivían a la ronda).

---

## 9. Código muerto 🟢

- `Assets/Scripts/Controllers/CameraController.cs` — todo el `Update` comentado; lo que hacía lo
  hace hoy `CinemachineVerticalRig2D`.
- `Assets/Scripts/CameraEffects.cs` — shake viejo, sustituido por el del rig.
- `KickCollider` — bloque grande de código comentado al final de `OnTriggerEnter2D`.
- `PlayerUI` — `InGameBox` / `DeadBox` comentados en todos los casos del switch.

---

## 10. Cosas menores 🟢

- **Finales de línea mezclados.** Hay scripts en CRLF y otros en LF dentro de `Assets/Scripts`.
  Con `core.autocrlf=true` no ensucia los diffs, pero conviene un `.gitattributes` que lo fije.
- **`TireProjectile.prefab` se llama como un script que ya no existe.** El doc dice que
  `TireProjectile` se eliminó y que la llanta es `SpringDisc`; el prefab lleva efectivamente el
  componente `SpringDisc`, sólo que conserva el nombre viejo. Renombrarlo a `SpringDisc.prefab`
  (con cuidado: `ItemsPool.asset` lo referencia por GUID, así que el rename es seguro).
- **Súper patada: el rango de `Block Damage` está tapado.** Los sub-bloques tienen 3 de vida y una
  patada normal hace 1, así que los únicos valores distinguibles son 1 (= patada normal), 2 y 3
  (= one-shot); de 3 en adelante da igual lo que pongas. **Pendiente de decisión de diseño**: subir
  la vida de los bloques para que la escala tenga recorrido, o que la súper patada rompa la pieza
  entera en vez del sub-bloque.

---

## Ya arreglado — no volver a investigarlo

Apuntado para que nadie pierda una tarde re-diagnosticando algo que ya tiene dueño.

| Qué parecía | Qué era | Arreglado |
|---|---|---|
| Los huevos de la ronda anterior se quedaban en pantalla | `PoolingManager.GetCapsule` miraba siempre `capsuleList[0]` y, si estaba ocupada, instanciaba una cápsula nueva **sin registrarla**. `ResetPool` recorre esa lista, así que las huérfanas no se apagaban nunca | sep 2026 |
| La capa oscura del victory salía detrás de la torre | El sorting layer `UI` está por debajo de `Items` y `Characters` (ver §2). Parcheado en runtime desde `UIManager` | sep 2026 |
| Piezas que no volvían al pool y consola llena de `Pool is full` | `BlockOverlapCheck.DisableBlock` escondía un sub-bloque solapado apagándole collider y sprite, pero seguía contando como vivo para el `BlockScript` padre. Sin collider nadie podía matarlo, así que la pieza nunca llegaba a cero hijos. Ahora pasa por `BlockDamageable.RemoveFromPlay()` | sep 2026 |
| Un sacudón de cámara corto se sentía flojo | `UpdateShake` normalizaba la caída de amplitud contra la duración **por defecto**, no contra la del sacudón en curso: cualquier shake más corto que el default arrancaba a una fracción de su amplitud | sep 2026 |
| `Block Damage` de la súper patada "no hacía nada" | Sí llegaba al bloque. El valor que se probó (1) es exactamente el daño de una patada normal, así que no había diferencia que ver. Lo que sigue abierto es el rango útil, arriba en §10 | sep 2026 (diagnóstico) |
