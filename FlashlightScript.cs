using System;
using UnityEngine;
using UnhollowerRuntimeLib;
using SG.Phoenix.Assets.Code.Interactables;
using MelonLoader;

namespace IEYTD2_SubmarineCode
{
    public class FlashlightScript : MonoBehaviour
    {
        public FlashlightScript(IntPtr ptr) : base(ptr) { }
        public FlashlightScript()
            : base(ClassInjector.DerivedConstructorPointer<FlashlightScript>())
            => ClassInjector.DerivedConstructorBody(this);

        private PickUp pickup;
        private Light beam;

        private bool isOn = false;
        private bool wasHeld = false;

        public string rightTriggerAxis = "Oculus_CrossPlatform_SecondaryIndexTrigger";
        public string leftTriggerAxis = "Oculus_CrossPlatform_PrimaryIndexTrigger";
        public float triggerThreshold = 0.8f;

        private float prevRight;
        private float prevLeft;
        ObjectBank bank;

        void Awake()
        {
            pickup = GetComponent<PickUp>();
            SetFlashlight(false);
        }

        void Start()
        {
            bank = ObjectBank.Instance;
        }

        void Update()
        {
            if (pickup == null) return;

            bool isHeld = pickup.isHeld;

            if (!isHeld && wasHeld)
            {
                SetFlashlight(false);
            }

            if (isHeld && GetAnyTriggerJustPressed())
            {
                SetFlashlight(!isOn);
            }

            if (isOn)
            {
                if (!beam.enabled) beam.enabled = true;
                if (!beam.gameObject.activeSelf) beam.gameObject.SetActive(true);
            }

            wasHeld = isHeld;
        }

        private void SetFlashlight(bool on)
        {
            if (on)
            {
                MelonLogger.Msg("[Flashlight] - Flashlight toggled");
                GameObject light = bank.P_Shop_INT_Flashlight.transform.GetChild(3).gameObject;
                light.SetActive(true);
                MelonLogger.Msg("[Flashlight] - Light enabled");
                MelonLogger.Msg("[Flashlight] - Name: " + light.name);
                light.GetComponent<Light>().intensity = 2f;
                MelonLogger.Msg("[Flashlight] - Light intensity changed");
            }
        }

        
        private bool GetAnyTriggerJustPressed()
        {
            float r = 0f, l = 0f;
            try { r = Input.GetAxis(rightTriggerAxis); } catch { }
            try { l = Input.GetAxis(leftTriggerAxis); } catch { }

            bool rNow = r > triggerThreshold;
            bool lNow = l > triggerThreshold;
            bool rPrev = prevRight > triggerThreshold;
            bool lPrev = prevLeft > triggerThreshold;

            prevRight = r;
            prevLeft = l;

            return (rNow && !rPrev) || (lNow && !lPrev);
        }
    }
}
