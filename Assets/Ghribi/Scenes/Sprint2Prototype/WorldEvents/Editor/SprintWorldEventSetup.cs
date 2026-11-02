using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using LittleDriver.Prototype;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    public static class SprintWorldEventSetup
    {
        const string Folder = "Assets/Ghribi/Scenes/Sprint2Prototype/WorldEvents";

        [MenuItem("Little Driver/World Events/Set Up Sprint2 Events")]
        public static void Setup()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Ghribi/Scenes/Sprint2Prototype.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open Sprint2Prototype in Edit mode first.");
            var player = UnityEngine.Object.FindFirstObjectByType<SprintPrototypeCar>();
            var police = GameObject.Find("Vehicle_Police Car");
            var bus = GameObject.Find("ParkedBus");
            var adult = GameObject.Find("Crossing Adult");
            var child = GameObject.Find("Crossing Child");
            if (player == null || police == null || bus == null || adult == null || child == null)
                throw new InvalidOperationException("Expected player, police, ParkedBus, Crossing Adult and Crossing Child.");

            var go = GameObject.Find("World Event GameManager");
            if (go == null) { go = new GameObject("World Event GameManager"); Undo.RegisterCreatedObjectUndo(go, "Create world events"); }
            var world = go.GetComponent<SprintWorldEventManager>();
            if (world == null) world = Undo.AddComponent<SprintWorldEventManager>(go);
            Undo.RecordObject(world, "Configure world events");
            world.player = player;
            world.policeCar = police.transform;
            world.playerMaximumKmh = 55;
            world.speedingThresholdKmh = 50;
            Undo.RecordObject(player, "Set maximum speed to 55 km/h");
            player.MaximumSpeedKmh = 55;
            EditorUtility.SetDirty(player);

            world.policeVision = Marker(police.transform, "Police Vision Origin", police.transform.position + Vector3.up * 1.3f);
            world.policeVision.localRotation = Quaternion.identity;
            world.policeAudio = police.GetComponent<AudioSource>();
            if (world.policeAudio == null) world.policeAudio = Undo.AddComponent<AudioSource>(police);
            Undo.RecordObject(world.policeAudio, "Configure police audio");
            world.policeAudio.playOnAwake = false;
            world.policeAudio.spatialBlend = 0.8f;
            world.policeAudio.minDistance = 8;
            world.policeAudio.maxDistance = 50;
            world.policeAudio.rolloffMode = AudioRolloffMode.Linear;
            world.policeAudio.volume = 0.25f;

            var red = Beacon(police.transform, "Red Warning Beacon", -0.32f, Color.red);
            var blue = Beacon(police.transform, "Blue Warning Beacon", 0.32f, new Color(0.05f, 0.25f, 1));
            world.redBeacon = red.GetComponent<Renderer>();
            world.blueBeacon = blue.GetComponent<Renderer>();
            world.redLight = red.GetComponentInChildren<Light>();
            world.blueLight = blue.GetComponentInChildren<Light>();

            world.crossingCenter = Marker(go.transform, "Marked Crossing Center", new Vector3(0, 0.2f, (adult.transform.position.z + child.transform.position.z) / 2));
            world.pedestrians = new[] { Pedestrian(world, adult, "Adult", false), Pedestrian(world, child, "Child", true) };

            var body = bus.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(bus);
            Undo.RecordObject(body, "Configure moving parked bus");
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var end = Marker(go.transform, "ParkedBus Road End", new Vector3(bus.transform.position.x, bus.transform.position.y, 74.5f));
            world.parkedVehicles = new[] { new DepartingVehicle { vehicle = bus.transform, roadEnd = end, speedKmh = 18 } };
            SprintWorldEventManagerEditor.PrepareMovingVehicles(world);

            var tree = go.GetComponent<BehaviorTree>();
            if (tree == null) tree = Undo.AddComponent<BehaviorTree>(go);
            Undo.RecordObject(tree, "Build utility world event tree");
            tree.StartWhenEnabled = true;
            var entry = new EntryTask { ID = 0, FriendlyName = "Entry", NodeData = new NodeData { Offset = new Vector2(0, 0) } };
            var repeat = new Repeater { ID = 1, FriendlyName = "Repeat Forever", repeatForever = true, endOnFailure = false, NodeData = new NodeData { Offset = new Vector2(0, 100) } };
            var selector = new UtilitySelector { ID = 2, FriendlyName = "Most Important World Event", NodeData = new NodeData { Offset = new Vector2(0, 120) } };
            var actions = new List<Task>
            {
                new PoliceWarningAction { ID = 3, FriendlyName = "Police Warning", NodeData = new NodeData { Offset = new Vector2(-300, 150) } },
                new PedestrianCrossingAction { ID = 4, FriendlyName = "Pedestrian Crossing", NodeData = new NodeData { Offset = new Vector2(-100, 150) } },
                new ParkedVehicleDepartureAction { ID = 5, FriendlyName = "Parked Vehicle Departure", NodeData = new NodeData { Offset = new Vector2(110, 150) } },
                new WaitForWorldEvent { ID = 6, FriendlyName = "Wait For Event", NodeData = new NodeData { Offset = new Vector2(310, 150) } }
            };
            for (int i = 0; i < actions.Count; i++) selector.AddChild(actions[i], i);
            repeat.AddChild(selector, 0);
            var source = tree.GetBehaviorSource();
            source.behaviorName = "Sprint2 World Events";
            source.Save(entry, repeat, null);
            source.HasSerialized = true;
            BehaviorDesigner.Editor.JSONSerialization.Save(source);
            world.eventTree = tree;
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = go;
            Debug.Log("Sprint2 world events configured and saved. Select World Event GameManager for settings and live scores.");
        }

        static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create event marker");
                t = go.transform;
                t.SetParent(parent, true);
            }
            Undo.RecordObject(t, "Position event marker");
            t.position = position;
            return t;
        }

        static GameObject Beacon(Transform police, string name, float lateral, Color color)
        {
            var existing = police.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, "Create police beacon");
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(police, true);
            }
            go.transform.position = police.position + Vector3.up * 1.65f + police.right * lateral;
            go.transform.rotation = police.rotation;
            go.transform.localScale = new Vector3(0.28f / police.lossyScale.x, 0.12f / police.lossyScale.y, 0.22f / police.lossyScale.z);
            string path = Folder + "/" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", color * 0.12f);
                material.EnableKeyword("_EMISSION");
                AssetDatabase.CreateAsset(material, path);
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            var light = go.GetComponentInChildren<Light>();
            if (light == null)
            {
                var lightGo = new GameObject("Warning Light");
                lightGo.transform.SetParent(go.transform, false);
                light = lightGo.AddComponent<Light>();
            }
            light.type = LightType.Point;
            light.color = color;
            light.range = 5;
            light.intensity = 0;
            light.shadows = LightShadows.None;
            return go;
        }

        static CrossingPedestrian Pedestrian(SprintWorldEventManager world, GameObject person, string label, bool child)
        {
            var animator = person.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) throw new InvalidOperationException("Missing pedestrian idle controller.");
            string path = Folder + "/" + label + "Crossing.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                var idle = animator.runtimeAnimatorController.animationClips.First(c => !c.name.StartsWith("__preview__"));
                var walk = AssetDatabase.LoadAllAssetsAtPath("Assets/Kenza/DenysAlmaral/CityPeople/Animations/locom_" + (child ? "m" : "f") + "_basicWalk_30f.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                var idleState = machine.AddState("Idle"); idleState.motion = idle;
                var walkState = machine.AddState("Walk"); walkState.motion = walk;
                machine.defaultState = idleState;
                var start = idleState.AddTransition(walkState);
                start.hasExitTime = false; start.duration = 0.15f;
                start.AddCondition(AnimatorConditionMode.If, 0, "Walking");
                var stop = walkState.AddTransition(idleState);
                stop.hasExitTime = false; stop.duration = 0.15f;
                stop.AddCondition(AnimatorConditionMode.IfNot, 0, "Walking");
            }
            Undo.RecordObject(animator, "Assign crossing animation");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var city = person.GetComponent<CityPeople.CityPeople>();
            if (city != null)
            {
                var so = new SerializedObject(city);
                so.FindProperty("AutoPlayAnimations").boolValue = false;
                so.ApplyModifiedProperties();
            }
            var collider = person.GetComponent<CapsuleCollider>();
            if (collider == null) collider = Undo.AddComponent<CapsuleCollider>(person);
            var bounds = person.GetComponentsInChildren<Renderer>().First().bounds;
            foreach (var renderer in person.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            collider.center = person.transform.InverseTransformPoint(bounds.center);
            collider.height = bounds.size.y / person.transform.lossyScale.y;
            collider.radius = 0.22f / person.transform.lossyScale.x;
            collider.isTrigger = false;
            var body = person.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(person);
            body.isKinematic = true; body.useGravity = false;
            var sideA = Marker(world.transform, label + " Crossing Side A", person.transform.position);
            var sideB = Marker(world.transform, label + " Crossing Side B", new Vector3(4.92f, person.transform.position.y, person.transform.position.z));
            return new CrossingPedestrian { person = person.transform, sideA = sideA, sideB = sideB, walkingSpeed = child ? 2 : 1.8f };
        }
    }
}
