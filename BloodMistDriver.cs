using System;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class BloodMistDriver : MonoBehaviour
    {
        public BloodMistDriver(IntPtr p) : base(p) { }
        public BloodMistDriver()
            : base(ClassInjector.DerivedConstructorPointer<BloodMistDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform HitPoint;

        public int PoolSize = 80;
        public int BurstCount = 45;

        public float LifeMin = 0.32f;
        public float LifeMax = 0.65f;
        public float SizeMin = 0.045f;
        public float SizeMax = 0.085f;

        public float SpeedMin = 1.8f;
        public float SpeedMax = 4.2f;
        public float ConeAngleDeg = 32f;
        public float Gravity = 3.6f;
        public float Drag = 3.5f;
        public float Turbulence = 0.75f;
        public float TurbulenceFrequency = 2.0f;

        public Color BloodColor = new Color(0.45f, 0.02f, 0.01f, 0.9f);
        public float StartAlphaBoost = 1.2f;
        public float EndDarken = 0.4f;

        private struct Puff
        {
            public Transform Transform;
            public Renderer Renderer;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float StartSize;
            public float NoiseSeed;
            public MaterialPropertyBlock PropertyBlock;
            public bool IsAlive => Age >= 0f && Age < Lifetime;
        }

        private Puff[] _puffs;
        private int _nextPuffIndex;

        private Material _material;
        private Texture2D _texture;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;

            Shader shader = Shader.Find("FX/FX_Alpha_UVPan_Shader");
            if (!shader) shader = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");
            if (!shader) shader = Shader.Find("Unlit/Transparent");

            _texture = BuildRadialTexture(64);

            _material = new Material(shader);
            if (_material.HasProperty("_MainTex")) _material.SetTexture("_MainTex", _texture);
            SetAllColorProperties(_material, BloodColor);
            if (_material.HasProperty("_PanSpeed")) _material.SetVector("_PanSpeed", Vector4.zero);
            if (_material.HasProperty("_TilingOffset")) _material.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _material.renderQueue = 3000;

            _puffs = new Puff[PoolSize];
            for (int i = 0; i < PoolSize; i++)
                _puffs[i] = CreatePuff("BloodMist_" + i, _material);
        }

        private Puff CreatePuff(string name, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(transform, false);

            Destroy(quad.GetComponent<Collider>());

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            Puff puff;
            puff.Transform = quad.transform;
            puff.Renderer = renderer;
            puff.Velocity = Vector3.zero;
            puff.Age = -1f;
            puff.Lifetime = 1f;
            puff.StartSize = 0.07f;
            puff.NoiseSeed = UnityEngine.Random.value * 1000f;
            puff.PropertyBlock = new MaterialPropertyBlock();
            return puff;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _puffs.Length; i++)
            {
                Puff puff = _puffs[i];
                puff.Age = -1f;
                puff.Renderer.enabled = false;
                _puffs[i] = puff;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < _puffs.Length; i++)
            {
                Puff puff = _puffs[i];
                if (!puff.IsAlive)
                {
                    _puffs[i] = puff;
                    continue;
                }

                puff.Age += deltaTime;
                float lifeT = puff.Age / puff.Lifetime;

                float noiseX = Mathf.PerlinNoise(puff.NoiseSeed, Time.time * TurbulenceFrequency) - 0.5f;
                float noiseY = Mathf.PerlinNoise(puff.NoiseSeed * 1.73f, Time.time * (TurbulenceFrequency * 1.2f)) - 0.5f;
                Vector3 noise = new Vector3(noiseX, noiseY * 0.6f, -noiseX * 0.4f);

                puff.Velocity += noise * (Turbulence * deltaTime);
                puff.Velocity += Vector3.down * (Gravity * deltaTime);
                puff.Velocity -= puff.Velocity * (Drag * deltaTime);

                puff.Transform.position += puff.Velocity * deltaTime;

                if (_camera)
                {
                    Vector3 toCamera = (_camera.transform.position - puff.Transform.position).normalized;
                    puff.Transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
                }

                float size = Mathf.Lerp(puff.StartSize, puff.StartSize * 1.4f, lifeT);
                puff.Transform.localScale = new Vector3(size, size, 1f);

                float alpha = Mathf.Clamp01(1f - lifeT);

                Color color = BloodColor;
                float brightness = Mathf.Lerp(StartAlphaBoost, EndDarken, lifeT);
                color.r *= brightness;
                color.g *= brightness * 0.7f;
                color.b *= brightness * 0.7f;
                color.a = BloodColor.a * alpha;

                puff.PropertyBlock.SetColor("_Color", color);
                puff.Renderer.SetPropertyBlock(puff.PropertyBlock);

                if (lifeT >= 1f)
                {
                    puff.Age = -1f;
                    puff.Renderer.enabled = false;
                }

                _puffs[i] = puff;
            }
        }

        public void TriggerMist(Vector3 incomingDirection)
        {
            Vector3 origin = (HitPoint != null) ? HitPoint.position : transform.position;
            TriggerMistAtPoint(origin, incomingDirection);
        }

        public void TriggerMistAtPoint(Vector3 hitPoint, Vector3 incomingDirection)
        {
            int spawnCount = BurstCount;
            if (spawnCount > _puffs.Length) spawnCount = _puffs.Length;

            Vector3 sprayDirection = -incomingDirection;
            if (sprayDirection.sqrMagnitude < 1e-4f)
                sprayDirection = (HitPoint != null) ? HitPoint.forward : transform.forward;

            sprayDirection.Normalize();

            for (int i = 0; i < spawnCount; i++)
                SpawnPuff(hitPoint, sprayDirection);
        }

        private void SpawnPuff(Vector3 origin, Vector3 sprayDirection)
        {
            Puff puff = _puffs[_nextPuffIndex];
            _nextPuffIndex = (_nextPuffIndex + 1) % _puffs.Length;

            puff.Age = 0f;
            puff.Lifetime = Mathf.Lerp(LifeMin, LifeMax, UnityEngine.Random.value);
            puff.StartSize = Mathf.Lerp(SizeMin, SizeMax, UnityEngine.Random.value);
            puff.NoiseSeed = UnityEngine.Random.value * 1000f;

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * (SizeMax * 0.4f);
            Vector3 spawnPos = origin
                               + transform.right * jitter.x * 0.4f
                               + transform.up * jitter.y * 0.4f;

            puff.Transform.position = spawnPos;

            Vector3 direction = RandomDirectionInCone(sprayDirection, ConeAngleDeg * Mathf.Deg2Rad);
            float speed = Mathf.Lerp(SpeedMin, SpeedMax, UnityEngine.Random.value);
            puff.Velocity = direction * speed;

            puff.Renderer.enabled = true;

            int writtenIndex = _nextPuffIndex == 0 ? _puffs.Length - 1 : _nextPuffIndex - 1;
            _puffs[writtenIndex] = puff;
        }

        private Vector3 RandomDirectionInCone(Vector3 forward, float angleRadians)
        {
            forward.Normalize();

            float u = UnityEngine.Random.value;
            float v = UnityEngine.Random.value;

            float theta = 2f * Mathf.PI * u;
            float cosAngle = Mathf.Cos(angleRadians);
            float cosZ = Mathf.Lerp(cosAngle, 1f, v);
            float sinZ = Mathf.Sqrt(1f - cosZ * cosZ);

            float x = Mathf.Cos(theta) * sinZ;
            float y = Mathf.Sin(theta) * sinZ;
            float z = cosZ;

            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;

            return right * x + up * y + forward * z;
        }

        private Texture2D BuildRadialTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.35f, 1.1f, radius));
                    a = Mathf.Pow(a, 1.4f);

                    byte alpha = (byte)(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        private static void SetAllColorProperties(Material material, Color color)
        {
            if (!material) return;

            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 0.2f);
        }
    }
}
