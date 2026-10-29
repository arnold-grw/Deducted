using Deducted.NPC;
using Deducted.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Shop UI.
    /// Adds ShopPanel to the DialogueCanvas (above the talk box, grows upwards with the number of offers):
    /// row template (name left, price right), balance before / after and the hold-F bar.
    /// Wires it as the DialogueSystem's Shop View and sets the hint to "&lt; A   D &gt;".
    /// Run Build Dialogue UI first (which also runs this). Replaces an existing ShopPanel.
    /// </summary>
    public static class ShopUIBuilder
    {
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.85f);

        [MenuItem("Deducted/Build Shop UI")]
        public static void Build()
        {
            DialogueSystem system = Object.FindFirstObjectByType<DialogueSystem>(FindObjectsInactive.Include);
            if (system == null)
            {
                EditorUtility.DisplayDialog("Build Shop UI",
                    "No DialogueSystem in the scene. Run Deducted > Build Dialogue UI first.", "OK");
                return;
            }
            BuildInto(system);
            EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
            Debug.Log("[ShopUIBuilder] ShopPanel built and wired to the DialogueSystem.");
        }

        public static void BuildInto(DialogueSystem system)
        {
            Transform canvas = system.transform;
            Transform old = canvas.Find("ShopPanel");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // Panel: left part of the screen, bottom edge just above the talk box, grows upwards.
            var panelGo = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            Undo.RegisterCreatedObjectUndo(panelGo, "Build Shop UI");
            panelGo.transform.SetParent(canvas, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = new Vector2(0.05f, 0.34f);
            panel.anchorMax = new Vector2(0.55f, 0.34f);
            panel.pivot = new Vector2(0f, 0f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.sizeDelta = new Vector2(0f, 300f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;
            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 8f;
            SetupLayout(layout);
            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Rows.
            var rowsGo = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsGo.transform.SetParent(panel, false);
            var rowsLayout = rowsGo.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 4f;
            SetupLayout(rowsLayout);

            var rowGo = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ShopRow));
            rowGo.transform.SetParent(rowsGo.transform, false);
            var rowBg = rowGo.GetComponent<Image>();
            rowBg.color = Color.clear;
            rowBg.raycastTarget = false;
            var rowLayout = rowGo.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 56f;
            TMP_Text nameText = Text("Name", rowGo.transform, 0.02f, 0f, 0.62f, 1f, "Item", 34, TextAlignmentOptions.MidlineLeft);
            TMP_Text priceText = Text("Price", rowGo.transform, 0.6f, 0f, 0.98f, 1f, "-20 ₵", 34, TextAlignmentOptions.MidlineRight);
            var rso = new SerializedObject(rowGo.GetComponent<ShopRow>());
            rso.FindProperty("background").objectReferenceValue = rowBg;
            rso.FindProperty("nameText").objectReferenceValue = nameText;
            rso.FindProperty("priceText").objectReferenceValue = priceText;
            rso.FindProperty("layout").objectReferenceValue = rowLayout;
            rso.ApplyModifiedPropertiesWithoutUndo();
            rowGo.SetActive(false);

            // Balance before / after.
            var balanceGo = new GameObject("Balance", typeof(RectTransform), typeof(LayoutElement));
            balanceGo.transform.SetParent(panel, false);
            balanceGo.GetComponent<LayoutElement>().preferredHeight = 44f;
            TMP_Text balanceText = Text("Text", balanceGo.transform, 0.02f, 0f, 0.98f, 1f, "BALANCE  0 ₵", 28, TextAlignmentOptions.MidlineLeft);

            // Hold bar: label, white frame, black inside, white fill (width = progress).
            var barGo = new GameObject("HoldBar", typeof(RectTransform), typeof(LayoutElement));
            barGo.transform.SetParent(panel, false);
            barGo.GetComponent<LayoutElement>().preferredHeight = 32f;
            Text("Label", barGo.transform, 0.02f, 0f, 0.24f, 1f, "HOLD SPACE", 26, TextAlignmentOptions.MidlineLeft);
            Image frame = Box("Frame", barGo.transform, 0.26f, 0.12f, 0.98f, 0.88f, Color.white);
            Image inner = Box("Inner", frame.transform, 0f, 0f, 1f, 1f, Color.black);
            var innerRt = (RectTransform)inner.transform;
            innerRt.offsetMin = new Vector2(3f, 3f);
            innerRt.offsetMax = new Vector2(-3f, -3f);
            Image fill = Box("Fill", inner.transform, 0f, 0f, 0f, 1f, Color.white);

            ShopView view = panelGo.AddComponent<ShopView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("rowContainer").objectReferenceValue = rowsGo.transform;
            vso.FindProperty("rowTemplate").objectReferenceValue = rowGo.GetComponent<ShopRow>();
            vso.FindProperty("balanceText").objectReferenceValue = balanceText;
            vso.FindProperty("holdFill").objectReferenceValue = fill.transform;
            vso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(system);
            so.FindProperty("shopView").objectReferenceValue = view;
            // Pages are turned with A / D now.
            var hint = so.FindProperty("continueHint").objectReferenceValue as GameObject;
            TMP_Text hintText = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (hintText != null)
            {
                Undo.RecordObject(hintText, "Build Shop UI");
                hintText.text = "< A   D >";
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            panelGo.SetActive(false);
            Selection.activeGameObject = panelGo;
        }

        private static void SetupLayout(VerticalLayoutGroup layout)
        {
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
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

        private static Image Box(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            var image = Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
    }
}
