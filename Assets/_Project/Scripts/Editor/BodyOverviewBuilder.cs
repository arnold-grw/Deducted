using Deducted.Body;
using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Body Overview.
    /// Creates OverviewCanvas in the open scene: full-screen background, part labels on the left
    /// (eyes, ears, legs as rows of left/right), receipt scroll view on the right, balance below it,
    /// name/ID at the top centre, and wires everything into BodyOverview.
    /// Runs through the 1-bit shader via DitheredCanvas. Replaces an existing MenuCanvas/OverviewCanvas.
    /// </summary>
    public static class BodyOverviewBuilder
    {
        private const string BackgroundPath = "Assets/_Project/UI/Menu/overview_background.png";
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.6f);

        [MenuItem("Deducted/Build Body Overview")]
        public static void Build()
        {
            if (!RemoveOld()) return;

            var canvasGo = new GameObject("OverviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Body Overview");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = Rect("Root", canvasGo.transform, 0, 0, 1, 1);

            var background = Rect("Background", root, 0, 0, 1, 1).gameObject.AddComponent<Image>();
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            background.color = background.sprite != null ? Color.white : Color.black;
            if (background.sprite == null)
                Debug.LogWarning($"[BodyOverviewBuilder] No sprite at {BackgroundPath} (Texture Type Sprite, Sprite Mode Single?).");

            TMP_Text nameText = Text("Name", root, 0.35f, 0.89f, 0.65f, 0.97f, "NAMELESS", 48, TextAlignmentOptions.Top);

            // Left column: header, then rows of left/right labels.
            Text("Header", root, 0.02f, 0.89f, 0.30f, 0.96f, "SUBJECT STATUS", 40, TextAlignmentOptions.TopLeft);
            var labels = new (BodyPartId id, TMP_Text text)[6];
            BodyPartId[,] rows =
            {
                { BodyPartId.LeftEye, BodyPartId.RightEye },
                { BodyPartId.LeftEar, BodyPartId.RightEar },
                { BodyPartId.LeftLeg, BodyPartId.RightLeg },
            };
            float[] colX = { 0.02f, 0.165f };
            for (int r = 0; r < 3; r++)
            {
                float top = 0.85f - r * 0.22f;
                for (int c = 0; c < 2; c++)
                {
                    BodyPartId id = rows[r, c];
                    var label = Text($"Label_{id}", root, colX[c], top - 0.18f, colX[c] + 0.135f, top,
                        $"{id}\nORGANIC", 28, TextAlignmentOptions.TopLeft);
                    labels[r * 2 + c] = (id, label);
                }
            }

            // Right column: receipt header, scroll view, balance.
            Text("ReceiptHeader", root, 0.70f, 0.89f, 0.98f, 0.96f, "RECEIPT", 40, TextAlignmentOptions.TopLeft);
            ScrollRect scroll = ScrollView(root, out TMP_Text receipt);
            TMP_Text balance = Text("Balance", root, 0.70f, 0.05f, 0.98f, 0.18f, "1000 ₵", 56, TextAlignmentOptions.Right);

            // Wire up BodyOverview.
            canvasGo.AddComponent<DitheredCanvas>();
            var overview = canvasGo.AddComponent<BodyOverview>();
            var so = new SerializedObject(overview);
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            so.FindProperty("receiptText").objectReferenceValue = receipt;
            so.FindProperty("receiptScroll").objectReferenceValue = scroll;
            so.FindProperty("balanceText").objectReferenceValue = balance;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            SerializedProperty parts = so.FindProperty("parts");
            parts.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                SerializedProperty element = parts.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("part").enumValueIndex = (int)labels[i].id;
                element.FindPropertyRelative("label").objectReferenceValue = labels[i].text;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[BodyOverviewBuilder] OverviewCanvas built. It hides itself on Play; Tab toggles it.");
        }

        private static bool RemoveOld()
        {
            var old = new System.Collections.Generic.List<GameObject>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas && (c.name == "MenuCanvas" || c.name == "OverviewCanvas"))
                    old.Add(c.gameObject);

            if (old.Count > 0 && !EditorUtility.DisplayDialog("Build Body Overview",
                    $"Replace {string.Join(", ", old.ConvertAll(o => o.name))}?", "Replace", "Cancel"))
                return false;

            foreach (var go in old) Undo.DestroyObjectImmediate(go);
            return true;
        }

        private static RectTransform Rect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text Text(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string content, float size, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static ScrollRect ScrollView(Transform parent, out TMP_Text receipt)
        {
            RectTransform area = Rect("ReceiptScroll", parent, 0.70f, 0.20f, 0.98f, 0.88f);
            area.gameObject.AddComponent<Image>().color = Panel;
            var scroll = area.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = Rect("Viewport", area, 0, 0, 1, 1);
            viewport.offsetMin = new Vector2(16, 16);
            viewport.offsetMax = new Vector2(-16, -16);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("Content", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var text = content.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            receipt = text;
            return scroll;
        }
    }
}
