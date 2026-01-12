using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class SolderListener : MonoBehaviour
    {
        public SolderListener(IntPtr ptr) : base(ptr) { }
        public SolderListener() : base(ClassInjector.DerivedConstructorPointer<WireCutterListener>())
            => ClassInjector.DerivedConstructorBody(this);

        public PickUp SolderGun;
        public SolderHitBox solderHitBox;
        SparkDriver sparkDriver;

        void Start()
        {
            SolderGun = GetComponent<PickUp>();
            SolderGun._OnUseEvent.AddListener((UnityEngine.Events.UnityAction)OnUsed);

            GameObject hitBoxObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hitBoxObj.transform.parent = gameObject.transform;
            if (hitBoxObj.GetComponent<MeshRenderer>() == null) hitBoxObj.AddComponent<MeshRenderer>();
            hitBoxObj.GetComponent<MeshRenderer>().enabled = false;
            hitBoxObj.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);
            hitBoxObj.transform.localPosition = new Vector3(0f, 0.07f, 0.24f);

            SphereCollider col = hitBoxObj.GetComponent<SphereCollider>();
            if (col == null) col = hitBoxObj.AddComponent<SphereCollider>();
            col.isTrigger = true;

            solderHitBox = hitBoxObj.AddComponent<SolderHitBox>();
            sparkDriver = hitBoxObj.AddComponent<SparkDriver>();
        }

        void OnUsed()
        {
            WireClamp clamp = solderHitBox.clamp;
            if (clamp == null) return;
            clamp.Solder();
        }

        public void playSpark()
        {
            MelonLogger.Msg("[Sparky] - Solder played spark");
            sparkDriver.TriggerBurst();
        }



    }

    public class SolderHitBox : MonoBehaviour
    {
        public SolderHitBox(IntPtr ptr) : base(ptr) { }
        public SolderHitBox() : base(ClassInjector.DerivedConstructorPointer<SolderHitBox>())
            => ClassInjector.DerivedConstructorBody(this);

        public WireClamp clamp = null;

        void OnTriggerEnter(Collider other)
        {
            WireClamp _clamp = other.gameObject.GetComponent<WireClamp>();
            if (_clamp != null)
            {
                clamp = _clamp;
            }
        }

        void OnTriggerExit(Collider other)
        {
            WireClamp _clamp = other.gameObject.GetComponent<WireClamp>();
            if(_clamp != null)
            {
                clamp = null;
            }
        }

    }
}
