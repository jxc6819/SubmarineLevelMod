using System;
using UnityEngine;
using MelonLoader;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public class HenchmanController : MonoBehaviour
    {
        public HenchmanController(IntPtr ptr) : base(ptr) { }
        public HenchmanController()
            : base(ClassInjector.DerivedConstructorPointer<HenchmanController>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform startPoint;
        public Transform endPoint;

        public float runSpeed = 1.5f;
        public float rotateSpeed = 5f;

        public Transform player;
        public float startShootingDelay = 0.5f;
        public string shootingStateName = "Shooting";
        public float shotRange = 50f;

        public string reachedEndTrigger = "ReachedEnd";
        public string dieRunningTrigger = "DieRunning";
        public string dieStandingTrigger = "DieStanding";

        public bool enableRagdollOnDeath = true;
        public float ragdollDelay = 0.5f;

        public Transform muzzle;

        public float flashDuration = 0.04f;
        public float flashSize = 0.008f;
        public Color flashColor = new Color(2.1f, 1.9f, 1.3f, 1.0f);

        public string[] ignoreNameContains = new string[] { "sun", "directional", "debug" };

        private Animator animator;
        private bool isRunning = true;
        private bool isDead = false;
        private bool reachedEnd = false;

        private bool inShootingPhase = false;
        private float shootingDelay = 0f;
        private int lastShotLoop = -1;

        private bool ragdollActive = false;
        private Rigidbody[] ragdollBodies;
        private Collider[] ragdollColliders;
        private Collider mainCollider;

        private bool pendingRagdoll = false;
        private float ragdollTimer = 0f;

        private Transform muzzleTransform;

        private Renderer flashRenderer;
        private Transform flashTransform;
        private Material flashMaterial;
        private Texture2D flashTexture;
        private MaterialPropertyBlock flashBlock;
        private float flashTimer;

        private ObjectBank bank;
        private Transform hmd;
        private AudioSource runLoop;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;

            mainCollider = GetComponent<Collider>();
            ragdollBodies = GetComponentsInChildren<Rigidbody>();
            ragdollColliders = GetComponentsInChildren<Collider>();

            if (startPoint == null)
                startPoint = transform;

            if (endPoint == null)
                CreateAutoEndPoint();

            if (player == null)
                player = GameObject.Find("PlayerHitbox").transform;

            hmd = GameObject.Find("HMD").transform;

            muzzleTransform = ResolveMuzzle();
            if (muzzleTransform != null)
                BuildMuzzleFlash();

            SetRagdoll(false);

            bank = ObjectBank.Instance;
        }

        private void OnEnable()
        {
            ResetState();

            if (startPoint != null)
                transform.position = startPoint.position;

            if (player == null)
                player = GameObject.Find("PlayerHitbox").transform;

            if (hmd == null)
                hmd = GameObject.Find("HMD").transform;

            if (muzzleTransform == null)
            {
                muzzleTransform = ResolveMuzzle();
                if (muzzleTransform != null && flashRenderer == null)
                    BuildMuzzleFlash();
            }

            animator.enabled = true;
            animator.speed = 1f;

            SetRagdoll(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (pendingRagdoll && !ragdollActive)
            {
                ragdollTimer -= dt;
                if (ragdollTimer <= 0f)
                {
                    pendingRagdoll = false;
                    SetRagdoll(true);
                }
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;

                float t = Mathf.Clamp01(1f - (flashTimer / flashDuration));
                float alpha = 1f - t;

                if (flashBlock == null)
                    flashBlock = new MaterialPropertyBlock();

                Color c = flashColor;
                c.a = flashColor.a * alpha;

                flashBlock.SetColor("_Color", c);
                flashRenderer.SetPropertyBlock(flashBlock);

                if (flashTimer <= 0f)
                    flashRenderer.enabled = false;
            }

            if (isDead || ragdollActive)
                return;

            if (isRunning)
            {
                UpdateRun(dt);

                if (runLoop == null)
                    runLoop = AudioUtil.PlayAt("metal_running", transform.position, 0.6f);
            }
            else
            {
                if (runLoop != null)
                {
                    AudioUtil.Stop(runLoop);
                    runLoop = null;
                }

                UpdateShooting(dt);
            }
        }

        private void UpdateRun(float dt)
        {
            Vector3 target = endPoint.position;

            float step = runSpeed * dt;
            transform.position = Vector3.MoveTowards(transform.position, target, step);

            Vector3 toTarget = target - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
            }

            if (!reachedEnd && transform.position == target)
            {
                reachedEnd = true;
                isRunning = false;

                inShootingPhase = true;
                shootingDelay = startShootingDelay;
                lastShotLoop = -1;

                if (!string.IsNullOrEmpty(reachedEndTrigger))
                    animator.SetTrigger(reachedEndTrigger);
            }
        }

        private void UpdateShooting(float dt)
        {
            Vector3 flatDir = player.position - transform.position;
            flatDir.y = 0f;

            if (flatDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
            }

            if (!inShootingPhase)
                return;

            if (shootingDelay > 0f)
            {
                shootingDelay -= dt;
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(shootingStateName))
                return;

            int loop = Mathf.FloorToInt(state.normalizedTime);
            if (loop > lastShotLoop)
            {
                lastShotLoop = loop;
                FireOnce();
            }
        }

        private void FireOnce()
        {
            FireMuzzleFlash();
            AudioUtil.PlayAt("assassinGun", transform.position);

            Vector3 origin = muzzleTransform != null
                ? muzzleTransform.position
                : transform.position + transform.forward * 0.5f;

            Vector3 toPlayer = player.position - origin;
            Vector3 direction = (toPlayer.sqrMagnitude > 0.001f) ? toPlayer.normalized : transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, shotRange, ~0, QueryTriggerInteraction.Collide))
            {
                MelonLogger.Msg($"[Henchman] Shot hit {hit.collider.name} at {hit.point}");

                if ((hit.collider.name == "PlayerHitbox" || hit.collider.name.Contains("Socket")) && hmd.position.y >= 0.75f)
                {
                    AudioUtil.PlayAt("bullet_impact_1", player.position);
                    bank.Manager.GetComponent<SubmarineLevelLogic>().DamagePlayer();
                }
            }
            else
            {
                AudioUtil.PlayAt("bullet_impact_metal_1", player.position);
                MelonLogger.Msg("[Henchman] Shot missed (no collider).");
            }
        }

        public void Kill()
        {
            if (isDead) return;
            isDead = true;

            AudioUtil.PlayAt("spearMan_gruntFall", transform.position);

            if (runLoop != null)
            {
                AudioUtil.Stop(runLoop);
                runLoop = null;
            }

            var cap = GetComponent<CapsuleCollider>();
            if (cap != null)
                cap.enabled = false;

            animator.enabled = true;
            animator.speed = 1f;

            if (isRunning && !string.IsNullOrEmpty(dieRunningTrigger))
                animator.SetTrigger(dieRunningTrigger);
            else if (!string.IsNullOrEmpty(dieStandingTrigger))
                animator.SetTrigger(dieStandingTrigger);

            if (enableRagdollOnDeath)
            {
                pendingRagdoll = true;
                ragdollTimer = ragdollDelay;
            }

            bank.Manager.GetComponent<SubmarineLevelLogic>().armedGuardDead();

            if (gameObject.name.Contains("1"))
                SpawnKey();
        }

        private void SpawnKey()
        {
            MelonLogger.Msg("[HenchmanController] - Spawn key");

            GameObject key = bank.ELV_MaintenanceKey_16;
            if (key.activeInHierarchy)
                return;

            key.transform.position = bank.KeySocket.transform.position;

            Rigidbody rb = key.GetComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            key.SetActive(true);
            rb.AddForce(new Vector3(-1f, 0f, 1f) * 0.06f, ForceMode.VelocityChange);
        }

        private void SetRagdoll(bool active)
        {
            ragdollActive = active;

            animator.enabled = !active;
            mainCollider.enabled = !active;

            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Rigidbody rb = ragdollBodies[i];
                rb.isKinematic = !active;
                rb.detectCollisions = active;

                if (!active)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                Collider col = ragdollColliders[i];
                if (col == mainCollider) continue;
                col.enabled = active;
            }
        }

        private void BuildMuzzleFlash()
        {
            for (int i = muzzleTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = muzzleTransform.GetChild(i);
                if (child.name.Contains("MuzzleFlash"))
                    Destroy(child.gameObject);
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = gameObject.name + "_MuzzleFlash";

            quad.transform.SetParent(muzzleTransform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(flashSize, flashSize * 0.6f, 1f);

            Destroy(quad.GetComponent<Collider>());

            flashRenderer = quad.GetComponent<MeshRenderer>();
            flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flashRenderer.receiveShadows = false;

            Shader sh = Shader.Find("FX/FX_Additive_UVPan_Shader");
            if (!sh) sh = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");
            if (!sh) sh = Shader.Find("Unlit/Transparent");

            flashTexture = BuildFlashTex(64);
            flashMaterial = new Material(sh);

            if (flashMaterial.HasProperty("_MainTex"))
                flashMaterial.SetTexture("_MainTex", flashTexture);

            SetAllColorProps(flashMaterial, flashColor);

            if (flashMaterial.HasProperty("_PanSpeed"))
                flashMaterial.SetVector("_PanSpeed", Vector4.zero);

            if (flashMaterial.HasProperty("_TilingOffset"))
                flashMaterial.SetVector("_TilingOffset", new Vector4(1f, 1f, 0f, 0f));

            flashMaterial.renderQueue = 3200;

            flashRenderer.sharedMaterial = flashMaterial;
            flashRenderer.enabled = false;

            flashTransform = quad.transform;
        }

        private void FireMuzzleFlash()
        {
            if (flashRenderer == null)
                return;

            flashTimer = flashDuration;
            flashRenderer.enabled = true;

            flashTransform.localPosition = new Vector3(0f, 0f, 0.01f);
            flashTransform.localRotation = Quaternion.identity;
            flashTransform.localScale = new Vector3(flashSize, flashSize * 0.6f, 1f);
        }

        private Transform ResolveMuzzle()
        {
            if (muzzle != null)
                return muzzle;

            string muzzleName = gameObject.name.Contains("1") ? "Henchman1Muzzle" : "Henchman2Muzzle";
            GameObject muzzleObj = GameObject.Find(muzzleName);
            if (muzzleObj != null)
                return muzzleObj.transform;

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (!string.IsNullOrEmpty(n) && n.ToLower().Contains("muzzle"))
                    return all[i];
            }

            return null;
        }

        private void CreateAutoEndPoint()
        {
            GameObject ep = new GameObject(gameObject.name + "_AutoEndPoint");
            endPoint = ep.transform;

            if (gameObject.name.Contains("1"))
                endPoint.position = new Vector3(3.508f, 3.852594f, -4.57f);
            else
                endPoint.position = new Vector3(-3.22f, 3.852594f, -4.57f);
        }

        private void ResetState()
        {
            isDead = false;
            isRunning = true;
            reachedEnd = false;

            inShootingPhase = false;
            shootingDelay = 0f;
            lastShotLoop = -1;

            pendingRagdoll = false;
            ragdollTimer = 0f;
            ragdollActive = false;

            flashTimer = 0f;
            if (flashRenderer != null)
                flashRenderer.enabled = false;
        }

        public void RebuildAutoEndPoint()
        {
            if (endPoint != null && endPoint.gameObject.name.Contains("_AutoEndPoint"))
                Destroy(endPoint.gameObject);

            CreateAutoEndPoint();
        }

        private static Texture2D BuildFlashTex(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;

            Color32[] px = new Color32[size * size];
            float c = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);

                    float falloff = Mathf.Clamp01(1f - Mathf.SmoothStep(0.15f, 1.0f, r));

                    float spike =
                        Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2f)), 3f) * 0.7f +
                        Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 2f) * 0.3f;

                    float intensity = falloff * (0.55f + 0.45f * spike);
                    intensity = Mathf.Clamp01(intensity);

                    byte v = (byte)(intensity * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, v);
                }
            }

            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        private static void SetAllColorProps(Material m, Color c)
        {
            if (!m) return;

            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Tint")) m.SetColor("_Tint", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.7f);
        }
    }
}
