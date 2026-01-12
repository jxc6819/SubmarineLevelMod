using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IEYTD2_SubmarineCode
{
    public class GatherGameAssets : MonoBehaviour
    {
        public GatherGameAssets(IntPtr ptr) : base(ptr) { }
        public GatherGameAssets()
            : base(ClassInjector.DerivedConstructorPointer<GatherGameAssets>())
            => ClassInjector.DerivedConstructorBody(this);

        public string[] sceneNames = { "Van", "Elevator_Level", "Shop_Level", "MovieSet_Level" };

        public string[][] gameObjectNames = new string[][]
        {
            new string[] { "P_Shared_GrenadeSmoke", "P_Shop_INT_Flashlight", "P_PrivateJet_OxyMask", "SM_Van_ENV_Small_Window_Glass", "P_Van_INT_Cabinet" },

            new string[] { "ELV_SignInSheet", "P_Elevator_INT_ZorCigar", "ELV_ZorLighter Variant", "P_Elevator_ENV_LobbyEmergencyLight",
                           "ELV_Screwdriver Variant (Top Floor)", "ELV_Screw Variant", "ELV_WireCutters", "ELV_Aerosol", "ELV_RocketThrusterControlBox",
                           "ELV_MaintenanceKey_16", "ELV_FireExtinguisher", "SM_Shop_ENV_Ladder_01", "ELV_PortableBattery", "Shooter",
                           "P_Elevator_INT_Apple", "PS_Shared_FX_Eat_Apple_01_0", "Edibble1", "Edibble2" },

            new string[] { "P_Shop_INT_SolderingIron", "P_Shop_INT_Mask_Chip_01", "Wire_3_Solder1", "P_PrivateJet_INT_Book_03 (4)",
                           "P_Shop_INT_BookEnds_01", "P_PrivateJet_INT_Book_03 (6)", "P_Shop_INT_SolderingBooklet_01",
                           "P_Shop_INT_BoxOfPaperclips_01 (1)", "P_Shop_INT_BoxOfScrews_01 (1)", "P_Shop_INT_Succulent" },

            new string[] { "MS_L5_ActI_Paper", "P_WinRoom_INT_Picture_3", "P_MovieSet_INT_LaserCageNote" }
        };

        public List<GameObject> indexedObjects = new List<GameObject>();
        public bool done = false;

        public void gatherAssets()
        {
            MelonCoroutines.Start(IndexThroughScenes());
        }

        [UnhollowerBaseLib.Attributes.HideFromIl2Cpp]
        private IEnumerator IndexThroughScenes()
        {
            indexedObjects.Clear();

            for (int i = 0; i < sceneNames.Length; i++)
            {
                string sceneName = sceneNames[i];

                bool alreadyLoaded = SceneManager.GetSceneByName(sceneName).IsValid()
                                     && SceneManager.GetSceneByName(sceneName).isLoaded;

                if (!alreadyLoaded)
                {
                    yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                }

                GameObject[] clones = CollectObjects(sceneName, gameObjectNames[i]);
                indexedObjects.AddRange(clones);

                if (!alreadyLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(sceneName);
                }
            }

            yield return new WaitForSeconds(1f);
            yield return SceneManager.LoadSceneAsync("Van", LoadSceneMode.Single);
            yield return new WaitForSeconds(2f);

            done = true;

            while (!IsCustomSceneLoaded())
                yield return null;

            PlantObjectsInScene();
        }

        public GameObject[] CollectObjects(string sceneName, string[] wantedNames)
        {
            var results = new List<GameObject>();
            var wanted = new HashSet<string>(wantedNames);

            Scene scene = SceneManager.GetSceneByName(sceneName);

            var roots = new Il2CppSystem.Collections.Generic.List<GameObject>();
            scene.GetRootGameObjects(roots);

            var transformType = Il2CppType.From(typeof(Transform));

            for (int r = 0; r < roots.Count; r++)
            {
                GameObject root = roots[r];
                if (root == null) continue;

                var allTransforms = root.GetComponentsInChildren(transformType, true);

                foreach (var comp in allTransforms)
                {
                    Transform tr = comp.Cast<Transform>();
                    if (tr == null) continue;

                    if (!wanted.Contains(tr.name))
                        continue;

                    GameObject clone = Instantiate(tr.gameObject);
                    clone.SetActive(false);
                    DontDestroyOnLoad(clone);

                    results.Add(clone);
                    wanted.Remove(tr.name);

                    if (wanted.Count == 0)
                        break;
                }

                if (wanted.Count == 0)
                    break;
            }

            return results.ToArray();
        }

        public bool IsCustomSceneLoaded()
        {
            return GameObject.Find("ModLevel_ROOT") != null;
        }

        public void PlantObjectsInScene()
        {
            Scene van = SceneManager.GetSceneByName("Van");

            GameObject toolsRoot = FindOrCreateToolsRoot(van);

            for (int i = 0; i < indexedObjects.Count; i++)
            {
                GameObject obj = indexedObjects[i];
                if (obj == null) continue;

                SceneManager.MoveGameObjectToScene(obj, van);
                obj.transform.SetParent(toolsRoot.transform, true);
                obj.SetActive(false);
            }
        }

        private GameObject FindOrCreateToolsRoot(Scene scene)
        {
            var roots = new Il2CppSystem.Collections.Generic.List<GameObject>();
            scene.GetRootGameObjects(roots);

            for (int i = 0; i < roots.Count; i++)
            {
                GameObject root = roots[i];
                if (root != null && root.name == "IEYTD2_Tools_ROOT")
                    return root;
            }

            GameObject toolsRoot = new GameObject("IEYTD2_Tools_ROOT");
            SceneManager.MoveGameObjectToScene(toolsRoot, scene);
            DontDestroyOnLoad(toolsRoot);
            return toolsRoot;
        }
    }
}
