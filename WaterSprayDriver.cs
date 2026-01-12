using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class WaterSprayDriver : MonoBehaviour
    {
        public WaterSprayDriver(IntPtr p) : base(p) { }
        public WaterSprayDriver() : base(ClassInjector.DerivedConstructorPointer<WaterSprayDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public bool emitting = false;
        public float Thickness = 2.0f;

        public int StreakPool = 180;
        public float StreakRate = 320f;
        public float StreakSpeed = 13.0f;
        public float StreakSpread = 0.12f;
        public float StreakLifeMin = 0.55f;
        public float StreakLifeMax = 0.90f;
        public float StreakWidthMin = 0.038f;
        public float StreakWidthMax = 0.055f;
        public float StreakLenMin = 0.28f;
        public float StreakLenMax = 0.42f;

        public float ConeOutward = 8.0f;
        public float Drag = 1.65f;
        public float SpeedToLength = 0.035f;
        public float BreakUpAtMin = 0.35f;
        public float BreakUpAtMax = 0.60f;

        public int MistPool = 90;
        public float MistRate = 140f;
        public float MistSpeed = 4.0f;
        public float MistSpread = 0.45f;
        public float MistLifeMin = 0.16f;
        public float MistLifeMax = 0.28f;
        public float MistSizeMin = 0.030f;
        public float MistSizeMax = 0.055f;

        public float Gravity = 7.5f;
        public float Turbulence = 1.6f;
        public Color CoolantColor = new Color(0.50f, 0.88f, 1.15f, 0.92f);

        const float MouthRadiusBase = 0.012f; 
        float _mouthRadius, _rateStreak, _rateMist, _widthMul;
        int _streakPoolN, _mistPoolN;

        struct Quad
        {
            public Transform tr;
            public Renderer r;
            public Vector3 vel, latDir;
            public float age, life, seed, w, h, breakT;
            public MaterialPropertyBlock mpb;
            public bool Active => age >= 0f && age < life;
        }

        Quad[] _streaks, _mist;
        int _si, _mi;
        float _accStreak, _accMist;

        Material _streakMat, _mistMat;
        Texture2D _streakTex, _mistTex;
        Camera _cam;
        Light _L;

        void Awake()
        {
            _cam = Camera.main;
            float rMul = Mathf.Max(0.2f, Thickness);
            float flowMul = rMul * rMul;
            _mouthRadius = MouthRadiusBase * rMul;
            _widthMul = rMul;
            _rateStreak = StreakRate * flowMul;
            _rateMist = MistRate * flowMul;

            _streakPoolN = Mathf.RoundToInt(StreakPool * Mathf.Clamp(flowMul, 1f, 3f));
            _mistPoolN = Mathf.RoundToInt(MistPool * Mathf.Clamp(rMul, 1f, 2f));

            var alphaSh = Shader.Find("FX/FX_Alpha_UVPan_Shader");
            if (!alphaSh) alphaSh = Shader.Find("Unlit/Transparent");
            var addSh = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (!addSh) addSh = Shader.Find("Particles/Additive");

            _streakTex = BuildStreakTex(256, 512);
            _mistTex = BuildRadialTex(64);

            _streakMat = new Material(alphaSh);
            _streakMat.SetTexture("_MainTex", _streakTex);
            if (_streakMat.HasProperty("_Color")) _streakMat.SetColor("_Color", CoolantColor);
            if (_streakMat.HasProperty("_PanSpeed")) _streakMat.SetVector("_PanSpeed", new Vector4(0f, 1.6f, 0, 0));
            if (_streakMat.HasProperty("_TilingOffset")) _streakMat.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _streakMat.renderQueue = 3000;

            _mistMat = new Material(addSh);
            _mistMat.SetTexture("_MainTex", _mistTex);
            if (_mistMat.HasProperty("_Color")) _mistMat.SetColor("_Color", new Color(0.60f, 0.98f, 1.15f, 1f));
            _mistMat.renderQueue = 4000;

            _streaks = new Quad[_streakPoolN];
            for (int i = 0; i < _streaks.Length; i++) _streaks[i] = MakeQuad("CoolantStreak_" + i, _streakMat);

            _mist = new Quad[_mistPoolN];
            for (int i = 0; i < _mist.Length; i++) _mist[i] = MakeQuad("CoolantMist_" + i, _mistMat);

            var lgo = new GameObject("CoolantSprayLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = Vector3.forward * 0.04f;
            _L = lgo.AddComponent<Light>();
            _L.type = LightType.Point;
            _L.color = new Color(0.60f, 0.95f, 1.0f);
            _L.intensity = 0.7f;
            _L.range = 1.2f;
            _L.shadows = LightShadows.None;
        }

        Quad MakeQuad(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(transform, false);
            var col = go.GetComponent<Collider>(); if (col) UnityEngine.Object.Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            Quad q;
            q.tr = go.transform; q.r = mr;
            q.vel = Vector3.zero; q.latDir = Vector3.zero;
            q.age = -1f; q.life = 1f; q.seed = UnityEngine.Random.value * 1000f;
            q.w = 0.04f; q.h = 0.25f; q.breakT = 0.4f;
            q.mpb = new MaterialPropertyBlock();
            return q;
        }

        void Update()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;

            if (emitting)
            {
                _accStreak += _rateStreak * dt;
                while (_accStreak >= 1f) { SpawnStreak(); _accStreak -= 1f; }

                _accMist += _rateMist * dt;
                while (_accMist >= 1f) { SpawnMist(); _accMist -= 1f; }
            }

            for (int i = 0; i < _streaks.Length; i++)
            {
                var q = _streaks[i]; if (!q.Active) { _streaks[i] = q; continue; }
                q.age += dt;
                float t = q.age / q.life;

                q.vel += q.latDir * (ConeOutward * dt);
                q.vel -= q.vel * (Drag * dt);
                q.vel += Vector3.down * Gravity * dt;

                float n = Mathf.PerlinNoise(q.seed, Time.time * 1.4f) - 0.5f;
                q.vel += new Vector3(n, 0.3f * n, (0.5f - n)) * (Turbulence * 0.18f);

                q.tr.position += q.vel * dt;

                Vector3 dir = (q.vel.sqrMagnitude > 1e-5f) ? q.vel.normalized : transform.forward;
                Vector3 toCam = _cam ? (_cam.transform.position - q.tr.position).normalized : Vector3.up;
                Vector3 right = Vector3.Cross(dir, toCam).normalized;
                Vector3 up = Vector3.Cross(right, dir).normalized; if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
                q.tr.rotation = Quaternion.LookRotation(dir, up);

                float speed = q.vel.magnitude;
                float laminar = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - q.breakT) / (1f - q.breakT)));
                float baseLen = Mathf.Lerp(q.h * 0.55f, q.h, Mathf.Min(1f, t / q.breakT));
                float len = Mathf.Clamp(baseLen + speed * SpeedToLength, q.h * 0.35f, q.h * 1.6f) * (0.7f + 0.3f * laminar);

                q.tr.localScale = new Vector3(q.w, len, 1f);

                float alpha = Mathf.Clamp01(1f - t);
                var col = CoolantColor; col.a = CoolantColor.a * alpha;
                q.mpb.SetColor("_Color", col);
                q.r.SetPropertyBlock(q.mpb);

                if (t >= 1f) { q.age = -1f; q.r.enabled = false; }
                _streaks[i] = q;
            }

            for (int i = 0; i < _mist.Length; i++)
            {
                var q = _mist[i]; if (!q.Active) { _mist[i] = q; continue; }
                q.age += dt;
                float t = q.age / q.life;

                float n = Mathf.PerlinNoise(q.seed * 0.7f, Time.time * 2.7f) - 0.5f;
                q.vel += new Vector3(n, 0.25f * n, (0.5f - n)) * (Turbulence * 0.25f) * dt;
                q.vel -= q.vel * (Drag * 0.6f * dt);
                q.vel += Vector3.down * (Gravity * 0.65f) * dt;

                q.tr.position += q.vel * dt;

                if (_cam) q.tr.rotation = Quaternion.LookRotation(_cam.transform.forward * -1f, Vector3.up);
                q.tr.localScale = Vector3.one * Mathf.Lerp(q.w, q.w * 0.6f, t);

                float alpha = Mathf.Clamp01(1f - t);
                var col = new Color(0.62f, 0.98f, 1.15f, alpha);
                q.mpb.SetColor("_Color", col);
                q.r.SetPropertyBlock(q.mpb);

                if (t >= 1f) { q.age = -1f; q.r.enabled = false; }
                _mist[i] = q;
            }

            if (_L)
            {
                float f = Mathf.PerlinNoise(transform.position.x * 0.37f, Time.time * 6.0f);
                _L.intensity = 0.65f + 0.35f * f;
                _L.range = 1.1f + 0.25f * f;
            }
        }

        void SpawnStreak()
        {
            var q = _streaks[_si]; _si = (_si + 1) % _streaks.Length;

            q.age = 0f;
            q.life = Lerp(StreakLifeMin, StreakLifeMax, Rand());
            q.w = Lerp(StreakWidthMin, StreakWidthMax, Rand()) * _widthMul;
            q.h = Lerp(StreakLenMin, StreakLenMax, Rand());
            q.breakT = Mathf.Lerp(BreakUpAtMin, BreakUpAtMax, Rand());
            q.seed = Rand() * 1000f;

            Vector3 f = transform.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(Vector3.up, f)) > 0.98f ? Vector3.right : Vector3.up;
            Vector3 r = Vector3.Cross(f, up).normalized; up = Vector3.Cross(r, f).normalized;

            Vector2 p = UnityEngine.Random.insideUnitCircle * _mouthRadius;
            q.tr.position = transform.position + f * 0.035f + r * p.x + up * p.y;

            Vector2 ang = UnityEngine.Random.insideUnitCircle * StreakSpread;
            Vector3 dir = (f + r * ang.x + up * ang.y).normalized;
            q.latDir = (r * ang.y + up * -ang.x).normalized; 
            q.vel = dir * StreakSpeed;

            q.r.enabled = true;
            _streaks[_si == 0 ? _streaks.Length - 1 : _si - 1] = q;
        }

        void SpawnMist()
        {
            var q = _mist[_mi]; _mi = (_mi + 1) % _mist.Length;

            q.age = 0f;
            q.life = Lerp(MistLifeMin, MistLifeMax, Rand());
            q.w = Lerp(MistSizeMin, MistSizeMax, Rand()) * Mathf.Sqrt(_widthMul);
            q.h = q.w;
            q.seed = Rand() * 1000f;

            Vector3 f = transform.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(Vector3.up, f)) > 0.98f ? Vector3.right : Vector3.up;
            Vector3 r = Vector3.Cross(f, up).normalized; up = Vector3.Cross(r, f).normalized;

            Vector2 p = UnityEngine.Random.insideUnitCircle * (_mouthRadius * 0.65f);
            q.tr.position = transform.position + f * 0.03f + r * p.x + up * p.y;

            Vector2 ang = UnityEngine.Random.insideUnitCircle * MistSpread;
            Vector3 dir = (f + r * ang.x + up * ang.y).normalized;

            q.vel = dir * MistSpeed;
            q.r.enabled = true;
            _mist[_mi == 0 ? _mist.Length - 1 : _mi - 1] = q;
        }

        Texture2D BuildStreakTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float vy = y / (float)(h - 1);
                float tip = Mathf.SmoothStep(0f, 0.15f, vy) * (1f - Mathf.SmoothStep(0.72f, 1f, vy));
                for (int x = 0; x < w; x++)
                {
                    float nx = (x / (float)(w - 1)) * 2f - 1f;
                    float gauss = Mathf.Exp(-4.0f * nx * nx);
                    float n = Mathf.PerlinNoise(nx * 1.3f + 1.73f, vy * 7.0f + 2.19f) * 0.23f +
                              Mathf.PerlinNoise(nx * 3.7f + 3.31f, vy * 12.5f + 1.07f) * 0.17f;
                    float a = Mathf.Clamp01(gauss * (0.78f + n) * (0.35f + 0.65f * tip));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            t.SetPixels32(px); t.Apply(true, false); return t;
        }

        Texture2D BuildRadialTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1.0f, r));
                    float n = Mathf.PerlinNoise(x * 0.21f, y * 0.19f) * 0.12f;
                    a = Mathf.Clamp01(a * (0.9f + n));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px); t.Apply(true, false); return t;
        }


        static float Rand() => UnityEngine.Random.value;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
