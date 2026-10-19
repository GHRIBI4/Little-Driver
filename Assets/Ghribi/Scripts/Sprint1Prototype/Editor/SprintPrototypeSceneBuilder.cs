using System;
using System.IO;
using LittleDriver.Prototype;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class SprintPrototypeSceneBuilder
{
    public const string ScenePath = "Assets/Ghribi/Scenes/Sprint1Prototype.unity";
    private const string MaterialsPath = "Assets/Ghribi/Scenes/Sprint1Prototype/Materials";
    private static readonly Color Dark = new Color(0.12f, 0.12f, 0.12f, 0.98f);
    private static readonly Color Mid = new Color(0.32f, 0.32f, 0.32f, 1);
    private static TMP_FontAsset font;

    [MenuItem("Little Driver/Create Sprint 1 Prototype")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Prototype scene already exists; open it instead of overwriting it.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your open scene before creating the prototype.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Import TMP essential resources before building the prototype.");

        Directory.CreateDirectory(MaterialsPath);
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 0.8f;
        RenderSettings.fog = false;
        var roadMat = MaterialAsset("Road", 0.3f);
        var groundMat = MaterialAsset("Ground", 0.48f);
        var carMat = MaterialAsset("Car", 0.78f);
        var darkMat = MaterialAsset("Dark", 0.18f);
        var lineMat = MaterialAsset("Markings", 0.9f);
        var obstacleMat = MaterialAsset("Obstacles", 0.52f);

        var course = new GameObject("Graybox Course").transform;
        Box("Ground", course, new Vector3(0, -0.45f, 58), new Vector3(34, 0.5f, 150), groundMat, false);
        Box("Two Forward Lanes", course, new Vector3(0, -0.12f, 58), new Vector3(7.5f, 0.2f, 136), roadMat, false);
        for (int z = 0; z < 126; z += 5)
            Box("Lane Divider " + z, course, new Vector3(0, 0.005f, z), new Vector3(0.12f, 0.02f, 2.5f), lineMat, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Box("Road Edge " + side, course, new Vector3(side * 3.7f, 0.015f, 58), new Vector3(0.12f, 0.03f, 136), lineMat, false);
            Box("Boundary " + side, course, new Vector3(side * 4.1f, 0.7f, 58), new Vector3(0.4f, 1.4f, 136), groundMat, true);
        }
        Box("Start Line", course, new Vector3(0, 0.02f, 3), new Vector3(7.2f, 0.03f, 0.3f), lineMat, false);
        Box("Finish Line", course, new Vector3(0, 0.02f, 110), new Vector3(7.2f, 0.03f, 0.6f), lineMat, false);
        var obstacles = new GameObject("Premade Obstacles").transform;
        Box("Obstacle 01 - Right Lane", obstacles, new Vector3(1.75f, 0.8f, 28), new Vector3(1.8f, 1.6f, 2), obstacleMat, true);
        Box("Obstacle 02 - Left Lane", obstacles, new Vector3(-1.75f, 0.8f, 53), new Vector3(1.8f, 1.6f, 2), obstacleMat, true);
        Box("Obstacle 03 - Right Lane", obstacles, new Vector3(1.75f, 0.8f, 78), new Vector3(1.8f, 1.6f, 2), obstacleMat, true);

        var lighting = new GameObject("Directional Light").AddComponent<Light>();
        lighting.type = LightType.Directional;
        lighting.intensity = 1.1f;
        lighting.transform.rotation = Quaternion.Euler(50, -25, 0);
        var camera = new GameObject("Prototype Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.backgroundColor = new Color(0.58f, 0.58f, 0.58f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.fieldOfView = 52;
        camera.transform.position = new Vector3(0, 12.75f, -11);
        camera.transform.LookAt(new Vector3(0, 0, 3));

        var player = new GameObject("Player Car");
        player.transform.position = new Vector3(1.75f, 0.75f, 0);
        var collider = player.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.35f, 0.9f, 2.5f);
        var frictionless = new PhysicsMaterial("Prototype frictionless car") { dynamicFriction = 0, staticFriction = 0, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
        AssetDatabase.CreateAsset(frictionless, MaterialsPath + "/CarPhysics.physicMaterial");
        collider.sharedMaterial = frictionless;
        var body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        player.AddComponent<AudioSource>().playOnAwake = false;
        var car = player.AddComponent<SprintPrototypeCar>();
        Reference(car, "followCamera", camera);
        Box("Body", player.transform, Vector3.zero, new Vector3(1.35f, 0.7f, 2.5f), carMat, false, true);
        Box("Cabin", player.transform, new Vector3(0, 0.5f, -0.25f), new Vector3(1.05f, 0.55f, 1.2f), obstacleMat, false, true);
        Box("Windshield", player.transform, new Vector3(0, 0.55f, 0.38f), new Vector3(0.92f, 0.35f, 0.06f), darkMat, false, true);
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                Box("Wheel", player.transform, new Vector3(x * 0.68f, -0.25f, z * 0.72f), new Vector3(0.22f, 0.4f, 0.52f), darkMat, false, true);

        var canvasObject = new GameObject("Prototype Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var hud = canvasObject.AddComponent<SprintPrototypeHUD>();
        Reference(hud, "car", car);
        var top = Panel("Header", canvasObject.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -49), new Vector2(0, 98), Dark, false);
        var title = Text("Title", top, "LITTLE DRIVER  /  SPRINT 1", new Vector2(0, 25), new Vector2(450, 30), 23);
        var speed = Text("Speed", top, "00 km/h", new Vector2(-370, -15), new Vector2(500, 30), 21);
        var beltLabel = Text("Belt Status", top, "BELT: NOT BUCKLED", new Vector2(175, 25), new Vector2(300, 30), 19);
        var collisions = Text("Contact Count", top, "CONTACTS: 0", new Vector2(175, -15), new Vector2(300, 30), 19);
        var reset = Button("Restart", top, "RESTART", new Vector2(535, 0), new Vector2(150, 56));
        Anchor(title.rectTransform, 0.24f, 25); Anchor(speed.rectTransform, 0.24f, -15);
        Anchor(beltLabel.rectTransform, 0.62f, 25); Anchor(collisions.rectTransform, 0.62f, -15);
        var resetRect = (RectTransform)reset.transform;
        resetRect.anchorMin = resetRect.anchorMax = new Vector2(1, 0.5f);
        resetRect.pivot = new Vector2(1, 0.5f); resetRect.anchoredPosition = new Vector2(-24, 0);
        UnityEventTools.AddPersistentListener(reset.onClick, hud.Restart);
        var feedback = Text("Feedback", canvasObject.transform, "Buckle up before driving", new Vector2(0, 0), new Vector2(1080, 45), 25);
        var feedbackRect = feedback.rectTransform;
        feedbackRect.anchorMin = feedbackRect.anchorMax = new Vector2(0.5f, 1);
        feedbackRect.anchoredPosition = new Vector2(0, -130);
        feedback.color = Color.white;
        feedback.outlineWidth = 0.2f;

        var controls = Panel("Driving Controls", canvasObject.transform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 85), new Vector2(0, 170), Dark, false);
        var left = Button("Left Lane", controls, "<  LEFT", new Vector2(-510, 0), new Vector2(180, 95));
        var right = Button("Right Lane", controls, "RIGHT  >", new Vector2(-310, 0), new Vector2(180, 95));
        var horn = Button("Horn", controls, "HORN", new Vector2(0, 0), new Vector2(150, 95));
        var stop = Button("Brake", controls, "STOP", new Vector2(300, 0), new Vector2(180, 95));
        var go = Button("Accelerate", controls, "GO", new Vector2(500, 0), new Vector2(180, 95));
        Anchor((RectTransform)left.transform, 0.09f, 0); Anchor((RectTransform)right.transform, 0.25f, 0);
        Anchor((RectTransform)horn.transform, 0.5f, 0); Anchor((RectTransform)stop.transform, 0.75f, 0);
        Anchor((RectTransform)go.transform, 0.91f, 0);
        UnityEventTools.AddPersistentListener(left.onClick, car.SelectLeftLane);
        UnityEventTools.AddPersistentListener(right.onClick, car.SelectRightLane);
        Hold(horn, car, false, true);
        Hold(go, car, false);
        Hold(stop, car, true);
        Text("Keyboard Hint", controls, "Keyboard: A/D or arrows = lanes | W/up = go | Space/down = brake | H = horn | R = restart", new Vector2(0, -66), new Vector2(1200, 26), 17);

        var overlay = Panel("Seat Belt Overlay", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.65f), true);
        var beltPanel = Panel("Belt Drag Area", overlay, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(920, 500), Dark, true);
        Text("Belt Title", beltPanel, "BEFORE WE DRIVE", new Vector2(0, 195), new Vector2(820, 50), 32);
        var instruction = Text("Belt Instruction", beltPanel, "Drag the belt handle into the buckle, then release.", new Vector2(0, 135), new Vector2(850, 50), 24);
        Panel("Seat Back", beltPanel, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -20), new Vector2(210, 230), Mid, false);
        Panel("Belt Anchor", beltPanel, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-290, 45), new Vector2(35, 35), Color.gray, false);
        var strap = Panel("Belt Strap", beltPanel, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-290, 45), new Vector2(25, 24), Color.gray, false);
        var buckle = Panel("Buckle Target", beltPanel, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(270, -60), new Vector2(150, 130), Mid, false);
        Text("Target Label", buckle, "BUCKLE\nHERE", Vector2.zero, new Vector2(140, 100), 23);
        var handle = Panel("Draggable Belt Handle", beltPanel, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-290, 45), new Vector2(135, 90), new Color(0.78f, 0.78f, 0.78f), true);
        var handleLabel = Text("Handle Label", handle, "DRAG", Vector2.zero, new Vector2(125, 70), 24);
        handleLabel.color = Color.black;
        var drag = handle.gameObject.AddComponent<SprintSeatBeltDrag>();
        Reference(drag, "car", car); Reference(drag, "buckle", buckle); Reference(drag, "strap", strap); Reference(drag, "handleImage", handle.GetComponent<UnityEngine.UI.Image>());
        Text("Belt Hint", beltPanel, "Release near the buckle to snap in. Miss it? Try again.", new Vector2(0, -205), new Vector2(850, 40), 21);
        Reference(hud, "seatBeltPanel", overlay.gameObject); Reference(hud, "drivingControls", controls.gameObject);
        Reference(hud, "beltDrag", drag); Reference(hud, "speedLabel", speed); Reference(hud, "beltLabel", beltLabel);
        Reference(hud, "feedbackLabel", feedback); Reference(hud, "collisionLabel", collisions); Reference(hud, "seatBeltInstruction", instruction);
        controls.gameObject.SetActive(false);

        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0, 1, 30), new Vector3(15, 10, 70)), false);
        Debug.Log("Created " + ScenePath);
    }

    private static Material MaterialAsset(string name, float gray)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(gray, gray, gray));
        mat.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(mat, MaterialsPath + "/" + name + ".mat");
        return mat;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid, bool local = false)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        if (local) obj.transform.localPosition = position; else obj.transform.position = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size, Color color, bool raycast)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = obj.GetComponent<UnityEngine.UI.Image>();
        image.color = color; image.raycastTarget = raycast;
        return rect;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string content, Vector2 position, Vector2 size, int fontSize)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = content; text.fontSize = fontSize;
        text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.one * 0.5f;
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        return text;
    }

    private static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        var rect = Panel(name, parent, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size, Mid, true);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        Text("Label", rect, label, Vector2.zero, size - new Vector2(8, 8), 28);
        return button;
    }

    private static void Hold(UnityEngine.UI.Button button, SprintPrototypeCar car, bool brake, bool horn = false)
    {
        var hold = button.gameObject.AddComponent<SprintHoldControl>();
        Reference(hold, "car", car);
        var serialized = new SerializedObject(hold);
        serialized.FindProperty("isBrake").boolValue = brake;
        serialized.FindProperty("isHorn").boolValue = horn;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Reference(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Anchor(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(x, 0.5f);
        rect.anchoredPosition = new Vector2(0, y);
    }
}
