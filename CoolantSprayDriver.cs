using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class CoolantSprayDriver : MonoBehaviour
    {
        public CoolantSprayDriver(IntPtr ptr) : base(ptr) { }
        public CoolantSprayDriver() : base(ClassInjector.DerivedConstructorPointer<CoolantSprayDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public float coneAngleDegrees = 12f;
        public float particlesPerSecond = 600f;
        public float spraySpeed = 6.5f;
        public float particleSize = 0.035f;
        public float particleLifetime = 0.75f;
        public Color sprayTint = new Color(0.38f, 0.85f, 1.00f, 0.85f);

        private ParticleSystem particleSystem;
        private ParticleSystemRenderer particleRenderer;
        private Material sprayMaterial;

        private void Awake()
        {
            particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem == null) particleSystem = gameObject.AddComponent<ParticleSystem>();

            particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null) particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();

            var main = particleSystem.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = spraySpeed;
            main.startSize = particleSize;
            main.startLifetime = particleLifetime;
            main.maxParticles = 4000;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = particlesPerSecond;
            emission.rateOverDistance = 0f;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngleDegrees;
            shape.radius = 0.01f;
            shape.arc = 360f;
            shape.length = 0f;

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortMode = ParticleSystemSortMode.Distance;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
            particleRenderer.receiveShadows = false;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Shader shader = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (shader == null) shader = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            sprayMaterial = new Material(shader);
            ApplyTint(sprayMaterial, sprayTint * 1.2f);
            sprayMaterial.SetVector("_PanSpeed", Vector4.zero);
            sprayMaterial.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            sprayMaterial.SetTexture("_MainTex", CreateSoftDiskTexture(64));
            sprayMaterial.renderQueue = 3001;
            ForceDepthTestIfPresent(sprayMaterial);

            particleRenderer.material = sprayMaterial;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            var gradient = new Gradient();

            Color startColor = sprayTint;
            startColor.a = sprayTint.a;

            Color endColor = sprayTint;
            endColor.a = 0f;

            var colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey { color = startColor, time = 0f };
            colorKeys[1] = new GradientColorKey { color = endColor, time = 1f };

            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey { alpha = startColor.a, time = 0f };
            alphaKeys[1] = new GradientAlphaKey { alpha = 0f, time = 1f };

            gradient.SetKeys(colorKeys, alphaKeys);
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnEnable()
        {
            particleSystem.Play();
        }

        private void OnDisable()
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void ApplyTint(Material material, Color color)
        {
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 0.35f);
        }

        private static void ForceDepthTestIfPresent(Material material)
        {
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }

        private static Texture2D CreateSoftDiskTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.R8, true, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];

            float radius = (size - 2) * 0.5f;
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;

                    float distance01 = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    float alpha01 = Mathf.Clamp01(1f - distance01);
                    alpha01 = Mathf.Pow(alpha01, 1.6f);

                    byte value = (byte)(alpha01 * 255f);
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }
    }
}
