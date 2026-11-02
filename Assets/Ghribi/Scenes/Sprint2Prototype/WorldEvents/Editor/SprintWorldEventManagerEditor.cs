using UnityEditor;
using UnityEngine;

namespace LittleDriver.WorldEvents
{
    [CustomEditor(typeof(SprintWorldEventManager))]
    public sealed class SprintWorldEventManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            var world = (SprintWorldEventManager)target;
            if (EditorGUI.EndChangeCheck() && !Application.isPlaying) PrepareMovingVehicles(world);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live world conditions and utility scores", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Player speed (km/h)", Application.isPlaying ? world.PlayerSpeedKmh : 0);
                EditorGUILayout.Toggle("Speeding", Application.isPlaying && world.PlayerIsSpeeding);
                EditorGUILayout.FloatField("Distance to police (m)", world.PoliceDistance);
                EditorGUILayout.Toggle("In police vision", world.PlayerInPoliceVision);
                EditorGUILayout.EnumPopup("Active event", world.ActiveEvent);
                EditorGUILayout.Toggle("Crossing requested", world.CrossingRequested);
                foreach (var vehicle in world.parkedVehicles)
                {
                    EditorGUILayout.Toggle(vehicle.vehicle != null ? vehicle.vehicle.name + " activated" : "Unassigned vehicle", vehicle.activated);
                    EditorGUILayout.Toggle("Currently moving", vehicle.moving);
                }
            }
            DrawCalculation(world, WorldEventKind.Police, "Police: faster speeding = higher score",
                $"Speed: {(Application.isPlaying ? world.PlayerSpeedKmh : 0):0.0} km/h. Limit: {world.speedingThresholdKmh:0.0}. Maximum: {world.playerMaximumKmh:0.0}.",
                "Speed urgency", world.PoliceSpeedUrgency, world.policeImportance,
                PoliceWarningAction.StartingPriority, PoliceWarningAction.MaximumBonus);
            DrawCalculation(world, WorldEventKind.Crossing, "Pedestrian: closer crossing = higher score",
                $"Closeness = 1 - (distance ahead / {world.crossingApproachDistance:0.0} m). Randomness only decides whether a crossing is requested.",
                "Crossing closeness", world.CrossingCloseness, world.crossingImportance,
                PedestrianCrossingAction.StartingPriority, PedestrianCrossingAction.MaximumBonus);
            DrawCalculation(world, WorldEventKind.Departure, "Vehicle: closer player = higher score",
                $"Closeness = 1 - (distance to nearest unused vehicle / {world.departureDetectionDistance:0.0} m).",
                "Vehicle closeness", world.DepartureCloseness, world.departureImportance,
                ParkedVehicleDepartureAction.StartingPriority, ParkedVehicleDepartureAction.MaximumBonus);
            EditorGUILayout.HelpBox("Zero = cannot start. Highest score wins. A running warning or crossing gets 1.1 to finish. A departing vehicle keeps moving after selection. Cyan = police vision, yellow = crossing, green = vehicle destination.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live Utility Values", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawUtilityValue("Police", Application.isPlaying ? world.PoliceUtility : 0);
                DrawUtilityValue("Pedestrian", Application.isPlaying ? world.CrossingUtility : 0);
                DrawUtilityValue("Vehicle", Application.isPlaying ? world.DepartureUtility : 0);
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        static void DrawUtilityValue(string label, float value)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.MinWidth(0));
                EditorGUILayout.LabelField(value.ToString("0.000"), EditorStyles.boldLabel, GUILayout.MinWidth(0));
            }
        }

        static void DrawCalculation(SprintWorldEventManager world, WorldEventKind kind, string title,
            string inputDescription, string inputLabel, float urgency, float importance, float startingPriority, float maximumBonus)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(inputDescription, EditorStyles.wordWrappedLabel);
            float bonus = maximumBonus * urgency;
            float potentialScore = importance * (startingPriority + bonus);
            float finalScore = 0;
            if (Application.isPlaying)
            {
                switch (kind)
                {
                    case WorldEventKind.Police: finalScore = world.PoliceUtility; break;
                    case WorldEventKind.Crossing: finalScore = world.CrossingUtility; break;
                    case WorldEventKind.Departure: finalScore = world.DepartureUtility; break;
                }
            }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Slider(inputLabel + " (0 to 1)", urgency, 0, 1);
                EditorGUILayout.FloatField("1. Starting priority", startingPriority);
                EditorGUILayout.FloatField("2. Bonus (" + maximumBonus.ToString("0.0") + " x input)", bonus);
                EditorGUILayout.FloatField("3. Importance multiplier", importance);
                EditorGUILayout.FloatField("4. Final utility", finalScore);
            }
            EditorGUILayout.LabelField($"Calculation: {importance:0.###} x ({startingPriority:0.###} + {bonus:0.###}) = {potentialScore:0.###}", EditorStyles.wordWrappedLabel);

            string status;
            if (!Application.isPlaying) status = "Enter Play mode to see the real score and conditions.";
            else if (world.ActiveEvent == kind) status = "Finishing this action: final utility is temporarily 1.1.";
            else if (world.ActiveEvent != WorldEventKind.None) status = "Another action is finishing: final utility is 0.";
            else status = world.GetBlockingReason(kind) ?? "Allowed: the calculated score can compete.";
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        public static void PrepareMovingVehicles(SprintWorldEventManager world)
        {
            foreach (var vehicle in world.parkedVehicles)
            {
                if (vehicle.vehicle == null) continue;
                foreach (var child in vehicle.vehicle.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetStaticEditorFlags(child.gameObject) == 0) continue;
                    Undo.RecordObject(child.gameObject, "Make departing vehicle movable");
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                }
            }
        }
    }
}
