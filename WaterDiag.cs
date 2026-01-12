using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public static class WaterDiag
    {
        const string PhoenixTransparentShaderName = "Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01";

        static readonly Vector2 MainScrollSpeed = new Vector2(0.00f, 0.03f);
        static readonly Vector2 FoamScrollSpeed = new Vector2(-0.015f, 0.06f);

        static readonly List<WaterMaterial> _waterMaterials = new List<WaterMaterial>(8);
        static readonly List<ScrollingTexture> _scrollingTextures = new List<ScrollingTexture>(8);

        static Shader _phoenixTransparentShader;
        static bool _initialized;

        class WaterMaterial
        {
            public Material Material;
            public Shader OriginalShader;
            public int OriginalRenderQueue;
            public Color OriginalColor;
            public Texture OriginalMainTexture;
            public Texture OriginalFoamTexture;
        }

        class ScrollingTexture
        {
            public Material Material;
            public string TextureProperty;
            public Vector2 Speed;
            public Vector2 Offset;
        }

        public static void Tick(float deltaTime)
        {
            if (_scrollingTextures.Count == 0) return;

            for (int i = 0; i < _scrollingTextures.Count; i++)
            {
                var item = _scrollingTextures[i];
                if (!item.Material) continue;

                item.Offset += item.Speed * deltaTime;

                if (item.Offset.x > 100f || item.Offset.x < -100f) item.Offset.x = 0f;
                if (item.Offset.y > 100f || item.Offset.y < -100f) item.Offset.y = 0f;

                item.Material.SetTextureOffset(item.TextureProperty, item.Offset);
            }
        }

        public static void Dump()
        {
            Initialize();

            var sb = new StringBuilder();
            sb.AppendLine("=== WaterDiag Dump ===");

            for (int i = 0; i < _waterMaterials.Count; i++)
            {
                var w = _waterMaterials[i];
                if (!w.Material) continue;

                sb.AppendLine($"[Water] name='{w.Material.name}' shader='{w.Material.shader?.name}' queue={w.Material.renderQueue}");

                AppendTexture(sb, w.Material, "_MainTexture");
                AppendTexture(sb, w.Material, "_MainTex");
                AppendTexture(sb, w.Material, "_FoamTexture");
                AppendTexture(sb, w.Material, "_AlphaMap");

                AppendColor(sb, w.Material, "_WaterColor");
                AppendColor(sb, w.Material, "_Color");

                sb.AppendLine();
            }

            var path = Path.Combine(Application.persistentDataPath, "WaterDiag_FullDump.txt");
            File.WriteAllText(path, sb.ToString());
            MelonLogger.Msg($"[WaterDiag] Wrote material dump to: {path}");
        }

        public static void KeepOriginalAndFix()
        {
            Initialize();

            for (int i = 0; i < _waterMaterials.Count; i++)
            {
                var w = _waterMaterials[i];
                if (!w.Material) continue;

                w.Material.shader = w.OriginalShader;
                w.Material.renderQueue = 2000;

                if (w.Material.HasProperty("_WaterColor"))
                    w.Material.SetColor("_Color", w.Material.GetColor("_WaterColor"));
                else if (w.Material.HasProperty("_Color"))
                    w.Material.SetColor("_Color", w.Material.GetColor("_Color"));
            }

            MelonLogger.Msg("[WaterDiag] Kept original water shader(s) but fixed render state.");
        }

        public static void ApplyPhoenixTransparent()
        {
            Initialize();
            LoadShaders();

            if (_phoenixTransparentShader == null)
                return;

            _scrollingTextures.Clear();

            for (int i = 0; i < _waterMaterials.Count; i++)
            {
                var w = _waterMaterials[i];
                if (!w.Material) continue;

                var mat = w.Material;

                mat.shader = _phoenixTransparentShader;
                mat.renderQueue = 3000;

                Color color = ReadWaterColor(mat, new Color(0.05f, 0.35f, 0.60f, 0.65f));
                mat.SetColor("_Color", color);

                Texture mainTexture = GetTexture(mat, "_MainTexture");
                if (!mainTexture) mainTexture = GetTexture(mat, "_MainTex");

                Texture foamTexture = GetTexture(mat, "_FoamTexture");

                if (mainTexture) mat.SetTexture("_MainTex", mainTexture);
                if (foamTexture) mat.SetTexture("_AlphaMap", foamTexture);

                if (mainTexture)
                {
                    _scrollingTextures.Add(new ScrollingTexture
                    {
                        Material = mat,
                        TextureProperty = "_MainTex",
                        Speed = MainScrollSpeed,
                        Offset = mat.GetTextureOffset("_MainTex")
                    });
                }

                if (foamTexture)
                {
                    _scrollingTextures.Add(new ScrollingTexture
                    {
                        Material = mat,
                        TextureProperty = "_AlphaMap",
                        Speed = FoamScrollSpeed,
                        Offset = mat.GetTextureOffset("_AlphaMap")
                    });
                }
            }

            MelonLogger.Msg("[WaterDiag] Switched to Phoenix Transparent.");
        }

        public static void Revert()
        {
            Initialize();

            _scrollingTextures.Clear();

            for (int i = 0; i < _waterMaterials.Count; i++)
            {
                var w = _waterMaterials[i];
                if (!w.Material) continue;

                w.Material.shader = w.OriginalShader;
                w.Material.renderQueue = w.OriginalRenderQueue;

                if (w.OriginalMainTexture) w.Material.SetTexture("_MainTexture", w.OriginalMainTexture);
                if (w.OriginalMainTexture) w.Material.SetTexture("_MainTex", w.OriginalMainTexture);
                if (w.OriginalFoamTexture) w.Material.SetTexture("_FoamTexture", w.OriginalFoamTexture);

                w.Material.SetColor("_WaterColor", w.OriginalColor);
                w.Material.SetColor("_Color", w.OriginalColor);
            }

            MelonLogger.Msg("[WaterDiag] Reverted to original shader(s).");
        }

        static void LoadShaders()
        {
            if (_phoenixTransparentShader != null) return;
            _phoenixTransparentShader = Shader.Find(PhoenixTransparentShaderName);
        }

        static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _waterMaterials.Clear();

            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (!renderer) continue;

                var materials = renderer.sharedMaterials;
                if (materials == null) continue;

                for (int m = 0; m < materials.Length; m++)
                {
                    var mat = materials[m];
                    if (!mat || mat.shader == null) continue;

                    string shaderName = mat.shader.name ?? "";
                    string materialName = mat.name ?? "";

                    bool looksLikeWater =
                        shaderName.Contains("Water") ||
                        materialName.Contains("Ocean") ||
                        mat.HasProperty("_WaterColor") ||
                        mat.HasProperty("_FoamTexture");

                    if (!looksLikeWater) continue;

                    _waterMaterials.Add(new WaterMaterial
                    {
                        Material = mat,
                        OriginalShader = mat.shader,
                        OriginalRenderQueue = mat.renderQueue,
                        OriginalColor = ReadWaterColor(mat, new Color(0.05f, 0.35f, 0.60f, 0.65f)),
                        OriginalMainTexture = GetTexture(mat, "_MainTexture") ? GetTexture(mat, "_MainTexture") : GetTexture(mat, "_MainTex"),
                        OriginalFoamTexture = GetTexture(mat, "_FoamTexture")
                    });
                }
            }
        }

        static void AppendTexture(StringBuilder sb, Material mat, string property)
        {
            if (!mat.HasProperty(property)) return;
            Texture tex = mat.GetTexture(property);
            sb.AppendLine($"    {property} = {(tex ? tex.name : "(null)")}");
        }

        static void AppendColor(StringBuilder sb, Material mat, string property)
        {
            if (!mat.HasProperty(property)) return;
            sb.AppendLine($"    {property} = {mat.GetColor(property)}");
        }

        static Texture GetTexture(Material mat, string property)
        {
            if (!mat.HasProperty(property)) return null;
            return mat.GetTexture(property);
        }

        static Color ReadWaterColor(Material mat, Color fallback)
        {
            if (mat.HasProperty("_WaterColor")) return mat.GetColor("_WaterColor");
            if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
            return fallback;
        }
    }
}
