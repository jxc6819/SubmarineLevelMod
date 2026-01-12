using System;
using UnityEngine;
using UnhollowerRuntimeLib;
using SG.Phoenix.Assets.Code.InputManagement;

namespace IEYTD2_SubmarineCode
{
    public class StopperScript : MonoBehaviour
    {
        public StopperScript(IntPtr ptr) : base(ptr) { }
        public StopperScript()
            : base(ClassInjector.DerivedConstructorPointer<StopperScript>())
            => ClassInjector.DerivedConstructorBody(this);

        private const float TriggerRadius = 0.05f;
        private const float BreakDistance = 0.08f;

        private Transform stopperTransform;
        private Rigidbody stopperRigidbody;

        private Vector3 socketPosition;
        private Quaternion socketRotation;

        private Transform leftHandTransform;
        private Transform rightHandTransform;
        private VRHandInput leftHandInput;
        private VRHandInput rightHandInput;

        private bool leftHandInside;
        private bool rightHandInside;

        private bool leftPressedLastFrame;
        private bool rightPressedLastFrame;

        private bool isGrabbed;
        public bool popped;

        private Transform grabbingHandTransform;
        private VRHandInput grabbingHandInput;

        private Vector3 grabStartHandPosition;

        private SkinnedMeshRenderer[] cachedSkinnedRenderers;
        private MeshRenderer[] cachedMeshRenderers;
        private bool[] cachedSkinnedEnabledStates;
        private bool[] cachedMeshEnabledStates;

        private ObjectBank bank;

        private GameObject screw1;
        private GameObject screw2;
        private GameObject screw3;
        private GameObject screw4;
        private readonly Screw[] screws = new Screw[4];

        private bool popTriggered;

        private void Awake()
        {
            stopperTransform = transform;
        }

        private void Start()
        {
            bank = ObjectBank.Instance;

            socketPosition = stopperTransform.position;
            socketRotation = stopperTransform.rotation;

            Collider collider = stopperTransform.GetComponent<Collider>();
            if (collider == null)
            {
                SphereCollider sphere = stopperTransform.gameObject.AddComponent<SphereCollider>();
                sphere.radius = TriggerRadius;
                collider = sphere;
            }
            collider.isTrigger = true;

            stopperRigidbody = stopperTransform.GetComponent<Rigidbody>();
            if (stopperRigidbody == null)
                stopperRigidbody = stopperTransform.gameObject.AddComponent<Rigidbody>();

            stopperRigidbody.useGravity = false;
            stopperRigidbody.isKinematic = true;

            leftHandTransform = GameObject.Find("LeftHandRoot").transform;
            rightHandTransform = GameObject.Find("RightHandRoot").transform;

            leftHandInput = leftHandTransform.GetComponent<VRHandInput>();
            rightHandInput = rightHandTransform.GetComponent<VRHandInput>();

            SetUpScrews();
        }

        private void Update()
        {
            bool leftPressedNow = leftHandInput.IsAnyInputPressed();
            bool rightPressedNow = rightHandInput.IsAnyInputPressed();

            if (!isGrabbed)
            {
                TryBeginGrab(leftPressedNow, rightPressedNow);
            }
            else
            {
                HandleGrabbed();
            }

            leftPressedLastFrame = leftPressedNow;
            rightPressedLastFrame = rightPressedNow;
        }

        private void LateUpdate()
        {
            if (!popped)
            {
                stopperTransform.position = socketPosition;
                stopperTransform.rotation = socketRotation;
            }
        }

        private void TryBeginGrab(bool leftPressedNow, bool rightPressedNow)
        {
            if (rightHandInside && rightPressedNow && !rightPressedLastFrame && Unscrewed())
                BeginGrab(rightHandTransform, rightHandInput);
            else if (leftHandInside && leftPressedNow && !leftPressedLastFrame && Unscrewed())
                BeginGrab(leftHandTransform, leftHandInput);
        }

        private void BeginGrab(Transform handTransform, VRHandInput handInput)
        {
            isGrabbed = true;
            grabbingHandTransform = handTransform;
            grabbingHandInput = handInput;

            grabStartHandPosition = handTransform.position;

            CacheAndHideHandMeshes(handTransform);
        }

        private void HandleGrabbed()
        {
            if (!grabbingHandInput.IsAnyInputPressed())
            {
                EndGrab();
                return;
            }

            if (!popped)
            {
                float pullDistance = Vector3.Distance(grabbingHandTransform.position, grabStartHandPosition);

                if (pullDistance >= BreakDistance)
                {
                    popped = true;
                    stopperRigidbody.isKinematic = true;
                    stopperRigidbody.useGravity = false;
                }
                else
                {
                    stopperTransform.position = socketPosition;
                    stopperTransform.rotation = socketRotation;
                }
            }

            if (popped)
            {
                stopperTransform.position = grabbingHandTransform.position;
                stopperTransform.rotation = grabbingHandTransform.rotation;

                if (!popTriggered)
                {
                    popTriggered = true;
                    AudioUtil.PlayAt("spark", transform.position);
                    bank.Manager.GetComponent<SubmarineLevelLogic>().terminalStopperSabotaged();
                }
            }
        }

        private void EndGrab()
        {
            isGrabbed = false;

            RestoreHandMeshes();

            if (popped)
            {
                stopperRigidbody.isKinematic = false;
                stopperRigidbody.useGravity = true;
            }

            grabbingHandTransform = null;
            grabbingHandInput = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.transform.IsChildOf(leftHandTransform)) leftHandInside = true;
            if (other.transform.IsChildOf(rightHandTransform)) rightHandInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform.IsChildOf(leftHandTransform)) leftHandInside = false;
            if (other.transform.IsChildOf(rightHandTransform)) rightHandInside = false;
        }

        private void CacheAndHideHandMeshes(Transform handTransform)
        {
            cachedSkinnedRenderers = handTransform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            cachedMeshRenderers = handTransform.GetComponentsInChildren<MeshRenderer>(true);

            cachedSkinnedEnabledStates = new bool[cachedSkinnedRenderers.Length];
            for (int i = 0; i < cachedSkinnedRenderers.Length; i++)
            {
                cachedSkinnedEnabledStates[i] = cachedSkinnedRenderers[i].enabled;
                cachedSkinnedRenderers[i].enabled = false;
            }

            cachedMeshEnabledStates = new bool[cachedMeshRenderers.Length];
            for (int i = 0; i < cachedMeshRenderers.Length; i++)
            {
                cachedMeshEnabledStates[i] = cachedMeshRenderers[i].enabled;
                cachedMeshRenderers[i].enabled = false;
            }
        }

        private void RestoreHandMeshes()
        {
            for (int i = 0; i < cachedSkinnedRenderers.Length; i++)
                cachedSkinnedRenderers[i].enabled = cachedSkinnedEnabledStates[i];

            for (int i = 0; i < cachedMeshRenderers.Length; i++)
                cachedMeshRenderers[i].enabled = cachedMeshEnabledStates[i];

            cachedSkinnedRenderers = null;
            cachedMeshRenderers = null;
            cachedSkinnedEnabledStates = null;
            cachedMeshEnabledStates = null;
        }

        private void SetUpScrews()
        {
            screw1 = Instantiate(bank.ELV_Screw_Variant); screw1.name = "Stopper_Screw1";
            screw2 = Instantiate(bank.ELV_Screw_Variant); screw2.name = "Stopper_Screw2";
            screw3 = Instantiate(bank.ELV_Screw_Variant); screw3.name = "Stopper_Screw3";
            screw4 = Instantiate(bank.ELV_Screw_Variant); screw4.name = "Stopper_Screw4";

            GameObject[] screwObjects = new GameObject[] { screw1, screw2, screw3, screw4 };

            screws[0] = screw1.GetComponent<Screw>();
            screws[1] = screw2.GetComponent<Screw>();
            screws[2] = screw3.GetComponent<Screw>();
            screws[3] = screw4.GetComponent<Screw>();

            Transform socketsRoot = transform;

            for (int i = 0; i < socketsRoot.childCount; i++)
            {
                Transform socketTransform = socketsRoot.GetChild(i);
                GameObject screwObject = screwObjects[i];

                screwObject.transform.position = socketTransform.position;
                screwObject.transform.parent = socketTransform;
                screwObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                screwObject.transform.localPosition = new Vector3(0f, -1f, 0f);

                Vector3 localScale = screwObject.transform.localScale;
                screwObject.transform.localScale = localScale * 0.6f;

                screwObject.SetActive(true);
            }
        }

        private bool Unscrewed()
        {
            for (int i = 0; i < screws.Length; i++)
            {
                if (!screws[i].UnScrewed)
                    return false;
            }
            return true;
        }
    }
}
