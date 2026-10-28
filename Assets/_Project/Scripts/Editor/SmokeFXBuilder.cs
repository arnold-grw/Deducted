using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deducted.EditorTools
{
    /// <summary>
    /// Menu: Deducted > Build Smoke FX.
    /// Creates Assets/_Project/FX with a soft smoke texture, M_Smoke (URP particles, transparent) and two prefabs:
    /// - FX_SmokePuff: one short puff, not looping, plays when triggered (exhale, a "Effect" event).
    /// - FX_SmokeLoop: rises forever, plays on start (burning objects, chimneys, vents).
    /// White soft smoke: the 1-bit pass turns its transparency into dither. Safe to run again;
    /// existing prefabs are overwritten, so tweak copies (or instances) if you want to keep changes.
    /// </summary>
    public static class SmokeFXBuilder
    {
        private const string Parent = "Assets/_Project";
        private const string Folder = Parent + "/FX";
        private const string TexturePath = Folder + "/T_SmokeSoft.png";
        private const string MaterialPath = Folder + "/M_Smoke.mat";

        [MenuItem("Deducted/Build Smoke FX")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Parent, "FX");

            Texture2D texture = BuildTexture();
            Material material = BuildMaterial(texture);
            BuildPrefab("FX_SmokePuff", material, false);
            BuildPrefab("FX_SmokeLoop", material, true);

            AssetDatabase.SaveAssets();
            Debug.Log($"[SmokeFXBuilder] Built smoke FX in {Folder}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FX_SmokeLoop.prefab");
        }

        // Soft white blob, alpha fading to the edge.
        private static Texture2D BuildTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material BuildMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;

            // Transparent, alpha blended, no depth write.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(string name, Material material, bool loop)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = loop;
            main.duration = loop ? 5f : 0.8f;
            main.startLifetime = loop ? new ParticleSystem.MinMaxCurve(2.5f, 4f) : new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = loop ? new ParticleSystem.MinMaxCurve(0.3f, 0.6f) : new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            // Final size; Size Over Lifetime grows into it.
            main.startSize = loop ? new ParticleSystem.MinMaxCurve(0.6f, 1f) : new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = loop ? -0.05f : -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = loop ? 200 : 60;

            var emission = ps.emission;
            emission.rateOverTime = loop ? 10f : 30f;

            // Cone along local +Z for the puff (point the object where the smoke should go),
            // straight up for the loop.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = loop ? 15f : 12f;
            shape.radius = loop ? 0.15f : 0.01f;
            shape.rotation = loop ? new Vector3(-90f, 0f, 0f) : Vector3.zero;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(loop ? 0.3f : 0.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            if (!loop)
            {
                // The exhale shoots out and quickly slows down, then drifts up.
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = 2f;
            }

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, loop ? 0.3f : 0.15f), new Keyframe(1f, 1f)));

            float alpha = loop ? 0.5f : 0.6f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(0f, 1f) });
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = loop ? 0.2f : 0.12f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.2f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            PrefabUtility.SaveAsPrefabAsset(go, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(go);
        }
    }
}
