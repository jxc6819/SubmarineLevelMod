using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class SparkDriver : MonoBehaviour
    {
        public SparkDriver(IntPtr ptr) : base(ptr) { }
        public SparkDriver()
            : base(ClassInjector.DerivedConstructorPointer<SparkDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public int PoolSize = 80;

        public int BurstSparkCount = 40;

        public bool LoopEnabled = false;
        public float LoopIntervalMin = 3.0f;
        public float LoopIntervalMax = 5.0f;
        public int LoopSparkCount = 7;

        public Color SparkColor = new Color(0.85f, 0.97f, 3.0f, 1.0f);

        public float SparkSizeMin = 0.03f;
        public float SparkSizeMax = 0.07f;

        public float SparkLifeMin = 0.09f;
        public float SparkLifeMax = 0.17f;

        public float BurstSpeedMin = 5.0f;
        public float BurstSpeedMax = 9.0f;
        public float LoopSpeedMin = 2.0f;
        public float LoopSpeedMax = 4.0f;

        public float UpBias = 0.8f;
        public float Drag = 5.5f;
        public float JitterStrength = 10.0f;
        public float Gravity = -3.5f;

        public float SpawnRadius = 0.015f;

        struct Spark
        {
            public Transform tr;
            public Renderer r;
            public MaterialPropertyBlock mpb;

            public Vector3 vel;
            public float age;
            public float life;
            public float size;
            public float seed;
            public float intensity;
            public bool isBurst;

            public bool Active => age >= 0f && age < life;
        }

        private Spark[] _pool;
        private int _nextIndex;

        private Material _material;
        private Texture2D _texture;
        private Camera _camera;

        private float _nextLoopTime;

        void Awake()
        {
            _camera = Camera.main;

            var shader = Shader.Find("FX/FX_Additive_UVPan_Shader");
            _texture = BuildSparkTex(64, 256);

            _material = new Material(shader);
            if (_material.HasProperty("_MainTex"))
                _material.SetTexture("_MainTex", _texture);

            ConfigureAsAdditive(_material);
            SetAllColorProps(_material, SparkColor);

            if (_material.HasProperty("_PanSpeed")) _material.SetVector("_PanSpeed", Vector4.zero);
            if (_material.HasProperty("_TilingOffset")) _material.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            PoolSize = Mathf.Max(8, PoolSize);
            _pool = new Spark[PoolSize];

            for (int i = 0; i < PoolSize; i++)
                _pool[i] = CreateSparkQuad("Spark_" + i, _material);

            _nextLoopTime = Time.time + UnityEngine.Random.Range(LoopIntervalMin, LoopIntervalMax);
        }

        private Spark CreateSparkQuad(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(transform, false);

            UnityEngine.Object.Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            Spark s;
            s.tr = go.transform;
            s.r = renderer;
            s.mpb = new MaterialPropertyBlock();
            s.vel = Vector3.zero;
            s.age = -1f;
            s.life = 0.1f;
            s.size = 0.04f;
            s.seed = UnityEngine.Random.value * 1000f;
            s.intensity = 1f;
            s.isBurst = false;
            return s;
        }

        void Update()
        {
            if (LoopEnabled && Time.time >= _nextLoopTime)
            {
                SpawnLoopSparks();
                _nextLoopTime = Time.time + UnityEngine.Random.Range(LoopIntervalMin, LoopIntervalMax);
            }

            float dt = Time.deltaTime;
            float now = Time.time;

            for (int i = 0; i < _pool.Length; i++)
            {
                var spark = _pool[i];
                if (!spark.Active)
                {
                    _pool[i] = spark;
                    continue;
                }

                spark.age += dt;
                float t = Mathf.Clamp01(spark.age / spark.life);

                float n1 = Mathf.PerlinNoise(spark.seed, now * 40f) - 0.5f;
                float n2 = Mathf.PerlinNoise(spark.seed * 1.7f, now * 32f) - 0.5f;
                float n3 = Mathf.PerlinNoise(spark.seed * 2.3f, now * 28f) - 0.5f;
                var jitter = new Vector3(n1, n2, n3) * (JitterStrength * dt);

                spark.vel += jitter;
                spark.vel += new Vector3(0f, Gravity, 0f) * dt;
                spark.vel -= spark.vel * (Drag * dt);

                spark.tr.position += spark.vel * dt;

                Vector3 toCam = _camera.transform.position - spark.tr.position;
                var look = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                float roll = (Mathf.PerlinNoise(spark.seed * 3.1f, now * 10f) - 0.5f) * 90f;
                spark.tr.rotation = look * Quaternion.AngleAxis(roll, Vector3.forward);

                float alpha;
                if (spark.isBurst)
                {
                    float sizeMul = Mathf.Lerp(1.7f, 0.9f, t);
                    spark.tr.localScale = new Vector3(spark.size * sizeMul, spark.size * 4.0f * sizeMul, 1f);
                    alpha = (t < 0.2f) ? 1.0f : Mathf.Clamp01(1f - (t - 0.2f) / 0.8f);
                }
                else
                {
                    float sizeMul = Mathf.Lerp(1.0f, 0.35f, t * t);
                    spark.tr.localScale = new Vector3(spark.size * sizeMul, spark.size * 3.0f * sizeMul, 1f);
                    alpha = Mathf.Clamp01(1f - t);
                }

                float baseBrightness = spark.isBurst ? 10.0f : 5.0f;
                float brightness = baseBrightness * spark.intensity;

                Color c = SparkColor * brightness;
                c.a = alpha;

                spark.mpb.SetColor("_Color", c);
                spark.r.SetPropertyBlock(spark.mpb);

                if (t >= 1f)
                {
                    spark.age = -1f;
                    spark.r.enabled = false;
                }

                _pool[i] = spark;
            }
        }

        public void TriggerBurst()
        {
            int count = Mathf.Clamp(BurstSparkCount, 1, PoolSize);
            for (int i = 0; i < count; i++)
                SpawnSpark(true);
        }

        public void EnableLoop(bool on)
        {
            LoopEnabled = on;
            if (on)
                _nextLoopTime = Time.time + UnityEngine.Random.Range(LoopIntervalMin, LoopIntervalMax);
        }

        private void SpawnLoopSparks()
        {
            int count = Mathf.Clamp(LoopSparkCount, 1, 16);
            for (int i = 0; i < count; i++)
                SpawnSpark(false);
        }

        private void SpawnSpark(bool isBurst)
        {
            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % _pool.Length;

            var spark = _pool[index];

            spark.age = 0f;
            spark.life = UnityEngine.Random.Range(SparkLifeMin, SparkLifeMax);
            spark.size = UnityEngine.Random.Range(SparkSizeMin, SparkSizeMax);
            spark.isBurst = isBurst;

            if (isBurst)
            {
                spark.size *= 3.5f;
                spark.life *= 1.4f;
                spark.intensity = UnityEngine.Random.Range(4.0f, 7.0f);
            }
            else
            {
                spark.size *= 0.7f;
                spark.intensity = UnityEngine.Random.Range(1.0f, 1.8f);
            }

            spark.seed = UnityEngine.Random.value * 1000f;

            float radius = isBurst ? (SpawnRadius * 3.0f) : SpawnRadius;
            spark.tr.position = transform.position + (UnityEngine.Random.insideUnitSphere * radius);

            Vector3 dir = (UnityEngine.Random.onUnitSphere + Vector3.up * UpBias).normalized;

            float speedMin = isBurst ? BurstSpeedMin : LoopSpeedMin;
            float speedMax = isBurst ? BurstSpeedMax : LoopSpeedMax;
            float speed = UnityEngine.Random.Range(speedMin, speedMax);

            spark.vel = dir * speed;
            spark.r.enabled = true;

            _pool[index] = spark;
        }

        private Texture2D BuildSparkTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float vertical = Mathf.SmoothStep(0f, 0.08f, v) * (1f - Mathf.SmoothStep(0.78f, 1f, v));

                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float nx = (u * 2f) - 1f;

                    float core = Mathf.Exp(-20f * nx * nx);

                    float p1 = Mathf.PerlinNoise(u * 10.5f + 1.7f, v * 22.1f + 0.3f) * 0.7f;
                    float p2 = Mathf.PerlinNoise(u * 21.7f + 4.1f, v * 41.3f + 2.9f) * 0.5f;
                    float jag = 0.55f + p1 + p2;

                    float a = Mathf.Clamp01(core * jag * vertical);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        private static void ConfigureAsAdditive(Material m)
        {
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);

            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
        }

        private static void SetAllColorProps(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 20.0f);
        }
    }
}
