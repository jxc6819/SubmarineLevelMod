using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MelonLoader;
using UnhollowerRuntimeLib;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD2_SubmarineCode
{
    public class reactorVentTriggerDetector : MonoBehaviour
    {
        public reactorVentTriggerDetector(IntPtr ptr) : base(ptr) { }
        public reactorVentTriggerDetector() : base(ClassInjector.DerivedConstructorPointer<reactorVentTriggerDetector>())
            => ClassInjector.DerivedConstructorBody(this);

        ObjectBank bank;

        public void Start()
        {
            bank = ObjectBank.Instance;
        }

        public void OnTriggerEnter(Collider other)
        {
            if (other.name == "Sponge")
            {
                other.gameObject.transform.parent.GetComponent<PickUp>().ForceRelease();
                Destroy(other.gameObject);
                bank.VentSponge.SetActive(true);
                bank.VentSponge.GetComponent<MeshRenderer>().enabled = true;
                //bank.VentSponge.transform.parent = transform.parent;
                bank.Manager.GetComponent<SubmarineLevelLogic>().reactorVentSabotaged();

                //GameObject obj = ObjectBank.Sponge;
            }
        }

    }
}
