using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD2_SubmarineCode
{
    public class GunScript : MonoBehaviour
    {
        public GunScript(IntPtr ptr) : base(ptr) { }
        public GunScript()
            : base(ClassInjector.DerivedConstructorPointer<GunScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public string hammerName = "spyGun_hammer";
        public string slideName = "spyGun_slide";
        public string triggerName = "spyGun_trigger";
        public string raycastOriginName = "RaycastOrigin";

        public float recoilDistance = 0.03f;
        public float recoilAngle = 5f;
        public float recoilDuration = 0.1f;

        public Vector3 muzzleLocalOffset = new Vector3(0f, 0.0f, 0.20f);

        public float slideBackDistance = 0.04f;
        public float hammerRotateDegrees = 18f;
        public float triggerRotateDegrees = 14f;
        public float fireAnimDuration = 0.12f;

        public float flashDuration = 0.06f;
        public float flashSize = 0.14f;
        public Color flashColor = new Color(2.1f, 1.9f, 1.3f, 1.0f);

        public string rightTriggerAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        public string leftTriggerAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
        public float triggerThreshold = 0.7f;

        public float maxShootDistance = 100f;
        public GameObject lastHitObject;
        public Vector3 lastHitPoint;

        private Transform _hammer;
        private Transform _slide;
        private Transform _trigger;
        private Transform _raycastOrigin;

        private Vector3 _hammerLocalPos0;
        private Vector3 _slideLocalPos0;
        private Vector3 _triggerLocalPos0;

        private Quaternion _hammerLocalRot0;
        private Quaternion _slideLocalRot0;
        private Quaternion _triggerLocalRot0;

        private Vector3 _gunLocalPos0;
        private Quaternion _gunLocalRot0;
        private float _recoilTimer;

        private float _slideAnimTimer;
        private float _hammerAnimTimer;
        private float _triggerAnimTimer;

        private Renderer _flashRenderer;
        private Transform _flashTransform;
        private Material _flashMaterial;
        private Texture2D _flashTexture;
        private MaterialPropertyBlock _flashMPB;
        private float _flashTimer;
        private float _flashRandomRot;

        private float _prevRightAxis;
        private float _prevLeftAxis;

        private PickUp _pickUp;
        private Camera _camera;

        public LoopingSfx alarmAudio;

        void OnEnable()
        {
            _camera = Camera.main;

            _gunLocalPos0 = transform.localPosition;
            _gunLocalRot0 = transform.localRotation;

            _pickUp = GetComponent<PickUp>();
            if (_pickUp == null && transform.parent != null)
                _pickUp = transform.parent.GetComponent<PickUp>();

            _hammer = FindChildDeep(hammerName);
            _slide = FindChildDeep(slideName);
            _trigger = FindChildDeep(triggerName);
            _raycastOrigin = FindChildDeep(raycastOriginName);

            if (_hammer != null)
            {
                _hammerLocalPos0 = _hammer.localPosition;
                _hammerLocalRot0 = _hammer.localRotation;
            }

            if (_slide != null)
            {
                _slideLocalPos0 = _slide.localPosition;
                _slideLocalRot0 = _slide.localRotation;
            }

            if (_trigger != null)
            {
                _triggerLocalPos0 = _trigger.localPosition;
                _triggerLocalRot0 = _trigger.localRotation;
            }

            BuildMuzzleFlash();
        }

        Transform FindChildDeep(string childName)
        {
            if (string.IsNullOrEmpty(childName)) return null;

            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                    return all[i];
            }
            return null;
        }

        void BuildMuzzleFlash()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MuzzleFlash";
            quad.transform.SetParent(transform, false);

            if (_raycastOrigin != null)
            {
                quad.transform.position = _raycastOrigin.position;
                quad.transform.rotation = _raycastOrigin.rotation;
            }
            else
            {
                quad.transform.localPosition = muzzleLocalOffset;
                quad.transform.localRotation = Quaternion.identity;
            }

            quad.transform.localScale = new Vector3(flashSize, flashSize * 0.6f, 1f);

            var collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            _flashRenderer = quad.GetComponent<MeshRenderer>();
            _flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flashRenderer.receiveShadows = false;

            var shader = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (shader == null) shader = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            _flashTexture = BuildFlashTex(64);

            _flashMaterial = new Material(shader);
            if (_flashMaterial.HasProperty("_MainTex")) _flashMaterial.SetTexture("_MainTex", _flashTexture);
            SetAllColorProps(_flashMaterial, flashColor);
            if (_flashMaterial.HasProperty("_PanSpeed")) _flashMaterial.SetVector("_PanSpeed", Vector4.zero);
            if (_flashMaterial.HasProperty("_TilingOffset")) _flashMaterial.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));
            _flashMaterial.renderQueue = 3200;

            _flashRenderer.sharedMaterial = _flashMaterial;

            _flashMPB = new MaterialPropertyBlock();
            _flashRenderer.enabled = false;
            _flashTransform = quad.transform;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            bool isHeld = _pickUp != null && _pickUp.isHeld;

            if (isHeld && TriggerJustPressed())
                FireShot();

            UpdateRecoil(deltaTime);
            UpdateSmallParts(deltaTime);
            UpdateMuzzleFlash(deltaTime);
        }

        bool TriggerJustPressed()
        {
            float rightAxis = Input.GetAxis(rightTriggerAxis);
            float leftAxis = Input.GetAxis(leftTriggerAxis);

            bool rightNow = rightAxis > triggerThreshold;
            bool leftNow = leftAxis > triggerThreshold;
            bool rightPrev = _prevRightAxis > triggerThreshold;
            bool leftPrev = _prevLeftAxis > triggerThreshold;

            _prevRightAxis = rightAxis;
            _prevLeftAxis = leftAxis;

            return (rightNow && !rightPrev) || (leftNow && !leftPrev);
        }

        void FireShot()
        {
            _slideAnimTimer = fireAnimDuration;
            _hammerAnimTimer = fireAnimDuration;
            _triggerAnimTimer = fireAnimDuration;

            _recoilTimer = recoilDuration;

            AudioUtil.PlayAt("spyGun", transform.position);

            TriggerMuzzleFlash();

            Vector3 origin;
            Vector3 direction;

            if (_raycastOrigin != null)
            {
                origin = _raycastOrigin.position;
                direction = _raycastOrigin.forward;
            }
            else
            {
                origin = transform.TransformPoint(muzzleLocalOffset);
                direction = transform.forward;
            }

            lastHitObject = null;
            lastHitPoint = origin;

            RaycastHit hit;
            if (!Physics.Raycast(origin, direction, out hit, maxShootDistance))
            {
                MelonLogger.Msg("[GunScript] Shot did not hit anything.");
                return;
            }

            GameObject hitObject = hit.collider.gameObject;

            if (hitObject.name.Contains("CatWalk") || hitObject.name.Contains("Railing"))
            {
                if (!Physics.Raycast(hit.point + direction * 0.01f, direction, out hit, maxShootDistance))
                {
                    MelonLogger.Msg("[GunScript] Shot hit railing and nothing behind it.");
                    return;
                }
                hitObject = hit.collider.gameObject;
            }

            lastHitObject = hitObject;
            lastHitPoint = hit.point;

            TrySpawnBloodMist(hitObject, hit.point, origin);

            bool hitHenchman = HandleDamageTargets(hitObject);
            if (!hitHenchman)
                AudioUtil.PlayAt("bullet_impact_metal_1", hitObject.transform.position);

            MelonLogger.Msg($"[GunScript] Shot hit '{hitObject.name}' at {hit.point}.");
        }

        void TriggerMuzzleFlash()
        {
            if (_flashRenderer == null) return;

            _flashTimer = flashDuration;
            _flashRenderer.enabled = true;

            if (_raycastOrigin != null)
            {
                _flashTransform.position = _raycastOrigin.position;
                _flashTransform.rotation = _raycastOrigin.rotation;
            }
            else
            {
                _flashTransform.localPosition = muzzleLocalOffset;
            }

            _flashRandomRot = UnityEngine.Random.Range(0f, 360f);
        }

        void UpdateRecoil(float deltaTime)
        {
            if (_recoilTimer > 0f)
            {
                _recoilTimer -= deltaTime;

                float normalized = Mathf.Clamp01(1f - (_recoilTimer / recoilDuration));
                float curve = Mathf.Sin(normalized * Mathf.PI);

                Vector3 recoilOffset = new Vector3(0f, 0f, -recoilDistance * curve);
                transform.localPosition = _gunLocalPos0 + recoilOffset;

                transform.localRotation = _gunLocalRot0 * Quaternion.Euler(-recoilAngle * curve, 0f, 0f);
            }
            else
            {
                transform.localPosition = _gunLocalPos0;
                transform.localRotation = _gunLocalRot0;
            }
        }

        void UpdateSmallParts(float deltaTime)
        {
            UpdateSlide(deltaTime);
            UpdateHammer(deltaTime);
            UpdateTrigger(deltaTime);
        }

        void UpdateSlide(float deltaTime)
        {
            if (_slide == null) return;

            if (_slideAnimTimer > 0f)
            {
                _slideAnimTimer -= deltaTime;

                float normalized = Mathf.Clamp01(1f - (_slideAnimTimer / fireAnimDuration));
                float curve = Mathf.Sin(normalized * Mathf.PI);

                Vector3 back = new Vector3(0f, 0f, -slideBackDistance);
                _slide.localPosition = _slideLocalPos0 + back * curve;
            }
            else
            {
                _slide.localPosition = _slideLocalPos0;
            }
        }

        void UpdateHammer(float deltaTime)
        {
            if (_hammer == null) return;

            if (_hammerAnimTimer > 0f)
            {
                _hammerAnimTimer -= deltaTime;

                float normalized = Mathf.Clamp01(1f - (_hammerAnimTimer / fireAnimDuration));
                float curve = Mathf.Sin(normalized * Mathf.PI);

                _hammer.localRotation = _hammerLocalRot0 * Quaternion.Euler(-hammerRotateDegrees * curve, 0f, 0f);
            }
            else
            {
                _hammer.localRotation = _hammerLocalRot0;
            }
        }

        void UpdateTrigger(float deltaTime)
        {
            if (_trigger == null) return;

            if (_triggerAnimTimer > 0f)
            {
                _triggerAnimTimer -= deltaTime;

                float normalized = Mathf.Clamp01(1f - (_triggerAnimTimer / fireAnimDuration));
                float curve = Mathf.Sin(normalized * Mathf.PI);

                _trigger.localRotation = _triggerLocalRot0 * Quaternion.Euler(-triggerRotateDegrees * curve, 0f, 0f);
            }
            else
            {
                _trigger.localRotation = _triggerLocalRot0;
            }
        }

        void UpdateMuzzleFlash(float deltaTime)
        {
            if (_flashRenderer == null || _flashTimer <= 0f) return;

            _flashTimer -= deltaTime;

            float normalized = Mathf.Clamp01(1f - (_flashTimer / flashDuration));
            float alpha = 1f - normalized;

            float sizeMul = 1.0f + 0.6f * (1f - normalized);
            _flashTransform.localScale = new Vector3(flashSize * sizeMul, flashSize * 0.6f * sizeMul, 1f);

            if (_camera != null)
            {
                Vector3 toCam = (_camera.transform.position - _flashTransform.position).normalized;
                Quaternion faceCamera = Quaternion.LookRotation(-toCam, Vector3.up);
                _flashTransform.rotation = faceCamera * Quaternion.AngleAxis(_flashRandomRot, Vector3.forward);
            }

            Color c = flashColor;
            c.a = flashColor.a * alpha;

            _flashMPB.SetColor("_Color", c);
            _flashRenderer.SetPropertyBlock(_flashMPB);

            if (_flashTimer <= 0f)
                _flashRenderer.enabled = false;
        }

        void TrySpawnBloodMist(GameObject hitObject, Vector3 hitPoint, Vector3 shotOrigin)
        {
            var current = hitObject.transform;

            while (current != null)
            {
                var mist = current.GetComponent<BloodMistDriver>();
                if (mist != null)
                {
                    Vector3 incomingDir = (hitPoint - shotOrigin).normalized;
                    mist.TriggerMistAtPoint(hitPoint, incomingDir);
                    return;
                }
                current = current.parent;
            }
        }

        bool HandleDamageTargets(GameObject hitObject)
        {
            var current = hitObject.transform;

            while (current != null)
            {
                var henchman = current.GetComponent<HenchmanController>();
                if (henchman != null)
                {
                    henchman.Kill();
                    AudioUtil.PlayAt("bullet_impact_1", current.position);
                    return true;
                }
                current = current.parent;
            }

            if (hitObject.name.ToLower().Contains("glass"))
            {
                var glass = hitObject.GetComponent<GlassDriver>();
                if (glass != null) glass.Break();
            }

            return false;
        }

        Texture2D BuildFlashTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;

                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float falloff = Mathf.Clamp01(1f - Mathf.SmoothStep(0.15f, 1.0f, radius));

                    float spike =
                        Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 3f) * 0.7f +
                        Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 2f) * 0.3f;

                    float intensity = falloff * (0.55f + 0.45f * spike);
                    intensity = Mathf.Clamp01(intensity);

                    byte a = (byte)(intensity * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            return tex;
        }

        static void SetAllColorProps(Material mat, Color color)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 0.7f);
        }
    }
}
