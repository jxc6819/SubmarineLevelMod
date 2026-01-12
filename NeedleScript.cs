using System;
using UnityEngine;
using MelonLoader;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public class NeedleScript : MonoBehaviour
    {
        public NeedleScript(IntPtr ptr) : base(ptr) { }
        public NeedleScript() : base(ClassInjector.DerivedConstructorPointer<NeedleScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public float value01 = 0f;   // 0 = min, 1 = max
        public float minAngle = -90f;
        public float maxAngle = 90f;
        public Axis rotationAxis = Axis.Y;
        public bool invert = false;

        public Vector3 baseTilt = new Vector3(40f, 0f, 0f);

        public Axis needleLengthAxis = Axis.X;
        public bool tipPointsPositive = true;
        public Vector3 pivotNudge = new Vector3(0f, -0.03f, 0.18f);

        private Transform pivot;
        private bool initialized;

        public enum Axis { X, Y, Z }

        private float lastValue01 = 0f;
        private readonly float[] thresholds = { 0.25f, 0.5f, 0.74f, 0.82f, 0.96f };

        public SubmarineLevelLogic sll;

        void Awake()
        {
            EnsurePivot();
            
        }

        void Start()
        {
            sll = GameObject.Find("Manager").GetComponent<SubmarineLevelLogic>();
        }

        void Update()
        {
            if (!initialized) return;

            float t = Mathf.Clamp01(value01);
            if (invert) t = 1f - t;
            float angle = Mathf.Lerp(minAngle, maxAngle, t);

            Quaternion sweep = AxisToEuler(rotationAxis, angle);
            Quaternion tilt = Quaternion.Euler(baseTilt);
            pivot.localRotation = tilt * sweep;

            CheckThresholds(lastValue01, t);
            lastValue01 = t;
        }

        private void EnsurePivot()
        {
            if (initialized) return;

            GameObject pivotGO = new GameObject(name + "_Pivot");
            pivot = pivotGO.transform;
            pivot.position = transform.position;
            pivot.rotation = transform.rotation;
            pivot.localScale = Vector3.one;

            pivot.SetParent(transform.parent, true);

            transform.SetParent(pivot, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 localBase = b.center;
                Vector3 ext = b.extents;
                bool baseIsNegative = tipPointsPositive;

                switch (needleLengthAxis)
                {
                    case Axis.X: localBase.x = b.center.x + (baseIsNegative ? -ext.x : ext.x); break;
                    case Axis.Y: localBase.y = b.center.y + (baseIsNegative ? -ext.y : ext.y); break;
                    case Axis.Z: localBase.z = b.center.z + (baseIsNegative ? -ext.z : ext.z); break;
                }

                Vector3 baseWorld = transform.TransformPoint(localBase);
                pivot.position = baseWorld;
            }

            if (pivotNudge != Vector3.zero)
                pivot.position += pivot.TransformDirection(pivotNudge);

            initialized = true;
        }

        private static Quaternion AxisToEuler(Axis axis, float deg)
        {
            switch (axis)
            {
                case Axis.X: return Quaternion.Euler(deg, 0, 0);
                case Axis.Y: return Quaternion.Euler(0, deg, 0);
                default: return Quaternion.Euler(0, 0, deg);
            }
        }

        private void CheckThresholds(float oldVal, float newVal)
        {
            const float eps = 1e-4f;

            foreach (float t in thresholds)
            {
                if (oldVal + eps < t && newVal + eps >= t)
                {
                    TriggerThreshold(t);
                }
            }
        }

        private void TriggerThreshold(float t)
        {

            if (Mathf.Approximately(t, 0.25f)) sll.yellowTempHit();
            else if (Mathf.Approximately(t, 0.5f)) sll.orangeTempHit();
            else if (Mathf.Approximately(t, 0.74f)) sll.redTempHit();
            else if (Mathf.Approximately(t, 0.82f)) sll.stopperHit();
            else if (Mathf.Approximately(t, 0.96f)) sll.criticalTempHit();
        }

        
    }
}
