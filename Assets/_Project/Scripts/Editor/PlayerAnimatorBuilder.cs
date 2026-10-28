using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Player Animator.
    /// Sets the Mixamo FBX import settings (Humanoid, shared avatar, loop, in place)
    /// and (re)builds AC_Player with all locomotion forms, interact, smoke and death.
    /// Safe to run again after adding or swapping clips.
    /// </summary>
    public static class PlayerAnimatorBuilder
    {
        private const string Folder = "Assets/_Project/Models/Player";
        private const string ModelFile = "player";
        private const string ControllerPath = Folder + "/AC_Player.controller";
        // Forms 0-2 (Normal, Injured, InjuredHeavy) are upright: interact + death. Forms 3-4 crawl: death_low only.
        private const int StandingFormCount = 3;

        // File name (without .fbx) -> loops?
        private static readonly Dictionary<string, bool> AnimationFiles = new Dictionary<string, bool>
        {
            { "walk", true },
            { "injured_idle", true },
            { "injured_walk", true },
            { "limp_walk", true },
            { "crawl_all_fors", true },
            { "crawl", true },
            { "interact", false },
            { "death", false },
            { "death_low", false },
            { "dancing", true },
            { "smoke", false },
        };

        [MenuItem("Deducted/Build Player Animator")]
        public static void Build()
        {
            string modelPath = $"{Folder}/{ModelFile}.fbx";
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
            {
                EditorUtility.DisplayDialog("Build Player Animator",
                    $"{modelPath} needs Rig > Animation Type = Humanoid and Avatar Definition = Create From This Model (then Apply).", "OK");
                return;
            }

            ConfigureClips(modelPath, "idle", true, null);
            foreach (var pair in AnimationFiles)
            {
                string path = $"{Folder}/{pair.Key}.fbx";
                if (AssetImporter.GetAtPath(path) == null)
                {
                    Debug.LogWarning($"[PlayerAnimatorBuilder] Missing {path}, skipped.");
                    continue;
                }
                ConfigureClips(path, pair.Key, pair.Value, avatar);
            }

            BuildController(modelPath);
            Debug.Log($"[PlayerAnimatorBuilder] Built {ControllerPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        private static void ConfigureClips(string path, string clipName, bool loop, Avatar sourceAvatar)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);

            if (sourceAvatar != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
                importer.SaveAndReimport();
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            if (clips.Length == 0) return;

            // Mixamo exports one take per file; keep only the first.
            ModelImporterClipAnimation clip = clips[0];
            clip.name = clipName;
            clip.loopTime = loop;
            clip.loopPose = loop;
            // Bake root motion into the pose: the clip stays in place, PlayerMovement moves the body.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;

            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string file)
        {
            return AssetDatabase.LoadAllAssetsAtPath($"{Folder}/{file}.fbx")
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static void BuildController(string modelPath)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Form", AnimatorControllerParameterType.Int);
            controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
            controller.AddParameter("MotionSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dance", AnimatorControllerParameterType.Float);
            controller.AddParameter("Smoke", AnimatorControllerParameterType.Trigger);
            // MotionSpeed must default to 1 or everything starts paused.
            var parameters = controller.parameters;
            foreach (var p in parameters)
                if (p.name == "MotionSpeed") p.defaultFloat = 1f;
            controller.parameters = parameters;

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Form 0: idle <-> walk blend tree.
            var normal = controller.CreateBlendTreeInController("Normal", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Blend";
            tree.useAutomaticThresholds = false;
            AddIdleWithDance(tree, LoadClipFromModel(modelPath));
            tree.AddChild(LoadClip("walk"), 1f);
            SetSpeedParam(normal);
            sm.defaultState = normal;

            // Forms 1-2: injured_idle <-> walk blend trees.
            var injured = AddBlendState(controller, "Injured", LoadClip("injured_idle"), LoadClip("injured_walk"));
            var injuredHeavy = AddBlendState(controller, "InjuredHeavy", LoadClip("injured_idle"), LoadClip("limp_walk"));

            // Forms 3-4: single clip, paused when standing still.
            var forms = new List<AnimatorState> { normal, injured, injuredHeavy };
            forms.Add(AddClipState(sm, "CrawlAllFours", LoadClip("crawl_all_fors"), new Vector3(250, 280)));
            forms.Add(AddClipState(sm, "CrawlLow", LoadClip("crawl"), new Vector3(250, 360)));
            foreach (var s in forms.Skip(1)) SetSpeedParam(s);

            // Any form -> any other form when Form changes.
            for (int from = 0; from < forms.Count; from++)
            {
                for (int to = 0; to < forms.Count; to++)
                {
                    if (from == to) continue;
                    var t = forms[from].AddTransition(forms[to]);
                    t.hasExitTime = false;
                    t.duration = 0.3f;
                    t.AddCondition(AnimatorConditionMode.Equals, to, "Form");
                }
            }

            // Interact only from the standing forms (crawling has no interact animation),
            // played at double speed, then back to whatever form is current.
            var interact = sm.AddState("Interact", new Vector3(550, 0));
            interact.motion = LoadClip("interact");
            interact.speed = 2f;
            foreach (var s in forms.Take(StandingFormCount))
            {
                var t = s.AddTransition(interact);
                t.hasExitTime = false;
                t.duration = 0.15f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Interact");
            }
            for (int i = 0; i < forms.Count; i++)
            {
                var back = interact.AddTransition(forms[i]);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.2f;
                back.AddCondition(AnimatorConditionMode.Equals, i, "Form");
            }

            // Smoke (R): like interact, standing forms only, normal speed.
            var smoke = sm.AddState("Smoke", new Vector3(550, 100));
            smoke.motion = LoadClip("smoke");
            foreach (var s in forms.Take(StandingFormCount))
            {
                var t = s.AddTransition(smoke);
                t.hasExitTime = false;
                t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Smoke");
            }
            for (int i = 0; i < forms.Count; i++)
            {
                var back = smoke.AddTransition(forms[i]);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.25f;
                back.AddCondition(AnimatorConditionMode.Equals, i, "Form");
            }

            // Death from the standing forms, no way back.
            var death = sm.AddState("Death", new Vector3(550, 300));
            death.motion = LoadClip("death");
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.hasExitTime = false;
            toDeath.duration = 0.2f;
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDeath.AddCondition(AnimatorConditionMode.Less, StandingFormCount, "Form");

            // Death while crawling uses death_low.
            var deathLow = sm.AddState("DeathLow", new Vector3(550, 380));
            deathLow.motion = LoadClip("death_low");
            var toDeathLow = sm.AddAnyStateTransition(deathLow);
            toDeathLow.hasExitTime = false;
            toDeathLow.duration = 0.2f;
            toDeathLow.canTransitionToSelf = false;
            toDeathLow.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDeathLow.AddCondition(AnimatorConditionMode.Greater, StandingFormCount - 1, "Form");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip LoadClipFromModel(string modelPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        // Idle slot of a standing blend tree: idle <-> dancing, driven by "Dance" (near a speaker).
        private static void AddIdleWithDance(BlendTree tree, AnimationClip idle)
        {
            BlendTree idleTree = tree.CreateBlendTreeChild(0f);
            idleTree.name = "IdleOrDance";
            idleTree.blendType = BlendTreeType.Simple1D;
            idleTree.blendParameter = "Dance";
            idleTree.useAutomaticThresholds = false;
            idleTree.AddChild(idle, 0f);
            idleTree.AddChild(LoadClip("dancing"), 1f);
        }

        private static AnimatorState AddBlendState(AnimatorController controller, string name, AnimationClip idle, AnimationClip move)
        {
            var state = controller.CreateBlendTreeInController(name, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Blend";
            tree.useAutomaticThresholds = false;
            AddIdleWithDance(tree, idle);
            tree.AddChild(move, 1f);
            if (idle == null || move == null)
                Debug.LogWarning($"[PlayerAnimatorBuilder] Missing clip for state {name}.");
            return state;
        }

        private static AnimatorState AddClipState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 position)
        {
            var state = sm.AddState(name, position);
            state.motion = clip;
            if (clip == null)
                Debug.LogWarning($"[PlayerAnimatorBuilder] No clip found for state {name}.");
            return state;
        }

        private static void SetSpeedParam(AnimatorState state)
        {
            state.speedParameterActive = true;
            state.speedParameter = "MotionSpeed";
        }
    }
}
