using System;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class ObjectBank : MonoBehaviour
    {
        public ObjectBank(IntPtr ptr) : base(ptr) { }
        public ObjectBank() : base(ClassInjector.DerivedConstructorPointer<ObjectBank>())
            => ClassInjector.DerivedConstructorBody(this);

        public static ObjectBank Instance { get; private set; }

        public GameObject Manager;
        public GameObject VRRig;

        public GameObject Sponge;
        public GameObject VentSponge;
        public GameObject GrateCollider;
        public GameObject FanTrigger;
        public GameObject ReactorVentTrigger;
        public GameObject ReactorPipeGrabbable;

        public GameObject Needle;
        public GameObject WheelTerminal;
        public GameObject Fan;

        public GameObject CoolantPlane;
        public GameObject CoolantNozzle;

        public GameObject ReactorFlame1;
        public GameObject ReactorFlame2;
        public GameObject ReactorFlame3;
        public GameObject Reactor;

        public GameObject DirectionalLight;
        public GameObject DebugLight;

        public GameObject WireCutters;
        public GameObject PickUpTest;

        public GameObject LeftHandRoot;
        public GameObject RightHandRoot;
        public GameObject LeftHand;
        public GameObject RightHand;
        public GameObject SharedHandL;
        public GameObject SharedHandR;

        public GameObject ReactorHub;
        public GameObject TerminalHub;
        public GameObject CoolantHub;
        public GameObject WireClip1;
        public GameObject WireClip2;
        public GameObject WireClip3;

        public GameObject Gun;
        public GameObject Henchman1;
        public GameObject Henchman2;

        public GameObject P_Shared_GrenadeSmoke;
        public GameObject P_Shop_INT_Flashlight;
        public GameObject P_PrivateJet_OxyMask;
        public GameObject SM_Van_ENV_Small_Window_Glass;
        public GameObject P_Van_INT_Cabinet;

        public GameObject Clipboard;
        public GameObject Cigar;
        public GameObject Lighter;
        public GameObject P_Elevator_ENV_LobbyEmergencyLight;
        public GameObject ScrewdriverSocket;
        public GameObject ELV_Screw_Variant;
        public GameObject ELV_WireCutters_Gathered;
        public GameObject ELV_Aerosol;
        public GameObject ELV_RocketThrusterControlBox;
        public GameObject ELV_MaintenanceKey_16;
        public GameObject ELV_FireExtinguisher;
        public GameObject SM_Shop_ENV_Ladder_01;
        public GameObject ELV_PortableBattery;
        public GameObject Shooter_Gathered;
        public GameObject Stopper;

        public GameObject SolderingGun;
        public GameObject MaskChip;
        public GameObject SolderObj;
        public GameObject WireSparkPoint;
        public GameObject KeySocket;
        public GameObject PlayerHitBox;

        public GameObject DNAPoster;
        public GameObject Notebook;
        public GameObject InfoCard;
        public GameObject PlayerObj;
        public GameObject Hatch;
        public GameObject ExTexMaterials;

        public GameObject RedBook;
        public GameObject ZorPlaque;
        public GameObject GreenBook;
        public GameObject SolderPaper;
        public GameObject Paperclips;
        public GameObject Screws;
        public GameObject Plant;
        public GameObject Apple;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (Manager == null) Manager = gameObject;

            RefreshAll();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RefreshAll()
        {
            Sponge = FindInScene(Sponge, "Sponge");
            VentSponge = FindInScene(VentSponge, "VentSponge");
            GrateCollider = FindInScene(GrateCollider, "GrateCollider");
            FanTrigger = FindInScene(FanTrigger, "FanTrigger");
            ReactorVentTrigger = FindInScene(ReactorVentTrigger, "ReactorVentTrigger");

            Needle = FindInScene(Needle, "SM_needle");
            WheelTerminal = FindInScene(WheelTerminal, "SM_wheelTerminal");
            Fan = FindInScene(Fan, "_Fan");
            PlayerObj = FindInScene(PlayerObj, "Player");
            Hatch = FindInScene(Hatch, "escape_hatch");

            CoolantPlane = FindInScene(CoolantPlane, "CoolantPlane");
            CoolantNozzle = FindInScene(CoolantNozzle, "CoolantNozzle");

            ReactorFlame1 = FindInScene(ReactorFlame1, "ReactorFlame1");
            ReactorFlame2 = FindInScene(ReactorFlame2, "ReactorFlame2");
            ReactorFlame3 = FindInScene(ReactorFlame3, "ReactorFlame3");
            Reactor = FindInScene(Reactor, "M_Reactor");

            VRRig = FindInScene(VRRig, "VRRig");
            DirectionalLight = FindInScene(DirectionalLight, "Directional Light");
            DebugLight = FindInScene(DebugLight, "DebugLight");

            WireCutters = FindInScene(WireCutters, "ELV_WireCutters(Clone)");
            PickUpTest = FindInScene(PickUpTest, "PickUpTest");

            LeftHandRoot = FindInScene(LeftHandRoot, "LeftHandRoot");
            RightHandRoot = FindInScene(RightHandRoot, "RightHandRoot");
            LeftHand = FindInScene(LeftHand, "LeftHand");
            RightHand = FindInScene(RightHand, "RightHand");
            SharedHandL = FindInScene(SharedHandL, "Shared_Hand_L_01");
            SharedHandR = FindInScene(SharedHandR, "Shared_Hand_R_01");

            ReactorHub = FindInScene(ReactorHub, "ReactorElectricHub");
            TerminalHub = FindInScene(TerminalHub, "TerminalElectricHub");
            CoolantHub = FindInScene(CoolantHub, "CoolantElectricHub");
            WireClip1 = FindInScene(WireClip1, "WireClip1");
            WireClip2 = FindInScene(WireClip2, "WireClip2");
            WireClip3 = FindInScene(WireClip3, "WireClip3");

            ReactorPipeGrabbable = FindInScene(ReactorPipeGrabbable, "ReactorPipeGrabbable");
            Stopper = FindInScene(Stopper, "SM_Stopper");

            Henchman1 = FindInScene(Henchman1, "Henchman1");
            Henchman2 = FindInScene(Henchman2, "Henchman2");
            KeySocket = FindInScene(KeySocket, "KeySocket");
            WireSparkPoint = FindInScene(WireSparkPoint, "WireSparkPoint");

            ExTexMaterials = FindInScene(ExTexMaterials, "ExTexMaterials");
            PlayerHitBox = FindInScene(PlayerHitBox, "PlayerHitBox");

            HookGatheredAssets();
        }

        void HookGatheredAssets()
        {
            AssignIfMissing(ref P_Shared_GrenadeSmoke, "P_Shared_GrenadeSmoke(Clone)");
            AssignIfMissing(ref P_Shop_INT_Flashlight, "P_Shop_INT_Flashlight(Clone)");
            AssignIfMissing(ref P_PrivateJet_OxyMask, "P_PrivateJet_OxyMask(Clone)");
            AssignIfMissing(ref SM_Van_ENV_Small_Window_Glass, "SM_Van_ENV_Small_Window_Glass(Clone)");
            AssignIfMissing(ref P_Van_INT_Cabinet, "P_Van_INT_Cabinet(Clone)");

            AssignIfMissing(ref Clipboard, "ELV_SignInSheet(Clone)");
            AssignIfMissing(ref Cigar, "P_Elevator_INT_ZorCigar(Clone)");
            AssignIfMissing(ref Lighter, "ELV_ZorLighter Variant(Clone)");
            AssignIfMissing(ref P_Elevator_ENV_LobbyEmergencyLight, "P_Elevator_ENV_LobbyEmergencyLight(Clone)");
            AssignIfMissing(ref ScrewdriverSocket, "ELV_Screwdriver Variant (Top Floor)(Clone)");
            AssignIfMissing(ref ELV_Screw_Variant, "ELV_Screw Variant(Clone)");
            AssignIfMissing(ref ELV_WireCutters_Gathered, "ELV_WireCutters(Clone)");
            AssignIfMissing(ref ELV_Aerosol, "ELV_Aerosol(Clone)");
            AssignIfMissing(ref ELV_RocketThrusterControlBox, "ELV_RocketThrusterControlBox(Clone)");
            AssignIfMissing(ref ELV_MaintenanceKey_16, "ELV_MaintenanceKey_16(Clone)");
            AssignIfMissing(ref ELV_FireExtinguisher, "ELV_FireExtinguisher(Clone)");
            AssignIfMissing(ref SM_Shop_ENV_Ladder_01, "SM_Shop_ENV_Ladder_01(Clone)");
            AssignIfMissing(ref ELV_PortableBattery, "ELV_PortableBattery(Clone)");
            AssignIfMissing(ref Shooter_Gathered, "Shooter(Clone)");

            AssignIfMissing(ref SolderingGun, "P_Shop_INT_SolderingIron(Clone)");
            AssignIfMissing(ref MaskChip, "SM_Shop_INT_Mask_Chip1");
            AssignIfMissing(ref SolderObj, "Wire_3_Solder1(Clone)");

            AssignIfMissing(ref DNAPoster, "P_WinRoom_INT_Picture_3(Clone)");
            AssignIfMissing(ref Notebook, "MS_L5_ActI_Paper(Clone)");
            AssignIfMissing(ref InfoCard, "P_MovieSet_INT_LaserCageNote(Clone)");

            AssignIfMissing(ref RedBook, "P_PrivateJet_INT_Book_03 (4)(Clone)");
            AssignIfMissing(ref ZorPlaque, "P_Shop_INT_BookEnds_01(Clone)");
            AssignIfMissing(ref GreenBook, "P_PrivateJet_INT_Book_03 (6)(Clone)");
            AssignIfMissing(ref SolderPaper, "P_Shop_INT_SolderingBooklet_01(Clone)");
            AssignIfMissing(ref Paperclips, "P_Shop_INT_BoxOfPaperclips_01 (1)(Clone)");
            AssignIfMissing(ref Screws, "P_Shop_INT_BoxOfScrews_01 (1)(Clone)");
            AssignIfMissing(ref Plant, "P_Shop_INT_Succulent(Clone)");
            AssignIfMissing(ref Apple, "P_Elevator_INT_Apple");
        }

        void AssignIfMissing(ref GameObject field, string objectName)
        {
            if (field == null)
                field = FindByNameGlobal(objectName);
        }

        GameObject FindInScene(GameObject current, string objectName)
        {
            if (current != null) return current;

            GameObject found = null;

            if (MyMod.Instance != null)
                found = MyMod.Instance.getObjectInScene(objectName);

            if (found == null)
                found = GameObject.Find(objectName);

            if (found == null)
                MelonLogger.Warning($"[ObjectBank] Missing '{objectName}'");

            return found;
        }

        GameObject FindByNameGlobal(string objectName)
        {
            string query = objectName.ToLowerInvariant();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject obj = allObjects[i];
                if (obj == null) continue;

                string name = (obj.name ?? "").ToLowerInvariant();
                if (name == query)
                    return obj;
            }

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject obj = allObjects[i];
                if (obj == null) continue;

                string name = (obj.name ?? "").ToLowerInvariant();
                if (name.Contains(query))
                    return obj;
            }

            return null;
        }
    }
}
