using Deducted.Interaction;
using Deducted.NPC;
using Deducted.Player;
using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Dialogue UI.
    /// Creates in the open scene: InteractionCamera (disabled, tagged MainCamera, settings copied from the
    /// main camera), DialogueCanvas (dithered; bottom text box with name, line and "< A   D >" hint, plus the
    /// interact prompt: black box with white text, bottom centre) with DialogueSystem wired up,
    /// plus the shop panel (Build Shop UI), and adds PlayerInteractor to the player.
    /// Replaces an existing DialogueCanvas / InteractionCamera.
    /// </summary>
    public static class DialogueUIBuilder
    {
        private static readonly Color Panel = new Color(0f, 0f, 0f, 0.85f);

        [MenuItem("Deducted/Build Dialogue UI")]
        public static void Build()
        {
            foreach (string oldName in new[] { "DialogueCanvas", "InteractionCamera" })
            {
                GameObject old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            Camera main = Camera.main;
            Camera cam = BuildCamera(main);

            var canvasGo = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Dialogue UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            // TextMeshPro needs these on a Screen Space - Camera canvas.
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                                              | AdditionalCanvasShaderChannels.Normal
                                              | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Talk box at the bottom.
            RectTransform root = Rect("DialogueBox", canvasGo.transform, 0.05f, 0.03f, 0.95f, 0.32f);
            root.gameObject.AddComponent<Image>().color = Panel;
            TMP_Text nameText = Text("Name", root, 0.03f, 0.74f, 0.97f, 0.95f, "VENDOR", 38, TextAlignmentOptions.TopLeft);
            TMP_Text lineText = Text("Line", root, 0.03f, 0.08f, 0.90f, 0.72f, "...", 46, TextAlignmentOptions.TopLeft);
            lineText.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text hint = Text("ContinueHint", root, 0.80f, 0.06f, 0.98f, 0.30f, "< A   D >", 34, TextAlignmentOptions.BottomRight);

            // Interact prompt while something is in reach, bottom centre: black box, white text.
            var prompt = Rect("InteractPrompt", canvasGo.transform, 0.40f, 0.05f, 0.60f, 0.13f).gameObject.AddComponent<Image>();
            prompt.color = Color.black;
            prompt.raycastTarget = false;
            TMP_Text promptText = Text("Text", prompt.transform, 0f, 0f, 1f, 1f, "[Interact]", 40, TextAlignmentOptions.Center);
            prompt.gameObject.SetActive(false);

            canvasGo.AddComponent<DitheredCanvas>();
            var system = canvasGo.AddComponent<DialogueSystem>();
            var so = new SerializedObject(system);
            so.FindProperty("interactionCamera").objectReferenceValue = cam;
            so.FindProperty("root").objectReferenceValue = root.gameObject;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("lineText").objectReferenceValue = lineText;
            so.FindProperty("continueHint").objectReferenceValue = hint.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            ShopUIBuilder.BuildInto(system);

            // Player: PlayerInteractor with the prompt.
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                var interactor = player.GetComponent<PlayerInteractor>();
                if (interactor == null) interactor = Undo.AddComponent<PlayerInteractor>(player.gameObject);
                var iso = new SerializedObject(interactor);
                iso.FindProperty("prompt").objectReferenceValue = prompt.gameObject;
                iso.FindProperty("promptText").objectReferenceValue = promptText;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[DialogueUIBuilder] No PlayerMovement in the scene: add PlayerInteractor to the player yourself and drag InteractPrompt into Prompt and its Text into Prompt Text.");
            }

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[DialogueUIBuilder] DialogueCanvas + InteractionCamera built.");
        }

        private static Camera BuildCamera(Camera main)
        {
            var go = new GameObject("InteractionCamera", typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, "Build Dialogue UI");
            var cam = go.GetComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
                var src = main.GetUniversalAdditionalCameraData();
                var dst = cam.GetUniversalAdditionalCameraData();
                dst.renderPostProcessing = src.renderPostProcessing;
                dst.antialiasing = src.antialiasing;
            }
            go.tag = "MainCamera"; // so Camera.main (dithered UI, hearing) follows it while it's on
            cam.orthographic = false;
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            return cam;
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
