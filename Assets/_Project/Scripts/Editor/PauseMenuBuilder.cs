using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Pause Menu.
    /// Creates PauseCanvas (dithered, above everything but the screen fade): darkened screen, "PAUSED",
    /// the list Resume / Controls / Main Menu and the controls table, with PauseMenu wired up.
    /// Replaces an existing PauseCanvas.
    /// </summary>
    public static class PauseMenuBuilder
    {
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.85f);
        private static readonly string[] EntryNames = { "RESUME", "CONTROLS", "MAIN MENU" };

        [MenuItem("Deducted/Build Pause Menu")]
        public static void Build()
        {
            GameObject old = GameObject.Find("PauseCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvasGo = new GameObject("PauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Pause Menu");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40; // above HUD (25), overview (20), dialogue (15); below ScreenFade (100)
            // TextMeshPro needs these on a Screen Space - Camera canvas.
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                                              | AdditionalCanvasShaderChannels.Normal
                                              | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<DitheredCanvas>();

            // Everything shown while paused.
            RectTransform root = Rect("Root", canvasGo.transform, 0f, 0f, 1f, 1f);
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = false;
            Text("Title", root, 0.3f, 0.74f, 0.7f, 0.86f, "PAUSED", 80, TextAlignmentOptions.Center);

            // List.
            RectTransform list = Rect("List", root, 0f, 0f, 1f, 0.72f);
            var listBox = Rect("Entries", list, 0.38f, 0.45f, 0.62f, 0.85f);
            var layout = listBox.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var entries = new (Image background, TMP_Text label)[EntryNames.Length];
            for (int i = 0; i < EntryNames.Length; i++)
            {
                var row = new GameObject(EntryNames[i], typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.transform.SetParent(listBox, false);
                row.GetComponent<LayoutElement>().preferredHeight = 72f;
                var bg = row.GetComponent<Image>();
                bg.color = i == 0 ? Color.white : Color.clear;
                bg.raycastTarget = false;
                TMP_Text label = Text("Label", row.transform, 0f, 0f, 1f, 1f, EntryNames[i], 46, TextAlignmentOptions.Center);
                if (i == 0) label.color = Color.black;
                entries[i] = (bg, label);
            }
            Text("Hint", list, 0.2f, 0.3f, 0.8f, 0.38f, "W / S  select        SPACE  confirm", 30, TextAlignmentOptions.Center);

            // Controls table.
            RectTransform controls = Rect("Controls", root, 0.2f, 0.1f, 0.8f, 0.72f);
            var controlsBg = controls.gameObject.AddComponent<Image>();
            controlsBg.color = Panel;
            controlsBg.raycastTarget = false;
            Text("Header", controls, 0.05f, 0.88f, 0.95f, 0.97f, "CONTROLS", 48, TextAlignmentOptions.Center);
            TMP_Text keys = Text("Keys", controls, 0.06f, 0.12f, 0.40f, 0.86f, "", 34, TextAlignmentOptions.TopLeft);
            TMP_Text actions = Text("Actions", controls, 0.42f, 0.12f, 0.95f, 0.86f, "", 34, TextAlignmentOptions.TopLeft);
            Text("Hint", controls, 0.05f, 0.02f, 0.95f, 0.1f, "SPACE / ESC  back", 28, TextAlignmentOptions.Center);
            controls.gameObject.SetActive(false);

            var menu = canvasGo.AddComponent<PauseMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            so.FindProperty("listPanel").objectReferenceValue = list.gameObject;
            so.FindProperty("controlsPanel").objectReferenceValue = controls.gameObject;
            so.FindProperty("controlKeysText").objectReferenceValue = keys;
            so.FindProperty("controlActionsText").objectReferenceValue = actions;
            SerializedProperty entryList = so.FindProperty("entries");
            entryList.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty e = entryList.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("background").objectReferenceValue = entries[i].background;
                e.FindPropertyRelative("label").objectReferenceValue = entries[i].label;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[PauseMenuBuilder] PauseCanvas built.");
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
    }
}
