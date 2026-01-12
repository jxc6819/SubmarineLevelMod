using UnityEngine;
using UnhollowerBaseLib.Attributes;
using SG.Phoenix.Assets.Code.InputManagement;
using System;
using UnhollowerRuntimeLib;
using MelonLoader;

namespace IEYTD2_SubmarineCode
{
    public class LooseWire : MonoBehaviour
    {
        public LooseWire(IntPtr ptr) : base(ptr) { }
        public LooseWire() : base(ClassInjector.DerivedConstructorPointer<LooseWire>())
            => ClassInjector.DerivedConstructorBody(this);

        public Transform[] nodes;
        public Transform topAnchor;
        public Transform controllerTarget;

        public float ropeLength = 0f;

        public float slackFraction = 0.1f;
        public float heldSlackBonusFraction = 1f;

        public float sagAmount = 0.25f;
        public Vector3 sagDirection = new Vector3(0f, -1f, 0f);
        public float followSpeed = 10f;

        public bool follow = false;
        public float limpDropFraction = 1.0f;
        public float limpSagMultiplier = 1.3f;
        public float limpFallSpeed = 30f;

        public WireTubeRenderer visual;
        public string Color;
        public WireClamp clamp;
        public bool isClamped = false;

        private Vector3 smoothedBottom;
        private bool initialized = false;

        private Transform[] clampPoints = null;

        public LooseWireHitBox hitBox;
        public bool _rightHandIn = false;
        public bool _leftHandIn = false;

        private GameObject rightHandRoot;
        private GameObject leftHandRoot;

        private VRHandInput rightHandInput;
        private VRHandInput leftHandInput;

        private bool lastRightPressed;
        private bool lastLeftPressed;

        private enum HeldHand { None, Right, Left }
        private HeldHand heldBy = HeldHand.None;

        public WireManager wm;

        void Start()
        {
            InitializeFromTransforms();

            var hitBoxObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hitBoxObj.transform.parent = transform;
            hitBoxObj.GetComponent<MeshRenderer>().enabled = false;
            hitBoxObj.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            hitBoxObj.transform.localPosition = Vector3.zero;

            var col = hitBoxObj.GetComponent<SphereCollider>();
            col.isTrigger = true;

            hitBox = hitBoxObj.AddComponent<LooseWireHitBox>();
            hitBox.wire = this;
        }

        void OnEnable()
        {
            rightHandRoot = GameObject.Find("RightHandRoot");
            leftHandRoot = GameObject.Find("LeftHandRoot");

            rightHandInput = rightHandRoot.GetComponent<VRHandInput>();
            leftHandInput = leftHandRoot.GetComponent<VRHandInput>();

            lastRightPressed = rightHandInput.IsAnyInputPressed();
            lastLeftPressed = leftHandInput.IsAnyInputPressed();
        }

        void Update()
        {
            hitBox.transform.position = nodes[nodes.Length - 1].position;

            bool rightPressed = rightHandInput.IsAnyInputPressed();
            bool leftPressed = leftHandInput.IsAnyInputPressed();

            bool rightJustPressed = rightPressed && !lastRightPressed;
            bool leftJustPressed = leftPressed && !lastLeftPressed;

            if (!follow)
            {
                if (_rightHandIn && rightJustPressed && !wm.rightHandCarrying)
                    BeginGrab(HeldHand.Right);

                if (_leftHandIn && leftJustPressed && !wm.leftHandCarrying)
                    BeginGrab(HeldHand.Left);
            }
            else
            {
                if (heldBy == HeldHand.Right && !rightPressed)
                    EndGrab(HeldHand.Right);

                if (heldBy == HeldHand.Left && !leftPressed)
                    EndGrab(HeldHand.Left);
            }

            lastRightPressed = rightPressed;
            lastLeftPressed = leftPressed;
        }

        private void BeginGrab(HeldHand hand)
        {
            if (isClamped) UnClampWire();

            heldBy = hand;

            if (hand == HeldHand.Right)
            {
                wm.rightHandCarrying = true;
                controllerTarget = rightHandRoot.transform;
            }
            else
            {
                wm.leftHandCarrying = true;
                controllerTarget = leftHandRoot.transform;
            }

            StartFollow();
        }

        private void EndGrab(HeldHand hand)
        {
            if (hand == HeldHand.Right) wm.rightHandCarrying = false;
            else wm.leftHandCarrying = false;

            heldBy = HeldHand.None;
            StopFollow();
        }

        public void StartFollow()
        {
            follow = true;
            isClamped = false;
            clampPoints = null;
        }

        public void StopFollow()
        {
            follow = false;
        }

        void InitializeFromTransforms()
        {
            initialized = false;

            if (topAnchor == null)
                topAnchor = nodes[0];

            if (ropeLength <= 0f)
            {
                float total = 0f;
                for (int i = 1; i < nodes.Length; i++)
                    total += Vector3.Distance(nodes[i - 1].position, nodes[i].position);

                ropeLength = total;
            }

            smoothedBottom = nodes[nodes.Length - 1].position;
            initialized = true;
        }

        void FixedUpdate()
        {
            if (!initialized) return;

            Vector3 topPos = topAnchor.position;

            Vector3 rawTarget;

            if (isClamped)
            {
                rawTarget = clampPoints[1].position;
            }
            else if (follow)
            {
                rawTarget = controllerTarget.position;
            }
            else
            {
                Vector3 downDir = sagDirection.normalized;
                Vector3 hangPos = topPos + downDir * (ropeLength * limpDropFraction);

                float percent = 1f - Mathf.Exp(-limpFallSpeed * Time.fixedDeltaTime);
                rawTarget = Vector3.Lerp(smoothedBottom, hangPos, percent);
            }

            Vector3 offset = rawTarget - topPos;
            float distance = offset.magnitude;

            Vector3 desiredBottom = rawTarget;

            float slack = slackFraction;

            if (follow && !isClamped)
            {
                slack += heldSlackBonusFraction;

                float maxReach = ropeLength * (1f + slack);
                if (distance > maxReach)
                    desiredBottom = topPos + offset.normalized * maxReach;
            }

            float followPercent = 1f - Mathf.Exp(-followSpeed * Time.fixedDeltaTime);
            smoothedBottom = Vector3.Lerp(smoothedBottom, desiredBottom, followPercent);

            Vector3 sagDir = sagDirection.normalized;

            float dynamicSag;

            if (follow && !isClamped)
            {
                float maxReach = ropeLength * (1f + slack);
                float tightness = Mathf.Clamp01(distance / maxReach);
                float sagFactor = 1f - Mathf.Pow(tightness, 4f);
                dynamicSag = sagAmount * sagFactor;
            }
            else
            {
                dynamicSag = sagAmount * limpSagMultiplier;
            }

            Vector3 span = smoothedBottom - topPos;
            Vector3 horiz = Vector3.ProjectOnPlane(span, sagDir);
            float horizRatio = Mathf.Clamp01(horiz.magnitude / ropeLength);
            dynamicSag *= horizRatio;

            Vector3 mid = (topPos + smoothedBottom) * 0.5f + (sagDir * dynamicSag);

            int count = nodes.Length;
            for (int i = 0; i < count; i++)
            {
                float percent = (count == 1) ? 0f : (float)i / (count - 1);
                nodes[i].position = QuadraticBezier(topPos, mid, smoothedBottom, percent);
            }

            if (isClamped && nodes.Length >= 3)
            {
                int last = nodes.Length - 1;
                nodes[last - 2].position = clampPoints[0].position;
                nodes[last - 1].position = clampPoints[1].position;
                nodes[last].position = clampPoints[2].position;
            }
        }

        private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float percent)
        {
            float inv = 1f - percent;
            return inv * inv * a
                 + 2f * inv * percent * b
                 + percent * percent * c;
        }

        public void clampWire(Transform[] points)
        {
            StopFollow();
            isClamped = true;
            clampPoints = points;
            AudioUtil.PlayAt("buttonPress_01", transform.position, 0.6f);

            smoothedBottom = points[1].position;
        }

        public void UnClampWire()
        {
            isClamped = false;
            clampPoints = null;

            if (clamp.slot1 == this) clamp.slot1 = null;
            else if (clamp.slot2 == this) clamp.slot2 = null;

            clamp = null;
        }
    }

    public class LooseWireHitBox : MonoBehaviour
    {
        public LooseWireHitBox(IntPtr ptr) : base(ptr) { }
        public LooseWireHitBox() : base(ClassInjector.DerivedConstructorPointer<LooseWireHitBox>())
            => ClassInjector.DerivedConstructorBody(this);

        public LooseWire wire;

        void OnEnable()
        {
            gameObject.AddComponent<Rigidbody>().isKinematic = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.name.ToLower().Contains("handgb")) return;

            if (other.name.Contains("Left")) wire._leftHandIn = true;
            else wire._rightHandIn = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.name.ToLower().Contains("handgb")) return;

            if (other.name.Contains("Left")) wire._leftHandIn = false;
            else wire._rightHandIn = false;
        }
    }
}
