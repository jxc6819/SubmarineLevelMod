using System.Diagnostics.SymbolStore;
using UnityEngine;
using System.Collections;
using UnhollowerRuntimeLib;
using System;

namespace IEYTD2_SubmarineCode
{
    public class LeverScript : MonoBehaviour
    {
        public LeverScript(IntPtr ptr) : base(ptr) { }
        public LeverScript() : base(ClassInjector.DerivedConstructorPointer<LeverScript>())
            => ClassInjector.DerivedConstructorBody(this);

        public float lockAfterDelta = 130f;
        private float startX;
        private Rigidbody rb;
        public float resistUpTorque = 30f;


        void Start()
        {
            startX = transform.localEulerAngles.x;
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            //transform.position = pos;
            float currentX = transform.localEulerAngles.x;
            float delta = Mathf.DeltaAngle(startX, currentX);


            //if (delta < 0)
            //{
            //    transform.localEulerAngles = new Vector3(startX, 0, 0);
            //}
            if (delta < 0f && rb != null)
            {
                Vector3 axis = transform.TransformDirection(Vector3.right);
                rb.AddTorque(axis * resistUpTorque, ForceMode.Acceleration);
                return;
            }

            if (delta >= lockAfterDelta)
            {
                LockLever();
            }


        }

        public void LockLever()
        {
            GameObject.Find("Manager").GetComponent<SubmarineLevelLogic>().coolantSabotaged();
            AudioUtil.PlayAt("lever_metal_thin", transform.position, 10f);
            GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            Destroy(this);
        }
    }
}
