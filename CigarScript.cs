using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.WorldAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Events;

namespace IEYTD2_SubmarineCode
{
    public class CigarScript : MonoBehaviour
    {
        public CigarScript(IntPtr ptr) : base(ptr) { }
        public CigarScript()
            : base(ClassInjector.DerivedConstructorPointer<CigarScript>())
            => ClassInjector.DerivedConstructorBody(this);


        Flammable flammable = null;
        ObjectBank bank;
        GameObject HMD;
        SubmarineLevelLogic sll;
        PickUp pickUp;
        bool wasHeld;
        public bool inMouth = false;
        public float mouthDist = 0.2f;
        Rigidbody rb;

        void Start()
        {
            flammable = GetComponent<Flammable>();
            flammable._OnStartBurningEvent.AddListener((UnityAction)OnLight);
            bank = ObjectBank.Instance;
            HMD = GameObject.Find("HMD");
            sll = bank.Manager.GetComponent<SubmarineLevelLogic>();
            pickUp = GetComponent<PickUp>();
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if(!wasHeld)
            {
                if (pickUp.isHeld) OnGrab();
            }
            if(wasHeld && !pickUp.isHeld)
            {
                OnRelease();
            }
        }

        void OnRelease()
        {
            wasHeld = false;
            if(Vector3.Distance(transform.position, HMD.transform.position) <= mouthDist)
            {
                OnMouth();
            }
        }

        void OnGrab()
        {
            wasHeld = true;
            rb.constraints = RigidbodyConstraints.None;
        }


        void OnMouth()
        {
            rb.constraints = RigidbodyConstraints.FreezeAll;
            transform.position = HMD.transform.position;
            transform.rotation = HMD.transform.rotation;
            transform.parent = HMD.transform;
            transform.localPosition = new Vector3(0, -0.1f, 0.1f);
            transform.localRotation = Quaternion.Euler(new Vector3(90, 0, 0));
        }

        void OnLight()
        {
            MelonCoroutines.Start(LightSequence());
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator LightSequence()
        {
            yield return new WaitForSeconds(3.5f);
            Explode();
        }

        void Explode()
        {
            createExplosion();
            //player
            //henchman1
            //henchman2
            //fan
            if(inRange(HMD))
            {
                sll.KillPlayer();
            }
            if(inRange(bank.Henchman1))
            {
                bank.Henchman1.GetComponent<HenchmanController>().Kill();
            }
            if(inRange(bank.Henchman2))
            {
                bank.Henchman2.GetComponent<HenchmanController>().Kill();
            }
            if(inRange(bank.Fan))
            {
                bank.FanTrigger.GetComponent<fanTriggerDetector>().disableFan();
            }
            Destroy(gameObject);
        }
        
        void createExplosion()
        {
            GameObject explodeObj = new GameObject("Cigar Explosion");
            explodeObj.transform.position = transform.position;
            ExplosionDriver explosion = explodeObj.AddComponent<ExplosionDriver>();
            explosion.TriggerExplosion();
        }

        bool inRange(GameObject obj)
        {
            float dist = 2f;
            if (Vector3.Distance(transform.position, obj.transform.position) <= dist) return true;
            return false;
        }



    }
}
