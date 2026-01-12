using System;
using UnhollowerRuntimeLib;
using UnityEngine;
using SG.Phoenix.Assets.Code.InputManagement;

namespace IEYTD2_SubmarineCode
{
  /*
      THIS IS A HELPER SCRIPT USED IN DEBUGGING - NOT USED IN FINAL GAME
      Used to determine the orientation that the Panel Chips should be aligned in, since it did not follow traditional rotation conventions
  */
    public class ChipOrientationHelper : MonoBehaviour
    {
        public ChipOrientationHelper(IntPtr ptr) : base(ptr) { }
        public ChipOrientationHelper()
            : base(ClassInjector.DerivedConstructorPointer<ChipOrientationHelper>())
            => ClassInjector.DerivedConstructorBody(this);

        private GameObject leftHandRoot;
        private GameObject rightHandRoot;
        private VRHandInput leftHandInput;
        private VRHandInput rightHandInput;

        private GameObject leftHandMesh;
        private GameObject rightHandMesh;

        private bool leftHandInside;
        private bool rightHandInside;

        private bool isGrabbed;
        private bool grabbedByRightHand;
        private Transform grabbingHand;

        private Vector3 handLocalPositionOffset;
        private Quaternion handLocalRotationOffset;

        private void OnEnable()
        {
            leftHandRoot = GameObject.Find("LeftHandRoot");
            rightHandRoot = GameObject.Find("RightHandRoot");

            if (leftHandRoot != null) leftHandInput = leftHandRoot.GetComponent<VRHandInput>();
            if (rightHandRoot != null) rightHandInput = rightHandRoot.GetComponent<VRHandInput>();

            EnsureHandTrigger("LeftHand");
            EnsureHandTrigger("RightHand");

            leftHandMesh = GameObject.Find("Shared_Hand_L_01");
            rightHandMesh = GameObject.Find("Shared_Hand_R_01");

            var chipCollider = GetComponent<Collider>() ?? gameObject.AddComponent<BoxCollider>();
            chipCollider.isTrigger = false;
        }

        private void Update()
        {
            bool rightPressed = rightHandInput != null && rightHandInput.IsAnyInputPressed();
            bool leftPressed = leftHandInput != null && leftHandInput.IsAnyInputPressed();

            if (!isGrabbed)
            {
                if (rightPressed && rightHandInside) StartGrab(true);
                else if (leftPressed && leftHandInside) StartGrab(false);
            }
            else
            {
                if (grabbedByRightHand && !rightPressed) EndGrab();
                else if (!grabbedByRightHand && !leftPressed) EndGrab();

                if (grabbingHand != null)
                {
                    transform.position = grabbingHand.TransformPoint(handLocalPositionOffset);
                    transform.rotation = grabbingHand.rotation * handLocalRotationOffset;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            string name = other.name;
            if (!name.ToLowerInvariant().Contains("hand")) return;

            if (name.Contains("L")) leftHandInside = true;
            else rightHandInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            string name = other.name;
            if (!name.ToLowerInvariant().Contains("hand")) return;

            if (name.Contains("L")) leftHandInside = false;
            else rightHandInside = false;
        }

        private void StartGrab(bool rightHand)
        {
            isGrabbed = true;
            grabbedByRightHand = rightHand;

            grabbingHand = rightHand
                ? (rightHandRoot != null ? rightHandRoot.transform : null)
                : (leftHandRoot != null ? leftHandRoot.transform : null);

            if (grabbingHand != null)
            {
                handLocalPositionOffset = grabbingHand.InverseTransformPoint(transform.position);
                handLocalRotationOffset = Quaternion.Inverse(grabbingHand.rotation) * transform.rotation;
            }

            SetHandMeshesVisible(false);
        }

        private void EndGrab()
        {
            isGrabbed = false;
            grabbingHand = null;
            SetHandMeshesVisible(true);
        }

        private void SetHandMeshesVisible(bool visible)
        {
            if (grabbedByRightHand)
            {
                if (rightHandMesh) rightHandMesh.SetActive(visible);
                if (leftHandMesh) leftHandMesh.SetActive(true);
            }
            else
            {
                if (leftHandMesh) leftHandMesh.SetActive(visible);
                if (rightHandMesh) rightHandMesh.SetActive(true);
            }
        }

        private void EnsureHandTrigger(string handObjectName)
        {
            var hand = GameObject.Find(handObjectName);
            if (!hand) return;

            var col = hand.GetComponent<BoxCollider>() ?? hand.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.1f, 0.1f, 0.1f);
            col.enabled = true;
        }
    }
}
