using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Cigarette Counter.
    /// Copies the HUD balance text (same look) into HUDCanvas as "Cigarettes", mirrored to the bottom left,
    /// with CigaretteCounter on it. Run Build Balance HUD first (which also runs this). Replaces an existing one.
    /// </summary>
    public static class CigaretteCounterBuilder
    {
        [MenuItem("Deducted/Build Cigarette Counter")]
        public static void Build()
        {
            BodyOverview overview = Object.FindFirstObjectByType<BodyOverview>(FindObjectsInactive.Include);
            var source = overview != null
                ? new SerializedObject(overview).FindProperty("hudBalanceText").objectReferenceValue as TMP_Text
                : null;
            if (source == null)
            {
                EditorUtility.DisplayDialog("Build Cigarette Counter",
                    "No HUD balance text found. Run Deducted > Build Balance HUD first.", "OK");
                return;
            }
            BuildFrom(source);
            EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            Debug.Log("[CigaretteCounterBuilder] Cigarette counter built (bottom left of HUDCanvas).");
        }

        public static void BuildFrom(TMP_Text balance)
        {
            Transform parent = balance.transform.parent;
            Transform old = parent.Find("Cigarettes");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            TMP_Text copy = Object.Instantiate(balance, parent, false);
            Undo.RegisterCreatedObjectUndo(copy.gameObject, "Build Cigarette Counter");
            copy.name = "Cigarettes";
            copy.text = "10 CIGS";

            // Mirror left <-> right.
            var src = (RectTransform)balance.transform;
            var rt = (RectTransform)copy.transform;
            rt.anchorMin = new Vector2(1f - src.anchorMax.x, src.anchorMin.y);
            rt.anchorMax = new Vector2(1f - src.anchorMin.x, src.anchorMax.y);
            rt.pivot = new Vector2(1f - src.pivot.x, src.pivot.y);
            rt.offsetMin = new Vector2(-src.offsetMax.x, src.offsetMin.y);
            rt.offsetMax = new Vector2(-src.offsetMin.x, src.offsetMax.y);
            if (copy.horizontalAlignment == HorizontalAlignmentOptions.Right) copy.horizontalAlignment = HorizontalAlignmentOptions.Left;
            else if (copy.horizontalAlignment == HorizontalAlignmentOptions.Left) copy.horizontalAlignment = HorizontalAlignmentOptions.Right;

            var counter = copy.gameObject.AddComponent<CigaretteCounter>();
            var so = new SerializedObject(counter);
            so.FindProperty("text").objectReferenceValue = copy;
            so.ApplyModifiedPropertiesWithoutUndo();
            copy.gameObject.SetActive(true);
            Selection.activeGameObject = copy.gameObject;
        }
    }
}
