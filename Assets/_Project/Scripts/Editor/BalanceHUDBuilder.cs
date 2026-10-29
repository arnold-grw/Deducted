using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Balance HUD.
    /// Creates HUDCanvas with a copy of the Tab overview's balance text (same look, same spot, bottom right),
    /// always visible in the game view, hidden during NPC talks. BodyOverview keeps it updated
    /// (wired as its HUD Balance Text), plus the cigarette counter bottom left. Run Build Body Overview first.
    /// Replaces an existing HUDCanvas.
    /// </summary>
    public static class BalanceHUDBuilder
    {
        [MenuItem("Deducted/Build Balance HUD")]
        public static void Build()
        {
            BodyOverview overview = Object.FindFirstObjectByType<BodyOverview>(FindObjectsInactive.Include);
            var so = overview != null ? new SerializedObject(overview) : null;
            var source = so?.FindProperty("balanceText").objectReferenceValue as TMP_Text;
            if (source == null)
            {
                EditorUtility.DisplayDialog("Build Balance HUD",
                    "No BodyOverview with a Balance Text in the scene. Run Deducted > Build Body Overview first.", "OK");
                return;
            }

            GameObject old = GameObject.Find("HUDCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Balance HUD");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25; // above the overview (20): balance + cigarettes stay visible in the Tab menu
            // TextMeshPro needs these on a Screen Space - Camera canvas.
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                                              | AdditionalCanvasShaderChannels.Normal
                                              | AdditionalCanvasShaderChannels.Tangent;
            EditorUtility.CopySerialized(source.canvas.rootCanvas.GetComponent<CanvasScaler>(), canvasGo.GetComponent<CanvasScaler>());
            canvasGo.GetComponent<CanvasGroup>().blocksRaycasts = false;
            canvasGo.AddComponent<DitheredCanvas>();
            canvasGo.AddComponent<HideDuringDialogue>();

            // Same look and spot: copy the overview's text. Its anchors are relative to the overview's
            // full-screen root, so they mean the same screen position here.
            TMP_Text copy = Object.Instantiate(source, canvasGo.transform, false);
            copy.name = "Balance";
            var rt = (RectTransform)copy.transform;
            var src = (RectTransform)source.transform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.offsetMin = src.offsetMin;
            rt.offsetMax = src.offsetMax;
            copy.gameObject.SetActive(true);

            so.FindProperty("hudBalanceText").objectReferenceValue = copy;
            so.ApplyModifiedPropertiesWithoutUndo();
            CigaretteCounterBuilder.BuildFrom(copy);

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[BalanceHUDBuilder] HUDCanvas built and wired to BodyOverview.");
        }
    }
}
