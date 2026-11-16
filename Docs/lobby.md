# Lobby de Pollos

Flujo previo a la partida, desde el menú principal hasta el mapa. Primera versión: nov 2026.
Fuentes: mockup del gallinero, UX scope (pain points + benchmark), matriz MUST/SHOULD/COULD de la demo y el
*Main Screen Flow*.

## Flujo

```
Menú principal ─Play─▶ escena Lobby
   Modo (Local · Online 🔒) ─▶ Mapa (The Barn + "?" 🔒) ─▶ Reglas (rondas: 1 · 3 · 5) ─▶ Gallinero
   ─P1 inicia─▶ escena del mapa ─▶ pausa breve ─▶ 3-2-1 ─▶ rondas ─▶ victoria final ─▶ Menú principal
```

- **Atrás** desanda el mismo camino. Atrás en *Modo* vuelve al menú principal. En el gallinero, Atrás de P1
  (sin estar listo) vuelve a *Reglas*.
- Las tres primeras pantallas las maneja **cualquier mando o teclado** (EventSystem + `MenuInputActions`), como el
  menú principal. El gallinero lee el input **de cada jugador por separado**.
- Online sale **bloqueado** (la demo es local + Steam Remote Play Together; matchmaking está en WON'T).

## Gallinero

Un cuarto por jugador (arte `Assets/Sprites/NewArt/Wait_*`). Orden de cuartos: **P1 arriba-izq, P2 abajo-izq,
P3 arriba-der, P4 abajo-der** (como el mockup).

| Estado del cuarto | Se ve |
|---|---|
| Vacío | Puerta cerrada con la oveja durmiendo + "Pulsa A · E · Shift Der. para entrar" |
| Ocupado | Se abre la puerta, aparece el pollo en su color (quieto, solo idle), badge `P#` con el color del jugador, "Personalizar · Próximamente" bloqueado, pista de botones |
| Listo | Sello "¡LISTO!" en el color del jugador + saltito del pollo |

Arriba: contador global **"LISTOS x/y"** (o "Faltan pollos: mínimo 2"). Abajo: "P1: [botón] ¡A jugar!" cuando se
puede empezar.

**Controles por jugador** (mapa `UI` de `InputSystem.inputactions`, filtrado al dispositivo y esquema de cada uno, así
dos jugadores de teclado no se cruzan y los remapeos futuros se heredan):

| | Mando | Teclado 1 | Teclado 2 |
|---|---|---|---|
| Entrar | A / Start | E | Shift Der. |
| Listo (y P1: empezar) | A / Start | E | Shift Der. |
| Cancelar listo / salir | B | Esc | Enter |

- **P1 (anfitrión)** es quien navegó el menú. Si su mando se desconecta, el siguiente jugador pasa a ser anfitrión.
- **Inicio:** P1 pulsa Listo otra vez cuando **todos los que están dentro** están listos y hay **≥ 2**.
- Start en un mando ya sentado cuenta como "Listo", no como "salir" (machacar Start no te saca del cuarto).
- Un mando que se desconecta deja su cuarto libre.

## Al llegar al mapa (nivel = escenario jugable, nada más)

Un nivel es solo el escenario: pollos con su etiqueta "Player N" encima, cuenta 3-2-1-GO!, rondas, **marcador de
huevos entre rondas** y vuelta al menú al ganar el match. `Main_Level` es la **plantilla base** de los demás niveles.
Todo lo del lobby antiguo en la arena se fue (nov 2026): heno para empezar (`StartGameButton`), `PlayerUI` (textos de
abajo y resultados), dim layer, panel de controles, contador "x / 4" y el join en la arena (`PlayerInputManager`).
Para limpiar una escena que aún los tenga y conectar el marcador: **Tools > Don't Chicken Out > Limpiar nivel
(quitar lobby antiguo)**.

- Un nivel **solo se juega desde el Lobby**. Abierto directo (Play sobre la escena) manda al Lobby.
- **Marcador entre rondas:** `RoundScoreboard` en el panel `PlayersPoints` (`PlayerNContent/PlayerN/PlayerNPoints/EggKImage`).
  Aparece al acabar la ronda (también en empate), se va al empezar la siguiente y se queda al ganar el match. Un huevo
  por ronda necesaria (1 · 3 · 5): la escena trae 3; para 5 clona el último y los encoge para que quepan en la fila.

- `PlayersManager` sienta a cada jugador **en el índice de su cuarto**: mismo color, mismo spawn, mismo
  "Player N". Con un cuarto vacío en medio, el índice queda con hueco (P1, P3).
- La cuenta regresiva arranca sola: espera a que termine la transición de círculo y luego
  `PlayersManager.lobbyCountdownDelay` (0,5 s) para que se vea a los pollos llegar.
- El ganador del match lo decide `GameManager` (`roundsWon >= RoundsToWin`); entre rondas espera
  `betweenRoundsTime` (2 s) con la cámara en el ganador.
- Si al cargar el mapa quedan < 2 jugadores (un mando se desenchufó en la transición), vuelve al Lobby.

## Dónde vive cada cosa

| Qué | Dónde |
|---|---|
| Lo que decidió el lobby (modo, mapa, rondas, jugadores + loadout) | `Scripts/Match/MatchSetup.cs` (estático, como `SessionData`) |
| Un mapa | `MapDefinitionSO` (`Assets/SOs/Maps/Map_*.asset`): id estable, nombre, escena, preview |
| Lista de mapas + nº de "?" | `Assets/SOs/Maps/MapCatalog.asset` |
| Pantallas | `Scripts/Lobby/`: `LobbyFlow` (orquesta), `ModeSelectScreen`, `MapSelectScreen` + `MapCard`, `MatchConfigScreen` + `OptionGroup` (fila de opciones excluyentes; mover el foco elige), `CoopLobby` (gallinero) + `LobbyRoom` (vista de un cuarto) |
| Escena | `Assets/Scenes/Lobby.unity`, se genera con **Tools > Don't Chicken Out > Crear escena Lobby** (la reemplaza entera) |
| Rondas para ganar | Opciones 1 · 3 · 5 (por defecto 3). `GameManager.RoundsToWin` ← `MatchSetup.RoundsToWin` |
| The Barn | `Map_TheBarn.asset` → escena `Main_Level` (el archivo de escena no se renombró). Preview = captura del inicio del nivel en `Assets/Sprites/Maps/Preview_TheBarn.png`; se rehace con **Tools > Don't Chicken Out > Actualizar preview de The Barn** |
| Sonidos | ids `lobby_join`, `lobby_ready`, `lobby_unready`, `lobby_leave`, `lobby_start` en `AudioEventTable.asset`, provisionalmente apuntando a eventos existentes (Spawn, Button Press/Hover, Start Round). Cuaac los cambia en FMOD sin tocar código |

**Añadir un mapa:** crear su escena (en Build Settings) → crear un `MapDefinitionSO` → añadirlo a `MapCatalog`
(y bajar `comingSoonSlots`). Sin código.

**Loadout:** `PlayerLoadout` ya viaja en cada `LobbySeat` como IDs de ancho fijo (tipo de pollo, color, gorro, ropa,
accesorio), listo para un `[Networked]` de Fusion (`fusion2-integracion.md` §4.1). Hoy siempre es el default: la
personalización está bloqueada y el color lo da el cuarto.

## Pendiente / abierto

- **Arte:** badge P1–P4, sello "¡LISTO!", candado, glifos de botones, card de mapa y "?" (hoy son placeholders de UI
  por defecto). Puerta que se abre con animación (hoy fade).
- **Huevos a 5 rondas:** caben encogidos (~50 px) en la fila de 250 px; si Arte quiere otro layout, añadir los huevos
  en la escena (`PlayerNPoints`) y la herramienta de limpieza los conecta.
- **SFX propios** para unirse (cacareo), listo (golpe seco), cancelar/salir y navegación (matriz de la demo).
- Las pantallas de menú las navega cualquier dispositivo, no solo P1.
- Tras la victoria final se vuelve al menú principal (según el flujo); "revancha" al gallinero queda para después.
