using System.Collections.Generic;
using System.IO;
using Deducted.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Main Menu. Run it in the MainMenu scene.
    /// Creates MainMenuCanvas (dithered): dark shade on the left that fades out to the right,
    /// big title, NEW GAME / QUIT and the key hint at the bottom, with MainMenu wired up.
    /// Also puts this scene first in the build profile's scene list and adds the game scene.
    /// Replaces an existing MainMenuCanvas.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ShadePath = "Assets/_Project/UI/Menu/menu_shade.png";
        private const string SelectSoundPath = "Assets/_Project/Audio/Interactions/menu_click.wav";
        private const string ConfirmSoundPath = "Assets/_Project/Audio/Interactions/menu_start.ogg";
        private const string GameSceneName = "Test";

        private static readonly (string label, MainMenu.Action action)[] EntryDefs =
        {
            ("NEW GAME", MainMenu.Action.NewGame),
            ("QUIT", MainMenu.Action.Quit),
        };

        [MenuItem("Deducted/Build Main Menu")]
        public static void Build()
        {
            GameObject old = GameObject.Find("MainMenuCanvas");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build Main Menu");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            // TextMeshPro needs these on a Screen Space - Camera canvas.
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                                              | AdditionalCanvasShaderChannels.Normal
                                              | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<DitheredCanvas>();

            // Dark on the left, fading out towards the middle of the screen.
            RectTransform shade = Rect("Shade", canvasGo.transform, 0f, 0f, 0.62f, 1f);
            Image shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.sprite = ShadeSprite();
            shadeImage.color = Color.black;
            shadeImage.raycastTarget = false;

            TMP_Text title = Text("Title", canvasGo.transform, 0.06f, 0.66f, 0.6f, 0.88f, "DEDUCTED", 150, TextAlignmentOptions.BottomLeft);
            title.fontStyle = FontStyles.Bold;

            RectTransform listBox = Rect("Entries", canvasGo.transform, 0.06f, 0.36f, 0.3f, 0.58f);
            var layout = listBox.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var built = new List<(Image background, TMP_Text label, MainMenu.Action action)>();
            for (int i = 0; i < EntryDefs.Length; i++)
            {
                var row = new GameObject(EntryDefs[i].label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.transform.SetParent(listBox, false);
                row.GetComponent<LayoutElement>().preferredHeight = 76f;
                var bg = row.GetComponent<Image>();
                bg.color = i == 0 ? Color.white : Color.clear;
                bg.raycastTarget = false;
                TMP_Text label = Text("Label", row.transform, 0f, 0f, 1f, 1f, EntryDefs[i].label, 50, TextAlignmentOptions.Left);
                label.margin = new Vector4(24f, 0f, 0f, 0f);
                if (i == 0) label.color = Color.black;
                built.Add((bg, label, EntryDefs[i].action));
            }

            Text("Hint", canvasGo.transform, 0.06f, 0.04f, 0.6f, 0.1f, "W / S  select        SPACE  confirm", 30, TextAlignmentOptions.Left);

            var menu = canvasGo.AddComponent<MainMenu>();
            var so = new SerializedObject(menu);
            SerializedProperty list = so.FindProperty("entries");
            list.arraySize = built.Count;
            for (int i = 0; i < built.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("action").enumValueIndex = (int)built[i].action;
                e.FindPropertyRelative("background").objectReferenceValue = built[i].background;
                e.FindPropertyRelative("label").objectReferenceValue = built[i].label;
            }
            so.FindProperty("gameScene").stringValue = GameSceneName;
            so.FindProperty("selectSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SelectSoundPath);
            so.FindProperty("confirmSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ConfirmSoundPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            AddScenesToBuild(canvasGo.scene.path);

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[MainMenuBuilder] MainMenuCanvas built.");
        }

        // This scene first (the game starts here), the game scene after it, everything else kept.
        private static void AddScenesToBuild(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath))
            {
                Debug.LogWarning("[MainMenuBuilder] Save the scene first, then build again to add it to the scene list.");
                return;
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == menuPath);
            scenes.Insert(0, new EditorBuildSettingsScene(menuPath, true));

            foreach (string guid in AssetDatabase.FindAssets(GameSceneName + " t:Scene", new[] { "Assets/_Project/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != GameSceneName) continue;
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 256x4 black-to-clear gradient, saved as a sprite next to the other menu art.
        private static Sprite ShadeSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
            if (existing != null) return existing;

            const int width = 256, height = 4;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                // Solid up to 55 %, then a smooth fade to nothing at the right edge.
                float a = 0.88f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, u)));
                for (int y = 0; y < height; y++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(ShadePath));
            File.WriteAllBytes(ShadePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ShadePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ShadePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
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
