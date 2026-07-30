using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WowLocomotionResearch.Editor
{
    /// <summary>
    /// Editor tool for creating a manually reviewed Generic bone profile from the selected skeleton.
    /// </summary>
    public sealed class WowGenericBoneProfileBuilder : EditorWindow
    {
        private const string SaveFolder = "Assets/Research/WowLocomotion/ScriptableObjects";

        private Transform skeletonRoot;
        private WowGenericBoneProfile draft;
        private Vector2 scroll;
        private readonly Dictionary<string, List<Transform>> candidateNotes = new Dictionary<string, List<Transform>>();

        /// <summary>
        /// Opens the Generic bone profile builder window.
        /// </summary>
        [MenuItem("Tools/Research/WoW Locomotion/Create Generic Bone Profile From Selected Skeleton")]
        public static void Open()
        {
            var window = GetWindow<WowGenericBoneProfileBuilder>("WoW Bone Profile");
            window.UseSelection();
            window.Show();
        }

        private void OnEnable()
        {
            UseSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Token suggestions are only a starting point. Review every assignment before creating the asset.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            skeletonRoot = (Transform)EditorGUILayout.ObjectField("Skeleton Root", skeletonRoot, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
                BuildDraft();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected Transform"))
                UseSelection();
            if (GUILayout.Button("Rebuild Suggestions"))
                BuildDraft();
            EditorGUILayout.EndHorizontal();

            if (draft == null)
                return;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSection("Root");
            DrawTransformField("Skeleton Root", ref draft.skeletonRoot, nameof(draft.skeletonRoot));
            DrawTransformField("Armature Root", ref draft.armatureRoot, nameof(draft.armatureRoot));
            DrawTransformField("Motion Root", ref draft.motionRoot, nameof(draft.motionRoot));
            DrawTransformField("Pelvis", ref draft.pelvis, nameof(draft.pelvis));
            DrawTransformField("Hips", ref draft.hips, nameof(draft.hips));

            DrawSection("Spine");
            DrawTransformField("Spine Lower", ref draft.spineLower, nameof(draft.spineLower));
            DrawTransformField("Spine Middle", ref draft.spineMiddle, nameof(draft.spineMiddle));
            DrawTransformField("Spine Upper", ref draft.spineUpper, nameof(draft.spineUpper));
            DrawTransformField("Chest", ref draft.chest, nameof(draft.chest));
            DrawTransformField("Neck", ref draft.neck, nameof(draft.neck));
            DrawTransformField("Head", ref draft.head, nameof(draft.head));

            DrawSection("Left Arm");
            DrawTransformField("Left Clavicle", ref draft.leftClavicle, nameof(draft.leftClavicle));
            DrawTransformField("Left Shoulder", ref draft.leftShoulder, nameof(draft.leftShoulder));
            DrawTransformField("Left Upper Arm", ref draft.leftUpperArm, nameof(draft.leftUpperArm));
            DrawTransformField("Left Forearm", ref draft.leftForearm, nameof(draft.leftForearm));
            DrawTransformField("Left Hand", ref draft.leftHand, nameof(draft.leftHand));
            DrawList("Left Fingers", draft.leftFingers, nameof(draft.leftFingers));

            DrawSection("Right Arm");
            DrawTransformField("Right Clavicle", ref draft.rightClavicle, nameof(draft.rightClavicle));
            DrawTransformField("Right Shoulder", ref draft.rightShoulder, nameof(draft.rightShoulder));
            DrawTransformField("Right Upper Arm", ref draft.rightUpperArm, nameof(draft.rightUpperArm));
            DrawTransformField("Right Forearm", ref draft.rightForearm, nameof(draft.rightForearm));
            DrawTransformField("Right Hand", ref draft.rightHand, nameof(draft.rightHand));
            DrawList("Right Fingers", draft.rightFingers, nameof(draft.rightFingers));

            DrawSection("Left Leg");
            DrawTransformField("Left Thigh", ref draft.leftThigh, nameof(draft.leftThigh));
            DrawTransformField("Left Calf", ref draft.leftCalf, nameof(draft.leftCalf));
            DrawTransformField("Left Foot", ref draft.leftFoot, nameof(draft.leftFoot));
            DrawTransformField("Left Toe", ref draft.leftToe, nameof(draft.leftToe));
            DrawList("Left Extra Leg Bones", draft.leftExtraLegBones, nameof(draft.leftExtraLegBones));

            DrawSection("Right Leg");
            DrawTransformField("Right Thigh", ref draft.rightThigh, nameof(draft.rightThigh));
            DrawTransformField("Right Calf", ref draft.rightCalf, nameof(draft.rightCalf));
            DrawTransformField("Right Foot", ref draft.rightFoot, nameof(draft.rightFoot));
            DrawTransformField("Right Toe", ref draft.rightToe, nameof(draft.rightToe));
            DrawList("Right Extra Leg Bones", draft.rightExtraLegBones, nameof(draft.rightExtraLegBones));

            DrawSection("Extra");
            DrawList("Extra Upper Body Bones", draft.extraUpperBodyBones, nameof(draft.extraUpperBodyBones));
            DrawList("Extra Lower Body Bones", draft.extraLowerBodyBones, nameof(draft.extraLowerBodyBones));
            DrawList("Ignored Bones", draft.ignoredBones, nameof(draft.ignoredBones));

            DrawValidation();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Create Reviewed Bone Profile Asset"))
                CreateAsset();
        }

        private void UseSelection()
        {
            if (Selection.activeTransform != null)
                skeletonRoot = Selection.activeTransform;
            BuildDraft();
        }

        private void BuildDraft()
        {
            candidateNotes.Clear();
            if (skeletonRoot == null)
            {
                draft = null;
                return;
            }

            draft = CreateInstance<WowGenericBoneProfile>();
            draft.skeletonRoot = skeletonRoot;
            draft.armatureRoot = skeletonRoot;

            var bones = skeletonRoot.GetComponentsInChildren<Transform>(true);
            draft.motionRoot = Best(bones, nameof(draft.motionRoot), false, "motion", "root");
            draft.pelvis = Best(bones, nameof(draft.pelvis), false, "pelvis");
            draft.hips = Best(bones, nameof(draft.hips), false, "hip", "hips");
            draft.spineLower = BestOrdinal(bones, nameof(draft.spineLower), 0, false, "spine", "torso");
            draft.spineMiddle = BestOrdinal(bones, nameof(draft.spineMiddle), 1, false, "spine", "torso");
            draft.spineUpper = BestOrdinal(bones, nameof(draft.spineUpper), 2, false, "spine", "torso");
            draft.chest = Best(bones, nameof(draft.chest), false, "chest");
            draft.neck = Best(bones, nameof(draft.neck), false, "neck");
            draft.head = Best(bones, nameof(draft.head), false, "head");

            draft.leftClavicle = Best(bones, nameof(draft.leftClavicle), true, "clavicle");
            draft.leftShoulder = Best(bones, nameof(draft.leftShoulder), true, "shoulder");
            draft.leftUpperArm = Best(bones, nameof(draft.leftUpperArm), true, "arm");
            draft.leftForearm = Best(bones, nameof(draft.leftForearm), true, "forearm");
            draft.leftHand = Best(bones, nameof(draft.leftHand), true, "hand");
            AddMatches(draft.leftFingers, bones, nameof(draft.leftFingers), true, "finger", "thumb");

            draft.rightClavicle = Best(bones, nameof(draft.rightClavicle), false, "clavicle");
            draft.rightShoulder = Best(bones, nameof(draft.rightShoulder), false, "shoulder");
            draft.rightUpperArm = Best(bones, nameof(draft.rightUpperArm), false, "arm");
            draft.rightForearm = Best(bones, nameof(draft.rightForearm), false, "forearm");
            draft.rightHand = Best(bones, nameof(draft.rightHand), false, "hand");
            AddMatches(draft.rightFingers, bones, nameof(draft.rightFingers), false, "finger", "thumb");

            draft.leftThigh = Best(bones, nameof(draft.leftThigh), true, "thigh", "leg");
            draft.leftCalf = Best(bones, nameof(draft.leftCalf), true, "calf", "shin", "knee");
            draft.leftFoot = Best(bones, nameof(draft.leftFoot), true, "foot");
            draft.leftToe = Best(bones, nameof(draft.leftToe), true, "toe");

            draft.rightThigh = Best(bones, nameof(draft.rightThigh), false, "thigh", "leg");
            draft.rightCalf = Best(bones, nameof(draft.rightCalf), false, "calf", "shin", "knee");
            draft.rightFoot = Best(bones, nameof(draft.rightFoot), false, "foot");
            draft.rightToe = Best(bones, nameof(draft.rightToe), false, "toe");
        }

        private Transform Best(Transform[] bones, string fieldName, bool leftSide, params string[] tokens)
        {
            var matches = Matches(bones, leftSide, tokens);
            candidateNotes[fieldName] = matches;
            return matches.Count > 0 ? matches[0] : null;
        }

        private Transform BestOrdinal(Transform[] bones, string fieldName, int ordinal, bool leftSide, params string[] tokens)
        {
            var matches = Matches(bones, leftSide, tokens);
            candidateNotes[fieldName] = matches;
            return matches.Count > ordinal ? matches[ordinal] : (matches.Count > 0 ? matches[matches.Count - 1] : null);
        }

        private void AddMatches(List<Transform> target, Transform[] bones, string fieldName, bool leftSide, params string[] tokens)
        {
            var matches = Matches(bones, leftSide, tokens);
            candidateNotes[fieldName] = matches;
            target.Clear();
            target.AddRange(matches);
        }

        private static List<Transform> Matches(Transform[] bones, bool leftSide, params string[] tokens)
        {
            var matches = new List<Transform>();
            for (int i = 0; i < bones.Length; i++)
            {
                var name = bones[i].name.ToLowerInvariant();
                if (!HasAny(name, tokens))
                    continue;
                if (HasSide(name, leftSide))
                    matches.Add(bones[i]);
            }
            return matches;
        }

        private static bool HasAny(string name, string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (name.Contains(tokens[i]))
                    return true;
            }
            return false;
        }

        private static bool HasSide(string name, bool leftSide)
        {
            var tokens = leftSide
                ? new[] { "left", "l_", "_l", ".l", "l-", "-l" }
                : new[] { "right", "r_", "_r", ".r", "r-", "-r" };

            for (int i = 0; i < tokens.Length; i++)
            {
                if (name.Contains(tokens[i]))
                    return true;
            }
            return false;
        }

        private void DrawSection(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawTransformField(string label, ref Transform value, string fieldName)
        {
            value = (Transform)EditorGUILayout.ObjectField(label, value, typeof(Transform), true);
            DrawCandidates(fieldName);
        }

        private void DrawList(string label, List<Transform> values, string fieldName)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var removeIndex = -1;
            for (int i = 0; i < values.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                values[i] = (Transform)EditorGUILayout.ObjectField(values[i], typeof(Transform), true);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
                values.RemoveAt(removeIndex);
            if (GUILayout.Button("Add " + label))
                values.Add(null);
            DrawCandidates(fieldName);
        }

        private void DrawCandidates(string fieldName)
        {
            if (!candidateNotes.TryGetValue(fieldName, out var candidates) || candidates.Count == 0)
                return;

            var names = new List<string>();
            for (int i = 0; i < candidates.Count; i++)
                names.Add(candidates[i] != null ? candidates[i].name : "None");
            EditorGUILayout.LabelField("Candidates", string.Join(", ", names), EditorStyles.miniLabel);
        }

        private void DrawValidation()
        {
            var report = draft.Validate();
            for (int i = 0; i < report.Errors.Count; i++)
                EditorGUILayout.HelpBox(report.Errors[i], MessageType.Error);
            for (int i = 0; i < report.Warnings.Count; i++)
                EditorGUILayout.HelpBox(report.Warnings[i], MessageType.Warning);
        }

        private void CreateAsset()
        {
            if (draft == null)
                return;

            if (!AssetDatabase.IsValidFolder(SaveFolder))
                AssetDatabase.CreateFolder("Assets/Research/WowLocomotion", "ScriptableObjects");
            var asset = Instantiate(draft);
            var path = AssetDatabase.GenerateUniqueAssetPath(SaveFolder + "/WowGenericBoneProfile.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
