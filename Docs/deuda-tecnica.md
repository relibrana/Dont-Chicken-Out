# Deuda técnica — cosas a arreglar en otra sesión

> Creado: 2026-09-25. Lista viva: al arreglar algo, borrarlo de aquí (o marcarlo ✅ con la fecha).
> Esto NO es el backlog de features — eso vive en `Docs/backlog.md`. Esto son cosas que ya están
> mal en el código y funcionan por suerte o a medias.

Orden: por cuánto duele, no por cuánto cuesta.

---

## Abierto

Nada (oct 2026). Todo lo que había está arreglado en código; falta probarlo (lista de abajo).

---

## Pendiente de probar en el editor

Arreglos ya hechos en código que nadie ha visto funcionar en el juego. Al probar uno, tacharlo
(`[x]`) y quitar el "pendiente de probar" de su fila en la tabla. Si algo falla, vuelve a "Abierto".

**Ítems**
- [ ] **Bomba — explosión entera:** la animación de explosión se ve completa antes de desaparecer.
- [ ] **Bomba — reutilizada:** tras varias explosiones, una bomba nueva sale con su color y su animator en reposo, se puede lanzar y vuelve a explotar.
- [ ] **Bomba — pool:** tras varias explosiones no sale `pool is full, spawn new one` en consola.
- [ ] **Bomba — color en mano:** en la mano se ve con el tinte translúcido de "en mano", como el moco y la llanta.
- [ ] **POW:** el 3-2-1-JUMP! dura y se ve igual que antes, aturde a los que están en el suelo y no a los que saltan; acabar la ronda a mitad de cuenta la corta sin aturdir.
- [ ] **Yunque — aviso:** la columna se abre, las flechas aparecen y el yunque cae justo al acabar ese aviso.
- [ ] **Yunque — fin de ronda:** un yunque a medio aviso o a medio caer desaparece (con su columna) al acabar la ronda, y el siguiente yunque funciona normal.
- [ ] **Súper patada:** rompe de un golpe solo el cuadrito tocado (no la pieza entera); una patada normal sigue haciendo 1.
- [ ] **Power-ups entre rondas:** un pollo con power-up no lo conserva (ni efecto ni tinte) en la ronda siguiente; con dos estados a la vez el tinte vuelve bien; la papa caliente explota donde muere el portador.

**Pollos y cámara**
- [ ] **Muerte de pollos:** el pollo se apaga donde muere (sin física, sin verse, sin patada ni cacareo), el mando sigue emparejado, revive en su punto de inicio en la ronda siguiente y al volver al menú; el teleporte nunca elige a un muerto.
- [ ] **Interpolación:** el movimiento se ve fluido en una TV a 60 Hz (el problema del evento); el salto es igual a cualquier framerate.
- [ ] **Cámara `MainCamera`:** el listón de progresión aparece donde toca (no en el origen) y el paneo del sonido sigue funcionando.
- [ ] **Bloques:** se rompen con 3 patadas normales desde la primera ronda.

**Menú**
- [ ] **Panel de ajustes:** correr Tools > Don't Chicken Out > Crear panel de ajustes en el menú con `MainMenu.unity` abierta; probar pestañas (LB/RB, Q/E), audio y pantalla (monitor, modo, VSync, límite de FPS) y que se guarden al reiniciar.
- [ ] **Menú de pausa con mando (playtest oct 2026):** A confirma, B y Start reanudan, la cruceta navega. Antes A/B estaban al revés: el mapa `UI` de `InputSystem.inputactions` tenía Select en `buttonEast`.
- [ ] **Pausa en countdown y entre rondas:** Start pausa durante el 3-2-1 y en la pantalla de victoria (no en la final del match); al reanudar la cuenta / la siguiente ronda sigue donde iba; salir al menú o reiniciar desde esa pausa no deja errores en consola. Ojo: el SFX del countdown (FMOD) no se pausa.
- [ ] **Cursor (build):** en pantalla completa se esconde a los 2 s sin mover el ratón y vuelve al moverlo; en ventana siempre se ve. No se activa en el editor.

---

## Ya arreglado — no volver a investigarlo

Apuntado para que nadie pierda una tarde re-diagnosticando algo que ya tiene dueño.

| Qué parecía | Qué era | Arreglado |
|---|---|---|
| Los huevos de la ronda anterior se quedaban en pantalla | `PoolingManager.GetCapsule` miraba siempre `capsuleList[0]` y, si estaba ocupada, instanciaba una cápsula nueva **sin registrarla**. `ResetPool` recorre esa lista, así que las huérfanas no se apagaban nunca | sep 2026 |
| La capa oscura del victory salía detrás de la torre (y en general el HUD detrás de bloques y pollos) | El sorting layer `UI` estaba por debajo de `Items` y `Characters`. Parcheado en runtime en sep 2026 (`UIManager.SetOverlayOnTop`); arreglado de raíz en oct 2026 reordenando en el editor: `Bg → Default → Items → Characters → UI → Pause → LoadingUI`, y el parche se borró. Solo el canvas `StartCanvas` del HUD usa `UI`. Diseño (oct 2026): **todo el HUD siempre encima**, también la capa oscura del lobby (`dimLayerBG`) sobre los pollos. **Reordenar sorting layers siempre desde Project Settings, nunca editando `TagManager.asset` a mano** | sep 2026 (parche) · oct 2026 (probado en editor) |
| Piezas que no volvían al pool y consola llena de `Pool is full` | `BlockOverlapCheck.DisableBlock` escondía un sub-bloque solapado apagándole collider y sprite, pero seguía contando como vivo para el `BlockScript` padre. Sin collider nadie podía matarlo, así que la pieza nunca llegaba a cero hijos. Ahora pasa por `BlockDamageable.RemoveFromPlay()` | sep 2026 |
| Un sacudón de cámara corto se sentía flojo | `UpdateShake` normalizaba la caída de amplitud contra la duración **por defecto**, no contra la del sacudón en curso: cualquier shake más corto que el default arrancaba a una fracción de su amplitud | sep 2026 |
| `Block Damage` de la súper patada "no hacía nada" | Sí llegaba al bloque. El valor que se probó (1) es exactamente el daño de una patada normal, así que no había diferencia que ver. Cerrado en oct 2026 (fila de abajo) | sep 2026 (diagnóstico) |
| `TireProjectile.prefab` llevaba el script `SpringDisc` (la llanta se llamaba "disco" por su versión colocable) | El script se renombró a `Tire` (`Items/Tire/Tire.cs`, mismo GUID: el prefab no se tocó). El prefab sigue llamándose `TireProjectile` | oct 2026 |
| El POW (y otros efectos con su propio reloj) seguían tras acabar la ronda: la cuenta salía al empezar la siguiente | Nada los cortaba al acabar la ronda. Ahora `GameManager.RoundEnded` (al pasar a Win o Menu) los corta: el POW se cancela y para su sonido, el yunque se destruye con su columna, la bomba lanzada se desactiva, y se cierran los power-ups de todos los pollos (la papa del ganador ya no explota en la pantalla de victoria). Además las muertes durante Win se ignoran (antes repetían la secuencia de victoria). **Efecto nuevo con temporizador → suscribirlo a `RoundEnded`** | oct 2026 |
| Un pollo con power-up lo conservaba (efecto y tinte) en la ronda siguiente | Dos cosas: (1) el cierre de los estados dependía de que el `Update` de cada uno notara que `isOnGame` bajó; ahora `GameManager.ResetPlayer` llama a `PlayerItemState.EndAll`. (2) Con dos estados a la vez, el segundo guardaba el **tinte** del primero como color "original" y lo devolvía al acabar; ahora se copian los originales reales y, al soltar un tinte, se reaplica el del estado que siga activo. Además (diseño, oct 2026) al morir también se cierran todos, y la papa caliente explota en el acto donde murió el portador | oct 2026 (pendiente de probar en editor) |
| En el evento (oct 2026) el juego iba a tirones en las TVs por HDMI y fluido en la laptop | La física corre a 50 Hz (`Fixed Timestep` 0.02) y nada estaba interpolado. En una pantalla de 60 Hz (casi todas las TVs) unos 10 frames por segundo repetían posición; en laptops de 120/144 Hz casi no se nota. Además `PlayerMovement` mezclaba `Update` y `FixedUpdate`, así que el salto variaba con el framerate. Ahora: **toda la simulación del pollo corre en el tick** y lee el `Rigidbody2D`; el `Transform` es la posición dibujada, interpolada entre ticks (pollo, bloques e ítems soltados, cápsulas). **Regla nueva:** reposicionar un pollo siempre con `PlayerController.TeleportTo`, nunca escribiendo `transform.position`; un `HoldableItem` desactiva la interpolación mientras está en mano. Con Fusion 2, `FixedUpdateNetwork()`/`Render()` sustituyen al tick y a la interpolación de Unity | oct 2026 (pendiente de probar en editor y en una TV a 60 Hz) |
| Seguimiento del evento: sin forma de diagnosticar ni de elegir pantalla | **Overlay de debug** (`Utility/DebugOverlay`, tecla **F3** o **L3 + R3** en cualquier mando, también en builds): FPS, peor frame, Hz y nombre del monitor, modo de ventana, VSync, límite y tick de física. **Ajustes de pantalla** (`Manager/DisplaySettings` + `UI/DisplaySettingsPanel`): monitor, modo (sin bordes / exclusiva / ventana), VSync y límite de FPS, guardados en `PlayerPrefs`. Viven en el **panel de Ajustes con pestañas** (`UI/SettingsPanel`: pestañas Audio y Pantalla, LB/RB o Q/E para cambiar, barra que marca la opción con foco vía `SelectionHighlight`/`SettingsRow`). Se monta con **Tools > Don't Chicken Out > Crear panel de ajustes en el menú** con `MainMenu.unity` abierta; sustituye los paneles sueltos anteriores. Nueva pestaña = subclase de `SettingsSection`. En un evento: mandar el juego solo a la TV (Monitor) en vez de duplicar pantalla | oct 2026 (falta correr el builder y probar) |
| Un bloque recién cambiado (cápsula) se podía colocar antes de tiempo | Los delays de `PlayerBlockHandler` eran `DOVirtual.DelayedCall` que no se cancelaban: un bloque que entraba a mitad de la cuenta heredaba la anterior. Ahora son temporizadores del tick que se reinician en cada cambio | oct 2026 |
| Código muerto | Borrados `CameraController.cs` (lo hace `CinemachineVerticalRig2D`), `CameraEffects.cs` (shake viejo) y `StartGame.cs` (Input Manager antiguo), los tres sin referencias en escenas, prefabs ni código; y los bloques comentados de `KickCollider` y `PlayerUI` | oct 2026 |
| La bomba "quemaba" una plaza del pool en cada explosión (`pool is full, spawn new one`) | `BombItem` acababa en `Destroy(gameObject)` aunque sale de `PoolingManager`. Ahora vuelve al pool con `SetActive(false)` tras `destroyDelay`, y `OnDisable` la deja como nueva (mecha apagada, colliders encendidos, colores originales) | oct 2026 (pendiente de probar en editor: que la explosión se vea entera y que una bomba reutilizada salga con el animator en reposo) |
| Bloque indestructible "de milagro" | `BlockDamageable` solo ponía la vida en `OnDisable`; uno activado sin pasar por ahí nacía con 0. Ahora también en `Awake` | oct 2026 |
| `Camera.main` null en todo el proyecto | La Camera de `CameraRig.prefab` no llevaba tag. Ahora es `MainCamera`; `GameplayCamera.Get()` es `Camera.main` y se quitaron los fallbacks de `AudioManager` y `ProgressionManager` | oct 2026 |
| La bomba no recibía el color de "en mano" | Su `Update` ocultaba el de `HoldableItem` (no era virtual). Ahora es `protected virtual` y `BombItem` llama a `base.Update()`. **Ítem nuevo con `Update` → `override` + `base.Update()`** | oct 2026 |
| Temporizadores de gameplay en tiempo real (se desincronizarían con Fusion 2) | Pasados al tick: mecha de la bomba, cuenta del POW (`PowSequence`) y aviso del yunque (lo cuenta `AnvilFall`; `AnvilZone` solo dibuja). Lo visual lee lo que queda menos el tiempo desde el último tick, como `PlayerItemState.RenderRemaining`. Siguen en tiempo real, a propósito, solo cosas de presentación: `HotPotatoVisual`, la aparición de `BlockScript`, el cierre de `AnvilZone` y la vibración del mando | oct 2026 (pendiente de probar en editor) |
| Un yunque podía sobrevivir a la ronda | `AnvilPickup` lo instanciaba fuera del pool, así que `ResetPool` no lo veía. Ahora sale de `PoolingManager.GetPooledItem(prefab)` y vuelve con `SetActive(false)`; su columna se cierra en `OnDisable` | oct 2026 (pendiente de probar en editor) |
| El rango de `Block Damage` de la súper patada estaba tapado (con 3 de vida, de 3 en adelante daba igual) | Decisión de diseño (oct 2026): **la súper patada rompe de un golpe el sub-bloque tocado, no la pieza entera**. Ya no es un valor tuneable: `SuperKickState.BlockDamage` es una constante que mata el sub-bloque sea cual sea su vida, así que sigue valiendo si un día se sube la vida de los bloques | oct 2026 (pendiente de probar en editor) |
| Los pollos no morían, se teletransportaban a una "zona de muertos" con `isOnGame` en `true` (contaban como el rival más alto; el teleporte a veces "hacía desaparecer" al pollo) | `PlayerController.SetAlive(false)` apaga al pollo **donde murió**: `Rigidbody2D.simulated = false` (fuera de toda consulta), sin tick, sin renderers, sin patada, sin cacarear (diseño, oct 2026), `isOnGame = false`. El GameObject y el `PlayerInput` siguen activos, así el mando no se desempareja. `GameManager.ResetPlayer` lo revive en su punto de inicio. `deathPos`/`DeathSpot` ya no existen; el teleporte descarta a cualquier pollo con `IsAlive == false`. Con Fusion 2 el objeto sobrevive a la muerte e `IsAlive` pasa a ser un flag replicado | oct 2026 (pendiente de probar en editor) |
| "Finales de línea mezclados" | Ya había `.gitattributes` con `* text=auto` desde el commit inicial y el índice está entero en LF: git no se ensucia. Lo mezclado era solo la copia local (3 scripts con CRLF y LF en el mismo archivo, ya normalizados) | oct 2026 |
