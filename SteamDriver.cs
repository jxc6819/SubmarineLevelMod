using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class SteamDriver : MonoBehaviour
    {
        public SteamDriver(IntPtr p) : base(p) { }
        public SteamDriver() : base(ClassInjector.DerivedConstructorPointer<SteamDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public bool emitting = false;

        // Where steam originates relative to transform
        public Vector3 localOffset = Vector3.zero;


        public int SteamPool = 140;
        public float SpawnRate = 260f;
        public float RiseSpeed = 1.8f;
        public float LateralSpread = 0.25f;
        public float LifeMin = 1.4f;
        public float LifeMax = 2.8f;
        public float SizeMin = 0.10f;
        public float SizeMax = 0.22f;
        public float Turbulence = 0.55f;  //curl strength
        public float TurbFrequency = 1.2f; //noise frequency

        public Color SteamColor = new Color(0.94f, 0.96f, 1.00f, 0.70f);

        struct Puff
        {
            public Transform tr;
            public Renderer r;
            public Vector3 vel;
            public float age, life, size, seed;
            public MaterialPropertyBlock mpb;
            public bool Active => age >= 0f && age < life;
        }

        Puff[] _puffs;
        int _pi;
        float _accum;
        Material _mat;
        Texture2D _tex;
        Camera _cam;

        void Awake()
        {
            _cam = Camera.main;

            //build material and sprite
            var sh = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (!sh)
                sh = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");
            if (!sh)
                sh = Shader.Find("Unlit/Transparent");

            _tex = BuildRadialTex(64);

            _mat = new Material(sh);
            if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", _tex);
            SetAllColorProps(_mat, SteamColor);
            if (_mat.HasProperty("_PanSpeed")) _mat.SetVector("_PanSpeed", Vector4.zero);
            if (_mat.HasProperty("_TilingOffset")) _mat.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _mat.renderQueue = 3000;

            // pool
            int poolN = Mathf.Max(20, SteamPool);
            _puffs = new Puff[poolN];
            for (int i = 0; i < poolN; i++)
                _puffs[i] = MakePuff("SteamPuff_" + i, _mat);
        }

        Puff MakePuff(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(transform, false);

            var col = go.GetComponent<Collider>();
            if (col) UnityEngine.Object.Destroy(col);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.enabled = false;

            Puff p;
            p.tr = go.transform;
            p.r = mr;
            p.vel = Vector3.zero;
            p.age = -1f;
            p.life = 1f;
            p.size = 0.1f;
            p.seed = UnityEngine.Random.value * 1000f;
            p.mpb = new MaterialPropertyBlock();
            return p;
        }

        void OnEnable()
        {
            if (_puffs == null) return;
            for (int i = 0; i < _puffs.Length; i++)
            {
                var p = _puffs[i];
                p.age = -1f;
                if (p.r) p.r.enabled = false;
                _puffs[i] = p;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _puffs == null) return;

            //spawn new puffs while emitting
            if (emitting)
            {
                _accum += SpawnRate * dt;
                while (_accum >= 1f)
                {
                    SpawnPuff();
                    _accum -= 1f;
                }
            }

            for (int i = 0; i < _puffs.Length; i++)
            {
                var p = _puffs[i];
                if (!p.Active)
                {
                    _puffs[i] = p;
                    continue;
                }

                p.age += dt;
                float t = p.age / p.life;

                float n = Mathf.PerlinNoise(p.seed, Time.time * TurbFrequency) - 0.5f;
                float n2 = Mathf.PerlinNoise(p.seed * 1.73f, Time.time * (TurbFrequency * 1.3f)) - 0.5f;

                Vector3 lateral =
                    transform.right * (n * LateralSpread) +
                    transform.forward * (n2 * LateralSpread);

                p.vel += (Vector3.up * RiseSpeed + lateral) * dt * Turbulence;

                p.tr.position += p.vel * dt;

                //face camera
                if (_cam)
                {
                    Vector3 toCam = (_cam.transform.position - p.tr.position).normalized;
                    if (toCam.sqrMagnitude > 1e-4f)
                        p.tr.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
                }

                //grow slightly over lifetime
                float s = Mathf.Lerp(p.size, p.size * 1.35f, t);
                p.tr.localScale = new Vector3(s, s, 1f);

                //fade out
                float alpha = Mathf.Clamp01(1f - t);
                var col = SteamColor;
                col.a = SteamColor.a * alpha;
                p.mpb.SetColor("_Color", col);
                p.r.SetPropertyBlock(p.mpb);

                if (t >= 1f)
                {
                    p.age = -1f;
                    p.r.enabled = false;
                }

                _puffs[i] = p;
            }
        }

        void SpawnPuff()
        {
            if (_puffs == null || _puffs.Length == 0) return;

            var p = _puffs[_pi];
            _pi = (_pi + 1) % _puffs.Length;

            p.age = 0f;
            p.life = Lerp(LifeMin, LifeMax, Rand());
            p.size = Lerp(SizeMin, SizeMax, Rand());
            p.seed = Rand() * 1000f;

            Vector3 origin = transform.TransformPoint(localOffset);
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * (SizeMax * 0.3f);
            origin += transform.right * jitter.x + transform.forward * jitter.y;
            p.tr.position = origin;

            Vector2 ang = UnityEngine.Random.insideUnitCircle * 0.6f;
            Vector3 dir =
                Vector3.up +
                transform.right * (ang.x * LateralSpread * 0.4f) +
                transform.forward * (ang.y * LateralSpread * 0.4f);

            dir.Normalize();
            p.vel = dir * RiseSpeed;

            p.r.enabled = true;
            _puffs[_pi == 0 ? _puffs.Length - 1 : _pi - 1] = p;
        }


        Texture2D BuildRadialTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1.0f, r));
                    float n = Mathf.PerlinNoise(x * 0.21f, y * 0.19f) * 0.16f;
                    a = Mathf.Clamp01(a * (0.9f + n));

                    byte v = (byte)(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, v);
                }
            }

            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        static void SetAllColorProps(Material m, Color c)
        {
            if (!m) return;
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.35f);
        }

        static float Rand() => UnityEngine.Random.value;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
