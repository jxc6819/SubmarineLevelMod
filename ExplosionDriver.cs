using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class ExplosionDriver : MonoBehaviour
    {
        public ExplosionDriver(IntPtr p) : base(p) { }
        public ExplosionDriver() : base(ClassInjector.DerivedConstructorPointer<ExplosionDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Vector3 localOffset = Vector3.zero;

        public int burstCount = 60;

        public float lifeMin = 0.35f;
        public float lifeMax = 0.85f;

        public float speedMin = 3.0f;
        public float speedMax = 7.0f;

        public float drag = 3.5f;
        public float upwardBias = 0.4f;

        public float sizeMin = 0.12f;
        public float sizeMax = 0.28f;

        public float turbulence = 0.55f;
        public float turbulenceFrequency = 2.4f;

        public Color startColor = new Color(1.2f, 1.05f, 0.85f, 1.0f);
        public Color endColor = new Color(0.7f, 0.25f, 0.05f, 0.0f);

        private struct Puff
        {
            public Transform transform;
            public Renderer renderer;
            public Vector3 velocity;
            public float age;
            public float life;
            public float baseSize;
            public float seed;
            public MaterialPropertyBlock mpb;
        }

        private const int MaxPool = 96;

        private Puff[] puffs;
        private int nextIndex;
        private Material material;
        private Texture2D texture;
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;

            Shader shader = Shader.Find("Unlit/Transparent");
            texture = BuildRadialTexture(64);

            material = new Material(shader);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            SetAllColorProps(material, startColor);
            if (material.HasProperty("_PanSpeed")) material.SetVector("_PanSpeed", Vector4.zero);
            if (material.HasProperty("_TilingOffset")) material.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            material.renderQueue = 3100;

            puffs = new Puff[MaxPool];
            for (int i = 0; i < puffs.Length; i++)
                puffs[i] = CreatePuff("ExplosionPuff_" + i);
        }

        private Puff CreatePuff(string name)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(transform, false);

            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            Puff puff;
            puff.transform = go.transform;
            puff.renderer = renderer;
            puff.velocity = Vector3.zero;
            puff.age = -1f;
            puff.life = 1f;
            puff.baseSize = 0.1f;
            puff.seed = UnityEngine.Random.value * 1000f;
            puff.mpb = new MaterialPropertyBlock();
            return puff;
        }

        private void OnEnable()
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                Puff puff = puffs[i];
                puff.age = -1f;
                puff.renderer.enabled = false;
                puffs[i] = puff;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < puffs.Length; i++)
            {
                Puff puff = puffs[i];
                if (puff.age < 0f)
                {
                    puffs[i] = puff;
                    continue;
                }

                puff.age += dt;
                float t = puff.age / puff.life;

                float n1 = Mathf.PerlinNoise(puff.seed, Time.time * turbulenceFrequency) - 0.5f;
                float n2 = Mathf.PerlinNoise(puff.seed * 1.37f, Time.time * (turbulenceFrequency * 1.4f)) - 0.5f;

                Vector3 noise =
                    transform.right * n1 +
                    transform.forward * n2 +
                    Vector3.up * (n1 * 0.25f);

                puff.velocity -= puff.velocity * (drag * dt);
                puff.velocity += noise * (turbulence * dt);

                puff.transform.position += puff.velocity * dt;

                Vector3 toCamera = (mainCamera.transform.position - puff.transform.position).normalized;
                puff.transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);

                float size = Mathf.Lerp(puff.baseSize, puff.baseSize * 2.2f, t);
                puff.transform.localScale = new Vector3(size, size, 1f);

                Color color = Color.Lerp(startColor, endColor, t);
                color.a *= Mathf.Clamp01(1f - t);

                puff.mpb.SetColor("_Color", color);
                puff.renderer.SetPropertyBlock(puff.mpb);

                if (t >= 1f)
                {
                    puff.age = -1f;
                    puff.renderer.enabled = false;
                }

                puffs[i] = puff;
            }
        }

        public void TriggerExplosion()
        {
            AudioUtil.PlayAt("vault_explosion_02", transform.position, 10f);

            int count = burstCount;
            if (count > puffs.Length) count = puffs.Length;

            Vector3 origin = transform.TransformPoint(localOffset);

            for (int i = 0; i < count; i++)
                SpawnPuff(origin);
        }

        private void SpawnPuff(Vector3 origin)
        {
            int index = nextIndex;
            nextIndex = (nextIndex + 1) % puffs.Length;

            Puff puff = puffs[index];

            puff.age = 0f;
            puff.life = Mathf.Lerp(lifeMin, lifeMax, UnityEngine.Random.value);
            puff.baseSize = Mathf.Lerp(sizeMin, sizeMax, UnityEngine.Random.value);
            puff.seed = UnityEngine.Random.value * 1000f;

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * (sizeMax * 0.15f);
            puff.transform.position =
                origin +
                transform.right * jitter.x +
                transform.forward * jitter.y;

            Vector3 dir = UnityEngine.Random.onUnitSphere + Vector3.up * upwardBias;
            dir.Normalize();

            float speed = Mathf.Lerp(speedMin, speedMax, UnityEngine.Random.value);
            puff.velocity = dir * speed;

            puff.renderer.enabled = true;

            puffs[index] = puff;
        }

        private Texture2D BuildRadialTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.2f, 1.0f, r));
                    a = Mathf.Pow(a, 1.3f);

                    byte alpha = (byte)(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            t.SetPixels32(pixels);
            t.Apply(true, false);
            return t;
        }

        private static void SetAllColorProps(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.7f);
        }
    }
}
