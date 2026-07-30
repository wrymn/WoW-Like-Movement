using Animancer;
using UnityEditor;
using UnityEngine;

namespace WowLocomotionResearch.Editor
{
    /// <summary>
    /// Editor report for reviewing and applying weighted mask groups to Animancer WeightedMaskLayers.
    /// </summary>
    public sealed class WowWeightedMaskSetupReport : EditorWindow
    {
        private WowGenericBoneProfile boneProfile;
        private WowWeightedMaskProfile weightedMaskProfile;
        private WeightedMaskLayers weightedMaskLayers;
        private Vector2 scroll;

        /// <summary>
        /// Opens the weighted mask setup report window.
        /// </summary>
        [MenuItem("Tools/Research/WoW Locomotion/Weighted Mask Setup Report")]
        public static void Open()
        {
            GetWindow<WowWeightedMaskSetupReport>("WoW Mask Report").Show();
        }

        private void OnGUI()
        {
            boneProfile = (WowGenericBoneProfile)EditorGUILayout.ObjectField(
                "Bone Profile",
                boneProfile,
                typeof(WowGenericBoneProfile),
                false);
            weightedMaskProfile = (WowWeightedMaskProfile)EditorGUILayout.ObjectField(
                "Weighted Mask Profile",
                weightedMaskProfile,
                typeof(WowWeightedMaskProfile),
                false);
            weightedMaskLayers = (WeightedMaskLayers)EditorGUILayout.ObjectField(
                "Weighted Mask Layers",
                weightedMaskLayers,
                typeof(WeightedMaskLayers),
                true);

            DrawVerification();

            using (new EditorGUI.DisabledScope(boneProfile == null || weightedMaskProfile == null || weightedMaskLayers == null))
            {
                if (GUILayout.Button("Apply Weights To WeightedMaskLayers"))
                {
                    Undo.RecordObject(weightedMaskLayers, "Apply WoW Weighted Mask Groups");
                    weightedMaskProfile.ApplyToWeightedMaskLayers(weightedMaskLayers, boneProfile);
                    EditorUtility.SetDirty(weightedMaskLayers);
                }
            }

            if (boneProfile == null || weightedMaskProfile == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawWeightTable();
            EditorGUILayout.EndScrollView();
        }

        private void DrawVerification()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Group Indices", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("0", WowWeightedMaskGroup.NoUpperBodyOverride.ToString());
            EditorGUILayout.LabelField("1", WowWeightedMaskGroup.UpperBodyActionWhileMoving.ToString());
            EditorGUILayout.LabelField("2", WowWeightedMaskGroup.UpperBodyAimOnly.ToString());
            EditorGUILayout.LabelField("3", WowWeightedMaskGroup.FullBodyActionWhileStationary.ToString());

            if (weightedMaskLayers == null)
            {
                EditorGUILayout.HelpBox("Assign a WeightedMaskLayers component to verify or apply setup.", MessageType.Info);
                return;
            }

            var groupCount = weightedMaskLayers.Definition != null ? weightedMaskLayers.Definition.GroupCount : 0;
            if (groupCount < WowWeightedMaskProfile.GetGroupCount())
            {
                EditorGUILayout.HelpBox(
                    $"WeightedMaskLayers currently has {groupCount} groups; this prototype requires {WowWeightedMaskProfile.GetGroupCount()}.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("WeightedMaskLayers has enough groups for the prototype.", MessageType.Info);
            }

            EditorGUILayout.HelpBox(
                "This tool uses Animancer public Definition APIs. It does not edit hidden serialized fields or use reflection.",
                MessageType.None);
        }

        private void DrawWeightTable()
        {
            var table = weightedMaskProfile.BuildWeightTable(boneProfile);
            EditorGUILayout.LabelField("Desired Weights", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Bone", GUILayout.Width(220f));
            EditorGUILayout.LabelField("None", GUILayout.Width(64f));
            EditorGUILayout.LabelField("Moving", GUILayout.Width(64f));
            EditorGUILayout.LabelField("Aim", GUILayout.Width(64f));
            EditorGUILayout.LabelField("Stationary", GUILayout.Width(76f));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < table.Count; i++)
            {
                var row = table[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(row.bone, typeof(Transform), true, GUILayout.Width(220f));
                DrawWeight(row.Get(WowWeightedMaskGroup.NoUpperBodyOverride), 64f);
                DrawWeight(row.Get(WowWeightedMaskGroup.UpperBodyActionWhileMoving), 64f);
                DrawWeight(row.Get(WowWeightedMaskGroup.UpperBodyAimOnly), 64f);
                DrawWeight(row.Get(WowWeightedMaskGroup.FullBodyActionWhileStationary), 76f);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawWeight(float value, float width)
        {
            EditorGUILayout.LabelField(value.ToString("0.00"), GUILayout.Width(width));
        }
    }
}
