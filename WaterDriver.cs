using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class WaterDriver : MonoBehaviour
    {
        public WaterDriver(IntPtr ptr) : base(ptr) { }
        public WaterDriver() : base(ClassInjector.DerivedConstructorPointer<WaterDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        Renderer _r;
        Material _baseMat;
        Material _swirlBig;
        Material _swirlFine;

        void Awake()
        {
            _r = GetComponent<Renderer>();
            if (!_r) return;

            var pbr = Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Transparent_01");
            if (!pbr) pbr = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            _baseMat = new Material(pbr);
            SetAllColorProps(_baseMat, new Color(0.38f, 0.85f, 1.00f, 0.65f));
            SafeSetFloat(_baseMat, "_ReceiveShadows", 0f);
            _baseMat.renderQueue = 3000;

            var add = Shader.Find("FX/FX_Additive_UVPan_Shader");

            _swirlBig = new Material(add);
            SetAllColorProps(_swirlBig, new Color(0.20f, 0.90f, 1.80f, 1f));
            _swirlBig.SetVector("_PanSpeed", new Vector4(0.18f, 0.05f, 0f, 0f));
            _swirlBig.SetVector("_TilingOffset", new Vector4(0.7f, 0.7f, 0f, 0f));
            var noiseBig = BuildNoiseTex(256, 256, 1.6f, 4.0f);
            _swirlBig.SetTexture("_MainTex", noiseBig);
            SafeSetTex(_swirlBig, "_MaskTex", noiseBig);
            ForceDepthIfSupported(_swirlBig);
            _swirlBig.renderQueue = 3001;

            _swirlFine = new Material(add);
            SetAllColorProps(_swirlFine, new Color(0.12f, 0.65f, 1.35f, 1f)); //cyan
            _swirlFine.SetVector("_PanSpeed", new Vector4(-0.06f, 0.22f, 0f, 0f));
            _swirlFine.SetVector("_TilingOffset", new Vector4(3.5f, 3.5f, 0f, 0f));
            var noiseFine = BuildNoiseTex(256, 256, 3.8f, 10.5f);
            _swirlFine.SetTexture("_MainTex", noiseFine);
            SafeSetTex(_swirlFine, "_MaskTex", noiseFine);
            ForceDepthIfSupported(_swirlFine);
            _swirlFine.renderQueue = 3002;

            _r.materials = new[] { _baseMat, _swirlBig, _swirlFine };
        }

        static void SetAllColorProps(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);

            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", c * 1.0f); 
        }

        static void ForceDepthIfSupported(Material m)
        {
            if (m.HasProperty("_ZTest"))
                m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }


        static void ForceDepth(Material m)
        {
            SafeSetInt(m, "_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            SafeSetInt(m, "_ZWrite", 0); 
        }

        static void SafeSetInt(Material m, string name, int v)
        {
            if (m.HasProperty(name)) m.SetInt(name, v);
        }


        void Update()
        {
            float t = Time.time;
            float pulse = 0.18f + 0.10f * Mathf.Sin(t * 1.3f); 
            if (_swirlBig) _swirlBig.SetColor("_Color", new Color(0.70f + pulse, 1.20f + pulse, 1.80f + pulse, 1f));
            if (_swirlFine) _swirlFine.SetColor("_Color", new Color(0.45f + pulse * 0.6f, 0.95f + pulse * 0.6f, 1.35f + pulse * 0.6f, 1f));
        }

        static void SafeSetFloat(Material m, string name, float v)
        {
            if (m.HasProperty(name)) m.SetFloat(name, v);
        }
        static void SafeSetTex(Material m, string name, Texture t)
        {
            if (m.HasProperty(name)) m.SetTexture(name, t);
        }

        static Texture2D BuildNoiseTex(int w, int h, float s1, float s2)
        {
            var tex = new Texture2D(w, h, TextureFormat.R8, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w, ny = (float)y / h;
                    float n = 0.55f * Mathf.PerlinNoise(nx * s1, ny * s1)
                            + 0.45f * Mathf.PerlinNoise(nx * s2 + 7.3f, ny * s2 + 2.1f);
                    n = Mathf.Clamp01((n - 0.35f) * 2.2f);
                    byte v = (byte)(n * 255f);
                    px[y * w + x] = new Color32(v, v, v, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }
    }
}
