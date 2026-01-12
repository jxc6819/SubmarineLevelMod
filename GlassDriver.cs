using System;
using UnhollowerRuntimeLib;
using UnhollowerBaseLib.Attributes;
using UnityEngine;
using MelonLoader;

namespace IEYTD2_SubmarineCode
{
    public class GlassDriver : MonoBehaviour
    {
        public GlassDriver(IntPtr ptr) : base(ptr) { }
        public GlassDriver() : base(ClassInjector.DerivedConstructorPointer<GlassDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public float breakVelocity = 3.5f;
        public float hitCooldown = 0.05f;

        public int shardCount = 18;
        public float shardSizeMin = 0.02f;
        public float shardSizeMax = 0.06f;

        public float shardImpulse = 2.5f;
        public float shardUpBias = 0.35f;
        public float shardTorque = 3.5f;

        public float shardLifetime = 4.0f;
        public float shardDrag = 0.05f;
        public float shardAngularDrag = 0.05f;

        public bool destroyOriginal = true;

        private bool broken;
        private float lastHitTime = -999f;

        private Renderer glassRenderer;
        private Collider[] glassColliders;
        private Material glassMaterial;

        private void Awake()
        {
            glassRenderer = GetComponentInChildren<Renderer>(true);
            glassColliders = GetComponentsInChildren<Collider>(true);

            if (glassRenderer != null)
                glassMaterial = glassRenderer.sharedMaterial;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (broken) return;
            if (Time.time - lastHitTime < hitCooldown) return;
            lastHitTime = Time.time;

            float impactSpeed = collision.relativeVelocity.magnitude;
            MelonLogger.Msg("[GlassDriver] Impact speed: " + impactSpeed);

            if (impactSpeed >= breakVelocity)
                Break(collision);
        }

        public void Break(Collision collision = null)
        {
            if (broken) return;
            broken = true;

            MelonLogger.Msg("[GlassDriver] Break");
            AudioUtil.PlayAt("chair_glass_break_03", transform.position);

            Vector3 impactPoint = transform.position;
            Vector3 sprayDirection = transform.forward;

            if (collision != null)
            {
                if (collision.contactCount > 0)
                {
                    var contact = collision.GetContact(0);
                    impactPoint = contact.point;
                    sprayDirection = -contact.normal;
                }
                else if (collision.relativeVelocity.sqrMagnitude > 0.0001f)
                {
                    sprayDirection = collision.relativeVelocity.normalized;
                }
            }

            if (glassColliders != null)
            {
                for (int i = 0; i < glassColliders.Length; i++)
                    glassColliders[i].enabled = false;
            }

            if (glassRenderer != null)
                glassRenderer.enabled = false;

            SpawnShards(impactPoint, sprayDirection);

            if (destroyOriginal)
                Destroy(gameObject);
        }

        private void SpawnShards(Vector3 impactPoint, Vector3 sprayDirection)
        {
            var shardRoot = new GameObject(name + "_Shards");
            shardRoot.transform.position = impactPoint;
            shardRoot.layer = gameObject.layer;

            float spawnRadius = 0.15f;
            if (glassRenderer != null)
                spawnRadius = Mathf.Max(0.06f, glassRenderer.bounds.extents.magnitude * 0.35f);

            Vector3 dir = sprayDirection.sqrMagnitude < 1e-6f ? transform.forward : sprayDirection.normalized;
            int count = Mathf.Max(1, shardCount);

            for (int i = 0; i < count; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "GlassShard_" + i;
                shard.transform.SetParent(shardRoot.transform, true);

                shard.transform.position = impactPoint + UnityEngine.Random.insideUnitSphere * spawnRadius;
                shard.transform.rotation = UnityEngine.Random.rotation;

                float size = UnityEngine.Random.Range(shardSizeMin, shardSizeMax);
                shard.transform.localScale = new Vector3(
                    size,
                    size * UnityEngine.Random.Range(0.35f, 0.9f),
                    size
                );

                var renderer = shard.GetComponent<MeshRenderer>();
                if (renderer != null && glassMaterial != null)
                    renderer.sharedMaterial = glassMaterial;

                var rb = shard.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.mass = 0.02f;
                rb.drag = shardDrag;
                rb.angularDrag = shardAngularDrag;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                Vector3 impulse =
                    dir * shardImpulse +
                    UnityEngine.Random.insideUnitSphere * (shardImpulse * 0.45f) +
                    Vector3.up * shardUpBias;

                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * shardTorque, ForceMode.Impulse);

                Destroy(shard, Mathf.Max(0.25f, shardLifetime));
            }

            Destroy(shardRoot, Mathf.Max(0.5f, shardLifetime + 0.5f));
        }
    }
}
