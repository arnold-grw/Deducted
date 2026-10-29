using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Setup NPC Animations.
    /// Sets the imports in Models/NPC/Male: male_npc.fbx becomes Humanoid (its own avatar), every other FBX
    /// there uses that avatar, loops and stays in place, so any of them can be an NPCIdle clip.
    /// Run again after adding new animations. Keeps Animation Events.
    /// </summary>
    public static class NPCAnimationSetup
    {
        private const string Folder = "Assets/_Project/Models/NPC/Male";
        private const string ModelFile = "male_npc";

        [MenuItem("Deducted/Setup NPC Animations")]
        public static void Setup()
        {
            string modelPath = $"{Folder}/{ModelFile}.fbx";
            var modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (modelImporter == null)
            {
                EditorUtility.DisplayDialog("Setup NPC Animations", $"{modelPath} not found.", "OK");
                return;
            }

            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.SaveAndReimport();
            ConfigureClip(modelImporter, ModelFile);

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
            {
                EditorUtility.DisplayDialog("Setup NPC Animations",
                    $"{modelPath} could not be made Humanoid. Check Rig > Configure for errors.", "OK");
                return;
            }

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == modelPath || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.SaveAndReimport();
                ConfigureClip(importer, Path.GetFileNameWithoutExtension(path));
                count++;
            }
            Debug.Log($"[NPCAnimationSetup] {ModelFile} + {count} animations set up in {Folder}.");
        }

        private static void ConfigureClip(ModelImporter importer, string clipName)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            if (clips.Length == 0) return;

            // Mixamo exports one take per file; keep only the first. Looping, in place.
            ModelImporterClipAnimation clip = clips[0];
            clip.name = clipName;
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;

            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }
    }
}
