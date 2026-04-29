#if UNITY_EDITOR
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Custom inspector that seeds and bakes compound submarine colliders from authored primitive segments.
    /// </summary>
    [CustomEditor(typeof(SubmarineCompoundColliderAuthoring))]
    public sealed class SubmarineCompoundColliderAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed From Render Bounds"))
                    SeedFromRenderBounds((SubmarineCompoundColliderAuthoring)target);

                if (GUILayout.Button("Bake Compound Colliders"))
                    BakeCompoundColliders((SubmarineCompoundColliderAuthoring)target);

                if (GUILayout.Button("Clear Generated"))
                    ClearGeneratedColliders((SubmarineCompoundColliderAuthoring)target);
            }
        }

        private static void SeedFromRenderBounds(SubmarineCompoundColliderAuthoring authoring)
        {
            if (authoring == null)
                return;

            Renderer[] renderers = authoring.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length <= 0)
            {
                Debug.LogWarning("[SubmarineCompoundColliderAuthoring] No renderers found to seed collider bounds.", authoring);
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                combinedBounds.Encapsulate(renderer.bounds);
            }

            Undo.RecordObject(authoring, "Seed Submarine Compound Collider");
            SerializedObject serializedObject = new SerializedObject(authoring);
            SerializedProperty boxShapesProperty = serializedObject.FindProperty("boxShapes");
            boxShapesProperty.arraySize = 1;
            SerializedProperty firstBox = boxShapesProperty.GetArrayElementAtIndex(0);
            firstBox.FindPropertyRelative("Name").stringValue = "HullBounds";
            firstBox.FindPropertyRelative("Center").vector3Value =
                authoring.transform.InverseTransformPoint(combinedBounds.center);
            firstBox.FindPropertyRelative("Size").vector3Value =
                TransformWorldSizeToLocal(authoring.transform, combinedBounds.size);
            firstBox.FindPropertyRelative("Material").objectReferenceValue = null;
            firstBox.FindPropertyRelative("IsTrigger").boolValue = false;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(authoring);
        }

        private static Vector3 TransformWorldSizeToLocal(Transform transform, Vector3 worldSize)
        {
            Vector3 lossyScale = transform != null ? transform.lossyScale : Vector3.one;
            return new Vector3(
                SafeDivide(worldSize.x, lossyScale.x),
                SafeDivide(worldSize.y, lossyScale.y),
                SafeDivide(worldSize.z, lossyScale.z));
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) > 0.0001f ? numerator / denominator : numerator;
        }

        private static void BakeCompoundColliders(SubmarineCompoundColliderAuthoring authoring)
        {
            if (authoring == null)
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Bake Submarine Compound Colliders");

            Transform generatedRoot = ResolveGeneratedRoot(authoring.transform, authoring.GeneratedRootName);
            if (authoring.ReplaceExistingGeneratedColliders)
                ClearGeneratedChildren(generatedRoot);

            SubmarineCompoundColliderAuthoring.BoxShape[] boxShapes = authoring.BoxShapes;
            for (int i = 0; i < boxShapes.Length; i++)
            {
                SubmarineCompoundColliderAuthoring.BoxShape shape = boxShapes[i];
                GameObject colliderObject = CreateColliderObject(generatedRoot, shape.Name, i, "Box");
                BoxCollider collider = Undo.AddComponent<BoxCollider>(colliderObject);
                collider.center = shape.Center;
                collider.size = new Vector3(
                    Mathf.Max(0.01f, shape.Size.x),
                    Mathf.Max(0.01f, shape.Size.y),
                    Mathf.Max(0.01f, shape.Size.z));
                collider.sharedMaterial = shape.Material;
                collider.isTrigger = shape.IsTrigger;
            }

            SubmarineCompoundColliderAuthoring.CapsuleShape[] capsuleShapes = authoring.CapsuleShapes;
            for (int i = 0; i < capsuleShapes.Length; i++)
            {
                SubmarineCompoundColliderAuthoring.CapsuleShape shape = capsuleShapes[i];
                GameObject colliderObject = CreateColliderObject(generatedRoot, shape.Name, i, "Capsule");
                CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(colliderObject);
                collider.center = shape.Center;
                collider.radius = Mathf.Max(0.01f, shape.Radius);
                collider.height = Mathf.Max(collider.radius * 2f, shape.Height);
                collider.direction = Mathf.Clamp(shape.Direction, 0, 2);
                collider.sharedMaterial = shape.Material;
                collider.isTrigger = shape.IsTrigger;
            }

            EditorUtility.SetDirty(authoring.gameObject);
        }

        private static void ClearGeneratedColliders(SubmarineCompoundColliderAuthoring authoring)
        {
            if (authoring == null)
                return;

            Transform generatedRoot = authoring.transform.Find(authoring.GeneratedRootName);
            if (generatedRoot == null)
                return;

            Undo.DestroyObjectImmediate(generatedRoot.gameObject);
            EditorUtility.SetDirty(authoring.gameObject);
        }

        private static Transform ResolveGeneratedRoot(Transform owner, string rootName)
        {
            Transform existing = owner.Find(rootName);
            if (existing != null)
                return existing;

            GameObject generatedRoot = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(generatedRoot, "Create Compound Collider Root");
            generatedRoot.transform.SetParent(owner, false);
            return generatedRoot.transform;
        }

        private static void ClearGeneratedChildren(Transform generatedRoot)
        {
            if (generatedRoot == null)
                return;

            for (int i = generatedRoot.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(generatedRoot.GetChild(i).gameObject);
        }

        private static GameObject CreateColliderObject(Transform parent, string authoredName, int index, string defaultPrefix)
        {
            string objectName = string.IsNullOrWhiteSpace(authoredName)
                ? $"{defaultPrefix}_{index:00}"
                : authoredName;
            GameObject colliderObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(colliderObject, "Create Compound Collider Segment");
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
            return colliderObject;
        }
    }
}
#endif
