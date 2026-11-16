using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MenuPanelBuilderKit;

/// <summary>
/// Tools > Don't Chicken Out > Crear escena Lobby.
/// Builds Assets/Scenes/Lobby.unity from scratch: the hen house art (world
/// sprites), one room per player, and the menu screens on a canvas laid
/// over it (mode → map → rules → hen house). Also creates the map catalog
/// and adds the scene to Build Settings. Everything it creates is a normal,
/// editable hierarchy — Arte can restyle it afterwards; re-running the tool
/// overwrites the scene.
/// </summary>
internal static class LobbySceneBuilder
{
    private const string ScenePath   = "Assets/Scenes/Lobby.unity";
    private const string MapsFolder  = "Assets/SOs/Maps";
    private const string MapPath     = MapsFolder + "/Map_TheBarn.asset";
    private const string OldMapPath  = MapsFolder + "/Map_MainLevel.asset";
    private const string CatalogPath = MapsFolder + "/MapCatalog.asset";

    // The Barn = the Main_Level scene. Its selector card shows the start of the level.
    private const string BarnSceneName   = "Main_Level";
    private const string BarnScenePath   = "Assets/Scenes/Main_Level.unity";
    private const string BarnPreviewPath = "Assets/Sprites/Maps/Preview_TheBarn.png";

    private const string ArtFolder = "Assets/Sprites/NewArt/";

    private static readonly Color Accent    = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Color DarkWood  = new(0.21f, 0.13f, 0.06f, 1f);
    private static readonly Color PanelDark = new(0.12f, 0.10f, 0.18f, 0.95f);

    private const float PPU = 100f; // NewArt import setting

    // Room interiors in Wait_Fondo pixels (1920x1080, origin top-left).
    // Slot order follows the mockup: P1 top-left, P2 bottom-left, P3 top-right, P4 bottom-right.
    private static readonly Rect[] RoomsPx =
    {
        new(95f,   103f, 815f, 417f),
        new(95f,   577f, 815f, 398f),
        new(1010f, 103f, 815f, 417f),
        new(1010f, 577f, 815f, 398f),
    };
    // Floor line (px from top) of each room, where the chicken's feet go.
    private static readonly float[] FloorPx = { 505f, 950f, 505f, 950f };

    [MenuItem("Tools/Don't Chicken Out/Crear escena Lobby")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Crear escena Lobby",
                $"{ScenePath} ya existe y se va a reemplazar. ¿Seguir?", "Reemplazar", "Cancelar"))
            return;

        Build();
    }

    /// <summary>Entry point for -batchmode -executeMethod LobbySceneBuilder.BuildBatch.</summary>
    public static void BuildBatch()
    {
        try
        {
            Build();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void Build()
    {
        EnsureCatalog();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Loaded after NewScene: the scene switch unloads the in-memory instance
        // and a reference taken before it would be saved as null.
        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalogSO>(CatalogPath);

        Camera cam = BuildCamera();
        LobbyRoom[] rooms = BuildHenHouse();

        TMP_FontAsset font = FindFont("CalSans-Regular SDF");
        TMP_DefaultControls.Resources r = DefaultResources();

        Canvas canvas = BuildCanvas(cam);
        InputSystemUIInputModule uiModule = BuildEventSystem();

        ModeSelectScreen  mode   = BuildModeScreen(canvas.transform, font, r);
        MapSelectScreen   map    = BuildMapScreen(canvas.transform, font, r, catalog);
        MatchConfigScreen config = BuildConfigScreen(canvas.transform, font, r);
        CoopLobby         coop   = BuildCoopScreen(canvas.transform, font, rooms);

        // The hen house HUD sits under the menu panels: those dim it.
        coop.transform.SetAsFirstSibling();

        var flowGo = new GameObject("LobbyFlow");
        LobbyFlow flow = flowGo.AddComponent<LobbyFlow>();
        Assign(flow, ("modeScreen", mode), ("mapScreen", map), ("configScreen", config),
                     ("coopScreen", coop), ("uiModule", uiModule));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();

        Debug.Log($"[LobbySceneBuilder] {ScenePath} creada.");
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private static MapCatalogSO EnsureCatalog()
    {
        if (!AssetDatabase.IsValidFolder(MapsFolder))
            AssetDatabase.CreateFolder("Assets/SOs", "Maps");

        // First version called the map "Main Level": rename the asset (GUID kept, so the catalog still points at it).
        if (AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(OldMapPath) != null &&
            AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(MapPath) == null)
        {
            string error = AssetDatabase.MoveAsset(OldMapPath, MapPath);
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"[LobbySceneBuilder] {error}");
        }

        var map = AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(MapPath);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<MapDefinitionSO>();
            AssetDatabase.CreateAsset(map, MapPath);
        }

        Sprite preview = AssetDatabase.LoadAssetAtPath<Sprite>(BarnPreviewPath);
        if (preview == null) preview = RenderBarnPreview();

        var mapSo = new SerializedObject(map);
        mapSo.FindProperty("id").stringValue          = "the_barn";
        mapSo.FindProperty("displayName").stringValue = "The Barn";
        mapSo.FindProperty("sceneName").stringValue   = BarnSceneName;
        if (preview != null) mapSo.FindProperty("preview").objectReferenceValue = preview;
        mapSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);

        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<MapCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var so = new SerializedObject(catalog);
            SerializedProperty maps = so.FindProperty("maps");
            maps.arraySize = 1;
            maps.GetArrayElementAtIndex(0).objectReferenceValue = map;
            so.FindProperty("comingSoonSlots").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        return catalog;
    }

    [MenuItem("Tools/Don't Chicken Out/Actualizar preview de The Barn")]
    private static void RefreshBarnPreviewFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string previous = SceneManager.GetActiveScene().path;
        Sprite preview = RenderBarnPreview();
        var map = AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(MapPath);
        if (map != null && preview != null)
        {
            var so = new SerializedObject(map);
            so.FindProperty("preview").objectReferenceValue = preview;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
    }

    /// <summary>
    /// Opens Main_Level (without saving it), frames the camera where the
    /// round starts (the Cinemachine camera), hides every canvas and renders
    /// a card-sized picture into <see cref="BarnPreviewPath"/>.
    /// </summary>
    private static Sprite RenderBarnPreview()
    {
        if (!System.IO.File.Exists(BarnScenePath))
        {
            Debug.LogWarning($"[LobbySceneBuilder] {BarnScenePath} not found: no map preview.");
            return null;
        }

        EditorSceneManager.OpenScene(BarnScenePath, OpenSceneMode.Single);

        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null) return null;

        var vcam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (vcam != null)
        {
            Vector3 p = vcam.transform.position;
            cam.transform.position = new Vector3(p.x, p.y, cam.transform.position.z);
            if (cam.orthographic) cam.orthographicSize = vcam.Lens.OrthographicSize;
        }

        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            c.enabled = false;

        const int w = 1236, h = 800; // the map card's aspect (340x220)
        var rt = new RenderTexture(w, h, 24);
        RenderTexture previousTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = previousTarget;
        Object.DestroyImmediate(rt);

        if (!AssetDatabase.IsValidFolder("Assets/Sprites/Maps"))
            AssetDatabase.CreateFolder("Assets/Sprites", "Maps");
        System.IO.File.WriteAllBytes(BarnPreviewPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(BarnPreviewPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(BarnPreviewPath);
        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled    = false;
        importer.SaveAndReimport();

        // Leave Main_Level untouched: the scene is discarded by whatever opens next.
        return AssetDatabase.LoadAssetAtPath<Sprite>(BarnPreviewPath);
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;

        int menu = scenes.FindIndex(s => s.path.EndsWith("/MainMenu.unity"));
        scenes.Insert(menu >= 0 ? menu + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ── World: camera + hen house ─────────────────────────────────────────────

    private static Camera BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = 1080f / PPU / 2f; // the art is 1920x1080: fills the height
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = DarkWood;
        go.AddComponent<FMODUnity.StudioListener>(); // FMOD warns without one in the scene
        return cam;
    }

    private static LobbyRoom[] BuildHenHouse()
    {
        var root = new GameObject("HenHouse");

        SpriteRenderer bg = NewSprite("Background", root.transform, LoadSprite(ArtFolder + "Wait_Fondo.jpg"), Vector2.zero);
        bg.sortingLayerName = "Bg";

        var rooms = new LobbyRoom[RoomsPx.Length];
        for (int i = 0; i < RoomsPx.Length; i++)
        {
            Rect px = RoomsPx[i];
            var roomGo = new GameObject($"Room_P{i + 1}");
            roomGo.transform.SetParent(root.transform, false);
            roomGo.transform.position = PxToWorld(px.center);

            bool top = i % 2 == 0;
            SpriteRenderer door = NewSprite("Door", roomGo.transform,
                LoadSprite(ArtFolder + (top ? "Wait_Puerta1-Arriba.jpg" : "Wait_Puerta2-Abajo.jpg")), Vector2.zero);
            door.sortingLayerName = "UI";
            door.sortingOrder     = -10;
            // Fit the door to the room opening.
            Vector2 size = door.sprite.bounds.size;
            door.transform.localScale = new Vector3(px.width / PPU / size.x, px.height / PPU / size.y, 1f);

            var anchor = new GameObject("ChickenAnchor").transform;
            anchor.SetParent(roomGo.transform, false);
            anchor.position = PxToWorld(new Vector2(px.center.x, FloorPx[i]));

            rooms[i] = roomGo.AddComponent<LobbyRoom>();
            Assign(rooms[i], ("door", door), ("chickenAnchor", anchor));
        }

        Sprite decoSprite = LoadSprite(ArtFolder + "Wait_Decoracion.png");
        SpriteRenderer deco = NewSprite("Decoration", root.transform, decoSprite,
            new Vector2(0f, -540f / PPU + decoSprite.bounds.extents.y));
        deco.sortingLayerName = "UI";
        deco.sortingOrder     = -5;

        return rooms;
    }

    // ── Canvas + input ────────────────────────────────────────────────────────

    private static Canvas BuildCanvas(Camera cam)
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode       = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera      = cam;
        canvas.planeDistance    = 5f;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder     = 10;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 1f; // same rule as the camera: UI pixels == art pixels
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static InputSystemUIInputModule BuildEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();

        var router = go.AddComponent<MenuInputRouter>();
        var menuActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/MenuInputActions.inputactions");
        Assign(router, ("uiInputModule", module), ("menuActions", menuActions));
        var so = new SerializedObject(router);
        so.FindProperty("keepExistingPrimary").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return module;
    }

    // ── Screens ───────────────────────────────────────────────────────────────

    private static ModeSelectScreen BuildModeScreen(Transform canvas, TMP_FontAsset font, TMP_DefaultControls.Resources r)
    {
        GameObject panel = NewWindowPanel("ModeScreen", canvas, new Vector2(720f, 540f), r, out GameObject window);
        Label(window, "Title", "Modo de juego", font, 56f, new Vector2(0f, 190f), new Vector2(640f, 80f),
              TextAlignmentOptions.Center);

        Button local  = NewButton(window, "LocalButton",  "Local",  font, r, new Vector2(0f, 50f),  new Vector2(440f, 96f));
        Button online = NewButton(window, "OnlineButton", "Online", font, r, new Vector2(0f, -76f), new Vector2(440f, 96f));
        Emphasize(local, Accent);
        Emphasize(online, Accent);
        Dim(online);

        TextMeshProUGUI badge = Label(online.gameObject, "LockedBadge", "Próximamente", font, 24f,
                                      new Vector2(160f, 34f), new Vector2(200f, 40f), TextAlignmentOptions.Center);
        badge.color = Accent;
        badge.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        Label(window, "Hint", "Online llegará más adelante", font, 24f, new Vector2(0f, -190f),
              new Vector2(640f, 40f), TextAlignmentOptions.Center).color = new Color(1f, 1f, 1f, 0.55f);

        AddFocusBar(window, r);

        ModeSelectScreen screen = panel.AddComponent<ModeSelectScreen>();
        Assign(screen, ("firstSelected", local.gameObject), ("localButton", local),
                       ("onlineButton", online), ("onlineLockedBadge", badge.gameObject));
        return screen;
    }

    private static MapSelectScreen BuildMapScreen(Transform canvas, TMP_FontAsset font, TMP_DefaultControls.Resources r,
                                                  MapCatalogSO catalog)
    {
        GameObject panel = NewWindowPanel("MapScreen", canvas, new Vector2(1320f, 860f), r, out GameObject window);
        Label(window, "Title", "Elige el mapa", font, 56f, new Vector2(0f, 360f), new Vector2(1200f, 80f),
              TextAlignmentOptions.Center);
        TextMeshProUGUI focused = Label(window, "FocusedName", "", font, 44f, new Vector2(0f, 285f),
                                        new Vector2(1200f, 64f), TextAlignmentOptions.Center);
        focused.color = Accent;

        GameObject grid = NewRect("Grid", window.transform);
        Place(grid, new Vector2(0f, -70f), new Vector2(1200f, 600f));
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize        = new Vector2(360f, 240f);
        layout.spacing         = new Vector2(30f, 30f);
        layout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.childAlignment  = TextAnchor.UpperCenter;

        MapCard template = BuildMapCard(grid.transform, font, r);
        template.gameObject.SetActive(false);
        AddFocusBar(window, r);

        MapSelectScreen screen = panel.AddComponent<MapSelectScreen>();
        Assign(screen, ("catalog", catalog), ("cardTemplate", template), ("grid", grid.transform),
                       ("focusedName", focused));
        return screen;
    }

    private static MapCard BuildMapCard(Transform grid, TMP_FontAsset font, TMP_DefaultControls.Resources r)
    {
        GameObject card = NewRect("MapCardTemplate", grid);
        Image bg = card.AddComponent<Image>();
        bg.sprite = r.standard;
        bg.type   = Image.Type.Sliced;
        bg.color  = new Color(0.25f, 0.21f, 0.33f, 1f);
        Button button = card.AddComponent<Button>();
        button.targetGraphic = bg;
        Emphasize(button, Accent); // focused card tints accent, plus the SelectionBar behind it
        card.AddComponent<UIButtonSFX>();

        GameObject previewGo = NewRect("Preview", card.transform);
        Inset(previewGo, 10f);
        Image preview = previewGo.AddComponent<Image>();
        preview.raycastTarget = false;

        GameObject strip = NewRect("NameStrip", card.transform);
        var stripRt = (RectTransform)strip.transform;
        stripRt.anchorMin = new Vector2(0f, 0f);
        stripRt.anchorMax = new Vector2(1f, 0f);
        stripRt.pivot     = new Vector2(0.5f, 0f);
        stripRt.offsetMin = new Vector2(10f, 10f);
        stripRt.offsetMax = new Vector2(-10f, 66f);
        Image stripImage = strip.AddComponent<Image>();
        stripImage.color = new Color(0f, 0f, 0f, 0.6f);
        stripImage.raycastTarget = false;
        TextMeshProUGUI name = Label(strip, "Name", "Mapa", font, 32f, Vector2.zero, new Vector2(320f, 56f),
                                     TextAlignmentOptions.Center);

        GameObject locked = NewRect("LockedOverlay", card.transform);
        Inset(locked, 10f);
        Image lockedImage = locked.AddComponent<Image>();
        lockedImage.color = new Color(0.10f, 0.08f, 0.14f, 1f);
        lockedImage.raycastTarget = false;
        TextMeshProUGUI q = Label(locked, "QuestionMark", "?", font, 150f, Vector2.zero, new Vector2(300f, 200f),
                                  TextAlignmentOptions.Center);
        q.color = new Color(1f, 1f, 1f, 0.35f);

        MapCard mapCard = card.AddComponent<MapCard>();
        Assign(mapCard, ("preview", preview), ("nameLabel", name), ("lockedOverlay", locked));
        return mapCard;
    }

    private static MatchConfigScreen BuildConfigScreen(Transform canvas, TMP_FontAsset font, TMP_DefaultControls.Resources r)
    {
        GameObject panel = NewWindowPanel("ConfigScreen", canvas, new Vector2(920f, 580f), r, out GameObject window);
        Label(window, "Title", "Reglas de la partida", font, 56f, new Vector2(0f, 215f), new Vector2(840f, 80f),
              TextAlignmentOptions.Center);
        TextMeshProUGUI mapLabel = Label(window, "MapLabel", "", font, 30f, new Vector2(0f, 145f),
                                         new Vector2(840f, 50f), TextAlignmentOptions.Center);
        mapLabel.color = new Color(1f, 1f, 1f, 0.6f);

        Label(window, "RoundsLabel", "Rondas para ganar", font, 36f, new Vector2(0f, 60f),
              new Vector2(840f, 60f), TextAlignmentOptions.Center);

        // Three options side by side: 1 · 3 · 5. The chosen one is painted in the accent colour.
        int[] values = { 1, 3, 5 };
        GameObject row = NewRect("RoundsToWin", window.transform);
        Place(row, new Vector2(0f, -35f), new Vector2(480f, 100f));
        var buttons = new Button[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            buttons[i] = NewButton(row, $"Rounds_{values[i]}", values[i].ToString(), font, r,
                                   new Vector2((i - 1) * 160f, 0f), new Vector2(130f, 100f));
            buttons[i].GetComponentInChildren<TextMeshProUGUI>().fontSize = 52f;
            // Chosen/idle colour lives on the Image; focus only brightens the hover.
            ColorBlock colors = buttons[i].colors;
            colors.normalColor = colors.selectedColor = colors.pressedColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            buttons[i].colors = colors;
        }

        Button cont = NewButton(window, "ContinueButton", "Continuar", font, r, new Vector2(0f, -185f),
                                new Vector2(440f, 96f));
        Emphasize(cont, Accent); // focused = accent fill (like the chosen round) + the pulsing SelectionBar

        for (int i = 0; i < buttons.Length; i++)
            SetNavigation(buttons[i], up: null, down: cont,
                          left:  i > 0 ? buttons[i - 1] : null,
                          right: i < buttons.Length - 1 ? buttons[i + 1] : null);
        SetNavigation(cont, up: buttons[1], down: null);

        OptionGroup group = row.AddComponent<OptionGroup>();
        var so = new SerializedObject(group);
        SerializedProperty optionsProp = so.FindProperty("options");
        SerializedProperty valuesProp  = so.FindProperty("values");
        optionsProp.arraySize = buttons.Length;
        valuesProp.arraySize  = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            optionsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            valuesProp.GetArrayElementAtIndex(i).intValue = values[i];
        }
        so.FindProperty("chosenColor").colorValue = Accent;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddFocusBar(window, r);

        MatchConfigScreen screen = panel.AddComponent<MatchConfigScreen>();
        Assign(screen, ("firstSelected", cont.gameObject), ("roundsToWin", group), ("continueButton", cont),
                       ("mapLabel", mapLabel));
        return screen;
    }

    private static CoopLobby BuildCoopScreen(Transform canvas, TMP_FontAsset font, LobbyRoom[] rooms)
    {
        GameObject root = NewRect("HenHouseScreen", canvas);
        Stretch(root);

        TextMeshProUGUI counter = Label(root, "ReadyCounter", "", font, 34f, new Vector2(0f, 514f),
                                        new Vector2(700f, 50f), TextAlignmentOptions.Center);
        counter.color = DarkWood;
        TextMeshProUGUI prompt = Label(root, "StartPrompt", "", font, 42f, new Vector2(0f, -495f),
                                       new Vector2(1100f, 70f), TextAlignmentOptions.Center);
        prompt.color = Accent;

        for (int i = 0; i < rooms.Length; i++)
            BuildRoomHud(root, font, i, rooms[i]);

        CoopLobby coop = root.AddComponent<CoopLobby>();

        var playerActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem.inputactions");
        var materials     = AssetDatabase.LoadAssetAtPath<MaterialsSO>("Assets/SOs/PlayerMaterials.asset");
        var playerPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
        Transform rig     = playerPrefab != null ? FindDeep(playerPrefab.transform, "Rig") : null;
        if (rig == null) Debug.LogWarning("[LobbySceneBuilder] PlayerPrefab/Rig not found: rooms will have no chicken.");

        var so = new SerializedObject(coop);
        SerializedProperty roomsProp = so.FindProperty("rooms");
        roomsProp.arraySize = rooms.Length;
        for (int i = 0; i < rooms.Length; i++)
            roomsProp.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
        so.FindProperty("playerActions").objectReferenceValue       = playerActions;
        so.FindProperty("materials").objectReferenceValue           = materials;
        so.FindProperty("chickenVisualSource").objectReferenceValue = rig != null ? rig.gameObject : null;
        so.FindProperty("readyCounter").objectReferenceValue        = counter;
        so.FindProperty("startPrompt").objectReferenceValue         = prompt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return coop;
    }

    private static void BuildRoomHud(GameObject screen, TMP_FontAsset font, int slot, LobbyRoom room)
    {
        Rect px = RoomsPx[slot];
        Vector2 c = new(px.center.x - 960f, 540f - px.center.y); // canvas units == art pixels

        GameObject hud = NewRect($"RoomHud_P{slot + 1}", screen.transform);
        Place(hud, c, px.size);

        // P-badge over the chicken's head.
        GameObject badgeGo = NewRect("Badge", hud.transform);
        Place(badgeGo, new Vector2(0f, 120f), new Vector2(120f, 60f));
        Image badge = badgeGo.AddComponent<Image>();
        badge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        badge.type   = Image.Type.Sliced;
        badge.raycastTarget = false;
        TextMeshProUGUI badgeLabel = Label(badgeGo, "Label", $"P{slot + 1}", font, 40f, Vector2.zero,
                                           new Vector2(120f, 60f), TextAlignmentOptions.Center);
        TextMeshProUGUI host = Label(hud, "HostTag", "Anfitrión", font, 22f, new Vector2(0f, 168f),
                                     new Vector2(200f, 32f), TextAlignmentOptions.Center);
        host.color = DarkWood;

        // Hint on the right half, on a dark backdrop so it reads on wall and door.
        GameObject hintRoot = NewRect("Hint", hud.transform);
        Place(hintRoot, new Vector2(250f, -20f), new Vector2(300f, 150f));
        Image hintBg = hintRoot.AddComponent<Image>();
        hintBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        hintBg.type   = Image.Type.Sliced;
        hintBg.color  = new Color(0f, 0f, 0f, 0.55f);
        hintBg.raycastTarget = false;
        TextMeshProUGUI hint = Label(hintRoot, "Label", "", font, 28f, Vector2.zero, new Vector2(285f, 140f),
                                     TextAlignmentOptions.Center);

        // Ready stamp on the left half.
        GameObject stamp = NewRect("ReadyStamp", hud.transform);
        Place(stamp, new Vector2(-235f, 40f), new Vector2(240f, 84f));
        stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
        Image stampImage = stamp.AddComponent<Image>();
        stampImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        stampImage.type   = Image.Type.Sliced;
        stampImage.raycastTarget = false;
        Label(stamp, "Label", "¡LISTO!", font, 46f, Vector2.zero, new Vector2(240f, 84f),
              TextAlignmentOptions.Center);

        // Locked customization: visible so players know it is coming.
        GameObject customize = NewRect("CustomizeLocked", hud.transform);
        Place(customize, new Vector2(-235f, -110f), new Vector2(260f, 76f));
        Image customizeBg = customize.AddComponent<Image>();
        customizeBg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        customizeBg.type   = Image.Type.Sliced;
        customizeBg.color  = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        customizeBg.raycastTarget = false;
        Label(customize, "Label", "Personalizar", font, 28f, new Vector2(0f, 12f), new Vector2(250f, 40f),
              TextAlignmentOptions.Center).color = new Color(1f, 1f, 1f, 0.6f);
        Label(customize, "Locked", "Próximamente", font, 20f, new Vector2(0f, -18f), new Vector2(250f, 30f),
              TextAlignmentOptions.Center).color = Accent;

        badgeGo.SetActive(false);
        host.gameObject.SetActive(false);
        stamp.SetActive(false);
        customize.SetActive(false);

        Assign(room, ("badge", badge), ("badgeLabel", badgeLabel), ("hintLabel", hint), ("hintRoot", hintRoot),
                     ("readyStamp", stamp), ("readyStampImage", stampImage), ("customizeLocked", customize),
                     ("hostTag", host.gameObject));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Vector3 PxToWorld(Vector2 px) => new((px.x - 960f) / PPU, (540f - px.y) / PPU, 0f);

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.IO.FileNotFoundException($"Sprite not found: {path}");
        return sprite;
    }

    private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, Vector2 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    /// <summary>
    /// Same focus marker as the settings panel: a pulsing accent bar that slides
    /// behind whatever has the focus. Focused buttons are also filled with the
    /// accent (like the chosen round); the pulsing bar is what tells "focus"
    /// apart from "chosen" when both are yellow.
    /// </summary>
    private static void AddFocusBar(GameObject window, TMP_DefaultControls.Resources r)
    {
        GameObject barGo = NewRect("SelectionBar", window.transform);
        barGo.transform.SetAsFirstSibling(); // behind every control of the window
        Place(barGo, Vector2.zero, new Vector2(100f, 60f));
        Image barImage = barGo.AddComponent<Image>();
        barImage.sprite = r.standard;
        barImage.type   = Image.Type.Sliced;
        barImage.color  = new Color(Accent.r, Accent.g, Accent.b, 0.5f);
        barImage.raycastTarget = false;

        SelectionHighlight highlight = window.AddComponent<SelectionHighlight>();
        var so = new SerializedObject(highlight);
        so.FindProperty("bar").objectReferenceValue      = barGo.transform;
        so.FindProperty("barImage").objectReferenceValue = barImage;
        so.FindProperty("accent").colorValue             = Accent;
        so.FindProperty("padding").vector2Value          = new Vector2(30f, 24f);
        // Stronger than the settings panel: here it sits next to accent-filled options.
        so.FindProperty("minAlpha").floatValue           = 0.45f;
        so.FindProperty("maxAlpha").floatValue           = 0.9f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Inset(GameObject go, float padding)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void Dim(Selectable s)
    {
        ColorBlock colors = s.colors;
        colors.normalColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        s.colors = colors;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static void Assign(Object component, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(component);
        foreach ((string field, Object value) in refs)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[LobbySceneBuilder] {component.GetType().Name}.{field} not found"); continue; }
            p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
