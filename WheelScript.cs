using System.Collections.Generic;
using UnityEngine;
using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using SG.Phoenix.Assets.Code.InputManagement;

namespace IEYTD2_SubmarineCode
{
    public class WheelScript : MonoBehaviour
    {
        public WheelScript(IntPtr ptr) : base(ptr) { }
        public WheelScript() : base(ClassInjector.DerivedConstructorPointer<WheelScript>())
            => ClassInjector.DerivedConstructorBody(this);

        bool rightHandEnter = false;
        bool leftHandEnter = false;
        bool wheelGrabbed = false;
        string grabHand = "";

        VRHandInput rightHandInput;
        VRHandInput leftHandInput;
        GameObject leftHandRoot;
        GameObject rightHandRoot;

        bool wheelLocked = false;

        bool rightGripPrev = false;
        bool leftGripPrev = false;

        public enum Axis { X, Y, Z }
        public Axis fallbackAxis = Axis.Z;
        public bool invert = false;
        public float rotationSensitivity = 1f;

        public bool useCustomWorldAxis = false;
        public Vector3 customWorldAxis = new Vector3(0, 1, 0);

        Transform activeHand;
        Quaternion wheelRotAtGrab;
        Vector3 initialDir;
        Vector3 worldAxis;

        Transform pivot;
        Rigidbody pivotRB;

        GameObject lHandMesh, rHandMesh;

        NeedleScript needle;
        float lastAngle = 0f;
        public float wheelResponse = 0.65f;
        public float wheelFollowLerp = 7f;
        public float needleGain = 0.15f;

        Vector3 prevDir;
        float totalAngle = 0f;

        public void OnEnable()
        {
            leftHandRoot = GameObject.Find("LeftHandRoot");
            rightHandRoot = GameObject.Find("RightHandRoot");
            rightHandInput = rightHandRoot.GetComponent<VRHandInput>();
            leftHandInput = leftHandRoot.GetComponent<VRHandInput>();

            var leftHand = GameObject.Find("LeftHand");
            var rightHand = GameObject.Find("RightHand");
            var leftCol = leftHand.GetComponent<BoxCollider>() ?? leftHand.AddComponent<BoxCollider>();
            var rightCol = rightHand.GetComponent<BoxCollider>() ?? rightHand.AddComponent<BoxCollider>();
            leftCol.isTrigger = true; rightCol.isTrigger = true;
            leftCol.size = new Vector3(0.1f, 0.1f, 0.1f);
            rightCol.size = new Vector3(0.1f, 0.1f, 0.1f);
            leftCol.enabled = true; rightCol.enabled = true;

            lHandMesh = GameObject.Find("Shared_Hand_L_01");
            rHandMesh = GameObject.Find("Shared_Hand_R_01");

            var nGo = GameObject.Find("SM_needle");
            if (nGo) needle = nGo.GetComponent<NeedleScript>();

            ensurePivot();
        }

        void ensurePivot()
        {
            if (pivot) return;

            var oldRB = GetComponent<Rigidbody>();
            var oldPos = transform.position;

            Vector3 spinAxis;
            if (useCustomWorldAxis && customWorldAxis != Vector3.zero) spinAxis = customWorldAxis.normalized;
            else
            {
                switch (fallbackAxis)
                {
                    case Axis.X: spinAxis = transform.right; break;
                    case Axis.Y: spinAxis = transform.up; break;
                    default: spinAxis = transform.forward; break;
                }
                spinAxis = spinAxis.normalized;
            }
            var up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(up, spinAxis)) > 0.98f) up = Vector3.right;

            var pgo = new GameObject($"{name}_Pivot_RT");
            pgo.transform.position = oldPos;
            pgo.transform.rotation = Quaternion.LookRotation(spinAxis, up);
            pivot = pgo.transform;

            var parent = transform.parent;
            transform.SetParent(pivot, true);
            pivot.SetParent(parent, true);

            if (oldRB)
            {
                pivotRB = pgo.AddComponent<Rigidbody>();
                pivotRB.useGravity = false;
                pivotRB.isKinematic = oldRB.isKinematic;
                pivotRB.interpolation = oldRB.interpolation;
                pivotRB.collisionDetectionMode = oldRB.collisionDetectionMode;
                pivotRB.mass = oldRB.mass;
                pivotRB.drag = oldRB.drag;
                pivotRB.angularDrag = oldRB.angularDrag;

                var c = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ |
                        RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                switch (fallbackAxis)
                {
                    case Axis.X: c &= ~RigidbodyConstraints.FreezeRotationX; break;
                    case Axis.Y: c &= ~RigidbodyConstraints.FreezeRotationY; break;
                    default: c &= ~RigidbodyConstraints.FreezeRotationZ; break;
                }
                pivotRB.constraints = c;

                Destroy(oldRB);
            }
            else
            {
                pivotRB = pgo.AddComponent<Rigidbody>();
                pivotRB.useGravity = false;
                pivotRB.isKinematic = false;
                pivotRB.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ |
                                       RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                switch (fallbackAxis)
                {
                    case Axis.X: pivotRB.constraints &= ~RigidbodyConstraints.FreezeRotationX; break;
                    case Axis.Y: pivotRB.constraints &= ~RigidbodyConstraints.FreezeRotationY; break;
                    default: pivotRB.constraints &= ~RigidbodyConstraints.FreezeRotationZ; break;
                }
            }
            useCustomWorldAxis = true;
        }

        public void Update()
        {
            if (rightHandInput == null || leftHandInput == null)
                return;

            bool rightPressed = rightHandInput.IsAnyInputPressed();
            bool leftPressed = leftHandInput.IsAnyInputPressed();

            bool rightDown = rightPressed && !rightGripPrev;
            bool leftDown = leftPressed && !leftGripPrev;
            bool rightUp = !rightPressed && rightGripPrev;
            bool leftUp = !leftPressed && leftGripPrev;


            if (!wheelLocked)
            {
                if (rightDown && rightHandEnter && !wheelGrabbed)
                    gripPressed("right");

                if (leftDown && leftHandEnter && !wheelGrabbed)
                    gripPressed("left");
            }

            if (wheelGrabbed)
            {
                if ((grabHand == "right" && rightUp) ||
                    (grabHand == "left" && leftUp) ||
                    wheelLocked)
                {
                    releaseWheel();
                }
            }

            if (wheelGrabbed && activeHand != null && !wheelLocked)
                rotateWhileHeld();

            rightGripPrev = rightPressed;
            leftGripPrev = leftPressed;
        }

        void OnTriggerEnter(Collider other)
        {
            var n = other.name.ToLower();
            if (n.Contains("hand"))
            {
                if (other.name.Contains("L")) leftHandEnter = true;
                else rightHandEnter = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            var n = other.name.ToLower();
            if (n.Contains("hand"))
            {
                if (other.name.Contains("L")) leftHandEnter = false;
                else rightHandEnter = false;
            }
        }

        void gripPressed(string hand)
        {
            if (wheelLocked) return; 

            if (hand == "right")
            {
                if (rightHandEnter && !wheelGrabbed)
                {
                    grabHand = "right";
                    grabWheel();
                }
            }
            else
            {
                if (leftHandEnter && !wheelGrabbed)
                {
                    grabHand = "left";
                    grabWheel();
                }
            }
        }

        void grabWheel()
        {
            wheelGrabbed = true;
            MelonLoader.MelonLogger.Msg($"wheel grabbed with {grabHand} hand");
            toggleHand(false);

            activeHand = (grabHand == "right") ? rightHandRoot.transform : leftHandRoot.transform;
            wheelRotAtGrab = pivot ? pivot.rotation : transform.rotation;

            if (useCustomWorldAxis && customWorldAxis != Vector3.zero) worldAxis = customWorldAxis.normalized;
            else
            {
                switch (fallbackAxis)
                {
                    case Axis.X: worldAxis = (pivot ? pivot.right : transform.right); break;
                    case Axis.Y: worldAxis = (pivot ? pivot.up : transform.up); break;
                    default: worldAxis = (pivot ? pivot.forward : transform.forward); break;
                }
            }

            var toHand = activeHand.position - (pivot ? pivot.position : transform.position);
            initialDir = Vector3.ProjectOnPlane(toHand, worldAxis).normalized;
            if (initialDir.sqrMagnitude < 1e-6f)
            {
                var v = Vector3.Cross(worldAxis, Vector3.up);
                if (v.sqrMagnitude < 1e-6f) v = Vector3.Cross(worldAxis, Vector3.right);
                initialDir = v.normalized;
            }

            prevDir = initialDir;
            totalAngle = 0f;
            lastAngle = 0f;
        }

        void releaseWheel()
        {
            MelonLoader.MelonLogger.Msg("wheel released");
            wheelGrabbed = false;
            toggleHand(true);
            grabHand = "";
            activeHand = null;
            if (pivotRB && !pivotRB.isKinematic) pivotRB.angularVelocity = Vector3.zero;
        }

        void toggleHand(bool toggle)
        {
            if (grabHand == "right")
            {
                if (rHandMesh) rHandMesh.SetActive(toggle);
                if (lHandMesh) lHandMesh.SetActive(true);
            }
            else if (grabHand == "left")
            {
                if (lHandMesh) lHandMesh.SetActive(toggle);
                if (rHandMesh) rHandMesh.SetActive(true);
            }
            else
            {
                if (rHandMesh) rHandMesh.SetActive(true);
                if (lHandMesh) lHandMesh.SetActive(true);
            }
        }

        void rotateWhileHeld()
        {
            var center = pivot ? pivot.position : transform.position;
            var toHand = activeHand.position - center;
            var curr = Vector3.ProjectOnPlane(toHand, worldAxis).normalized;
            if (curr.sqrMagnitude < 1e-6f) return;

            float angleDelta = Vector3.SignedAngle(prevDir, curr, worldAxis);
            if (invert) angleDelta = -angleDelta;
            angleDelta *= rotationSensitivity;

            totalAngle += angleDelta;
            prevDir = curr;

            float appliedAngle = totalAngle * wheelResponse;

            var curRot = pivot ? pivot.rotation : transform.rotation;
            var tgtRot = wheelRotAtGrab * Quaternion.AngleAxis(appliedAngle, worldAxis);
            var newRot = Quaternion.Slerp(curRot, tgtRot, Time.deltaTime * wheelFollowLerp);

            if (pivotRB && !pivotRB.isKinematic) pivotRB.MoveRotation(newRot);
            else if (pivot) pivot.rotation = newRot;
            else transform.rotation = newRot;

            if (needle != null)
            {
                float step = (angleDelta / 360f) * needleGain;
                needle.value01 = Mathf.Clamp01(needle.value01 + step);
            }

            lastAngle = appliedAngle;
        }

        public void SetLocked(bool locked)
        {
            if (wheelLocked == locked) return;

            wheelLocked = locked;

            if (wheelLocked && wheelGrabbed)
            {
                releaseWheel();
            }
        }
    }
}
