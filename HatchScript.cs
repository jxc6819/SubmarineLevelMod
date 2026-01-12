using MelonLoader;
using SG.Phoenix.Assets.Code.Interactables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IEYTD2_SubmarineCode
{
    public class HatchScript : MonoBehaviour
    {
        public HatchScript(IntPtr ptr) : base(ptr) { }
        public HatchScript()
            : base(ClassInjector.DerivedConstructorPointer<HatchScript>())
            => ClassInjector.DerivedConstructorBody(this);

        RotationalMotion rm = null;
        bool opened = false;

        public void Unlock()
        {
            rm = gameObject.GetComponent<RotationalMotion>();
            if (rm == null) rm = gameObject.AddComponent<RotationalMotion>();
            AudioUtil.PlayAt("lever_metal_thin", transform.position);
        }

        public void Update()
        {
            if (rm != null && opened == false)
            {
                if(rm.isHeld)
                {
                    opened = true;
                    OnOpen();
                }
            }
        }

        public void OnOpen()
        {
            MelonLogger.Msg("OnOpen");
            MelonCoroutines.Start(OpenSequence());
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator OpenSequence()
        {
            MelonLogger.Msg("[OpenSequence] - Started");
          //  rm.SetRotation(180f);
            MelonLogger.Msg("[OpenSequence] - Rotated");
            AudioUtil.PlayAt("valve_turn_01", transform.position);
            MelonLogger.Msg("[OpenSequence] - Sound played");
            yield return new WaitForSeconds(0.7f);
            SceneManager.LoadSceneAsync("WinRoom", LoadSceneMode.Single);
            MelonLogger.Msg("[OpenSequence] - Done");

        }



    }
}
