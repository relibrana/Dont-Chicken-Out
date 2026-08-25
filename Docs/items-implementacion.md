# Sistema de Ítems — Estado de implementación y guía de wiring

> Código: **10/10 ítems del catálogo implementados** (ago 2026). La bomba ya existía; la llanta es la evolución del SpringDisc (sustituye al disco colocable — confirmado por diseño); el resto es nuevo.
> Wiring: **hecho** — prefabs en `Assets/Prefabs/Items/` y `Assets/SOs/ItemsPool.asset` poblado con pesos (autoría directa de YAML).
> Falta: playtest de valores, sprites/VFX/SFX finales (todos los visuales son el sprite de la bomba tintado).
> Fuente de diseño: doc del GD "Sistema de Ítems — Investigación y Candidatos".

## Arquitectura (resumen para el equipo)

- **`ItemsPoolSO`** — pool con pesos por ítem (palanca "Distribución" del doc). La cápsula tira de aquí; sin asset asignado cae al azar uniforme viejo.
- **Dos tipos de premio de cápsula:**
  - `HoldableItem` — va a la mano y sustituye el bloque (bomba, disco, lanzables).
  - `IInstantItem` — se aplica al instante al romper la cápsula y **conservas tu bloque** (estados propios: súper patada, POW, papa, teleporte, yunque, metálico, doble salto).
- **`PlayerItemState`** — base de estados con temporizador en el jugador: tinte placeholder, limpieza automática al morir o acabar la ronda (`isOnGame`), y hooks a `PlayerMovement` (multiplicadores de velocidad/salto/gravedad, salto aéreo, congelación) y `PlayerController` (bloqueo de input, inmunidad a empujes).
- **`ThrowableItem`** — lanzables: se llevan en la mano y con el botón de colocar se **lanzan** (sin checks de suelo/overlap). Apuntado v1 = dirección del sprite + ángulo fijo serializado (no existe input de apuntar; Move es un eje 1D).

## Prefabs y pool (hechos — verificar al abrir Unity)

Todo vive en `Assets/Prefabs/Items/`. `Assets/SOs/ItemsPool.asset` ya tiene las 10 entradas; pesos sugeridos (letales pesan menos, palanca del doc):

| Prefab | Peso | Valores serializados (default / rango doc) |
|---|---|---|
| `BombItem` (existente) | 20 | sin cambios |
| `SpringDisc` (= **Llanta**, ítem 5) | 12 | ahora lanzable: vel 12, ángulo 25°, grav 2 · conserva su rebote (25,35) y squash/recoil · empuje al impactar en vuelo (8,6) |
| `MocoProjectile` | 12 | lanzamiento: vel 12, ángulo 25°, grav 2 · 8 pulsaciones, tope 3 s, trampa un solo uso ✔ |
| `DoubleJumpPickup` | 12 | duración 6 s, altura 2º salto ×0.85, repetible ✔ |
| `TeleportPickup` | 10 | altura 2.5, capas ocupadas = Ground+Block |
| `PowPickup` | 8 | cuenta desde 3, stun 2 s |
| `MetalChickenPickup` | 5 | duración 6 s, salto ×0.75, gravedad ×1.6, patada ×1.6 |
| `HotPotatoPickup` | 4 | mecha 8 s, vel ×1.3, radio 2.5, empuje 12, capas explosión = Player+Block |
| `SuperKickPickup` | 4 | duración 4 s (3–5), daño a bloques 3, empuje ×1.5 |
| `AnvilPickup` | 3 | vel caída 14, telegrafiado 1 s, no se detiene al matar; referencia a `Anvil.prefab` (kinemático + trigger, layer Ground) |

Para probar un ítem aislado: su peso a 100 y el resto a 0. Opcional: añadir prefabs a `Pooled Items` del PoolingManager (amount 2–3) para pre-instanciar; si no, se instancian al vuelo.

**Checklist de verificación al abrir Unity** (los prefabs/asset se escribieron por YAML a mano):
1. Los prefabs de `Assets/Prefabs/Items/` abren sin warnings y su script está asignado (no "Missing Script").
2. `ItemsPool.asset` muestra las 10 entradas con prefab y peso (ninguna en None).
3. En los lanzables, los campos `colliders`/`rb2d`/`spriteRenderers` apuntan a sus propios componentes.
4. Los sprites son el de la bomba tintado (moco verde, llanta oscura, yunque gris) — placeholder hasta tener arte.

POW crea su propio canvas en runtime (placeholder hasta integrarlo a UIManager). Ningún otro sistema requiere tocar escenas.

## Plan de pruebas (por ítem)

1. **Pool**: pesos 100/0 reparten como se espera; sin asset asignado la cápsula sigue funcionando como antes.
2. **Súper patada**: tinte naranja; patada mata al rival (no a ti); rompe sub-bloques de una; parpadea el último segundo; expira limpio; morir con el estado activo no deja el tinte pegado.
3. **POW**: countdown visible para todos; al llegar a 0, sólo se aturden los que tocan suelo (saltar lo esquiva); el que lo activó también cae; los aturdidos no se mueven/patean/colocan ~2 s pero siguen cayendo por gravedad.
4. **Moco**: impacto a jugador lo congela en el sitio (ni gravedad); mash de patada lo libera antes; a los 3 s se libera solo (accesibilidad); impacto en pared/bloque deja mancha verde que atrapa al que la toca; tras liberarse hay ~1 s de inmunidad a re-pegarse.
5. **Llanta**: vuela con arco; pega en pared/bloque y queda fija toda la ronda; rebota a CUALQUIER jugador (portador incluido); si golpea a un jugador en vuelo lo empuja y queda suspendida ahí.
6. **Papa caliente**: portador más rápido y pulsando rojo (acelera al agotarse); tocar a otro la transfiere con el tiempo restante (0.5 s de gracia anti ping-pong); al expirar el portador muere y los cercanos salen empujados + bloques dañados; morir por otra causa NO explota.
7. **Teleporte**: apareces ~2.5 sobre un rival vivo al azar; si arriba hay bloques, busca hueco más arriba; sin rivales vivos no hace nada.
8. **Yunque**: aparece parpadeando arriba de tu columna ~1 s; cae recto matando jugadores (tú incluido si te quedas debajo) y borrando los sub-bloques que atraviesa; sigue hasta abajo y despawnea.
9. **Metálico**: salto más bajo y caída pesada; patadas/bombas/muelles no lo empujan (la bomba SÍ lo mata); su patada empuja más fuerte; expira limpio.
10. **Doble salto**: un salto extra en el aire (recargable al aterrizar por default); sirve para recuperarse tras una patada; interactúa con el planeo (mantener salto tras el 2º salto planea — validar sensación, doc §A.4).
11. **Combos**: súper patada + metálico multiplican empuje; aturdido puede recibir la papa; morir en cualquier estado limpia tintes y multiplicadores.

## Decisiones v1 — validar con los design leads

1. **Activación al recoger**: no hay inventario ni botón de "usar ítem"; los estados/globales se activan al romper la cápsula (el timing = cuándo la pateas). ¿Se quiere inventario con botón de uso?
2. **Apuntado v1 = dirección + ángulo fijo**: no existe input de apuntar (Move es 1D). ¿Se añade apuntado real (stick/8-way) al esquema de controles?
3. **La bomba sigue siendo colocable** (como en build), aunque el doc la describe como proyectil apuntado. Con `ThrowableItem` hecho, la versión lanzada es ~1 h — pero cambia una mecánica ya shippeada: decisión de diseño.
4. Los ítems instantáneos **no sustituyen el bloque en mano** (antes la cápsula siempre lo sustituía).
5. Papa caliente: explosión final **no letal** para los vecinos (solo empuje + daño a bloques); el doc dice "daña" sin definir. Toggle fácil si debe matar.
6. Moco como trampa: **un solo uso** por default para evitar stunlock junto a la mancha (el doc dice "permanece toda la ronda" — hay un bool serializado para probar ambas).
7. Metálico: "empuja bloques más rápido" no está implementado — el empuje de bloques es caminar contra ellos y escala con velocidad lateral; darle más velocidad contradice "más pesado". Definir qué significa exactamente.
8. Teleporte con hueco ocupado: resuelto como "sube hasta encontrar hueco libre" (el caso abierto del doc).
9. **Resuelto (ago 2026): la llanta sustituye al disco colocable.** `SpringDisc` ES la llanta (lanzable, fija toda la ronda) y `TireProjectile` se eliminó. Efecto secundario a vigilar en playtest: el disco viejo se podía patear para negar el atajo; la llanta fija es inamovible (según doc), así que ese counterplay desaparece.

## Pendientes técnicos

- **SFX**: los ítems nuevos no tienen claves de audio propias (solo reusan `tire_bounce` en la llanta). Definir eventos con el encargado de FMOD.
- **VFX/animaciones**: todo es tinte placeholder + parpadeos. Feather VFX / sprites reales pendientes.
- **UI real**: countdown POW y timer de papa deberían vivir en UIManager/HUD (hoy: canvas runtime y pulso de color).
- **Pooling de proyectiles**: moco/llanta persisten toda la ronda y el yunque usa Instantiate/Destroy; si se abusa, integrarlos al PoolingManager (la ronda los limpia vía `ResetPool` solo si entran al pool de ítems).
- **Netcode (Fusion 2)**: los estados usan `Time.deltaTime` en `Update` y el POW usa corrutinas — al migrar al modelo por tick de la reconstrucción del core habrá que mover los temporizadores al tick simulado. Los hooks ya están centralizados en `PlayerMovement`/`PlayerController`, que es la frontera que se va a networkear.
