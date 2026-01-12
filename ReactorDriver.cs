using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class ReactorDriver : MonoBehaviour
    {
        public ReactorDriver(IntPtr ptr) : base(ptr) { }
        public ReactorDriver() : base(ClassInjector.DerivedConstructorPointer<ReactorDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public float breatheHz = 0.28f;
        public float breatheAmp = 0.32f;
        public float perlinHz = 3.1f;
        public float sparkleHz = 9.0f;
        public float sparkleAmp = 0.16f;

        public float shellJitterAmp = 0.020f;
        public float shellJitterHz = 2.3f;
        public float coreJitterAmp = 0.030f;
        public float coreJitterHz = 3.7f;

        public float baseLightIntensity = 1.2f;
        public float baseLightRange = 0.70f;
        public Vector3 localLightPos = new Vector3(0f, 0f, 0.02f);

        private Renderer reactorRenderer;
        private Material glowMat;
        private Material shellMat;
        private Material coreMat;
        private Light reactorLight;
        private float seed;

        private static readonly float Tau = (float)(Math.PI * 2.0);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int TilingOffsetId = Shader.PropertyToID("_TilingOffset");
        private static readonly int PanSpeedId = Shader.PropertyToID("_PanSpeed");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        private Vector2 shellOffset;
        private Vector2 coreOffset;
        private Vector2 shellPanPerSec;
        private Vector2 corePanPerSec;

        private void Awake()
        {
            reactorRenderer = GetComponent<Renderer>();
            if (reactorRenderer == null) return;

            seed = Hash01(transform.position);

            Shader pbr = Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Transparent_01");
            if (pbr == null) pbr = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            glowMat = new Material(pbr);
            glowMat.SetColor(ColorId, new Color(0.95f, 0.45f, 0.10f, 0.35f));
            glowMat.SetFloat(MetallicId, 0f);
            glowMat.SetFloat(GlossinessId, 0.1f);
            SetEmissive(glowMat, new Color(2.2f, 1.0f, 0.25f, 0f), 1f);
            glowMat.renderQueue = 3000;

            Shader additive = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (additive == null) additive = Shader.Find("FX/FX_Alpha_UVPan_Shader");

            shellMat = new Material(additive);
            shellMat.SetColor(ColorId, Color.white);
            shellMat.SetTexture(
                MainTexId,
                BuildFlameColorTex(
                    384, 1024, seed + 0.17f,
                    coreTightness: 0.35f, detail: 2.4f, verticalPower: 1.6f, contrast: 1.8f, hueBias: -0.04f
                )
            );
            shellMat.SetVector(TilingOffsetId, new Vector4(1f, 1f, 0f, 0f));
            shellMat.SetVector(PanSpeedId, new Vector4(0.03f + 0.01f * seed, 0.55f + 0.10f * seed, 0f, 0f));
            shellMat.renderQueue = 3001;

            coreMat = new Material(additive);
            coreMat.SetColor(ColorId, Color.white);
            coreMat.SetTexture(
                MainTexId,
                BuildFlameColorTex(
                    256, 1024, seed + 1.31f,
                    coreTightness: 0.55f, detail: 4.0f, verticalPower: 1.2f, contrast: 2.2f, hueBias: +0.06f
                )
            );
            coreMat.SetVector(TilingOffsetId, new Vector4(1f, 1f, 0f, 0f));
            coreMat.SetVector(PanSpeedId, new Vector4(-0.05f, 0.95f + 0.20f * seed, 0f, 0f));
            coreMat.renderQueue = 3002;

            reactorRenderer.materials = new[] { glowMat, shellMat, coreMat };

            Vector4 shellPan = shellMat.GetVector(PanSpeedId);
            Vector4 corePan = coreMat.GetVector(PanSpeedId);
            shellPanPerSec = new Vector2(shellPan.x, shellPan.y);
            corePanPerSec = new Vector2(corePan.x, corePan.y);
            shellOffset = Vector2.zero;
            coreOffset = Vector2.zero;

            var lightObj = new GameObject("ReactorLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = localLightPos;

            reactorLight = lightObj.AddComponent<Light>();
            reactorLight.type = LightType.Point;
            reactorLight.intensity = baseLightIntensity;
            reactorLight.range = baseLightRange;
            reactorLight.shadows = LightShadows.None;
            reactorLight.color = new Color(1.0f, 0.55f, 0.15f);
        }

        private void Update()
        {
            if (glowMat == null) return;

            float time = Time.time;
            float deltaTime = Time.deltaTime;

            float perlin = Mathf.PerlinNoise(seed * 13.2f, time * perlinHz);
            float breathe = 1f + breatheAmp * Mathf.Sin((time + seed * 2.13f) * breatheHz * Tau);
            float sparkle = 1f + sparkleAmp * Mathf.Sin(time * sparkleHz + seed * 5.0f);
            float flicker = Mathf.Clamp(0.70f + 0.60f * perlin, 0.6f, 1.9f);
            float alive = Mathf.Clamp(breathe * sparkle * flicker, 0.55f, 2.4f);

            SetEmissive(glowMat, new Color(2.2f, 1.0f, 0.25f, 0f), 2.3f * alive);

            shellMat.SetColor(ColorId, Color.white * (0.9f + 0.9f * alive));
            coreMat.SetColor(ColorId, Color.white * (1.1f + 1.2f * alive));

            ApplyUV(shellMat, ref shellOffset, shellPanPerSec, time, 0.7f + seed, shellJitterAmp, shellJitterHz, deltaTime);
            ApplyUV(coreMat, ref coreOffset, corePanPerSec, time, 1.9f + seed, coreJitterAmp, coreJitterHz, deltaTime);

            reactorLight.intensity = baseLightIntensity + 2.0f * alive;
            reactorLight.range = baseLightRange + 0.20f * alive;
        }

        private static void ApplyUV(Material material, ref Vector2 offset, Vector2 panPerSec, float time, float seed, float jitterAmp, float jitterHz, float deltaTime)
        {
            offset += panPerSec * deltaTime;

            float jitterX = jitterAmp * Mathf.Sin((time + seed) * jitterHz);
            float jitterY = jitterAmp * Mathf.Cos((time * 0.8f + seed * 1.7f) * jitterHz);

            Vector4 st = material.GetVector(TilingOffsetId);
            st.z = offset.x + jitterX;
            st.w = offset.y + jitterY;
            material.SetVector(TilingOffsetId, st);

            material.SetTextureOffset(MainTexId, offset + new Vector2(jitterX, jitterY));
            material.SetTextureOffset(BaseMapId, offset + new Vector2(jitterX, jitterY));
        }

        private static void SetEmissive(Material material, Color baseColor, float intensity)
        {
            material.EnableKeyword("_EMISSION");

            Color hdr = baseColor * intensity;
            material.SetColor(EmissionColorId, hdr);
            material.SetColor("_EmissiveColor", hdr);
            material.SetColor("_EmissiveColorLDR", hdr);

            material.SetFloat("_EmissionStrength", intensity);
            material.SetFloat("_EmissiveIntensity", intensity);
            material.SetFloat("_EmissionIntensity", intensity);
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionEnabled", 1f);
        }

        private static float Hash01(Vector3 v)
        {
            unchecked
            {
                uint h = (uint)(v.x * 73856093) ^ (uint)(v.y * 19349663) ^ (uint)(v.z * 83492791);
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static Texture2D BuildFlameColorTex(int w, int h, float seed, float coreTightness, float detail, float verticalPower, float contrast, float hueBias)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };

            var pixels = new Color32[w * h];
            float sx = 37.1f * seed, sy = 23.7f * (seed + 1.234f);

            for (int y = 0; y < h; y++)
            {
                float vy = y / (float)(h - 1);
                float heat = Mathf.Pow(1f - vy, verticalPower);

                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)(w - 1);

                    float cx = Mathf.Abs(nx - 0.5f) * 2f;
                    float core = Mathf.Clamp01(1f - Mathf.Pow(cx, 1.0f / Mathf.Max(0.001f, coreTightness)));

                    float n =
                        0.60f * Mathf.PerlinNoise((nx + sx) * 1.2f, (vy + sy) * 1.2f) +
                        0.30f * Mathf.PerlinNoise((nx * 2.0f + sx * 1.7f), (vy * 2.0f + sy * 0.7f)) +
                        0.10f * Mathf.PerlinNoise((nx * detail + sx * 2.9f), (vy * detail + sy * 1.9f));

                    float v = Mathf.Clamp01((core * heat * n - 0.25f) * contrast);

                    Color cool = new Color(0.85f, 0.10f, 0.02f);
                    Color mid = new Color(1.00f, 0.50f, 0.08f);
                    Color hot = new Color(1.00f, 0.93f, 0.75f);

                    float tMid = Mathf.SmoothStep(0.15f, 0.85f, v);
                    float tHot = Mathf.SmoothStep(0.60f, 1.00f, v);

                    Color c = Color.Lerp(cool, mid, tMid);
                    c = Color.Lerp(c, hot, tHot);

                    c.g *= (1.0f + hueBias);
                    c.b *= (1.0f - 0.5f * hueBias);
                    c *= (0.9f + 1.6f * v);

                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
                    pixels[y * w + x] = new Color32(r, g, b, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }
    }
}
