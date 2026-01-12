using UnityEngine;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD2_SubmarineCode
{
    public static class MakeGrabbable
    {
        public static bool TryMake(string objectName)
        {
            return TryMake(GameObject.Find(objectName));
        }

        public static bool TryMake(string objectName, out GameObject target)
        {
            target = GameObject.Find(objectName);
            return TryMake(target);
        }

        public static bool TryMake(GameObject target)
        {
            if (target == null) return false;

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body == null) body = target.AddComponent<Rigidbody>();

            if (body.mass <= 0f) body.mass = 1f;
            if (body.mass < 0.1f) body.mass = 0.1f;

            body.useGravity = true;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Bounds renderBounds = GetRenderBounds(target);

                SphereCollider sphere = target.AddComponent<SphereCollider>();
                sphere.center = target.transform.InverseTransformPoint(renderBounds.center);
                sphere.radius = MaxExtent(renderBounds);
            }

            PickUp pickUp = target.GetComponent<PickUp>();
            if (pickUp == null) pickUp = target.AddComponent<PickUp>();

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
                pickUp._renderers = ToIl2CppArray(renderers);

            Bounds bounds = GetRenderBounds(target);
            pickUp._LocalBoundCenter = target.transform.InverseTransformPoint(bounds.center);
            pickUp._BoundRadius = MaxExtent(bounds);

            pickUp._OnReleaseForceMode = ForceMode.VelocityChange;
            pickUp._OnReleaseVelocityMultiplier = 1f;
            pickUp._OnReleaseAngVelocityMultiplier = 1f;

            pickUp._EnableEdgeGrab = true;
            pickUp._EnableHeldRotation = false;
            pickUp._PlayHapticWhenShot = true;
            pickUp._ImpactedByShot = true;
            pickUp._DisableHiddenVolume = false;
            pickUp._OverrideMaxAngularVelocity = false;

            AssignDefaultPickUpSettingsIfPresent(pickUp);

            return true;
        }

        private static Bounds GetRenderBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one * 0.25f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static float MaxExtent(Bounds bounds)
        {
            float x = bounds.extents.x;
            float y = bounds.extents.y;
            float z = bounds.extents.z;
            return Mathf.Max(x, Mathf.Max(y, z));
        }

        private static Il2CppReferenceArray<Renderer> ToIl2CppArray(Renderer[] renderers)
        {
            var array = new Il2CppReferenceArray<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
                array[i] = renderers[i];
            return array;
        }

        private static void AssignDefaultPickUpSettingsIfPresent(PickUp pickUp)
        {
            var found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PickUpSettings>());
            for (int i = 0; i < found.Length; i++)
            {
                var obj = found[i];
                if (obj == null) continue;

                string name = obj.name;
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Contains("DefaultPickUpSettings") || name.Contains("DefaultPickupSettings"))
                {
                    var settings = obj.TryCast<PickUpSettings>();
                    if (settings != null)
                    {
                        pickUp._PickUpSettings = settings;
                        return;
                    }
                }
            }
        }
    }
}
