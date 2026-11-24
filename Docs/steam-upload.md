# Subir DCO a Steam (SteamPipe)

Fuente: video "How to Upload a Game to Steam - Step by Step Guide" (AuroDev). Esta es la versión aplicada a DCO.

## Panorama: subir ≠ publicar

Una app de Steam tiene **dos carriles independientes**, y cada uno pasa su propia revisión de Valve antes de hacerse público:

| Carril | Qué es | Estados |
|---|---|---|
| **Ficha de tienda** | Lo que la gente *ve*: textos, cápsulas, tráiler, capturas, tags. | Oculta → *Coming Soon* (acumula wishlists) → Lanzada |
| **Builds** | Lo que la gente *descarga*. Es lo que hace este doc. | Privado (solo partner) → con keys → lanzado |

Subir un build y ponerlo en `default` **no publica nada**: sin keys repartidas ni botón *Release*, solo el equipo de Steamworks puede jugarlo.

**Quién puede jugar, de menos a más:** equipo (builds privados) → testers con key (Request Keys) → Playtest (la gente se apunta, tú das acceso por tandas) → Demo pública (gratis) → juego lanzado.

**Demo y Playtest son apps aparte**, con su propio App ID, depots, builds y launch options, pero cuelgan de la app de DCO (la demo sale en su ficha, el playtest es un botón en ella). Se crean desde la página de la app principal en Steamworks y no pagan la tarifa de Steam Direct. El flujo de subida de abajo es idéntico para ellas, cambiando App ID / Depot ID. Steam Next Fest pide una demo.

Fechas y requisitos que hay que **verificar en Steamworks antes de planificar** (cambian): la ficha debe estar en *Coming Soon* un mínimo de semanas antes del lanzamiento, la revisión de Valve tarda días hábiles, y la inscripción a cada Next Fest cierra semanas antes del evento.

## Las 4 palabras de Steam

| Término | Qué es | En DCO |
|---|---|---|
| **Depot** | Una carpeta de archivos que Steam entrega a los jugadores. | Uno solo: el build de Windows. |
| **Build** | Una subida. Cada vez que subes depots, Steam crea un build nuevo con número (BuildID). | Un build por versión (`bundleVersion`, hoy 0.10.0). |
| **Branch** | Qué build recibe cada jugador. `default` es la que ven todos los que tienen acceso. | `default` por ahora; luego una `beta` con contraseña para probar. |
| **Package** | Cómo se obtiene acceso al juego (comprarlo, una key). El de la tienda se crea solo. | No tocar. Las keys de testers salen de **Request Keys**. |

Ningún paso de aquí publica el juego: sin keys repartidas, solo las cuentas del partner pueden jugarlo.

## Datos de la app

- **Partner:** cuenta de Raymi Games en Steamworks (si tu usuario está en varios partners, cambiar en el desplegable de arriba a la derecha; no confundir con otros juegos).
- **App ID de DCO:** `5513740`.
- **Depot ID (Windows, único):** `5513741` "Don't Chicken Out! Content" — configuración *Todos*, ya está en 3 paquetes (no hace falta añadirlo a ninguno).
- **Primer upload:** 23 nov 2026, BuildID `26882905` (v0.10.0). SDK en `E:\GameDev\SteamworksSDK\` (máquina de builds); los VDF generados están en `sdk\tools\ContentBuilder\scripts\` y los logs en `ContentBuilder\output\`.
- Menús en Steamworks (UI en español): Editar configuración → **SteamPipe** > *Compilaciones* (= Builds), *Repositorios* (= Depots), *Cargas web*; **Instalación** > General; **Publicar**.

## Una sola vez (setup)

1. **Steamworks SDK**: bajarlo desde la home de Steamworks, descomprimirlo en una ruta fija (p. ej. `E:\GameDev\SteamworksSDK\`). No va en el repo.
2. **Carpeta del depot**: crear `sdk\tools\ContentBuilder\content\windows\`.
3. **En Unity**: Tools > Don't Chicken Out > Steam > **Elegir carpeta del depot (ContentBuilder)** → elegir esa carpeta. Se guarda por máquina (EditorPrefs).
4. **SteamPipeGUI**: descomprimir `sdk\tools\SteamPipeGUI.zip` y abrirlo. Rellenar:
   - **App ID**: el de DCO (página de la app en Steamworks).
   - **Depot list**: añadir la carpeta `content\windows`. Depot ID = App ID + 1 normalmente; comprobar en Steamworks > SteamPipe > Depots.
   - **ContentBuilder path** y la cuenta de Steamworks (la escribe cada uno; pedirá código de Steam Guard).
   - Pulsar **Generate VDFs** (solo la primera vez o si se añade un depot).
5. **Launch option** (Steamworks > Installation > General Installation): añadir una con
   - Executable: `DontChickenOut.exe` (nombre fijo, lo pone `SteamBuildTool`; está en la raíz del depot, sin subcarpeta)
   - Operating System: Windows
   - Luego **Publish** > Prepare for Publishing > Publish to Steam. Esto publica la *configuración*, no el juego.

## Cada vez que se sube una versión

1. Subir `bundleVersion` si toca (Project Settings > Player).
2. Unity: Tools > Don't Chicken Out > Steam > **Build Windows para Steam**. Genera `Builds/Steam/Windows/` y lo copia al depot.
   - Con Unity cerrado también vale: `Unity.exe -batchmode -quit -projectPath . -executeMethod SteamBuildTool.BuildFromCommandLine -buildTarget Win64`
3. SteamPipeGUI: descripción del build = versión + qué cambia → **Upload**. Esperar el "success".
4. Steamworks > SteamPipe > **Builds**: en el build nuevo elegir la branch (`default`) → **Preview Change** → confirmar.
5. Reiniciar el cliente de Steam y jugar desde la biblioteca.
   - El cliente tiene que estar con una cuenta **miembro del partner de Raymi** con acceso a la app (Usuarios y permisos > Administrar usuarios). Una cuenta personal que no está en el partner no ve el juego hasta que la inviten (o le den una key).

## Si algo falla

| Síntoma | Causa típica |
|---|---|
| Steam dice que falta el ejecutable / no arranca | Launch option mal escrita o sin publicar. Revisar `DontChickenOut.exe` exacto, publicar y reiniciar Steam. |
| El juego no aparece en la biblioteca | La cuenta no está en el partner group de la app o Steam no se reinició. |
| Upload falla con "access denied" | La cuenta de Steamworks no tiene permiso *Edit App Metadata* / *Publish*. |
| El depot sube vacío | La carpeta del depot no es la que se eligió en Unity, o el build falló (ver consola). |

## Updates, branches y DLC (modelo mental)

- **Build = commit inmutable.** Cada upload crea uno nuevo con su BuildID; nunca se edita.
- **Branch = puntero** a un build. Cambiar qué build tiene una branch es instantáneo y no sube nada.
- **Update** = subir build nuevo + apuntar `default` a él. Los clientes descargan solo los trozos de archivos que cambiaron (SteamPipe trocea y compara), no el juego entero.
- **Rollback** = apuntar `default` al build anterior. Un clic, sin recompilar. Por eso los builds viejos no se borran.
- **Flujo recomendado para DCO:** subir siempre primero a `beta` (con contraseña), probar desde Steam, y solo entonces mover `default`. `default` se cambia a mano en la web.
- Los jugadores eligen branch en Steam: clic derecho en el juego > Propiedades > Betas (con la contraseña si la tiene).
- Una launch option se puede limitar a una branch (p. ej. un exe de debug solo en `beta`).
- **Varios depots** = varios paquetes de archivos dentro de un mismo build: por SO (Windows/Mac/Linux), por idioma, o contenido de DLC. Cada build sube todos los depots juntos.
- **DLC = app aparte** (tipo DLC, su App ID) que el jugador compra. O desbloquea contenido que ya viene en el depot base, o trae su propio depot que solo descargan sus dueños. En ambos casos el juego tiene que **preguntarle a Steam** si el jugador lo posee → requiere integrar el SDK (Steamworks.NET o Facepunch.Steamworks), que **DCO aún no tiene**. Decisión ligada a la tienda de skins (Steam Inventory vs backend propio, ver `overview.md`).
- **Demo / Playtest** no son branches: son apps aparte (ver Panorama).

## Remote Play Together (la demo es local + RPT)

- No necesita SDK ni código: Steam transmite la pantalla y los invitados aparecen como **mandos virtuales**. El juego tiene que arrancarse **desde Steam**.
- Se activa en la ficha de la tienda (Store Page > Basic Info > funciones de Remote Play) y luego Publish. Verificar el nombre exacto de la casilla al hacerlo.
- Probar: lanzar DCO desde Steam, Shift+Tab > amigo > *Remote Play Together*. El invitado entra al Gallinero con su mando como cualquier jugador local.

## Estado y pendientes (23 nov 2026)

Hecho: SDK instalado, depot configurado, build `26882905` (v0.10.0) subido y puesto en `default`.

- [ ] **Probar desde Steam:** cliente con la cuenta raymigames, o invitar la cuenta personal al partner (Usuarios y permisos > Administrar usuarios > Invitar, acceso a DCO con permisos de editar/publicar). Confirmar con quien administra la cuenta raymigames antes de tocar permisos.
- [ ] Verificar que la launch option `DontChickenOut.exe` (Windows) existe y está **publicada**.
- [ ] Añadir **autenticador de Steam Mobile** a la cuenta que sube builds: sin él no se puede cambiar `default` de una app lanzada, y añadirlo bloquea 3 días. Hacerlo mucho antes de demo/lanzamiento.
- [ ] Crear branch `beta` con contraseña y adoptar el flujo beta → default.

## Para más adelante

- Branch `beta` con contraseña para builds que aún no van a `default`.
- Keys para testers/publisher: Steamworks > Request Keys (tardan unas horas).
- Automatizar el upload con `steamcmd` + los VDF que generó SteamPipeGUI (sin GUI).
