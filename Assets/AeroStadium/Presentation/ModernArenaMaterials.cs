using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Authored PBR palette for the rebuilt Elite Four arena. Detail is evaluated
    /// in metres by ModernArenaSurface; meshes do not need source atlas UVs.
    /// The caller owns this palette and disposes it with the generated arena.
    /// </summary>
    public sealed class ModernArenaMaterials : IDisposable
    {
        public const string ShaderName = "AeroStadium/Modern Arena Surface";
        public const string ShaderReferencePath = "Materials/ModernArenaShaderReference";

        readonly List<Material> owned = new List<Material>(10);
        readonly IReadOnlyList<Material> ownedView;
        bool disposed;

        public Material Floor { get; }
        public Material FloorAccent { get; }
        public Material Stone { get; }
        public Material DarkStone { get; }
        public Material Gold { get; }
        public Material Trim { get; }
        public Material EmissionWarm { get; }
        public Material EmissionCool { get; }
        public Material Glass { get; }
        public Material Water { get; }
        public IReadOnlyList<Material> OwnedMaterials => ownedView;

        public Material Lava { get; private set; }
        public ModernArenaMaterials()
        {
            ownedView = owned.AsReadOnly();
            // This serialized Resources material keeps the custom shader in a
            // player build. Shader.Find is also useful during first editor import.
            var reference = Resources.Load<Material>(ShaderReferencePath);
            Shader shader = reference != null ? reference.shader : Shader.Find(ShaderName);
            if (shader == null)
                throw new InvalidOperationException("Missing " + ShaderName +
                    ". Prepare the modern arena shader reference before building.");

            Lava = Create(shader, "Elite4 Modern / Turbulent liquid lava", Rgb(.80f,.095f,.007f),
                Rgb(1f,.34f,.025f), 5, .02f, .91f, .38f, .035f, .055f);
            Lava.SetFloat("_LavaFlowSpeed", .46f);
            Lava.SetFloat("_LavaWaveHeight", .34f);
            Lava.SetShaderPassEnabled("ShadowCaster", false);
            Floor = Create(shader, "Elite4 Modern / Warm Arena Stone", Rgb(.34f, .32f, .255f),
                Rgb(.235f, .22f, .178f), 2, 0, .43f, .62f, .065f, .014f);
            Floor.SetFloat("_PanelJoints", .9f);
            Floor.SetFloat("_TileSize", 1.35f);
            Floor.SetFloat("_JointWidth", .027f);
            Floor.SetFloat("_TileVariation", .055f);
            Floor.SetColor("_JointColor", Rgb(.15f, .135f, .10f));

            FloorAccent = Create(shader, "Elite4 Modern / Warm Arena Stone Accent", Rgb(.385f, .357f, .282f),
                Rgb(.275f, .247f, .187f), 2, 0, .40f, .62f, .065f, .014f);
            FloorAccent.SetFloat("_PanelJoints", .9f);
            FloorAccent.SetFloat("_TileSize", 1.35f);
            FloorAccent.SetFloat("_JointWidth", .027f);
            FloorAccent.SetFloat("_TileVariation", .055f);
            FloorAccent.SetColor("_JointColor", Rgb(.15f, .135f, .10f));

            Stone = Create(shader, "Elite4 Modern / Golden Limestone", Rgb(.80f, .715f, .505f),
                Rgb(.56f, .458f, .293f), 1, 0, .54f, .39f, .055f, .009f);
            Stone.SetFloat("_VeinStrength", .24f);
            Stone.SetFloat("_VeinWidth", .038f);
            Stone.SetFloat("_RoughnessVariation", .065f);

            DarkStone = Create(shader, "Elite4 Modern / Umber Foundation", Rgb(.215f, .171f, .123f),
                Rgb(.13f, .097f, .063f), 2, 0, .35f, .46f, .085f, .018f);
            DarkStone.SetFloat("_RoughnessVariation", .09f);

            Gold = Create(shader, "Elite4 Modern / Brushed Champagne Bronze", Rgb(.86f, .66f, .30f),
                Rgb(.66f, .405f, .13f), 3, .94f, .66f, .55f, .035f, .0008f);
            Gold.SetFloat("_GrainScale", 112);
            Gold.SetFloat("_RoughnessVariation", .065f);
            Gold.SetVector("_BrushDirection", new Vector4(.68f, .28f, .68f, 0));

            Trim = Create(shader, "Elite4 Modern / Satin Architectural Bronze", Rgb(.57f, .345f, .16f),
                Rgb(.385f, .205f, .075f), 3, .86f, .49f, .5f, .05f, .001f);
            Trim.SetFloat("_GrainScale", 96);
            Trim.SetFloat("_RoughnessVariation", .075f);

            EmissionWarm = Create(shader, "Elite4 Modern / Amber Lantern", Rgb(.98f, .67f, .29f),
                Color.black, 0, .08f, .4f, 1, 0, 0);
            EmissionWarm.SetColor("_EmissionColor", Rgb(1, .55f, .155f) * 2.15f);
            EmissionWarm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EmissionCool = Create(shader, "Elite4 Modern / Sky Accent", Rgb(.62f, .75f, .78f),
                Color.black, 0, .05f, .5f, 1, 0, 0);
            EmissionCool.SetColor("_EmissionColor", Rgb(.62f, .78f, .84f) * .9f);
            EmissionCool.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            Glass = Create(shader, "Elite4 Modern / Warm Clear Glass", Rgb(.86f, .87f, .785f, .16f),
                Color.black, 0, 0, .94f, 1, 0, 0);
            Glass.SetFloat("_OpacityFresnel", .68f);
            Glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            Glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            Glass.SetFloat("_ZWrite", 0);
            Glass.SetOverrideTag("RenderType", "Transparent");
            Glass.renderQueue = (int)RenderQueue.Transparent;
            Glass.SetShaderPassEnabled("ShadowCaster", false);
            Glass.SetShaderPassEnabled("DepthOnly", false);
            Glass.SetShaderPassEnabled("DepthNormals", false);

            Water = Create(shader, "Elite4 Modern / Still Reflecting Water", Rgb(.13f, .23f, .22f),
                Rgb(.23f, .33f, .29f), 4, 0, .92f, .48f, .045f, .021f);
            Water.SetFloat("_RoughnessVariation", .022f);
        }

        Material Create(Shader shader, string name, Color color, Color accent,
            int pattern, float metallic, float smoothness, float scale, float detail, float relief)
        {
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetColor("_AccentColor", accent);
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_SurfacePattern", pattern);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_PatternScale", scale);
            material.SetFloat("_DetailStrength", detail);
            material.SetFloat("_Relief", relief);
            material.SetFloat("_GrainScale", 36);
            material.SetFloat("_RoughnessVariation", .045f);
            material.SetFloat("_VeinStrength", .14f);
            material.SetFloat("_VeinWidth", .045f);
            material.SetFloat("_PanelJoints", 0);
            material.SetFloat("_TileSize", 1.35f);
            material.SetFloat("_JointWidth", .027f);
            material.SetFloat("_TileVariation", .035f);
            material.SetFloat("_OpacityFresnel", 0);
            material.SetVector("_BrushDirection", new Vector4(1, .12f, .15f, 0));
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.SetFloat("_Cull", (float)CullMode.Back);
            owned.Add(material);
            return material;
        }

        // ShaderLab Color properties accept display RGB. Unity converts them to
        // linear values for this project's linear rendering pipeline.
        static Color Rgb(float r, float g, float b, float alpha = 1)
        {
            return new Color(r, g, b, alpha);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var material in owned)
            {
                if (material == null) continue;
#if UNITY_EDITOR
                // The editor preparation step may persist these same objects as
                // material assets. Such assets belong to the project thereafter.
                if (UnityEditor.EditorUtility.IsPersistent(material)) continue;
#endif
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            owned.Clear();
        }
    }
}

