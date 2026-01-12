using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MelonLoader;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public class fanTriggerDetector : MonoBehaviour
    {
        public fanTriggerDetector(IntPtr ptr) : base(ptr) { }
        public fanTriggerDetector() : base(ClassInjector.DerivedConstructorPointer<fanTriggerDetector>())
            => ClassInjector.DerivedConstructorBody(this);

        ObjectBank bank;
        bool triggered = false;
        void Start()
        {
            bank = ObjectBank.Instance;
        }
        public void OnCollisionEnter(Collision other)
        {
            MelonLogger.Msg("[fanTriggerDetector] - COLLISION: " + other.gameObject.name);
            if ((other.gameObject.name == "fanPipeTrigger" || other.gameObject.name == "PickUp_HOST_ReactorPipeGrabbable" || other.gameObject.name == "PickUp_HOST_PickUpTest") && !triggered)
            {
                triggered = true;
                other.gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                other.gameObject.GetComponent<Rigidbody>().rotation = Quaternion.Euler(0, 0, 0);
                other.gameObject.GetComponent<Rigidbody>().isKinematic = true;
                other.transform.position = new Vector3(transform.position.x+1, other.transform.position.y, other.transform.position.z);
                bank.Fan.GetComponent<ExplosionDriver>().TriggerExplosion();
                bank.Fan.GetComponent<SmokeDriver>().emitting = true;
                Invoke("disableCollider", 4.0f);
                GameObject.Find("Manager").GetComponent<SubmarineLevelLogic>().fanSabotaged();
            }
        }

        void disableCollider()
        {
            GetComponent<BoxCollider>().enabled = false;
            bank.Fan.GetComponent<SmokeDriver>().emitting = false;
        }

        public void disableFan()
        {
            triggered = true;
           // other.gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
           //other.gameObject.GetComponent<Rigidbody>().rotation = Quaternion.Euler(0, 0, 0);
          //  other.gameObject.GetComponent<Rigidbody>().isKinematic = true;
          //  other.transform.position = new Vector3(transform.position.x + 1, other.transform.position.y, other.transform.position.z);
            bank.Fan.GetComponent<ExplosionDriver>().TriggerExplosion();
            bank.Fan.GetComponent<SmokeDriver>().emitting = true;
            Invoke("disableCollider", 4.0f);
            GameObject.Find("Manager").GetComponent<SubmarineLevelLogic>().fanSabotaged();
        }
    }
}
