// Assets/Editor/BombermanSetup.cs
// ═══════════════════════════════════════════════════════════════
// TWO-STEP SETUP:
//   Step 1: Bomberman → Step 1: Create Sprites
//   Step 2: Bomberman → Step 2: Create Prefabs & Scene
//
// Must run Step 1 first, wait for Unity to finish importing,
// then run Step 2.
// ═══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public class BombermanSetup : EditorWindow
{
    // ══════════════════════════════════════════════
    // STEP 1: Create folders + sprites
    // ══════════════════════════════════════════════
    [MenuItem("Bomberman/Step 1 - Create Sprites")]
    public static void Step1_CreateSprites()
    {
        CreateFolders();
        CreateSprites();

        EditorUtility.DisplayDialog("Step 1 Complete!",
            "Sprites created in Assets/Sprites/.\n\n" +
            "IMPORTANT: Wait a few seconds for Unity to finish importing,\n" +
            "then click 'Bomberman → Step 2 - Create Prefabs & Scene'.",
            "OK");
    }

    // ══════════════════════════════════════════════
    // STEP 2: Create prefabs + scene + UI
    // ══════════════════════════════════════════════
    [MenuItem("Bomberman/Step 2 - Create Prefabs and Scene")]
    public static void Step2_CreatePrefabsAndScene()
    {
        // Verify sprites exist first
        if (!File.Exists("Assets/Sprites/Sprite_Floor.png"))
        {
            EditorUtility.DisplayDialog("Error",
                "Sprites not found! Run 'Step 1 - Create Sprites' first.", "OK");
            return;
        }

        // Force a fresh import of all sprites
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Configure all sprite import settings
        ConfigureSprite("Assets/Sprites/Sprite_Floor.png");
        ConfigureSprite("Assets/Sprites/Sprite_WallIndestructible.png");
        ConfigureSprite("Assets/Sprites/Sprite_WallDestructible.png");
        ConfigureSprite("Assets/Sprites/Sprite_Player.png");
        ConfigureSprite("Assets/Sprites/Sprite_Bomb.png");
        ConfigureSprite("Assets/Sprites/Sprite_Explosion.png");

        // Refresh again after reimporting
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        CreatePrefabs();
        SetupGameScene();

        EditorUtility.DisplayDialog("Step 2 Complete!",
            "Prefabs and scene created!\n\n" +
            "1. Open Assets/Scenes/Game.unity\n" +
            "2. Verify GameManager has all references\n" +
            "3. Hit Play to test!",
            "Got it!");
    }

    // ══════════════════════════════════════════════
    // FOLDERS
    // ══════════════════════════════════════════════
    static void CreateFolders()
    {
        string[] folders = {
            "Assets/Sprites",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Scripts/Networking",
            "Assets/Scripts/GameLogic",
            "Assets/Scripts/GUI",
            "Assets/Scripts/Storage"
        };
        foreach (var f in folders)
        {
            if (!Directory.Exists(f))
                Directory.CreateDirectory(f);
        }
        AssetDatabase.Refresh();
        Debug.Log("[BombermanSetup] Folders created.");
    }

    // ══════════════════════════════════════════════
    // SPRITES
    // ══════════════════════════════════════════════
    static void CreateSprites()
    {
        CreateSquareSprite("Sprite_Floor",              new Color(0.85f, 0.85f, 0.80f));
        CreateSquareSprite("Sprite_WallIndestructible",  new Color(0.30f, 0.30f, 0.35f));
        CreateSquareSprite("Sprite_WallDestructible",    new Color(0.65f, 0.45f, 0.25f));
        CreateSquareSprite("Sprite_Player",              new Color(0.20f, 0.70f, 0.20f));
        CreateSquareSprite("Sprite_Bomb",                new Color(0.15f, 0.15f, 0.15f));
        CreateSquareSprite("Sprite_Explosion",           new Color(1.00f, 0.40f, 0.10f));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[BombermanSetup] Sprites created in Assets/Sprites/");
    }

    static void CreateSquareSprite(string name, Color color)
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Point;

        Color borderColor = color * 0.7f;
        borderColor.a = 1f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool isBorder = (x == 0 || x == size - 1 || y == 0 || y == size - 1);
                tex.SetPixel(x, y, isBorder ? borderColor : color);
            }
        }
        tex.Apply();

        string path = $"Assets/Sprites/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void ConfigureSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            Debug.Log($"[BombermanSetup] Configured sprite: {path}");
        }
        else
        {
            Debug.LogError($"[BombermanSetup] Could not get importer for: {path}");
        }
    }

    // ══════════════════════════════════════════════
    // PREFABS
    // ══════════════════════════════════════════════
    static void CreatePrefabs()
    {
        bool allOk = true;
        allOk &= CreateSpritePrefab("Floor",              "Sprite_Floor",              0);
        allOk &= CreateSpritePrefab("WallIndestructible",  "Sprite_WallIndestructible", 1);
        allOk &= CreateSpritePrefab("WallDestructible",    "Sprite_WallDestructible",   1);
        allOk &= CreateSpritePrefab("Player",              "Sprite_Player",             3);
        allOk &= CreateSpritePrefab("Bomb",                "Sprite_Bomb",               2);
        allOk &= CreateSpritePrefab("Explosion",           "Sprite_Explosion",          4);

        AssetDatabase.Refresh();

        if (allOk)
            Debug.Log("[BombermanSetup] All prefabs created in Assets/Prefabs/");
        else
            Debug.LogError("[BombermanSetup] Some prefabs failed — check errors above.");
    }

    static bool CreateSpritePrefab(string prefabName, string spriteName, int sortOrder)
    {
        string spritePath = $"Assets/Sprites/{spriteName}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogError($"[BombermanSetup] Sprite not found: {spritePath}. " +
                "Did you run Step 1 and wait for import?");
            return false;
        }

        GameObject go = new GameObject(prefabName);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortOrder;

        string prefabPath = $"Assets/Prefabs/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        Debug.Log($"[BombermanSetup] Prefab created: {prefabPath}");
        return true;
    }

    // ══════════════════════════════════════════════
    // SCENE SETUP
    // ══════════════════════════════════════════════
    static void SetupGameScene()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        // Camera
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(5.5f, 5.5f, -10f);
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.20f);

        // GameManager
        GameObject gameManager = new GameObject("GameManager");

        MonoScript gameControllerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/Scripts/GUI/GameController.cs");

        if (gameControllerScript == null)
        {
            Debug.LogError("[BombermanSetup] GameController.cs not found at Assets/Scripts/GUI/. " +
                "Make sure you copied all script files first.");
        }
        else
        {
            var controllerType = gameControllerScript.GetClass();
            if (controllerType != null)
            {
                var controller = gameManager.AddComponent(controllerType);
                WireUpController(controller);
            }
            else
            {
                Debug.LogError("[BombermanSetup] GameController class not found. " +
                    "Check for compile errors in Console.");
            }
        }

        CreateUI(gameManager);

        string scenePath = "Assets/Scenes/Game.unity";
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[BombermanSetup] Scene saved to {scenePath}");
    }

    static void WireUpController(Component controller)
    {
        if (controller == null) return;

        SerializedObject so = new SerializedObject(controller);

        SetPrefabRef(so, "wallIndestructiblePrefab", "Assets/Prefabs/WallIndestructible.prefab");
        SetPrefabRef(so, "wallDestructiblePrefab",   "Assets/Prefabs/WallDestructible.prefab");
        SetPrefabRef(so, "floorPrefab",              "Assets/Prefabs/Floor.prefab");
        SetPrefabRef(so, "playerPrefab",             "Assets/Prefabs/Player.prefab");
        SetPrefabRef(so, "bombPrefab",               "Assets/Prefabs/Bomb.prefab");
        SetPrefabRef(so, "explosionPrefab",          "Assets/Prefabs/Explosion.prefab");

        so.ApplyModifiedProperties();
        Debug.Log("[BombermanSetup] Prefabs wired to GameController.");
    }

    static void SetPrefabRef(SerializedObject so, string propertyName, string assetPath)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
                prop.objectReferenceValue = prefab;
            else
                Debug.LogWarning($"[BombermanSetup] Prefab not found: {assetPath}");
        }
    }

    // ══════════════════════════════════════════════
    // UI CREATION
    // ══════════════════════════════════════════════
    static void CreateUI(GameObject gameManager)
    {
        // Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        // EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ── LOBBY PANEL ──
        GameObject lobbyPanel = CreatePanel(canvasGO, "LobbyPanel",
            new Vector2(0.5f, 0.5f), new Vector2(400, 350));

        CreateText(lobbyPanel, "TitleText", "BOMBERMAN",
            new Vector2(0, 120), new Vector2(350, 50), 28, FontStyle.Bold, Color.white);

        GameObject nameInput = CreateInputField(lobbyPanel, "NameInput",
            "Enter your name...", new Vector2(0, 50), new Vector2(300, 40));

        GameObject joinButton = CreateButton(lobbyPanel, "JoinButton",
            "JOIN GAME", new Vector2(0, -10), new Vector2(300, 45),
            new Color(0.2f, 0.6f, 0.3f));

        GameObject startButton = CreateButton(lobbyPanel, "StartButton",
            "START GAME", new Vector2(0, -65), new Vector2(300, 45),
            new Color(0.2f, 0.4f, 0.7f));

        GameObject statusText = CreateText(lobbyPanel, "StatusText",
            "Enter your name and click Join.",
            new Vector2(0, -120), new Vector2(350, 30), 14, FontStyle.Normal,
            new Color(0.8f, 0.8f, 0.8f));

        GameObject playerListText = CreateText(lobbyPanel, "PlayerListText",
            "Players:\n(none yet)",
            new Vector2(0, -160), new Vector2(350, 60), 13, FontStyle.Normal,
            new Color(0.7f, 0.9f, 0.7f));

        // ── HUD PANEL ──
        GameObject hudPanel = CreatePanel(canvasGO, "HUDPanel",
            new Vector2(0.5f, 1f), new Vector2(400, 60), new Vector2(0, -40));

        Image hudImg = hudPanel.GetComponent<Image>();
        hudImg.color = new Color(0, 0, 0, 0.5f);

        GameObject hudStatusText = CreateText(hudPanel, "HUDStatusText",
            "FIGHT!", Vector2.zero, new Vector2(380, 40), 22, FontStyle.Bold, Color.white);

        // ── Wire UI to GameController ──
        SerializedObject so = null;
        foreach (var c in gameManager.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "GameController")
            {
                so = new SerializedObject(c);
                break;
            }
        }

        if (so != null)
        {
            SetUIRef(so, "nameInput",       nameInput.GetComponent<InputField>());
            SetUIRef(so, "joinButton",       joinButton.GetComponent<Button>());
            SetUIRef(so, "startButton",      startButton.GetComponent<Button>());
            SetUIRef(so, "statusText",       statusText.GetComponent<Text>());
            SetUIRef(so, "playerListText",   playerListText.GetComponent<Text>());
            SetUIRef(so, "lobbyPanel",       lobbyPanel);
            SetUIRef(so, "hudPanel",         hudPanel);
            SetUIRef(so, "hudStatusText",    hudStatusText.GetComponent<Text>());
            so.ApplyModifiedProperties();
            Debug.Log("[BombermanSetup] UI references wired to GameController.");
        }
    }

    static void SetUIRef(SerializedObject so, string propName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop != null && value != null)
            prop.objectReferenceValue = value;
    }

    // ══════════════════════════════════════════════
    // UI HELPERS
    // ══════════════════════════════════════════════
    static GameObject CreatePanel(GameObject parent, string name,
        Vector2 pivot, Vector2 size, Vector2? pos = null)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent.transform, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = pivot; rt.anchorMax = pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos ?? Vector2.zero;
        Image img = panel.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.18f, 0.92f);
        return panel;
    }

    static GameObject CreateText(GameObject parent, string name, string content,
        Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = position; rt.sizeDelta = size;
        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize; text.fontStyle = style;
        text.color = color; text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return go;
    }

    static GameObject CreateButton(GameObject parent, string name, string label,
        Vector2 position, Vector2 size, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = position; rt.sizeDelta = size;
        Image img = go.AddComponent<Image>(); img.color = bgColor;
        Button btn = go.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        GameObject labelGO = new GameObject("Text");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        Text text = labelGO.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18; text.fontStyle = FontStyle.Bold;
        text.color = Color.white; text.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    static GameObject CreateInputField(GameObject parent, string name,
        string placeholder, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = position; rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.30f);
        InputField input = go.AddComponent<InputField>();

        // Text child
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 2); textRT.offsetMax = new Vector2(-10, -2);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16; text.color = Color.white; text.supportRichText = false;

        // Placeholder child
        GameObject phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        RectTransform phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 2); phRT.offsetMax = new Vector2(-10, -2);
        Text phText = phGO.AddComponent<Text>();
        phText.text = placeholder;
        phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        phText.fontSize = 16; phText.fontStyle = FontStyle.Italic;
        phText.color = new Color(0.6f, 0.6f, 0.6f);

        input.textComponent = text;
        input.placeholder = phText;
        return go;
    }
}
#endif