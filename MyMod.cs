

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using IEYTD2_SubmarineCode;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Linq;
using UnhollowerBaseLib.Attributes;
using SG.Phoenix.Assets.Code.Interactables;
using SG.Phoenix.Assets.Code.WorldAttributes;
using System.Runtime.CompilerServices;
using SG.Phoenix.Assets.Code.WorldAttributes.PlayerManagement;
using UnhollowerBaseLib;
using SG.Phoenix.Assets.Code;
using SG.Phoenix.Assets.Code.Localization;
using TMPro;
using SG.Phoenix.Assets.Code.InputManagement;
using UnityEngine.XR;

[assembly: MelonInfo(typeof(IEYTD2_SubmarineCode.MyMod), "IEYTD2 Auto Phoenix Merge + Grabbable", "5.1.0", "James Connors")]
[assembly: MelonGame("Schell Games", "I Expect You To Die 2")]

namespace IEYTD2_SubmarineCode
{
    public class MyMod : MelonMod
    {

        const string BundleFileName = "submarinelevel_assets";
        const string FallbackSceneAssetPath = "Assets/ModAssets/MyCustomLevel.unity";
        const string MergedRootName = "ModLevel_ROOT";

        const string RedLightName = "Red Light";
        static readonly Vector3 RedLightWorldPos = new Vector3(0f, 5f, -3f);
        const float RedLightIntensity = 15f;

        static bool _loading;
        private static bool _autoMergeOnNextVan = false;
        const float _sceneLoadTimeout = 30f;
        string _pendingAdditive = null;

        public static MyMod Instance;
        AssetBundle _bundle;
        string _sceneName, _scenePath;
        GameObject _mergedRoot;
        Camera _hmd;

        Shader _phoenixPackedOpaque, _phoenixDefaultOpaque, _phoenixCutout, _phoenixTransparent;
        readonly HashSet<Material> _mergedMats = new HashSet<Material>();

        ReflectionProbe _globalProbe;
        Cubemap _brightFallbackCube;

        readonly Dictionary<Light, float> _origLightIntensity = new Dictionary<Light, float>();
        bool _strongBoostApplied = false;

        static readonly string[] KeepNameContains = { "vrrig", "player", "hmd", "reflection probe", "sceneportal", "portal", "interact", "gesture", "pickup" };

        Type _tPickUpManaged, _tShakeManaged;
        Il2CppSystem.Type _tPickUpIl2, _tShakeIl2;

        bool _holdersDisabled = true;

        private static GameObject _mlManager;
        private static GatherGameAssets _gatheredAssets;
        private static bool _gathered = false;
        private static bool f9_pressed = false;
        private bool _mergeDonePending = false;

        private bool _restartInProgress = false;
        private GameObject _restartBlackoutQuad;
        private Camera _restartBlackoutParentCam;

        public GameObject playerBlindness;
        ObjectBank bank;

        [Obsolete]
        public override void OnApplicationStart()
        {
            Instance = this;
            TryReadBundle();
            SubBundle2Manager.Init();
        }

        public override void OnInitializeMelon()
        {
            ClassInjector.RegisterTypeInIl2Cpp<WheelScript>();
            ClassInjector.RegisterTypeInIl2Cpp<SubmarineLevelLogic>();
            ClassInjector.RegisterTypeInIl2Cpp<NeedleScript>();
            ClassInjector.RegisterTypeInIl2Cpp<reactorVentTriggerDetector>();
            ClassInjector.RegisterTypeInIl2Cpp<fanTriggerDetector>();
            ClassInjector.RegisterTypeInIl2Cpp<SpinFan>();
            ClassInjector.RegisterTypeInIl2Cpp<WaterDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<ReactorDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<CoolantSprayDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<WaterSprayDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<SharedPrefabProbe>();
            ClassInjector.RegisterTypeInIl2Cpp<GatherGameAssets>();
            ClassInjector.RegisterTypeInIl2Cpp<WireCutterListener>();
            ClassInjector.RegisterTypeInIl2Cpp<WireManager>();
            ClassInjector.RegisterTypeInIl2Cpp<LooseWire>();
            ClassInjector.RegisterTypeInIl2Cpp<WireTubeRenderer>();
            ClassInjector.RegisterTypeInIl2Cpp<Wire>();
            ClassInjector.RegisterTypeInIl2Cpp<WireClamp>();
            ClassInjector.RegisterTypeInIl2Cpp<WireCutterHitBox>();
            ClassInjector.RegisterTypeInIl2Cpp<LooseWireHitBox>();
            ClassInjector.RegisterTypeInIl2Cpp<LeverScript>();
            ClassInjector.RegisterTypeInIl2Cpp<ObjectBank>();
            ClassInjector.RegisterTypeInIl2Cpp<SteamDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<ExplosionDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<SmokeDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<HenchmanController>();
            ClassInjector.RegisterTypeInIl2Cpp<BloodMistDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<StopperScript>();
            ClassInjector.RegisterTypeInIl2Cpp<SolderListener>();
            ClassInjector.RegisterTypeInIl2Cpp<SolderHitBox>();
            ClassInjector.RegisterTypeInIl2Cpp<SubLoopAmbience>();
            ClassInjector.RegisterTypeInIl2Cpp<SubmarineRunManager>();
            ClassInjector.RegisterTypeInIl2Cpp<ChipOrientationHelper>();
            ClassInjector.RegisterTypeInIl2Cpp<UseInteractable>();
            ClassInjector.RegisterTypeInIl2Cpp<GunScript>();
            ClassInjector.RegisterTypeInIl2Cpp<FlashlightScript>();
            ClassInjector.RegisterTypeInIl2Cpp<SparkDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<LoopingSfx>();
            ClassInjector.RegisterTypeInIl2Cpp<PhoenixButtonHook>();
            ClassInjector.RegisterTypeInIl2Cpp<DamageOverlayDriver>();
            ClassInjector.RegisterTypeInIl2Cpp<AlarmSequence>();
            ClassInjector.RegisterTypeInIl2Cpp<HatchScript>();
            ClassInjector.RegisterTypeInIl2Cpp<CigarScript>();
            ClassInjector.RegisterTypeInIl2Cpp<GlassDriver>();

            PhoenixProbe.Initialize();

            if (_mlManager == null)
            {
                _mlManager = new GameObject("MelonLoader Manager");
                GameObject.DontDestroyOnLoad(_mlManager);
                _gatheredAssets = _mlManager.AddComponent<GatherGameAssets>();
                _mlManager.AddComponent<SubmarineRunManager>();
            }

        }

        public override void OnUpdate()
        {

            if (Input.GetKeyDown(KeyCode.F1))
            {
                RestartLevel();

            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                f9_pressed = true;
                if (!_gathered)
                {
                    _gathered = true;
                    _gatheredAssets.gatherAssets();
                }
            }

            if (f9_pressed && _gatheredAssets.done)
            {
                f9_pressed = false;
                if (_loading) { MelonLogger.Warning("[F9] Merge already in progress. Ignoring."); }
                else { MelonCoroutines.Start(Co_BeginMerge()); }
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                var gun = GameObject.Find("PickUp_HOST_Shooter(Clone)");
                var right = bank.RightHandRoot.transform;
                var left = bank.LeftHandRoot.transform;

                Quaternion rRot = Quaternion.Inverse(right.rotation) * gun.transform.rotation;
                Vector3 rPos = Quaternion.Inverse(right.rotation) * (gun.transform.position - right.position);

                Quaternion lRot = Quaternion.Inverse(left.rotation) * gun.transform.rotation;
                Vector3 lPos = Quaternion.Inverse(left.rotation) * (gun.transform.position - left.position);

                MelonLogger.Msg($"[Calib] RIGHT rotQuat=({rRot.x:F6},{rRot.y:F6},{rRot.z:F6},{rRot.w:F6}) posLocal=({rPos.x:F6},{rPos.y:F6},{rPos.z:F6})");
                MelonLogger.Msg($"[Calib] LEFT  rotQuat=({lRot.x:F6},{lRot.y:F6},{lRot.z:F6},{lRot.w:F6}) posLocal=({lPos.x:F6},{lPos.y:F6},{lPos.z:F6})");
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Vector3 spongePos = new Vector3(3.0166f, 0.2311f, -1.1792f);
                Vector3 spongeRot = new Vector3(225.9859f, 153.4006f, 109.9736f);

            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                PickUp flash = bank.P_Shop_INT_Flashlight.GetComponent<PickUp>();
                flash.heldHand = bank.RightHandRoot.GetComponent<VRHandInput>();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                PickUp flash = bank.P_Shop_INT_Flashlight.GetComponent<PickUp>();
                flash.heldHand = null;

            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                MelonLogger.Msg("F8 pressed - turning off blackout");
                SetBlackout(false);
                RefreshGlobalProbe();
            }

            if (Input.GetKeyDown(KeyCode.F4))
            {
                RunWaterFix();
                AddReactorFlames();
                GameObject.Find("CoolantNozzle").AddComponent<WaterSprayDriver>();
            }

            if (_mergeDonePending)
            {
                _mergeDonePending = false;
                mergeDone();
            }
        }

        private void RunWaterFix()
        {

            GameObject plane = GameObject.Find("CoolantPlane");
            plane.AddComponent<WaterDriver>();
        }
        void AddReactorFlames()
        {
            foreach (var name in new[] { "ReactorFlame1", "ReactorFlame2", "ReactorFlame3" })
                GameObject.Find(name)?.AddComponent<ReactorDriver>();
        }

        public void KillPlayer()
        {
            MelonCoroutines.Start(Co_KillPlayer());
        }

        public void DamagePlayer()
        {
            var driver = bank.VRRig.GetComponent<DamageOverlayDriver>();
            if (!driver) driver = bank.VRRig.AddComponent<DamageOverlayDriver>();

            driver.Pulse(0.5f);
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_KillPlayer()
        {
            MelonCoroutines.Start(DieVisual());
            yield return new WaitForSeconds(0.6f);
            RestartLevel();
        }

        void blindPlayer(bool blind)
        {
            if (playerBlindness == null)
                playerBlindness = GameObject.Find("PlayeBlindnessVisual");

            PlayerBlindnessVisual pbv = playerBlindness.GetComponent<PlayerBlindnessVisual>();
            pbv.Blind(blind);
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator DieVisual()
        {
            var driver = bank.VRRig.GetComponent<DamageOverlayDriver>();
            if (!driver) driver = bank.VRRig.AddComponent<DamageOverlayDriver>();

            driver.FadeTo(1f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            blindPlayer(true);
        }

        void ToggleHolders()
        {

            _holdersDisabled = !AnyHolderActive();
            _holdersDisabled = !_holdersDisabled;
            SetHolders(_holdersDisabled);
        }

        void SetHolders(bool disable)
        {
            DisablePickUpHoldersByNameAndType(disable);
            _holdersDisabled = disable;
            int active = CountActiveHolders();
            MelonLogger.Msg($"[Holders] {(disable ? "Disabled" : "Enabled")} (forced). Active after op: {active}");
        }

        bool AnyHolderActive()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i]; if (!t) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                string n = t.name.ToLowerInvariant();
                if ((n.Contains("pickupholder") || n.Contains("scriptpickupholder") || n.Contains("socket")) &&
                    t.gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        bool TypeEndsWith(System.Type t, string suffix)
        {
            if (t == null) return false;
            var fn = t.FullName ?? t.Name;
            return !string.IsNullOrEmpty(fn) && fn.EndsWith(suffix, StringComparison.Ordinal);
        }

        void TryEnableBehaviour(Component c)
        {
            if (c == null) return;
            try { var b = c as Behaviour; if (b != null) b.enabled = true; } catch { }

            try
            {
                var ty = c.GetType();
                var p = ty.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite) p.SetValue(c, true, null);
                var f = ty.GetField("enabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) f.SetValue(c, true);
            }
            catch { }
        }

        void ForceEnablePickUpsUnder(GameObject root)
        {
            if (!root) return;
            int n = 0;
            var comps = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i]; if (!c) continue;
                var t = c.GetType();
                if (TypeEndsWith(t, ".Interactables.PickUp") || TypeEndsWith(t, ".Gestures.PickUpShakeGesture"))
                {
                    TryEnableBehaviour(c);
                    n++;
                }
            }

            MelonCoroutines.Start(Co_NudgeEnablePickUps(root));
            MelonLogger.Msg($"[PickUp] Forced enabled on {n} component(s) under '{root.name}'.");
        }

        System.Collections.IEnumerator Co_NudgeEnablePickUps(GameObject root)
        {
            for (int i = 0; i < 10; i++)
            {
                if (!root) yield break;
                try
                {
                    root.SetActive(true);
                    var comps = root.GetComponentsInChildren<Component>(true);
                    for (int k = 0; k < comps.Length; k++)
                    {
                        var c = comps[k]; if (!c) continue;
                        var t = c.GetType();
                        if (TypeEndsWith(t, ".Interactables.PickUp") || TypeEndsWith(t, ".Gestures.PickUpShakeGesture"))
                            TryEnableBehaviour(c);
                    }
                }
                catch { }
                yield return null;
            }
        }

        int CountActiveHolders()
        {
            int cnt = 0;
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i]; if (!t) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                string n = t.name.ToLowerInvariant();
                if ((n.Contains("pickupholder") || n.Contains("scriptpickupholder") || n.Contains("socket")) &&
                    t.gameObject.activeInHierarchy)
                    cnt++;
            }
            return cnt;
        }

        public void MakeGrabbable(string itemName, bool preferDonorHost = true)
        {
            if (string.IsNullOrEmpty(itemName)) { MelonLogger.Error("[MakeGrabbable] Empty item name."); return; }
            ResolvePhoenixTypes();

            var target = FindTargetByName(itemName);
            if (target == null) { MelonLogger.Error($"[MakeGrabbable] '{itemName}' not found in scene."); return; }

            Component donor = preferDonorHost ? FindBestDonorPickUp() : null;
            if (preferDonorHost && donor != null && TryCloneDonorAsHost(donor, target))
            {
                MelonLogger.Msg($"[MakeGrabbable] '{itemName}' ready (donor host).");
                return;
            }

            if (TryFallbackAddComponents(target))
                MelonLogger.Msg($"[MakeGrabbable] '{itemName}' ready (fallback add-components).");
            else
                MelonLogger.Error($"[MakeGrabbable] Could not attach Phoenix components to '{itemName}'.");
        }

        GameObject FindTargetByName(string name)
        {
            name = name.ToLowerInvariant();
            GameObject best = null;

            if (_mergedRoot != null)
            {
                var all = _mergedRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var go = all[i].gameObject; if (!go) continue;
                    var n = go.name.ToLowerInvariant();
                    if (n == name) return go;
                    if (best == null && n.Contains(name)) best = go;
                }
                if (best != null) return best;
            }

            var gos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g || !g.scene.IsValid()) continue;
                var n = g.name.ToLowerInvariant();
                if (n == name) return g;
                if (best == null && n.Contains(name)) best = g;
            }
            return best;
        }

        string ReadTargetOverrideName()
        {
            try
            {
                var p = Path.Combine(MelonUtils.UserDataDirectory, "IEYTD2_Target.txt");
                if (!File.Exists(p)) return null;
                foreach (var line in File.ReadAllLines(p))
                {
                    var s = (line ?? "").Trim();
                    if (s.Length > 0 && !s.StartsWith("#")) return s;
                }
            }
            catch { }
            return null;
        }

        System.Collections.IEnumerator Co_BeginMerge()
        {
            if (_loading) yield break;
            _loading = true;

            try
            {
                if (!EnsureBundle())
                {
                    MelonLogger.Error("[F9] Bundle not ready.");
                    yield break;
                }

                try
                {
                    var paths = _bundle?.GetAllScenePaths();
                    if (paths != null && paths.Length > 0)
                        MelonLogger.Msg("[Bundle] Scenes in bundle: " + string.Join(", ", paths));
                }
                catch { }

                string toLoadName = _sceneName;
                string toLoadPath = _scenePath;

                AsyncOperation op = null;
                _pendingAdditive = null;
                try
                {
                    op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(toLoadName, LoadSceneMode.Additive);
                    _pendingAdditive = toLoadName;
                }
                catch
                {
                    try
                    {
                        op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(toLoadPath, LoadSceneMode.Additive);
                        _pendingAdditive = System.IO.Path.GetFileNameWithoutExtension(toLoadPath);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error("[Merge] Could not start additive load: " + ex.Message);
                        yield break;
                    }
                }

                if (op == null)
                {
                    MelonLogger.Error("[Merge] LoadSceneAsync returned null.");
                    yield break;
                }

                float t = 0f;
                while (true)
                {
                    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_pendingAdditive);
                    if (sc.IsValid() && sc.isLoaded) break;

                    t += Time.deltaTime;
                    if (t > _sceneLoadTimeout)
                    {
                        MelonLogger.Error("[Merge] Timeout waiting for additive scene '" + _pendingAdditive + "'.");
                        yield break;
                    }
                    yield return null;
                }

                yield return MelonCoroutines.Start(Co_MergeCore(_pendingAdditive));
            }
            finally
            {
                _loading = false;
                _pendingAdditive = null;
            }
        }

        System.Collections.IEnumerator Co_MergeCore(string loadedSceneName)
        {
            _mergeDonePending = false;
            var src = UnityEngine.SceneManagement.SceneManager.GetSceneByName(loadedSceneName);
            if (!src.IsValid() || !src.isLoaded)
            {
                MelonLogger.Error("[Merge] Internal error: source scene not loaded.");
                yield break;
            }
            var dst = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            _mergedRoot = GameObject.Find(MergedRootName) ?? new GameObject(MergedRootName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_mergedRoot, dst);

            int moved = 0, enabled = 0;
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                var go = allGos[i];
                if (!go || !go.scene.IsValid() || go.scene != src) continue;
                if (go.transform.parent != null) continue;
                if (go.name == "VRRig" || go.name == "Player" || go.name == "HMD") continue;

                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, dst);
                go.transform.SetParent(_mergedRoot.transform, true);
                enabled += HardEnableDraw(go);
                moved++;
            }

            _hmd = FindHMDCamera();
            if (_hmd) _hmd.depthTextureMode |= DepthTextureMode.Depth;
            if (_hmd)
            {
                _hmd.nearClipPlane = 0.01f; _hmd.farClipPlane = 1000f;
                _hmd.useOcclusionCulling = false;
                try { _hmd.allowHDR = true; } catch { }
                EnsureLayerVisibleToCamera(_mergedRoot, _hmd);
            }
            EnableDepthTexturesOnAllCameras();

            if (QualitySettings.pixelLightCount < 4) QualitySettings.pixelLightCount = 4;
            try { QualitySettings.realtimeReflectionProbes = true; } catch { }

            MelonLogger.Msg($"[F9] Merged {moved} roots; renderers enabled: {enabled}. ColorSpace={QualitySettings.activeColorSpace}");

            HarvestPhoenixShaders();
            _mergedMats.Clear();
            ConvertMergedMaterialsToPhoenix();
            SyncAllPhoenixTiling();
            ForceAllOpaque();

            EnsureBrightFallbackReflectionCubemap();
            BuildOrUpdateGlobalProbe(true);
            TuneRedLight();
            ApplyColoredLightAssist(2.0f, true);

            RunWaterFix();
            AddReactorFlames();
            GameObject.Find("CoolantNozzle").AddComponent<WaterSprayDriver>().enabled = false;

            InspectVanGrabbable(false);

            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(src);
            CleanHostRoots();

            DumpEnvDebug();

            MelonCoroutines.Start(CloneCheckpointState());
            while(_clonedToolsTemplate == null)
            {
                yield return null;
            }

            attachScripts();
            setGrabbables();

            MiscStuff();

            ObjectBank.Instance?.RefreshAll();
            bank = ObjectBank.Instance;

            _mergeDonePending = true;

            MelonCoroutines.Start(Co_ForceEnableAllPickUpsFor(3f));

            MelonLogger.Msg("[F9] Done.");
            yield break;
        }

        List<string> _keepRootNames = new List<string>();
        GameObject _clonedModRoot = null;
        GameObject _clonedToolsTemplate = null;
        GameObject _clonedVRRig = null;
        HashSet<GameObject> _liveModObjects = new HashSet<GameObject>();
        HashSet<GameObject> _liveToolObjects = new HashSet<GameObject>();

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator CloneCheckpointState()
        {
            _cloneModRoot();

            float timeout = 15f;
            float t = 0f;

            while (t < timeout)
            {
                var liveTools = FindToolsRootDdol();
                if (liveTools)
                {
                    _cloneTools(liveTools);
                    yield break;
                }

                t += Time.deltaTime;
                yield return null;
            }

            MelonLogger.Warning("[Restart] Timed out waiting for IEYTD2_Tools_ROOT in DDoL; tools template not captured.");
        }

        void _cloneModRoot()
        {
            _keepRootNames.Clear();

            var van = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            var all = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (!go) continue;

                if (!go.scene.IsValid() || go.scene != van) continue;

                if (go.transform.parent != null) continue;

                _keepRootNames.Add(go.name);

                if (go.name == "ModLevel_ROOT")
                {
                    CacheHierarchy(go, _liveModObjects);

                    if (_clonedModRoot) UnityEngine.Object.Destroy(_clonedModRoot);

                    var clone = UnityEngine.Object.Instantiate(go);
                    clone.name = "ModLevel_ROOT(Clone)";
                    clone.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(clone);
                    AddSuffixRecursive(clone, CLONE_CHILD_SUFFIX);

                    _clonedModRoot = clone;
                }

                if(go.name == "VRRig")
                {
                    MelonLogger.Msg("[Restart] - VRRig Found");
                    var clone = UnityEngine.Object.Instantiate(go);
                    clone.name = "VRRig(Clone)";
                    clone.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(clone);
                    AddSuffixRecursive(clone, CLONE_CHILD_SUFFIX);
                    _clonedVRRig = clone;
                }
            }

            _keepRootNames.Remove("ModLevel_ROOT");
            if (!_keepRootNames.Contains("ModLevel_ROOT(Clone)"))
                _keepRootNames.Add("ModLevel_ROOT(Clone)");
            _keepRootNames.Remove("VRRig");

        }

        void _cloneTools(GameObject liveTools)
        {
            if (!liveTools)
            {
                MelonLogger.Warning("[Restart] _cloneTools called with null liveTools.");
                return;
            }

            MelonLogger.Msg("[Restart] IEYTD2_Tools_ROOT found in DDoL; cloning template.");

            CacheHierarchy(liveTools, _liveToolObjects);

            if (_clonedToolsTemplate) UnityEngine.Object.Destroy(_clonedToolsTemplate);

            var toolsClone = UnityEngine.Object.Instantiate(liveTools);
            toolsClone.name = "IEYTD2_Tools_ROOT(Clone)";
            toolsClone.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(toolsClone);
            AddSuffixRecursive(toolsClone, CLONE_CHILD_SUFFIX);

            _clonedToolsTemplate = toolsClone;

            if (!_keepRootNames.Contains("IEYTD2_Tools_ROOT(Clone)"))
                _keepRootNames.Add("IEYTD2_Tools_ROOT(Clone)");

            MelonLogger.Msg("[Restart] _clonedToolsTemplate is " + (_clonedToolsTemplate ? "NOT null" : "NULL"));
            MelonLogger.Msg("[Restart] Captured keep roots: " + string.Join(", ", _keepRootNames));
        }

        [HideFromIl2Cpp]
        void CacheHierarchy(GameObject root, HashSet<GameObject> set)
        {
            if (!root) return;

            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (!t) continue;
                set.Add(t.gameObject);
            }
        }

        [HideFromIl2Cpp]
        GameObject FindToolsRootDdol()
        {
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (!go) continue;

                if (go.name != "IEYTD2_Tools_ROOT") continue;
                if (go.transform.parent != null) continue;
                if (go.scene.name != "DontDestroyOnLoad") continue;

                return go;
            }
            return null;
        }

        const string CLONE_CHILD_SUFFIX = " (TEMPLATE)";

        [HideFromIl2Cpp]
        static void AddSuffixRecursive(GameObject root, string suffix)
        {
            if (!root || string.IsNullOrEmpty(suffix)) return;

            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform ch = root.transform.GetChild(i);
                if (!ch) continue;

                GameObject go = ch.gameObject;
                if (go && (go.name == null || !go.name.EndsWith(suffix, StringComparison.Ordinal)))
                    go.name = (go.name ?? "") + suffix;

                AddSuffixRecursive(go, suffix);
            }
        }

        [HideFromIl2Cpp]
        static void RemoveSuffixRecursive(GameObject root, string suffix)
        {
            if (!root || string.IsNullOrEmpty(suffix)) return;

            if (!string.IsNullOrEmpty(root.name) && root.name.EndsWith(suffix, StringComparison.Ordinal))
                root.name = root.name.Substring(0, root.name.Length - suffix.Length);

            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform ch = root.transform.GetChild(i);
                if (!ch) continue;
                RemoveSuffixRecursive(ch.gameObject, suffix);
            }
        }

        static string StripSuffix(string name, string suffix)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(suffix)) return name;
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
        }

        [HideFromIl2Cpp]
        static GameObject FindUnderRootByNameLoose(GameObject root, string wantedName, string suffixToIgnore)
        {
            if (!root || string.IsNullOrEmpty(wantedName)) return null;

            var trs = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (!t) continue;

                string n = t.gameObject.name ?? "";
                if (StripSuffix(n, suffixToIgnore) == wantedName)
                    return t.gameObject;
            }
            return null;
        }

        private SharedPrefabProbe _probe;
        private SharedPrefabProbe EnsureProbe()
        {
            if (_probe) return _probe;
            var go = new GameObject("SharedPrefabProbe_GO");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _probe = go.AddComponent<SharedPrefabProbe>();
            return _probe;
        }

        public void attachScripts()
        {
            AttachUnityScript.AttachScriptToGameObject("Manager", "ObjectBank");
            AttachUnityScript.AttachScriptToGameObject("SM_needle", "NeedleScript");
            AttachUnityScript.AttachScriptToGameObject("SM_wheelTerminal", "WheelScript");
            AttachUnityScript.AttachScriptToGameObject("FanTrigger", "fanTriggerDetector");
            AttachUnityScript.AttachScriptToGameObject("ReactorVentTrigger", "reactorVentTriggerDetector");
            AttachUnityScript.AttachScriptToGameObject("ReactorVentTrigger", "SteamDriver");
            AttachUnityScript.AttachScriptToGameObject("_Fan", "ExplosionDriver");

            AttachUnityScript.AttachScriptToGameObject("_Fan", "SmokeDriver");
            AttachUnityScript.AttachScriptToGameObject("_Fan", "SpinFan");
            AttachUnityScript.AttachScriptToGameObject("Manager", "SubmarineLevelLogic");
            AttachUnityScript.AttachScriptToGameObject("M_Reactor", "SubLoopAmbience");

            AttachUnityScript.AttachScriptToGameObject("Manager", "WireManager");
            AttachUnityScript.AttachScriptToGameObject("CoolantLeverHandle", "LeverScript");
            AttachUnityScript.AttachScriptToGameObject("Henchman1", "HenchmanController");
            AttachUnityScript.AttachScriptToGameObject("Henchman2", "HenchmanController");
            AttachUnityScript.AttachScriptToGameObject("Henchman1", "BloodMistDriver");
            AttachUnityScript.AttachScriptToGameObject("Henchman2", "BloodMistDriver");
            AttachUnityScript.AttachScriptToGameObject("SM_Stopper", "StopperScript");
            AttachUnityScript.AttachScriptToGameObject("Manager", "AlarmSequence");
            AttachUnityScript.AttachScriptToGameObject("escape_hatch", "HatchScript");

        }

        public void setGrabbables()
        {
            string[] names = new string[] { "Sponge" };
            foreach (string name in names)
            {
                MakeGrabbable(name, true);
                GameObject obj = GameObject.Find(name);
                obj.layer = LayerMask.NameToLayer("Interactable");
                obj.transform.parent.GetComponent<PickUp>().enabled = true;
            }
        }

        public void MiscStuff()
        {

            var rig = GameObject.Find("VRRig");
            if (rig != null)
            {
                var opt = rig.transform;
                rig.transform.SetPositionAndRotation(
                    new Vector3(0f, 1.4f, 0.85f),
                    Quaternion.Euler(opt.rotation.x, 180f, opt.rotation.z)
                );
            }
            else
            {
                MelonLogger.Warning("[MiscStuff] VRRig not found");
            }

            disableMeshRenderers();
            MelonLogger.Msg("[MiscStuff] mesh renderers turned off");

            SafeSetInactive("Directional Light");
            SafeSetInactive("DebugLight");
            SafeSetInactive("VentSponge");
            SafeSetInactive("Henchmen");
            GameObject.Find("Directional Light (1)").SetActive(false);
            GameObject.Find("SM_wheelTerminal_Pivot_RT").GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;

            GameObject hatch = GameObject.Find("escape_hatch");
            hatch.layer = LayerMask.NameToLayer("Interactable");

            EnsurePlayerHitbox();

        }

        Camera _hmdCam;
        GameObject _playerHitbox;
        CapsuleCollider _playerHitboxCol;
        GameObject _playerHitboxDebug;

        const float PlayerHitboxRadius = 0.4f;
        const float PlayerHitboxHeight = 2f;

        void EnsurePlayerHitbox()
        {
            if (_playerHitbox != null) return;

            if (_hmdCam == null)
            {
                var allCams = UnityEngine.Object.FindObjectsOfType<Camera>();
                for (int i = 0; i < allCams.Length; i++)
                {
                    var c = allCams[i];
                    if (!c) continue;
                    var name = (c.name ?? "").ToLowerInvariant();
                    if (name.Contains("hmd") || c.tag == "MainCamera")
                    {
                        _hmdCam = c;
                        break;
                    }
                }
            }

            if (_hmdCam == null)
            {
                MelonLogger.Warning("[PlayerHitbox] HMD camera not found.");
                return;
            }

            _playerHitbox = new GameObject("PlayerHitbox");
            _playerHitbox.transform.SetParent(_hmdCam.transform, false);
            _playerHitbox.transform.localPosition = Vector3.zero;
            _playerHitbox.transform.localRotation = Quaternion.identity;

            _playerHitboxCol = _playerHitbox.AddComponent<CapsuleCollider>();
            _playerHitboxCol.isTrigger = true;
            _playerHitboxCol.direction = 1;
            _playerHitboxCol.radius = PlayerHitboxRadius;
            _playerHitboxCol.height = PlayerHitboxHeight;

            _playerHitboxCol.center = new Vector3(0f, -PlayerHitboxHeight * 0.5f, 0f);

            var rb = _playerHitbox.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                _playerHitbox.layer = playerLayer;

            MelonLogger.Msg("[PlayerHitbox] Created as child of HMD (top at head, rest below).");
        }

        public void RestartLevel()
        {

            MelonCoroutines.Start(Co_RestartLevel());

        }

        bool restartLevel = false;

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_RestartLevel()
        {
            MelonLogger.Msg("[Restart] - Restart Begun");
            blindPlayer(true);
            SetBlackout(false);
            yield return new WaitForSeconds(1f);
            _restartTearDown();
            yield return new WaitForSeconds(1f);
            yield return SceneManager.LoadSceneAsync("DeathRoom", LoadSceneMode.Additive);
            GameObject button = GameObject.Find("P_WinRoom_INT_DebriefCaseButton_01");
            GameObject deathTextObj = GameObject.Find("CauseOfDeath Text");
            GameObject sceneLoader = GameObject.Find("Scene Loader");
            UnityEngine.Object.Destroy(sceneLoader);
            TMPro.TextMeshPro deathText = deathTextObj.GetComponent<TextMeshPro>();
            deathText.text = "balled too hard";
            PhoenixButtonHook hook = button.AddComponent<PhoenixButtonHook>();
            while(!PhoenixButtonHook.restarting)
            {
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            blindPlayer(true);
            yield return SceneManager.UnloadSceneAsync("DeathRoom");
            yield return new WaitForSeconds(1f);

            yield return SceneManager.LoadSceneAsync("DeathRoom", LoadSceneMode.Additive);
            blindPlayer(true);
            yield return new WaitForSeconds(1f);
            yield return SceneManager.UnloadSceneAsync("DeathRoom");

            var restoredTools = UnityEngine.Object.Instantiate(_clonedToolsTemplate);
            restoredTools.name = "IEYTD2_Tools_ROOT";
            RemoveSuffixRecursive(restoredTools, CLONE_CHILD_SUFFIX);
            restoredTools.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(restoredTools);

            var restoredRig = UnityEngine.Object.Instantiate(_clonedVRRig);
            restoredRig.name = "VRRig";
            RemoveSuffixRecursive(restoredRig, CLONE_CHILD_SUFFIX);
            restoredRig.SetActive(false);

            var restoredRoot = UnityEngine.Object.Instantiate(_clonedModRoot);
            restoredRoot.name = "ModLevel_ROOT";
            RemoveSuffixRecursive(restoredRoot, CLONE_CHILD_SUFFIX);
            restoredRoot.SetActive(false);

            restoredTools.SetActive(true);
            restoredRig.SetActive(true);
            restoredRoot.SetActive(true);

            blindPlayer(true);
            yield return new WaitForSeconds(1f);

            ReplaceHenchman2UsingHenchman1();
            _mergedRoot = GameObject.Find(MergedRootName);
            if (!_mergedRoot)
            {

                _mergedRoot = restoredRoot;
                MelonLogger.Warning("[Restart] MergedRoot not found; using restoredRoot as _mergedRoot.");
            }
            else
            {
                MelonLogger.Msg("[Restart] Rebound _mergedRoot = " + _mergedRoot.name);
            }

            yield return null;
            yield return null;
            yield return null;

            attachScripts();
            setGrabbables();

            MiscStuff();

            ObjectBank.Instance?.RefreshAll();
            bank = ObjectBank.Instance;

            _mergeDonePending = true;

            MelonCoroutines.Start(Co_ForceEnableAllPickUpsFor(3f));

            MelonLogger.Msg("[Restart] Done.");
            yield return new WaitForSeconds(2f);
            blindPlayer(false);

            PhoenixButtonHook.restarting = false;
            yield break;

        }

        [HideFromIl2Cpp]
        void _restartTearDown()
        {
            DestroyCachedLiveObjects();

            var van = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all = Resources.FindObjectsOfTypeAll<GameObject>();

            int destroyed = 0;

            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (!go) continue;

                if (!go.scene.IsValid() || go.scene != van) continue;

                if (go.transform.parent != null) continue;

                if (_keepRootNames.Contains(go.name)) continue;

                UnityEngine.Object.Destroy(go);
                destroyed++;
            }

            MelonLogger.Msg("[Restart] Teardown destroyed Van roots: " + destroyed);

            var live = FindToolsRootDdol();
            if (live)
            {
                UnityEngine.Object.Destroy(live);
                MelonLogger.Msg("[Restart] Destroyed live IEYTD2_Tools_ROOT in DDoL.");
            }
        }

        [HideFromIl2Cpp]
        void DestroyCachedLiveObjects()
        {
            int killed = 0;

            foreach (var go in _liveModObjects)
            {
                if (go)
                {
                    UnityEngine.Object.Destroy(go);
                    killed++;
                }
            }

            foreach (var go in _liveToolObjects)
            {
                if (go)
                {
                    UnityEngine.Object.Destroy(go);
                    killed++;
                }
            }

            _liveModObjects.Clear();
            _liveToolObjects.Clear();

            MelonLogger.Msg("[Restart] Destroyed cached mod/tool objects: " + killed);
        }

        [HideFromIl2Cpp]
        void ReplaceHenchman2UsingHenchman1()
        {
            var h1 = GameObject.Find("Henchman1");
            var h2 = GameObject.Find("Henchman2");

            if (!h1 || !h2)
            {
                MelonLogger.Warning("[HenchFix] Could not find Henchman1 or Henchman2.");
                return;
            }

            Transform parent = h2.transform.parent;
            Vector3 pos = h2.transform.position;
            Quaternion rot = h2.transform.rotation;

            UnityEngine.Object.Destroy(h2);

            var clone = UnityEngine.Object.Instantiate(h1);
            clone.name = "Henchman2";

            clone.transform.SetParent(parent, true);
            clone.transform.position = pos;
            clone.transform.rotation = rot;

            var hc = clone.GetComponent<HenchmanController>();
            if (hc != null)
            {
                hc.startPoint = clone.transform;

                hc.RebuildAutoEndPoint();
            }

            var trs = clone.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < trs.Length; i++)
            {
                var t = trs[i];
                if (!t) continue;

                if (t.name == "Henchman1Muzzle")
                    t.name = "Henchman2Muzzle";
            }

            var rbs = clone.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (!rb) continue;
                rb.isKinematic = true;
                rb.detectCollisions = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var anim = clone.GetComponent<Animator>();
            if (anim)
            {
                anim.enabled = true;
                anim.applyRootMotion = false;
                anim.Rebind();
                anim.Update(0f);
            }

            MelonLogger.Msg("[HenchFix] Replaced Henchman2 with a clone of Henchman1.");
        }

        private void SafeSetInactive(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                go.SetActive(false);
            else
                MelonLogger.Warning($"[MiscStuff] GameObject '{name}' not found, cannot SetActive(false)");
        }

        public static GameObject FindContains(string substring)
        {
            substring = substring.ToLower();

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name.IndexOf(substring, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return go;
            }

            return null;
        }

        public void stageItems()
        {
            ObjectBank bank = ObjectBank.Instance;
            GameObject gun = bank.Shooter_Gathered;
            if (gun == null) MelonLogger.Warning("[StageItems] Shooter(Clone) is null");
            else MelonLogger.Msg("[StageItems] Shooter(Clone) confirmed");
            gun.AddComponent<BoxCollider>().size = new Vector3(0.1f, 0.28f, 0.38f);
            MakeGrabbable("Shooter(Clone)");
            gun.AddComponent<GunScript>();
            Shooter gunShooterScript = gun.GetComponent<Shooter>();
            LineRenderer gunLineRenderer = gun.GetComponent<LineRenderer>();
            if (gunShooterScript == null || gunLineRenderer == null) MelonLogger.Warning("one of gun scripts null");
            try { UnityEngine.Object.Destroy(gun.GetComponent<Shooter>()); } catch { }
            try { UnityEngine.Object.Destroy(gun.transform.GetChild(1).GetChild(0).GetComponent<LineRenderer>()); } catch { }
            gun.transform.GetChild(0).GetChild(0).gameObject.SetActive(true);
            GameObject wireCutters = bank.ELV_WireCutters_Gathered;

            GameObject cabinet = bank.P_Van_INT_Cabinet;

            _stageItem(
                cabinet,
                new Vector3(0.85f, -0.15f, 0f),
                new Vector3(0f, 0f, 0f),
                new Vector3(1.2f, 1.2f, 1.2f)
            );

            MelonLogger.Msg("Starting Drawer Stuff");

                GameObject TopDrawer = null;
                GameObject MiddleDrawer = null;
                for (int i = 0; i < cabinet.transform.childCount; i++)
                {
                    Transform child = cabinet.transform.GetChild(i);
                    string name = child.gameObject.name;

                    if (name == "TopDrawerRoot" ||
                        name == "MiddleDrawerRoot" ||
                        name == "BottomDrawerRoot")
                    {
                        if (name == "TopDrawerRoot") TopDrawer = child.gameObject;
                        else if (name == "MiddleDrawerRoot") MiddleDrawer = child.gameObject;

                        LinearMotion lm = child.GetComponent<LinearMotion>();
                        Vector3 p = child.position;

                        lm._worldStartPosition = new Vector3(p.x, p.y, p.z);
                        lm._worldEndPosition = new Vector3(p.x, p.y, 1.5f * (p.z + 0.4f));
                    }

                MelonLogger.Msg("Survived Drawer for loop");

                GameObject topDrawerInterior = getObjectInScene("SM_Van_INT_Top_DrawerInterior");
                MelonLogger.Msg("Got top drawer interior");
                GameObject middleDrawerInterior = getObjectInScene("SM_Van_INT_Middle_DrawerInterior");
                MelonLogger.Msg("Got middle drawer interior");
                GameObject newTopDrawerInterior = GameObject.Instantiate(middleDrawerInterior);
                MelonLogger.Msg("Made new top drawer interior");
                GameObject middleDrawerCollider = getObjectInScene("MiddleDrawerCollision");
                MelonLogger.Msg("Got middle drawer collider");
                GameObject topDrawerCollider = getObjectInScene("TopDrawerCollision");
                MelonLogger.Msg("Got top drawer collider");
                GameObject newTopDrawerCollider = GameObject.Instantiate(middleDrawerCollider);
                MelonLogger.Msg("made new top drawer collider");
                newTopDrawerCollider.transform.parent = topDrawerCollider.transform.parent;
                newTopDrawerCollider.transform.position = topDrawerCollider.transform.position;
                newTopDrawerInterior.transform.parent = topDrawerInterior.transform.parent;
                newTopDrawerInterior.transform.position = topDrawerInterior.transform.position;
                newTopDrawerInterior.transform.localScale = new Vector3(1, newTopDrawerInterior.transform.position.y, 1);
                newTopDrawerCollider.transform.localScale = new Vector3(1, 0.8f, 1);
                topDrawerInterior.SetActive(false);
                topDrawerCollider.SetActive(false);
                MelonLogger.Msg("End of cabinet stuff");

            }
            MelonLogger.Msg("Cabinet if statement survived");

            GameObject electricPanel = bank.ELV_RocketThrusterControlBox;

            _stageItem(electricPanel, new Vector3(0, -0.28f, -0.21f), new Vector3(0, 0, 0), new Vector3(1.2f, 1.2f, 1.2f));
            MelonLogger.Msg("staged electric panel");
            GameObject _lock = getChild(electricPanel, "ELV_PadlockMaintenence");
            MelonLogger.Msg("got lock");
            _stageItem(_lock, new Vector3(0.2737f, 0.43f, 0.4072f), Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f));
            MelonLogger.Msg("staged lock");
            getChild(electricPanel, "FireExtinguisherSocket").SetActive(false);
            MelonLogger.Msg("disable fire extinguisher");

            GameObject.Find("P_Elevator_INT_ThrusterConsole").transform.GetChild(0).gameObject.SetActive(false);
            MelonLogger.Msg("find thrust console");

            GameObject screwdriver = bank.ScrewdriverSocket;

            MelonLogger.Msg("Start panel stuff");

            bank.ELV_WireCutters_Gathered.AddComponent<WireCutterListener>();

            GameObject solderGun = bank.SolderingGun;
            solderGun.AddComponent<SolderListener>();

            GameObject panelHub1 = GameObject.Instantiate(bank.MaskChip);
            GameObject panelHub2 = GameObject.Instantiate(bank.MaskChip);
            GameObject panelHub3 = GameObject.Instantiate(bank.MaskChip);
            _stageItem(panelHub1, bank.ReactorHub.transform.position, new Vector3(6.3278f, 222.3137f, 340.6441f), new Vector3(3.1f, 3.1f, 3.1f));
            _stageItem(panelHub2, bank.CoolantHub.transform.position, new Vector3(6.3278f, 222.3137f, 340.6441f), new Vector3(3.1f, 3.1f, 3.1f));
            _stageItem(panelHub3, bank.TerminalHub.transform.position, new Vector3(6.3278f, 222.3137f, 340.6441f), new Vector3(3.1f, 3.1f, 3.1f));
            bank.ReactorHub.GetComponent<MeshRenderer>().enabled = false;
            bank.TerminalHub.GetComponent<MeshRenderer>().enabled = false;
            bank.CoolantHub.GetComponent<MeshRenderer>().enabled = false;
            Material pmat2 = panelHub2.GetComponent<Renderer>().material;
            Material pmat3 = panelHub3.GetComponent<Renderer>().material;
            Material[] exTex = bank.ExTexMaterials.GetComponent<MeshRenderer>().materials;
            Texture blueTex = exTex[0].mainTexture;
            Texture greenTex = exTex[1].mainTexture;
            pmat2.mainTexture = blueTex;
            pmat3.mainTexture = greenTex;
            panelHub1.name = "Red Chip";
            panelHub2.name = "Blue Chip";
            panelHub3.name = "Green Chip";
            MelonLogger.Msg("End panel stuff");

            GameObject flashlight = bank.P_Shop_INT_Flashlight;
            flashlight.AddComponent<FlashlightScript>();

            bank.WireSparkPoint.AddComponent<SparkDriver>();

            GameObject clipboard = bank.Clipboard;
            GameObject notebook = bank.Notebook;
            GameObject infoCard = bank.InfoCard;
            GameObject dnaPoster = bank.DNAPoster;
            GameObject sponge = bank.Sponge;

            infoCard.transform.GetChild(0).GetChild(0).gameObject.GetComponent<MeshRenderer>().material.mainTexture = SubBundle2Manager.GetTexture("MaintenanceGuide");
            clipboard.transform.GetChild(0).GetChild(0).gameObject.GetComponent<MeshRenderer>().material.mainTexture = SubBundle2Manager.GetTexture("Safety protocal v1");
            dnaPoster.transform.GetChild(0).GetChild(0).gameObject.GetComponent<MeshRenderer>().material.mainTexture = SubBundle2Manager.GetTexture("DNA Poster");
            notebook.transform.GetChild(0).GetChild(0).GetChild(0).gameObject.GetComponent<MeshRenderer>().material.mainTexture = SubBundle2Manager.GetTexture("MaintenanceGuide");

            MelonLogger.Msg("End of misc 1");

            Vector3 screwDriverPos = new Vector3(-1.0699f, -0.0344f, 1.2469f);
            Vector3 screwDriverRot = new Vector3(63.0761f, 244.7769f, 266.4102f);
            Vector3 wireCutterPos = new Vector3(-1.0626f, -0.05f, 0.8464f);
            Vector3 wireCutterRot = new Vector3(19.9753f, 90.0015f, 275.2095f);
            Vector3 solderingGunPos = new Vector3(-1.0616f, 0.0007f, 1.0666f);
            Vector3 solderingGunRot = new Vector3(55.9806f, 253.6458f, 76.3072f);
            Vector3 flashlightPos = new Vector3(0.6566f, 0.695f, 0.1496f);
            Vector3 flashlightRot = new Vector3(76.5124f, 89.1966f, 270);
            Vector3 dnaPos = new Vector3(1.4951f, 1.7219f, 5.6696f);
            Vector3 dnaRot = new Vector3(0.0707f, 2.6447f, 0.1433f);
            Vector3 dnaScale = new Vector3(1.5f, 1.5f, 1.5f);
            Vector3 clipboardPos = new Vector3(0.8841f, 0.5321f, 0.041f);
            Vector3 clipboardRot = new Vector3(359.974f, 87.4575f, 359.9668f);
            Vector3 notebookPos = new Vector3(0.8071f, 0.6927f, 0.1598f);
            Vector3 notebookRot = new Vector3(0, 358.872f, - 0.0001f);
            Vector3 infoPos = new Vector3(0.9219f, 0.5116f, 0.0959f);
            Vector3 infoRot = new Vector3(0.0421f, 358.5565f, 359.9732f);
            Vector3 spongePos = new Vector3(0.8235f, 0.0915f, 0.1707f);
            Vector3 spongeRot = new Vector3(89.5352f, 105.6711f, 324.355f);
            Vector3 gunPos = new Vector3(-1.1972f, -0.0127f, 1.0826f);
            Vector3 gunRot = new Vector3(-0.0002f, 0.8192f, 0.0047f);

            Vector3 cigarPos1 = new Vector3(1.0655f, 0.6968f, 0.2231f);
            Vector3 cigarRot = new Vector3(284.2945f, 268.2607f, 270.9795f);
            Vector3 cigarPos2 = new Vector3(0.9861f, 0.6968f, 0.223f);
            Vector3 cigarPos3 = new Vector3(1.0128f, 0.6968f, 0.223f);
            Vector3 cigarPos4 = new Vector3(0.9589f, 0.6968f, 0.2229f);
            Vector3 cigarPos5 = new Vector3(1.039f, 0.6969f, 0.223f);
            GameObject cigarOG = bank.Cigar;
            cigarOG.GetComponent<PickUp>().enabled = true;
            GameObject cigar1 = UnityEngine.Object.Instantiate(cigarOG); cigar1.name = "Cigar1";
            GameObject cigar2 = UnityEngine.Object.Instantiate(cigarOG); cigar2.name = "Cigar2";
            GameObject cigar3 = UnityEngine.Object.Instantiate(cigarOG); cigar3.name = "Cigar3";
            GameObject cigar4 = UnityEngine.Object.Instantiate(cigarOG); cigar4.name = "Cigar4";
            GameObject cigar5 = UnityEngine.Object.Instantiate(cigarOG); cigar5.name = "Cigar5";
            cigar1.AddComponent<CigarScript>(); cigar2.AddComponent<CigarScript>(); cigar3.AddComponent<CigarScript>();
            cigar4.AddComponent<CigarScript>(); cigar5.AddComponent<CigarScript>();
            Vector3 lighterPos = new Vector3(1.0075f, 0.7334f, 0.1342f);
            Vector3 lighterRot = new Vector3(0.0185f, 356.8924f, 359.9926f);
            GameObject lighter = bank.Lighter;
            lighter.GetComponent<PickUp>().enabled = true;

            Vector3 oxyPos = new Vector3(0.8847f, 0.0863f, 0.0177f);
            Vector3 oxyRot = new Vector3(293.5586f, 133.3137f, 166.096f);
            GameObject oxy = bank.P_PrivateJet_OxyMask;
            Vector3 applePos = new Vector3(0.7047f, 0.7444f, -0.1732f);
            Vector3 appleRot = new Vector3(351.0301f, 149.2909f, 350.9375f);
            GameObject apple = bank.Apple;
            Vector3 plaquePos = new Vector3(0.9915f, 0.6839f, - 0.2574f);
            Vector3 plaqueRot = new Vector3(270.0593f, 149.6739f, 0f);
            GameObject plaque = bank.ZorPlaque;
            Vector3 redBookPos = new Vector3(0.9697f, 0.2923f, 0.1554f);
            Vector3 redBookRot = new Vector3(89.9234f, 182.6562f, 0);
            GameObject redBook = bank.RedBook;
            Vector3 greenBookPos = new Vector3(0.9686f, 0.3166f, 0.142f);
            Vector3 greenBookRot = new Vector3(89.8298f, 326.8405f, 149.8868f);
            GameObject greenBook = bank.GreenBook;
            Vector3 plantPos = new Vector3(0.9329f, 0.7709f, -0.1733f);
            Vector3 plantRot = new Vector3(270.368f, 325.0144f, 124.1181f);
            GameObject plant = bank.Plant;
            Vector3 screwsPos = new Vector3(0.8464f, 0.3013f, 0.1507f);
            Vector3 screwsRot = new Vector3(0, 273.524f, -0.0001f);
            GameObject screws = bank.Screws;
            Vector3 solderPaperPos = new Vector3(0.9068f, 0.0567f, -0.096f);
            Vector3 solderPaperRot = new Vector3(0.002f, 81.0796f, 0.0279f);
            GameObject solderPaper = bank.SolderPaper;
            Vector3 paperClipPos = new Vector3(0.761f, 0.2966f, 0.1461f);
            Vector3 paperClipRot = new Vector3(0, 266.7548f, 0);
            GameObject paperClips = bank.Paperclips;

            MelonLogger.Msg("Vector3s got");

            _stageItem(clipboard, clipboardPos, clipboardRot);
            _stageItem(notebook, notebookPos, notebookRot);
            _stageItem(infoCard, infoPos, infoRot);
            _stageItem(dnaPoster, dnaPos, dnaRot, dnaScale);
            _stageItem(sponge, spongePos, spongeRot);
            _stageItem(flashlight, flashlightPos, flashlightRot);
            _stageItem(solderGun, solderingGunPos, solderingGunRot);
            _stageItem(screwdriver, screwDriverPos, screwDriverRot);
            _stageItem(wireCutters, wireCutterPos, wireCutterRot);
            _stageItem(cigar1, cigarPos1, cigarRot);
            _stageItem(cigar2, cigarPos2, cigarRot);
            _stageItem(cigar3, cigarPos3, cigarRot);
            _stageItem(cigar4, cigarPos4, cigarRot);
            _stageItem(cigar5, cigarPos5, cigarRot);
            _stageItem(lighter, lighterPos, lighterRot);
            apple.transform.parent = null;
            apple.SetActive(true);
            _stageItem(apple, applePos, appleRot);
            _stageItem(plaque, plaquePos, plaqueRot);
            _stageItem(plant, plantPos, plantRot);
            _stageItem(redBook, redBookPos, redBookRot);
            _stageItem(greenBook, greenBookPos, greenBookRot);
            _stageItem(screws, screwsPos, screwsRot);
            _stageItem(paperClips, paperClipPos, paperClipRot);
            _stageItem(solderPaper, solderPaperPos, solderPaperRot);
            _stageItem(oxy, oxyPos, oxyRot);

            GameObject gunPickUp = GameObject.Find("PickUp_HOST_Shooter(Clone)");
            if (gunPickUp == null) MelonLogger.Warning("[StageItems] - gunPickUp null");
            _stageItem(gunPickUp, gunPos, gunRot);
            bank.ScrewdriverSocket.GetComponent<PickUp>().enabled = true;
            MakeEachChipHaveUniqueLedMaterial();
            oxy.GetComponent<Rigidbody>().isKinematic = false;
            apple.GetComponent<Rigidbody>().isKinematic = true;
            GameObject glass = GameObject.Find("SM_Glass");
            glass.GetComponent<MeshRenderer>().enabled = true;
            glass.GetComponent<MeshRenderer>().material = bank.SM_Van_ENV_Small_Window_Glass.transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material;
            glass.AddComponent<GlassDriver>();
            GameObject.Find("PickUpTest").SetActive(false);

        }

        readonly Dictionary<int, Color> _chipLedOnColorByRenderer = new Dictionary<int, Color>();

        public void SetChipLed_OnlyLightMaterial(string chipNameContains, bool on)
        {
            int matsTouched = 0;

            var rends = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r || !r.gameObject) continue;

                string goName = r.gameObject.name ?? "";
                if (goName.IndexOf(chipNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                int rid = r.GetInstanceID();

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (!mat) continue;

                    string matName = mat.name ?? "";
                    if (matName.IndexOf("Chip_Light", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!mat.HasProperty("_EmissionColor")) continue;

                    if (!_chipLedOnColorByRenderer.ContainsKey(rid))
                    {
                        Color e = new Color(0.146f, 1.0f, 0.0f, 1.0f);
                        try { e = mat.GetColor("_EmissionColor"); } catch { }
                        _chipLedOnColorByRenderer[rid] = e;
                    }

                    Color onE = _chipLedOnColorByRenderer[rid];
                    Color finalE = on ? onE : Color.black;

                    try
                    {
                        mat.SetColor("_EmissionColor", finalE);

                        if (on)
                        {
                            mat.EnableKeyword("_EMISSION");
                            mat.EnableKeyword("EMISSION");
                        }
                        else
                        {
                            mat.DisableKeyword("_EMISSION");
                            mat.DisableKeyword("EMISSION");
                        }
                    }
                    catch { }

                    matsTouched++;
                }
            }

            MelonLogger.Msg($"[ChipLED] '{chipNameContains}' -> {(on ? "ON" : "OFF")} | ledMatsTouched={matsTouched}");
        }

        public void MakeEachChipHaveUniqueLedMaterial()
        {
            int clones = 0;

            var rends = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (!r || !r.gameObject) continue;

                string goName = r.gameObject.name ?? "";
                if (goName.IndexOf("Chip", StringComparison.OrdinalIgnoreCase) < 0) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                bool changed = false;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (!mat) continue;

                    string matName = mat.name ?? "";
                    if (matName.IndexOf("Chip_Light", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var inst = new Material(mat);
                    inst.name = mat.name + "_INST_" + goName;
                    inst.hideFlags = HideFlags.DontUnloadUnusedAsset;

                    mats[m] = inst;
                    clones++;
                    changed = true;
                }

                if (changed)
                    r.sharedMaterials = mats;
            }

            MelonLogger.Msg("[ChipLED] Unique Chip_Light materials created: " + clones);
        }

        GameObject getChild(GameObject parent, string name)
        {
            for(int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                string childName = child.gameObject.name;
                if (childName == name) return child.gameObject;
            }
            return null;
        }

        private void _stageItem(GameObject obj, Vector3 worldPos)
        {
            if (!obj)
            {
                MelonLogger.Error("[MyMod] _stageItem called with null obj; skipping.");
                return;
            }

            obj.SetActive(true);
            obj.transform.position = worldPos;
            if (!obj.name.ToLower().Contains("cigar")) ensureNotFlammable(obj);
        }

        void _stageItem(GameObject obj, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            obj.SetActive(true);
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(rot);
            Vector3 _scale = obj.transform.localScale;
            obj.transform.localScale = scale;
            if(!obj.name.ToLower().Contains("cigar")) ensureNotFlammable(obj);

        }

        void _stageItem(GameObject obj, Vector3 pos, Vector3 rot)
        {
            obj.transform.position = pos;
            obj.transform.rotation = Quaternion.Euler(rot);
            if (!obj.name.ToLower().Contains("cigar")) ensureNotFlammable(obj);
            obj.SetActive(true);
        }

        void ensureNotFlammable(GameObject obj)
        {
            SG.Phoenix.Assets.Code.WorldAttributes.Flammable flammable = obj.GetComponent<Flammable>();
            if (flammable == null) return;
            else
            {
                UnityEngine.Object.Destroy(obj.GetComponent<Flammable>());
            }
        }

        public void disableMeshRenderers()
        {
            string[] objects = new string[] { "GrateCollider", "FanTrigger", "ReactorVentTrigger", "VentSponge" };
            foreach (string name in objects)
            {
                GameObject.Find(name).GetComponent<MeshRenderer>().enabled = false;
            }
            MelonLogger.Msg("mesh renderers turned off");
        }

        public void testAUS(string name, string script)
        {
            AttachUnityScript.AttachScriptToGameObject(name, script);
        }

        void mergeDone()
        {
            stageItems();

            ObjectBank bank = ObjectBank.Instance;

            if (SubmarineRunManager.Instance != null)
            {
                SubmarineRunManager.Instance.RegisterInitialRunRoot(_mergedRoot);
            }
            else
            {
                MelonLogger.Warning("[RunManager] No SubmarineRunManager instance found; restart from template will be unavailable.");
            }

        }

        public GameObject getObjectInScene(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;

            string query = objectName.Trim().ToLowerInvariant();
            GameObject best = null;

            if (_mergedRoot != null)
            {
                var trs = _mergedRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    var go = trs[i]?.gameObject; if (!go) continue;
                    string n = (go.name ?? "").ToLowerInvariant();
                    if (n == query) return go;
                    if (best == null && n.Contains(query)) best = go;
                }
                if (best != null) return best;
            }

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i];
                if (!go || !go.scene.IsValid()) continue;
                string n = (go.name ?? "").ToLowerInvariant();
                if (n == query) return go;
                if (best == null && n.Contains(query)) best = go;
            }

            return best;
        }

        Type _tRotMotionManaged, _tRotVelManaged;
        Il2CppSystem.Type _tRotMotionIl2, _tRotVelIl2;

        void ResolveRotationTypes()
        {
            if (_tRotMotionManaged == null)
                _tRotMotionManaged = FindTypeBySuffix(".Interactables.RotationalMotion");
            if (_tRotVelManaged == null)
                _tRotVelManaged = FindTypeBySuffix(".WorldAttributes.Tracking.RotationalVelocityTracker");

            if (_tRotMotionManaged != null && _tRotMotionIl2 == null)
                _tRotMotionIl2 = Il2CppType.From(_tRotMotionManaged);
            if (_tRotVelManaged != null && _tRotVelIl2 == null)
                _tRotVelIl2 = Il2CppType.From(_tRotVelManaged);

            if (_tRotMotionManaged == null) MelonLogger.Error("[Wheel] Phoenix RotationalMotion type not found.");
            if (_tRotVelManaged == null) MelonLogger.Warning("[Wheel] RotationalVelocityTracker type not found (optional).");
        }

        static void SetMemberIfExists(Component c, string name, object value)
        {
            if (c == null || name == null) return;
            try
            {
                var t = c.GetType();
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType.IsAssignableFrom(value?.GetType() ?? typeof(object)))
                { f.SetValue(c, value); return; }

                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite)
                {

                    var targetType = p.PropertyType;
                    object val = value;
                    if (targetType == typeof(Vector3) && value is Vector3 v3) val = v3;
                    if (targetType == typeof(float) && value is float f32) val = f32;
                    if (targetType.IsEnum && value is int i) val = Enum.ToObject(targetType, i);
                    p.SetValue(c, val, null); return;
                }
            }
            catch { }
        }

        static Collider CloneAsChildCollider(GameObject dst, Collider src, bool isTrigger)
        {
            if (!dst || !src) return null;
            Collider c = null;
            try
            {
                if (src is BoxCollider b)
                {
                    var nb = dst.AddComponent<BoxCollider>();
                    nb.center = b.center; nb.size = b.size; nb.isTrigger = isTrigger; c = nb;
                }
                else if (src is SphereCollider s)
                {
                    var ns = dst.AddComponent<SphereCollider>();
                    ns.center = s.center; ns.radius = s.radius; ns.isTrigger = isTrigger; c = ns;
                }
                else if (src is CapsuleCollider ca)
                {
                    var nca = dst.AddComponent<CapsuleCollider>();
                    nca.center = ca.center; nca.radius = ca.radius; nca.height = ca.height; nca.direction = ca.direction;
                    nca.isTrigger = isTrigger; c = nca;
                }
                else if (src is MeshCollider mc)
                {
                    var nmc = dst.AddComponent<MeshCollider>();
                    nmc.sharedMesh = mc.sharedMesh; nmc.convex = true; nmc.isTrigger = isTrigger; c = nmc;
                }
            }
            catch { }
            return c;
        }

        public void MakeRotationalWheel(string itemName, Vector3 axisLocal, float minDeg = -90f, float maxDeg = 90f)
        {
            ResolveRotationTypes();
            if (_tRotMotionIl2 == null)
            {
                MelonLogger.Error("[Wheel] RotationalMotion type missing; cannot proceed.");
                return;
            }

            var target = FindTargetByName(itemName);
            if (!target) { MelonLogger.Error($"[Wheel] '{itemName}' not found."); return; }

            var host = new GameObject("Wheel_HOST_" + target.name);
            if (_mergedRoot) host.transform.SetParent(_mergedRoot.transform, true);
            host.transform.position = target.transform.position;
            host.transform.rotation = target.transform.rotation;
            host.layer = target.layer;

            var rb = host.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;

            target.transform.SetParent(host.transform, true);

            var physRoot = new GameObject("Collision_Phys"); physRoot.transform.SetParent(host.transform, false);
            var gripRoot = new GameObject("Collision_Grip"); gripRoot.transform.SetParent(host.transform, false);

            int phys = 0, trig = 0;
            foreach (var c in target.GetComponentsInChildren<Collider>(true))
            {
                if (!c || !c.enabled) continue;
                var a = CloneAsChildCollider(physRoot, c, false); if (a) phys++;
                var b = CloneAsChildCollider(gripRoot, c, true); if (b) trig++;
            }

            if (phys == 0 && trig == 0)
            {
                var ring = gripRoot.AddComponent<CapsuleCollider>();
                ring.isTrigger = true;
                ring.center = Vector3.zero;
                ring.height = 0.4f; ring.radius = 0.25f; ring.direction = 0;
                trig = 1;
            }

            var rot = host.AddComponent(_tRotMotionIl2);

            SetMemberIfExists(rot, "RotationAxis", axisLocal.normalized);
            SetMemberIfExists(rot, "StartRotation", minDeg);
            SetMemberIfExists(rot, "EndRotation", maxDeg);

            SetMemberIfExists(rot, "Friction", 0.001f);
            SetMemberIfExists(rot, "SlideSpeed", 50f);
            SetMemberIfExists(rot, "Locked", false);

            try
            {
                var planeCtor = typeof(Plane).GetConstructor(new[] { typeof(Vector3), typeof(float) });
                if (planeCtor != null)
                {
                    var plane = planeCtor.Invoke(new object[] { axisLocal.normalized, 0f });
                    SetMemberIfExists(rot, "RotationPlane", plane);
                }
            }
            catch { }

            try
            {
                var coll = new List<Collider>();
                coll.AddRange(physRoot.GetComponentsInChildren<Collider>(true));
                coll.AddRange(gripRoot.GetComponentsInChildren<Collider>(true));
                var arr = (Array)Activator.CreateInstance(typeof(Collider[]), coll.Count);
                for (int i = 0; i < coll.Count; i++) arr.SetValue(coll[i], i);
                SetMemberIfExists(rot, "_CachedColliders", arr);
            }
            catch { }

            if (_tRotVelIl2 != null)
            {
                var tracker = host.AddComponent(_tRotVelIl2);
                SetMemberIfExists(tracker, "RotationalMotion", rot);
            }

            MelonLogger.Msg($"[Wheel] '{itemName}' set up as RotationalMotion. Phys={phys}, Trig={trig}, Axis={axisLocal}, Min={minDeg}, Max={maxDeg}");
        }

        void SetupWheelLikeSchell(string wheelName, Vector3 localAxis, float minDeg, float maxDeg)
        {
            var wheel = FindTargetByName(wheelName);
            if (!wheel) { MelonLogger.Error($"[Wheel] '{wheelName}' not found."); return; }

            var rb = wheel.GetComponent<Rigidbody>() ?? wheel.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            RigidbodyConstraints freeze =
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationY |
                RigidbodyConstraints.FreezeRotationZ;

            Vector3 a = localAxis.normalized;
            int free = 2;
            if (Mathf.Abs(a.x) >= Mathf.Abs(a.y) && Mathf.Abs(a.x) >= Mathf.Abs(a.z)) free = 0;
            else if (Mathf.Abs(a.y) >= Mathf.Abs(a.x) && Mathf.Abs(a.y) >= Mathf.Abs(a.z)) free = 1;
            if (free == 0) freeze &= ~RigidbodyConstraints.FreezeRotationX;
            if (free == 1) freeze &= ~RigidbodyConstraints.FreezeRotationY;
            if (free == 2) freeze &= ~RigidbodyConstraints.FreezeRotationZ;
            rb.constraints = freeze;

            Collider solid = null;
            foreach (var c in wheel.GetComponentsInChildren<Collider>(true))
                if (c && !c.isTrigger) { solid = c; break; }
            if (!solid)
            {
                var rend = wheel.GetComponentInChildren<Renderer>(true);
                var bc = wheel.AddComponent<BoxCollider>();
                if (rend)
                {
                    var b = rend.bounds;
                    bc.center = wheel.transform.InverseTransformPoint(b.center);
                    Vector3 s = b.size;
                    var p0 = wheel.transform.InverseTransformVector(new Vector3(s.x, 0, 0));
                    var p1 = wheel.transform.InverseTransformVector(new Vector3(0, s.y, 0));
                    var p2 = wheel.transform.InverseTransformVector(new Vector3(0, 0, s.z));
                    bc.size = new Vector3(Mathf.Abs(p0.x) + Mathf.Abs(p1.x) + Mathf.Abs(p2.x),
                                          Mathf.Abs(p0.y) + Mathf.Abs(p1.y) + Mathf.Abs(p2.y),
                                          Mathf.Abs(p0.z) + Mathf.Abs(p1.z) + Mathf.Abs(p2.z));
                }
                solid = bc;
            }

            var trigger = wheel.GetComponent<SphereCollider>();
            if (!trigger)
            {
                trigger = wheel.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.25f;
            }

            ResolvePhoenixTypes();
            if (_tPickUpIl2 != null && wheel.GetComponent(_tPickUpIl2) == null)
            {
                var pu = wheel.AddComponent(_tPickUpIl2) as Component;
                if (pu is Behaviour b) b.enabled = true;
                KickPickUpEnableGuardian(pu, 4f);
            }

            BreakAllJoints(wheel);
            DisablePickUpHoldersByNameAndType(true);
            _holdersDisabled = true;

            var tRotManaged = FindTypeBySuffix(".Interactables.RotationalMotion");
            if (tRotManaged == null) { MelonLogger.Error("[Wheel] RotationalMotion type not found."); return; }
            var tRotIl2 = UnhollowerRuntimeLib.Il2CppType.From(tRotManaged);
            var rm = wheel.GetComponent(tRotIl2) as Component;
            if (!rm) rm = wheel.gameObject.AddComponent(tRotIl2);

            TrySetMember(rm, "RotationAxis", localAxis);
            TrySetMember(rm, "_rotationAxis", localAxis);
            TrySetMember(rm, "LockedMinValue", minDeg);
            TrySetMember(rm, "LockedMaxValue", maxDeg);
            TrySetMember(rm, "minAngle", minDeg);
            TrySetMember(rm, "maxAngle", maxDeg);

            var cols = wheel.GetComponentsInChildren<Collider>(true);
            BindCollidersToInteractable(rm, cols);

            int interact = LayerMask.NameToLayer("Interactable"); if (interact < 0) interact = wheel.layer;
            SetLayerRecursive(wheel, interact);

            int physCount = 0, trigCount = 0; foreach (var c in cols) if (c) { if (c.isTrigger) trigCount++; else physCount++; }
            MelonLogger.Msg($"[Wheel] '{wheel.name}' ready. Phys={physCount}, Trig={trigCount}, Axis={localAxis}, Min={minDeg}, Max={maxDeg}, FreeRotAxis={(free == 0 ? "X" : free == 1 ? "Y" : "Z")}");
        }

        void TrySetMember(object obj, string name, object value)
        {
            if (obj == null) return;
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try { var f = t.GetField(name, F); if (f != null && f.FieldType.IsAssignableFrom(value.GetType())) { f.SetValue(obj, value); return; } } catch { }
            try { var p = t.GetProperty(name, F); if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(value.GetType())) { p.SetValue(obj, value, null); return; } } catch { }
        }

        void BindCollidersToInteractable(Component interactable, Collider[] cols)
        {
            if (!interactable) return;
            try
            {
                var arr = new UnhollowerBaseLib.Il2CppReferenceArray<Collider>(cols.Length);
                for (int i = 0; i < cols.Length; i++) arr[i] = cols[i];
                if (TrySetFirstColliderArrayField(interactable, arr)) return;
            }
            catch { }

            if (TrySetFirstColliderArrayField(interactable, cols)) return;
        }

        bool TrySetFirstColliderArrayField(object obj, object value)
        {
            var t = obj.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            foreach (var f in t.GetFields(F))
            {
                try
                {
                    var ft = f.FieldType;
                    if (ft.FullName != null && ft.FullName.Contains("Collider") && ft.IsArray == value.GetType().IsArray)
                    { f.SetValue(obj, value); return true; }
                    if (ft.FullName != null && ft.FullName.Contains("Il2CppReferenceArray") && value.GetType().FullName.Contains("Il2CppReferenceArray"))
                    { f.SetValue(obj, value); return true; }
                }
                catch { }
            }
            return false;
        }

        void InspectVanGrabbable(bool writeOnly)
        {
            ResolvePhoenixTypes();

            var donor = FindBestDonorPickUp();
            if (donor == null)
            {
                MelonLogger.Error("[GrabInspect] No Phoenix PickUp donor found in host scene.");
                return;
            }

            var go = donor.gameObject;
            var path = Path.Combine(MelonUtils.UserDataDirectory, "IEYTD2_GrabbableReport.txt");
            using (var w = new StreamWriter(path, false))
            {
                w.WriteLine("IEYTD2 Grabbable Report");
                w.WriteLine("Timestamp: " + DateTime.Now);
                w.WriteLine();

                DumpGameObjectDeep(w, go, "DONOR");
                w.WriteLine();
                w.WriteLine("Scene-wide notes:");
                w.WriteLine("• Interactable layer index: " + LayerMask.NameToLayer("Interactable"));
                w.WriteLine("• Potential holders in scene: " + CountHolders());
            }
            MelonLogger.Msg("[GrabInspect] Wrote IEYTD2_GrabbableReport.txt");

            if (writeOnly) return;
        }

        void TryBootstrapGrabbableFromDonor()
        {
            var target = FindTargetByName("PickUpTest");
            if (!target) return;

            var donor = FindBestDonorPickUp();
            if (donor != null && TryCloneDonorAsHost(donor, target))
            { MelonLogger.Msg("[Grab] PickUpTest ready (donor host)."); return; }

            if (TryFallbackAddComponents(target))
                MelonLogger.Msg("[Grab] PickUpTest ready (fallback add-components).");
        }

        void ResolvePhoenixTypes()
        {
            if (_tPickUpManaged == null) _tPickUpManaged = FindTypeBySuffix(".Interactables.PickUp");
            if (_tShakeManaged == null) _tShakeManaged = FindTypeBySuffix(".Gestures.PickUpShakeGesture");

            if (_tPickUpManaged != null && _tPickUpIl2 == null) _tPickUpIl2 = Il2CppType.From(_tPickUpManaged);
            if (_tShakeManaged != null && _tShakeIl2 == null) _tShakeIl2 = Il2CppType.From(_tShakeManaged);

            if (_tPickUpManaged == null) MelonLogger.Error("[Grab] Phoenix PickUp type not found.");
            else MelonLogger.Msg("[Grab] PickUp=" + _tPickUpManaged.FullName + "  Shake=" + (_tShakeManaged != null ? _tShakeManaged.FullName : "(none)"));
        }

        Component FindBestDonorPickUp()
        {
            if (_tPickUpIl2 == null) return null;
            Component best = null;

            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGos.Length; i++)
            {
                var g = allGos[i];
                if (!g || !g.scene.IsValid()) continue;
                var c = g.GetComponent(_tPickUpIl2) as Component;
                if (c == null) continue;

                string nm = g.name.ToLowerInvariant();
                if (nm.Contains("fastfoodcup") || nm.Contains("cup")) return c;

                if (best == null || g.GetComponent<Rigidbody>() != null) best = c;
            }
            return best;
        }

        void KickPickUpEnableGuardian(Component pickUp, float seconds = 3f)
        {
            if (pickUp == null) return;
            MelonCoroutines.Start(Co_PickUpEnableGuardian(pickUp, seconds));
        }

        System.Collections.IEnumerator Co_PickUpEnableGuardian(Component pickUp, float seconds)
        {
            float end = Time.time + Mathf.Max(0.25f, seconds);
            while (pickUp != null && Time.time < end)
            {
                try
                {
                    var beh = pickUp as Behaviour;
                    if (beh != null && !beh.enabled) beh.enabled = true;

                    var go = pickUp.gameObject;
                    if (go != null && !go.activeSelf) go.SetActive(true);

                    TrySetBoolProperty(pickUp, "IsEnabled", true);
                    TrySetBoolProperty(pickUp, "Enabled", true);
                    TrySetBoolField(pickUp, "isEnabled", true);

                    var rb = (go != null) ? go.GetComponent<Rigidbody>() : null;
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.detectCollisions = true;
                    }
                }
                catch { }
                yield return null;
            }
        }

        static bool TrySetBoolProperty(object o, string name, bool val)
        {
            try
            {
                var p = o.GetType().GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) { p.SetValue(o, val, null); return true; }
            }
            catch { }
            return false;
        }
        static bool TrySetBoolField(object o, string name, bool val)
        {
            try
            {
                var f = o.GetType().GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(bool)) { f.SetValue(o, val); return true; }
            }
            catch { }
            return false;
        }

        void ForceEnableAllPickUpsInScene()
        {
            ResolvePhoenixTypes();
            var all = Resources.FindObjectsOfTypeAll<Component>();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i]; if (!c) continue;
                if (IsPickUpComponent(c))
                {
                    try
                    {
                        var b = c as Behaviour;
                        if (b != null && !b.enabled) { b.enabled = true; n++; }
                        var go = c.gameObject; if (go && !go.activeSelf) go.SetActive(true);
                    }
                    catch { }
                }
            }
        }

        bool IsPickUpComponent(Component comp)
        {
            if (comp == null) return false;
            try
            {
                var st = comp.GetType();
                var name = st.FullName ?? st.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    if (_tPickUpManaged != null && name == _tPickUpManaged.FullName) return true;
                    if (name.EndsWith(".Interactables.PickUp", StringComparison.Ordinal)) return true;
                }

                try
                {
                    var il2 = Il2CppType.From(st);
                    var ilName = il2?.FullName ?? il2?.Name;
                    if (!string.IsNullOrEmpty(ilName) && ilName.EndsWith(".Interactables.PickUp", StringComparison.Ordinal))
                        return true;
                }
                catch { }

            }
            catch { }
            return false;
        }

        System.Collections.IEnumerator Co_ForceEnableAllPickUpsFor(float seconds = 3f)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { ForceEnableAllPickUpsInScene(); yield return null; }
        }

        bool TryCloneDonorAsHost(Component donorPickUp, GameObject target)
        {
            try
            {
                var donorGO = donorPickUp.gameObject;

                var host = UnityEngine.Object.Instantiate(donorGO);
                host.name = "PickUp_HOST_" + target.name;

                host.transform.SetParent(_mergedRoot != null ? _mergedRoot.transform : null, true);
                host.transform.position = target.transform.position;
                host.transform.rotation = target.transform.rotation;
                host.transform.localScale = target.transform.lossyScale;

                var donorRends = host.GetComponentsInChildren<Renderer>(true);

                var rb = host.GetComponent<Rigidbody>() ?? host.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                if (rb.mass <= 0f) rb.mass = 1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var mcs = host.GetComponentsInChildren<MeshCollider>(true);
                for (int i = 0; i < mcs.Length; i++)
                    if (mcs[i]) mcs[i].convex = true;

                for (int i = 0; i < donorRends.Length; i++)
                {
                    var r = donorRends[i];
                    if (r) r.enabled = false;
                }

                target.transform.SetParent(host.transform, true);

                foreach (var tRb in target.GetComponentsInChildren<Rigidbody>(true))
                    UnityEngine.Object.Destroy(tRb);

                {
                    var allCols = host.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < allCols.Length; i++)
                    {
                        var c = allCols[i];
                        if (!c) continue;

                        if (c.transform == target.transform || c.transform.IsChildOf(target.transform))
                            continue;

                        if (c.isTrigger)
                            continue;

                        UnityEngine.Object.Destroy(c);
                    }
                }

                EnsureTargetVisualsOn(target);
                int interact = LayerMask.NameToLayer("Interactable");
                if (interact < 0) interact = 8;
                SetLayerRecursive(target, interact);
                EnsureLayerVisibleToCamera(target, _hmd);

                if (_tShakeIl2 != null && host.GetComponent(_tShakeIl2) == null)
                    host.AddComponent(_tShakeIl2);

                var pickUpComp = host.GetComponent(_tPickUpIl2);
                if (pickUpComp != null)
                {
                    try { (pickUpComp as Behaviour).enabled = true; } catch { }
                    KickPickUpEnableGuardian(pickUpComp, 4f);

                    var il2Cols = target.GetComponentsInChildren<Collider>(true);

                    var list = new System.Collections.Generic.List<Collider>();
                    if (il2Cols != null)
                    {
                        for (int i = 0; i < il2Cols.Length; i++)
                        {
                            var c = il2Cols[i];
                            if (c) list.Add(c);
                        }
                    }

                    if (list.Count == 0)
                    {
                        var mf = target.GetComponentInChildren<MeshFilter>(true);
                        if (mf && mf.sharedMesh)
                        {
                            var mc = target.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = true;
                            list.Add(mc);
                        }
                        else
                        {
                            list.Add(target.AddComponent<BoxCollider>());
                        }
                    }

                    BindCollidersToInteractable(pickUpComp, list.ToArray());
                }

                TogglePickUpHolders(true);
                MelonLogger.Msg("[GrabHost] Donor '" + donorGO.name + "' cloned. Host drives physics & PickUp.");

                EnsureTargetVisualsOn(target);
                ForceEnablePickUpsUnder(host);
                ForceEnablePickUpsUnder_Fallback(host);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[GrabHost] " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        void EnsureTargetVisualsOn(GameObject target)
        {
            if (!target) return;
            target.SetActive(true);

            var rends = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r)
                {
                    try
                    {
                        r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                        try { r.forceRenderingOff = false; } catch {}
#endif
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        r.receiveShadows = true;
                    }
                    catch { }
                }
            }
        }

        bool TryFallbackAddComponents(GameObject target)
        {
            try
            {
                if (_tPickUpIl2 == null) return false;

                var parent = target.transform.parent;
                if (parent != null && parent.name.StartsWith("PickUp_HOST_", StringComparison.Ordinal))
                {
                    target.transform.SetParent(parent.parent, true);
                    try { UnityEngine.Object.Destroy(parent.gameObject); } catch { }
                    MelonLogger.Msg("[GrabFallback] Removed donor host, switching to direct attach.");
                }

                BreakAllJoints(target);

                DisablePickUpHoldersByNameAndType(true);
                _holdersDisabled = true;

                var cols = target.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i]; if (!c) continue;
                    try { c.enabled = true; } catch { }
                    var mc = c as MeshCollider; if (mc != null) { try { mc.convex = true; } catch { } }
                }

                if (target.GetComponent(_tPickUpIl2) == null) target.AddComponent(_tPickUpIl2);
                if (_tShakeIl2 != null && target.GetComponent(_tShakeIl2) == null) target.AddComponent(_tShakeIl2);

                var rb = target.GetComponent<Rigidbody>() ?? target.AddComponent<Rigidbody>();
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.mass = (rb.mass <= 0f) ? 1f : rb.mass;
                rb.drag = Mathf.Max(rb.drag, 0.05f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, 0.05f);
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.None;
                rb.detectCollisions = true;

                bool hadCollider = false;
                for (int i = 0; i < cols.Length; i++) if (cols[i]) { hadCollider = true; break; }
                if (!hadCollider)
                {
                    var mf = target.GetComponentInChildren<MeshFilter>(true);
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = target.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh; mc.convex = true;
                    }
                    else target.AddComponent<BoxCollider>();
                }

                int interact = LayerMask.NameToLayer("Interactable"); if (interact < 0) interact = 8;
                SetLayerRecursive(target, interact);

                var pu = target.GetComponent(_tPickUpIl2);
                if (pu != null)
                {
                    try { (pu as Behaviour).enabled = true; } catch { }
                    KickPickUpEnableGuardian(pu, 4f);
                }

                try
                {
                    var rend = target.GetComponentInChildren<Renderer>(true);
                    if (rend != null)
                        rb.centerOfMass = rb.transform.InverseTransformPoint(rend.bounds.center);
                }
                catch { }

                ForceEnablePickUpsUnder(target);
                ForceEnablePickUpsUnder_Fallback(target);

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[GrabFallback] " + ex.GetType().Name + ": " + ex.Message);
                ForceEnablePickUpsUnder(target);
                return false;
            }
        }

        void BreakAllJoints(GameObject go)
        {
            if (!go) return;
            foreach (var j in go.GetComponents<Joint>()) { try { UnityEngine.Object.Destroy(j); } catch { } }
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) { try { UnityEngine.Object.Destroy(j); } catch { } }
        }

        string SafeTypeName(Component c)
        {
            try
            {
                var t = c.GetType();
                return (t.FullName ?? t.Name) ?? "";
            }
            catch { return ""; }
        }

        void TryEnableBehaviourAny(Component c)
        {
            if (c == null) return;

            try { var b = c as Behaviour; if (b != null) { b.enabled = true; return; } } catch { }

            try
            {
                var ty = c.GetType();
                var p = ty.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite) { p.SetValue(c, true, null); return; }
                var f = ty.GetField("enabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) { f.SetValue(c, true); return; }
            }
            catch { }
        }

        void ForceEnablePickUpsUnder_Fallback(GameObject root)
        {
            if (!root) return;
            root.SetActive(true);

            int hitsByName = 0, hitsBrute = 0;
            var comps = root.GetComponentsInChildren<Component>(true);

            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i]; if (!c) continue;
                var tn = SafeTypeName(c);
                if (tn.IndexOf("pickup", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryEnableBehaviourAny(c);
                    hitsByName++;
                }
            }

            if (hitsByName == 0)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i]; if (!c) continue;
                    TryEnableBehaviourAny(c);
                    hitsBrute++;
                }
            }

            MelonLogger.Msg($"[PickUp] Fallback enable -> byName={hitsByName}, bruteBehaviours={(hitsByName == 0 ? hitsBrute : 0)} under '{root.name}'.");
            MelonCoroutines.Start(Co_NudgeEnablePickUps(root));
        }

        void DisablePickUpHoldersByNameAndType(bool disable)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i]; if (!t) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("pickupholder") || n.Contains("pickuph") || n.Contains("scriptpickupholder") || n.Contains("socket"))
                {
                    try { t.gameObject.SetActive(!disable); count++; } catch { }
                }
            }

            try
            {
                var comps = Resources.FindObjectsOfTypeAll<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i]; if (!c) continue;
                    var ty = c.GetType();
                    if (ty != null && ty.Name.EndsWith("PickUpHolder", StringComparison.OrdinalIgnoreCase))
                    {
                        try { c.gameObject.SetActive(!disable); count++; } catch { }
                    }
                }
            }
            catch { }

            MelonLogger.Msg($"[Holders] {(disable ? "Disabled" : "Enabled")} ~{count} objects.");
        }

        void DumpGameObjectDeep(StreamWriter w, GameObject go, string label)
        {
            w.WriteLine("==== " + label + " ====");
            w.WriteLine("Name: " + go.name);
            w.WriteLine("Path: " + GetPath(go.transform));
            w.WriteLine("ActiveInHierarchy: " + go.activeInHierarchy);
            w.WriteLine("Tag: " + go.tag);
            w.WriteLine("Layer: " + go.layer + " (" + LayerMaskLayerName(go.layer) + ")");
            w.WriteLine();

            w.WriteLine("Children:");
            for (int i = 0; i < go.transform.childCount; i++)
                w.WriteLine(" - " + go.transform.GetChild(i).name);
            w.WriteLine();

            var comps = go.GetComponents<Component>();
            w.WriteLine("Components:");
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i]; if (c == null) continue;
                var t = c.GetType();
                w.WriteLine("• " + t.FullName);
                DumpPublicFieldsAndProps(w, c);
                var rb = c as Rigidbody; if (rb != null) DumpRB(w, rb);
            }

            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) DumpCollider(w, cols[i]);

            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++) DumpRenderer(w, rends[i]);
        }

        void DumpPublicFieldsAndProps(StreamWriter w, Component c)
        {
            try
            {
                var t = c.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public;
                var fields = t.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    object val = null; try { val = f.GetValue(c); } catch { }
                    w.WriteLine("   - " + f.FieldType.Name + " " + f.Name + " = " + SafeObj(val));
                }
                var props = t.GetProperties(flags);
                for (int i = 0; i < props.Length; i++)
                {
                    var p = props[i]; if (!p.CanRead) continue;
                    object val = null; try { val = p.GetValue(c, null); } catch { }
                    w.WriteLine("   - " + p.PropertyType.Name + " " + p.Name + " = " + SafeObj(val));
                }
            }
            catch { }
        }

        void DumpRB(StreamWriter w, Rigidbody rb)
        {
            try
            {
                w.WriteLine("   [Rigidbody]");
                w.WriteLine("     mass=" + rb.mass + " drag=" + rb.drag + " angDrag=" + rb.angularDrag);
                w.WriteLine("     useGravity=" + rb.useGravity + " isKinematic=" + rb.isKinematic);
                w.WriteLine("     interp=" + rb.interpolation + " detect=" + rb.collisionDetectionMode);
                w.WriteLine("     constraints=" + rb.constraints);
            }
            catch { }
        }

        void DumpCollider(StreamWriter w, Collider c)
        {
            try
            {
                string t = c.GetType().Name;
                var mat = c.sharedMaterial;
                w.WriteLine("[Collider] " + t + "   enabled=" + c.enabled + "  isTrigger=" + c.isTrigger + "  layer=" + c.gameObject.layer);
                if (mat != null)
                    w.WriteLine("   PhysMat: " + mat.name + "  staticFriction=" + mat.staticFriction + " dynamicFriction=" + mat.dynamicFriction + " bounciness=" + mat.bounciness);
                var mc = c as MeshCollider;
                if (mc != null) w.WriteLine("   MeshCollider convex=" + mc.convex + " mesh=" + (mc.sharedMesh != null ? mc.sharedMesh.name : "(none)"));
            }
            catch { }
        }

        void DumpRenderer(StreamWriter w, Renderer r)
        {
            try
            {
                w.WriteLine("[Renderer] " + r.name + "  enabled=" + r.enabled + "  castShadows=" + r.shadowCastingMode + "  receive=" + r.receiveShadows);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    w.WriteLine("   Mat[" + i + "] " + m.name + "  shader=" + (m.shader != null ? m.shader.name : "(null)"));
                }
            }
            catch { }
        }

        string GetPath(Transform t)
        {
            var stack = new System.Collections.Generic.List<string>();
            while (t != null) { stack.Add(t.name); t = t.parent; }
            stack.Reverse();
            return string.Join("/", stack.ToArray());
        }
        string LayerMaskLayerName(int l) { try { return LayerMask.LayerToName(l); } catch { return "(unknown)"; } }
        string SafeObj(object o) { return (o == null) ? "null" : o.ToString(); }

        void TogglePickUpHolders(bool disable)
        {
            int count = 0;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    var go = all[i];
                    if (!go || !go.scene.IsValid()) continue;
                    string n = go.name.ToLowerInvariant();
                    bool nameMatch = n.Contains("pickupholder") || n.Contains("scriptpickupholder");
                    bool compMatch = false;
                    try
                    {
                        var cs = go.GetComponents<Component>();
                        for (int c = 0; c < cs.Length; c++)
                        {
                            var comp = cs[c];
                            if (comp != null && comp.GetType().Name.ToLowerInvariant().Contains("pickupholder"))
                            { compMatch = true; break; }
                        }
                    }
                    catch { }

                    if (nameMatch || compMatch)
                    {
                        if (go.activeSelf == (!disable)) { go.SetActive(!disable); count++; }
                    }
                }
            }
            catch { }
            MelonLogger.Msg("[Holders] " + (disable ? "Disabled " : "Enabled ") + count + " PickUpHolder objects.");
        }
        int CountHolders()
        {
            int c = 0;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    var go = all[i];
                    if (!go || !go.scene.IsValid()) continue;
                    string n = go.name.ToLowerInvariant();
                    if (n.Contains("pickupholder") || n.Contains("scriptpickupholder")) c++;
                }
            }
            catch { }
            return c;
        }

        void EnsureBrightFallbackReflectionCubemap()
        {
            try
            {
                if (_brightFallbackCube == null)
                {
                    int s = 16;
                    _brightFallbackCube = new Cubemap(s, TextureFormat.RGBAHalf, false);
                    _brightFallbackCube.wrapMode = TextureWrapMode.Clamp;
                    _brightFallbackCube.hideFlags = HideFlags.DontUnloadUnusedAsset;

                    Color c = new Color(0.75f, 0.78f, 0.82f, 1f);
                    Color[] px = new Color[s * s];
                    for (int i = 0; i < px.Length; i++) px[i] = c;

                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveX);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeX);
                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveY);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeY);
                    _brightFallbackCube.SetPixels(px, CubemapFace.PositiveZ);
                    _brightFallbackCube.SetPixels(px, CubemapFace.NegativeZ);
                    _brightFallbackCube.Apply(false, false);
                }

                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflection = _brightFallbackCube;
                RenderSettings.reflectionIntensity = 1.35f;
                RenderSettings.reflectionBounces = 1;

                if (RenderSettings.ambientMode != AmbientMode.Skybox && RenderSettings.ambientLight.maxColorComponent < 0.1f)
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.14f);
                }
                MelonLogger.Msg("[BrightEnv] Injected bright fallback reflection cubemap.");
            }
            catch (Exception ex) { MelonLogger.Warning("[BrightEnv] " + ex.GetType().Name + ": " + ex.Message); }
        }

        void BuildOrUpdateGlobalProbe(bool forceRender)
        {
            if (_mergedRoot == null) return;

            if (_globalProbe == null)
            {
                var go = new GameObject("ModLevel_GlobalReflectionProbe");
                go.transform.SetParent(_mergedRoot.transform, false);
                _globalProbe = go.AddComponent<ReflectionProbe>();
                _globalProbe.mode = ReflectionProbeMode.Realtime;
                _globalProbe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                _globalProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                _globalProbe.boxProjection = true;
                _globalProbe.importance = 100;
                _globalProbe.clearFlags = ReflectionProbeClearFlags.Skybox;
                _globalProbe.hdr = true;
            }

            Bounds world;
            if (!TryComputeWorldBounds(_mergedRoot, out world))
                world = new Bounds(_mergedRoot.transform.position, Vector3.one * 20f);

            _globalProbe.transform.position = world.center;
            _globalProbe.center = Vector3.zero;
            Vector3 size = world.size; if (size.sqrMagnitude < 0.01f) size = Vector3.one * 10f;
            _globalProbe.size = size * 1.25f;
            _globalProbe.cullingMask = ~0;
            _globalProbe.intensity = 1.2f;
            _globalProbe.enabled = true;

            var rends = _mergedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
            }

            if (forceRender) { try { _globalProbe.RenderProbe(); } catch { } }
            MelonLogger.Msg("[Probe] Global probe ready. Size=" + _globalProbe.size + " Center=" + world.center);
        }

        bool TryComputeWorldBounds(GameObject root, out Bounds b)
        {
            b = new Bounds(root.transform.position, Vector3.zero);
            bool inited = false;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue;
                if (!inited) { b = r.bounds; inited = true; }
                else b.Encapsulate(r.bounds);
            }
            return inited;
        }

        void EnsurePickUpEnabledLater(Component pickup, int frames = 3)
        {
            if (pickup == null) return;
            MelonCoroutines.Start(Co_EnsurePickUpEnabled(pickup, frames));
        }

        System.Collections.IEnumerator Co_EnsurePickUpEnabled(Component pickup, int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
            try
            {
                var b = pickup as Behaviour;
                if (b != null) b.enabled = true;
            }
            catch { }
        }

        void HarvestPhoenixShaders()
        {
            _phoenixPackedOpaque = Shader.Find("Phoenix/Packed/SH_Shared_PackedPBR_Metallic_Opaque_01");
            _phoenixDefaultOpaque = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Opaque_01");
            _phoenixCutout = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_Cutout_01");
            if (_phoenixCutout == null) _phoenixCutout = _phoenixPackedOpaque;
            _phoenixTransparent = Shader.Find("Phoenix/SH_Shared_DefaultPBR_Metallic_TransparentColorAlpha_01");

            MelonLogger.Msg("[Phoenix] Packed='" + (_phoenixPackedOpaque != null ? _phoenixPackedOpaque.name : "null")
                + "' Default='" + (_phoenixDefaultOpaque != null ? _phoenixDefaultOpaque.name : "null")
                + "' Transparent='" + (_phoenixTransparent != null ? _phoenixTransparent.name : "null") + "'");
        }

        void ConvertMergedMaterialsToPhoenix()
        {
            if (_mergedRoot == null) return;

            var rends = _mergedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue;
                var srcMats = r.sharedMaterials; if (srcMats == null) continue;

                bool any = false;
                for (int m = 0; m < srcMats.Length; m++)
                {
                    var src = srcMats[m]; if (src == null) continue;
                    if (ShouldPreserveSpecial(src)) continue;

                    bool cutout = HasKw(src, "_ALPHATEST_ON") || src.HasProperty("_Cutoff");
                    bool blend = HasKw(src, "_ALPHABLEND_ON") || HasKw(src, "_ALPHAPREMULTIPLY_ON") || src.renderQueue >= 3000;

                    Shader target = (!blend && !cutout) ? (_phoenixPackedOpaque != null ? _phoenixPackedOpaque : _phoenixDefaultOpaque)
                                  : (cutout ? _phoenixCutout : _phoenixTransparent);
                    if (target == null) continue;

                    var dst = new Material(target);
                    dst.name = src.name + "_Phoenix";

                    CopyTexST(src, dst, "_BaseMap", "_MainTex");
                    CopyTexST(src, dst, "_MainTex", "_MainTex");
                    CopyTexST(src, dst, "_BumpMap", "_BumpMap");
                    CopyTexST(src, dst, "_NormalMap", "_BumpMap");

                    float srcMetal = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
                    float srcSmooth = src.HasProperty("_Smoothness") ? src.GetFloat("_Smoothness")
                                   : (src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.5f);

                    bool hadMetalTex = (src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap") != null)
                                     || (src.HasProperty("_SpecGlossMap") && src.GetTexture("_SpecGlossMap") != null);

                    float fallbackMetal = hadMetalTex ? Math.Max(0.6f, srcMetal) : srcMetal;
                    float fallbackSmooth = srcSmooth;

                    if (dst.HasProperty("_Metallic")) dst.SetFloat("_Metallic", Mathf.Clamp01(fallbackMetal));
                    if (dst.HasProperty("_Smoothness")) dst.SetFloat("_Smoothness", Mathf.Clamp01(fallbackSmooth));
                    if (dst.HasProperty("_Glossiness")) dst.SetFloat("_Glossiness", Mathf.Clamp01(fallbackSmooth));

                    SupplyNeutralMaskMap(dst, Mathf.Clamp01(fallbackMetal), Mathf.Clamp01(fallbackSmooth));
                    TryCopyEmission(src, dst);

                    srcMats[m] = dst; _mergedMats.Add(dst); any = true;
                }

                if (any) r.sharedMaterials = srcMats;

                r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                try { r.forceRenderingOff = false; } catch {}
#endif
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                r.allowOcclusionWhenDynamic = false;
            }
        }

        void SupplyNeutralMaskMap(Material dst, float metallic, float smoothness)
        {
            if (!dst.HasProperty("_MaskMap")) return;

            var t2 = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t2.wrapMode = TextureWrapMode.Repeat;
            t2.filterMode = FilterMode.Bilinear;

            var c = new Color(Mathf.Clamp01(metallic), 1f, 1f, Mathf.Clamp01(smoothness));
            t2.SetPixels(new Color[] { c, c, c, c }); t2.Apply();
            t2.hideFlags = HideFlags.DontUnloadUnusedAsset;

            dst.SetTexture("_MaskMap", t2);
            try
            {
                var st = dst.GetTextureScale("_MainTex");
                var of = dst.GetTextureOffset("_MainTex");
                dst.SetTextureScale("_MaskMap", st);
                dst.SetTextureOffset("_MaskMap", of);
            }
            catch { }
        }

        class STInfo { public Vector2 scale; public Vector2 offset; public string prop; }
        STInfo GetBaseST(Material m)
        {
            for (int i = 0; i < 2; i++)
            {
                string p = (i == 0) ? "_MainTex" : "_BaseMap";
                try
                {
                    if (m.HasProperty(p) && m.GetTexture(p) != null)
                    {
                        var info = new STInfo();
                        info.scale = m.GetTextureScale(p);
                        info.offset = m.GetTextureOffset(p);
                        info.prop = p;
                        return info;
                    }
                }
                catch { }
            }
            var def = new STInfo { scale = new Vector2(1f, 1f), offset = Vector2.zero, prop = null };
            return def;
        }

        static readonly string[] kFollowerTexProps = { "_BumpMap", "_MaskMap", "_MetallicGlossMap", "_SpecGlossMap", "_OcclusionMap", "_EmissionMap" };
        void SyncMaterialTiling(Material m, out int changedProps)
        {
            changedProps = 0; if (m == null) return;
            var baseST = GetBaseST(m);
            if (baseST.prop == null) return;

            for (int i = 0; i < kFollowerTexProps.Length; i++)
            {
                string p = kFollowerTexProps[i];
                try
                {
                    if (!m.HasProperty(p)) continue;
                    var tex = m.GetTexture(p);
                    if (tex == null) continue;

                    var st = m.GetTextureScale(p);
                    var of = m.GetTextureOffset(p);
                    if (st != baseST.scale || of != baseST.offset)
                    {
                        m.SetTextureScale(p, baseST.scale);
                        m.SetTextureOffset(p, baseST.offset);
                        changedProps++;
                    }
                }
                catch { }
            }
        }

        int SyncAllPhoenixTiling()
        {
            if (_mergedMats == null || _mergedMats.Count == 0) return 0;
            int matsTouched = 0, props = 0;
            foreach (var m in _mergedMats)
            {
                if (m == null) continue;
                string sh = (m.shader != null) ? m.shader.name : "";
                if (string.IsNullOrEmpty(sh) || sh.IndexOf("Phoenix/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                int c; SyncMaterialTiling(m, out c);
                if (c > 0) { matsTouched++; props += c; }
            }
            MelonLogger.Msg("[UV] Synced tiling to base on " + matsTouched + " materials (" + props + " map STs updated).");
            return matsTouched;
        }

        void TuneRedLight()
        {
            if (_mergedRoot == null) return;
            var lights = _mergedRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                var L = lights[i];
                if (L == null) continue;
                if (string.Equals(L.name, RedLightName, StringComparison.OrdinalIgnoreCase))
                {
                    _origLightIntensity[L] = RedLightIntensity;
                    try { L.transform.position = RedLightWorldPos; } catch { }
                    L.intensity = RedLightIntensity;
                    L.renderMode = LightRenderMode.ForcePixel;
                    L.shadows = LightShadows.Soft;
                    L.cullingMask = ~0;
                    MelonLogger.Msg("[RedLight] Positioned to " + RedLightWorldPos + " and set intensity " + RedLightIntensity);
                    return;
                }
            }
            MelonLogger.Warning("[RedLight] Light named '" + RedLightName + "' not found.");
        }

        void ApplyColoredLightAssist(float factor, bool log)
        {
            if (_mergedRoot == null) return;
            int n = 0, affected = 0;
            var lights = _mergedRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                var L = lights[i]; if (L == null) continue; n++;
                if (string.Equals(L.name, RedLightName, StringComparison.OrdinalIgnoreCase)) continue;

                Color c = L.color;
                float max = Math.Max(c.r, Math.Max(c.g, c.b));
                float min = Math.Min(c.r, Math.Min(c.g, c.b));
                float sat = max > 0f ? (max - min) / max : 0f;
                if (sat < 0.25f) continue;

                if (!_origLightIntensity.ContainsKey(L)) _origLightIntensity[L] = L.intensity;
                L.intensity = _origLightIntensity[L] * factor;

                L.renderMode = LightRenderMode.ForcePixel;
                L.shadows = LightShadows.Soft;
                L.cullingMask = ~0;

                affected++;
            }
            if (log) MelonLogger.Msg("[Lights] Colored light assist applied ×" + factor.ToString("0.##") + " on " + affected + "/" + n + " lights (skipped '" + RedLightName + "').");
        }

        readonly Dictionary<Light, float> _blackoutOrigLight = new Dictionary<Light, float>();
        readonly Dictionary<Material, Color> _origMatColor = new Dictionary<Material, Color>();
        readonly Dictionary<Material, Color> _origMatEmission = new Dictionary<Material, Color>();

        readonly System.Collections.Generic.List<GameObject> _blackoutGatheredRoots
            = new System.Collections.Generic.List<GameObject>();

        readonly Dictionary<GameObject, GameObject> _gaDarkClones = new Dictionary<GameObject, GameObject>();

        readonly Dictionary<Renderer, ReflectionProbeUsage> _origRendererReflUsage
            = new Dictionary<Renderer, ReflectionProbeUsage>();

        Cubemap _darkFallbackCube;
        bool _reflectionEnvSaved = false;
        DefaultReflectionMode _savedDefaultReflMode;
        Cubemap _savedCustomReflection;
        float _savedReflectionIntensity;
        int _savedReflectionBounces;

        bool _envSaved = false;
        Color _envAmbientLight;
        float _envAmbientIntensity;

        bool _isBlackout;
        bool _envSnapshotValid;
        AmbientMode _savedAmbientMode;
        Color _savedAmbientLight;
        float _savedGlobalProbeIntensity = -1f;

        UnityEngine.Rendering.DefaultReflectionMode _envDefaultReflMode;
        Cubemap _envCustomReflection;
        float _envReflectionIntensity;
        int _envReflectionBounces;

        readonly Dictionary<Light, float> _blackoutTargetLight = new Dictionary<Light, float>();

        readonly Dictionary<Material, Color> _darkMatColor = new Dictionary<Material, Color>();
        readonly Dictionary<Material, Color> _darkMatEmission = new Dictionary<Material, Color>();

        object _blackoutRoutine;
        const float BlackoutFadeSeconds = 0.75f;

        public void SetBlackout(bool enable)
        {
            MelonLogger.Msg("[Blackout] SetBlackout(" + enable + ")");
            _isBlackout = enable;

            if (_mergedRoot == null)
            {
                MelonLogger.Warning("[Blackout] _mergedRoot is null.");
                return;
            }

            if (_blackoutRoutine != null)
            {
                try { MelonCoroutines.Stop(_blackoutRoutine); }
                catch { }
                _blackoutRoutine = null;
            }

            GameObject toolsRoot = GameObject.Find("IEYTD2_Tools_ROOT");

            const float METALLIC_COLOR_SCALE = 0.20f;
            const float DEFAULT_COLOR_SCALE = 0.50f;

            if (enable)
            {

                _blackoutOrigLight.Clear();
                _blackoutTargetLight.Clear();
                _origMatColor.Clear();
                _origMatEmission.Clear();
                _darkMatColor.Clear();
                _darkMatEmission.Clear();

                if (bank != null && bank.CoolantPlane != null)
                    bank.CoolantPlane.GetComponent<MeshRenderer>().enabled = false;

                try
                {
                    var allRenderers = Resources.FindObjectsOfTypeAll<Renderer>();
                    int snappedColors = 0;
                    int snappedEmission = 0;

                    for (int r = 0; r < allRenderers.Length; r++)
                    {
                        var rend = allRenderers[r];
                        if (rend == null) continue;

                        var mats = rend.sharedMaterials;
                        if (mats == null) continue;

                        for (int m = 0; m < mats.Length; m++)
                        {
                            var mat = mats[m];
                            if (mat == null) continue;

                            if (mat.HasProperty("_Color"))
                            {
                                try
                                {
                                    _origMatColor[mat] = mat.color;
                                    snappedColors++;
                                }
                                catch { }
                            }

                            if (mat.HasProperty("_EmissionColor"))
                            {
                                try
                                {
                                    _origMatEmission[mat] = mat.GetColor("_EmissionColor");
                                    snappedEmission++;
                                }
                                catch { }
                            }
                        }
                    }

                    MelonLogger.Msg("[Blackout] Global material snapshot: colors=" +
                                    snappedColors + " emission=" + snappedEmission);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Blackout] Global material snapshot failed: " + ex.Message);
                }
            }
            else
            {

                if (bank != null && bank.CoolantPlane != null)
                    bank.CoolantPlane.GetComponent<MeshRenderer>().enabled = true;
            }

            if (!_envSaved)
            {
                _envAmbientLight = RenderSettings.ambientLight;
                _envAmbientIntensity = RenderSettings.ambientIntensity;
                _envDefaultReflMode = RenderSettings.defaultReflectionMode;
                _envCustomReflection = RenderSettings.customReflection;
                _envReflectionIntensity = RenderSettings.reflectionIntensity;
                _envReflectionBounces = RenderSettings.reflectionBounces;
                _envSaved = true;
            }

            if (_darkFallbackCube == null)
            {
                try
                {
                    int s = 16;
                    _darkFallbackCube = new Cubemap(s, TextureFormat.RGBAHalf, false);
                    _darkFallbackCube.wrapMode = TextureWrapMode.Clamp;
                    _darkFallbackCube.hideFlags = HideFlags.DontUnloadUnusedAsset;

                    Color c = new Color(0.10f, 0.11f, 0.12f, 1f);
                    Color[] px = new Color[s * s];

                    for (int i = 0; i < px.Length; i++)
                        px[i] = c;

                    _darkFallbackCube.SetPixels(px, CubemapFace.PositiveX);
                    _darkFallbackCube.SetPixels(px, CubemapFace.NegativeX);
                    _darkFallbackCube.SetPixels(px, CubemapFace.PositiveY);
                    _darkFallbackCube.SetPixels(px, CubemapFace.NegativeY);
                    _darkFallbackCube.SetPixels(px, CubemapFace.PositiveZ);
                    _darkFallbackCube.SetPixels(px, CubemapFace.NegativeZ);
                    _darkFallbackCube.Apply(false, false);

                    MelonLogger.Msg("[Blackout] Created dark fallback reflection cubemap.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[Blackout] Failed to create dark fallback cubemap: " + ex.Message);
                    _darkFallbackCube = null;
                }
            }

            void ProcessRoot(GameObject root)
            {
                if (root == null) return;

                var lights = root.GetComponentsInChildren<Light>(true);
                int totalLights = 0, changedLights = 0;

                for (int i = 0; i < lights.Length; i++)
                {
                    var L = lights[i];
                    if (L == null) continue;

                    totalLights++;
                    string name = L.name ?? string.Empty;

                    bool isRedEmergency = string.Equals(name, RedLightName, StringComparison.OrdinalIgnoreCase);
                    bool isReactorLight =
                        string.Equals(name, "ReactorLight", StringComparison.OrdinalIgnoreCase) ||
                        name.IndexOf("reactor", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (enable)
                    {
                        float origInt = L.intensity;
                        _blackoutOrigLight[L] = origInt;

                        float targetInt;
                        if (isReactorLight)
                        {
                            targetInt = origInt;
                        }
                        else if (isRedEmergency)
                        {
                            targetInt = origInt * 0.25f;
                        }
                        else
                        {
                            targetInt = 0f;
                        }

                        _blackoutTargetLight[L] = targetInt;
                        changedLights++;
                    }
                }

                MelonLogger.Msg("[Blackout] Root '" + root.name +
                                "' lights scheduled: " + changedLights + "/" + totalLights);

                var renderers = root.GetComponentsInChildren<Renderer>(true);
                int totalMats = 0, changedMats = 0;

                for (int r = 0; r < renderers.Length; r++)
                {
                    var rend = renderers[r];
                    if (rend == null) continue;

                    string goName = rend.gameObject.name ?? string.Empty;
                    bool isReactorObj =
                        goName.IndexOf("Reactor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        goName.IndexOf("Flame", StringComparison.OrdinalIgnoreCase) >= 0;

                    var mats = rend.sharedMaterials;
                    if (mats == null) continue;

                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null) continue;

                        totalMats++;

                        string shaderName = mat.shader != null ? mat.shader.name : string.Empty;
                        bool looksMetallic =
                            shaderName.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            shaderName.IndexOf("PBR", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (enable)
                        {
                            if (isReactorObj) continue;

                            if (mat.HasProperty("_Color"))
                            {
                                if (!_origMatColor.TryGetValue(mat, out Color origC))
                                    origC = mat.color;

                                float luminance = origC.r * 0.299f +
                                                  origC.g * 0.587f +
                                                  origC.b * 0.114f;

                                Color darkC;

                                if (luminance < 0.02f && mat.mainTexture != null)
                                {
                                    float baseGrey = looksMetallic ? 0.18f : 0.28f;
                                    darkC = new Color(baseGrey, baseGrey, baseGrey, origC.a);
                                }
                                else
                                {
                                    float scale = looksMetallic ? METALLIC_COLOR_SCALE : DEFAULT_COLOR_SCALE;
                                    darkC = origC * scale;
                                }

                                _darkMatColor[mat] = darkC;
                                changedMats++;
                            }

                            if (mat.HasProperty("_EmissionColor"))
                            {
                                _darkMatEmission[mat] = Color.black;
                                changedMats++;
                            }
                        }
                    }
                }

                MelonLogger.Msg("[Blackout] Root '" + root.name +
                                "' materials scheduled: " + changedMats + "/" + totalMats);
            }

            ProcessRoot(_mergedRoot);
            if (toolsRoot != null) ProcessRoot(toolsRoot);

            if (enable)
            {
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientIntensity = 0f;

                if (_darkFallbackCube != null)
                {
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflection = _darkFallbackCube;
                    RenderSettings.reflectionIntensity = _envReflectionIntensity;
                    RenderSettings.reflectionBounces = _envReflectionBounces;

                    MelonLogger.Msg("[Blackout] Using dark fallback reflection cubemap.");
                }
                else
                {
                    MelonLogger.Warning("[Blackout] Dark fallback cube missing; reflections may still be bright.");
                }

                RefreshGlobalProbe();
            }
            else
            {
                if (_envSaved)
                {
                    RenderSettings.ambientLight = _envAmbientLight;
                    RenderSettings.ambientIntensity = _envAmbientIntensity;
                    RenderSettings.defaultReflectionMode = _envDefaultReflMode;
                    RenderSettings.customReflection = _envCustomReflection;
                    RenderSettings.reflectionIntensity = _envReflectionIntensity;
                    RenderSettings.reflectionBounces = _envReflectionBounces;

                    MelonLogger.Msg("[Blackout] Restored original reflection environment.");
                }

                RefreshGlobalProbe();
            }

            _blackoutRoutine = MelonCoroutines.Start(Co_FadeBlackout(enable, BlackoutFadeSeconds));
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_FadeBlackout(bool enable, float duration)
        {
            MelonLogger.Msg("[Blackout] Fade " + (enable ? "IN (to dark)" : "OUT (to bright)") +
                            " over " + duration + "s");

            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);

                foreach (var kv in _blackoutTargetLight)
                {
                    var L = kv.Key;
                    if (!L) continue;

                    float orig = _blackoutOrigLight.TryGetValue(L, out float o) ? o : L.intensity;
                    float target = kv.Value;

                    float from = enable ? orig : target;
                    float to = enable ? target : orig;

                    try { L.intensity = Mathf.Lerp(from, to, t); }
                    catch { }
                }

                foreach (var kv in _darkMatColor)
                {
                    var mat = kv.Key;
                    if (mat == null || !mat.HasProperty("_Color")) continue;

                    Color origC = _origMatColor.TryGetValue(mat, out Color oc) ? oc : mat.color;
                    Color darkC = kv.Value;

                    Color from = enable ? origC : darkC;
                    Color to = enable ? darkC : origC;

                    try { mat.color = Color.Lerp(from, to, t); }
                    catch { }
                }

                foreach (var kv in _darkMatEmission)
                {
                    var mat = kv.Key;
                    if (mat == null || !mat.HasProperty("_EmissionColor")) continue;

                    Color origE =
                        _origMatEmission.TryGetValue(mat, out Color e0)
                        ? e0
                        : mat.GetColor("_EmissionColor");

                    Color darkE = kv.Value;

                    Color fromE = enable ? origE : darkE;
                    Color toE = enable ? darkE : origE;

                    Color e = Color.Lerp(fromE, toE, t);

                    try
                    {
                        mat.SetColor("_EmissionColor", e);
                        if (e.maxColorComponent > 0.001f) mat.EnableKeyword("_EMISSION");
                        else mat.DisableKeyword("_EMISSION");
                    }
                    catch { }
                }

                try { RefreshGlobalProbe(); } catch { }

                elapsed += Time.deltaTime;
                yield return null;
            }

            foreach (var kv in _blackoutTargetLight)
            {
                var L = kv.Key;
                if (!L) continue;

                float orig = _blackoutOrigLight.TryGetValue(L, out float o) ? o : L.intensity;
                float target = kv.Value;

                try { L.intensity = enable ? target : orig; }
                catch { }
            }

            foreach (var kv in _darkMatColor)
            {
                var mat = kv.Key;
                if (mat == null || !mat.HasProperty("_Color")) continue;

                Color origC = _origMatColor.TryGetValue(mat, out Color oc) ? oc : mat.color;
                Color darkC = kv.Value;

                try { mat.color = enable ? darkC : origC; }
                catch { }
            }

            foreach (var kv in _darkMatEmission)
            {
                var mat = kv.Key;
                if (mat == null || !mat.HasProperty("_EmissionColor")) continue;

                Color origE = _origMatEmission.TryGetValue(mat, out Color oe)
                              ? oe
                              : mat.GetColor("_EmissionColor");

                Color darkE = kv.Value;
                Color finalE = enable ? darkE : origE;

                try
                {
                    mat.SetColor("_EmissionColor", finalE);
                    if (finalE.maxColorComponent > 0.001f) mat.EnableKeyword("_EMISSION");
                    else mat.DisableKeyword("_EMISSION");
                }
                catch { }
            }

            if (!enable)
            {
                int restoredLights = 0;
                foreach (var kv in _blackoutOrigLight)
                {
                    var L = kv.Key;
                    if (!L) continue;

                    try { L.intensity = kv.Value; restoredLights++; }
                    catch { }
                }

                MelonLogger.Msg("[Blackout] Final snapshot light restore: " +
                                restoredLights + "/" + _blackoutOrigLight.Count);

                int restoredColor = 0, restoredEmission = 0;

                foreach (var kv in _origMatColor)
                {
                    var mat = kv.Key;
                    if (mat == null || !mat.HasProperty("_Color")) continue;

                    try { mat.color = kv.Value; restoredColor++; }
                    catch { }
                }

                foreach (var kv in _origMatEmission)
                {
                    var mat = kv.Key;
                    if (mat == null || !mat.HasProperty("_EmissionColor")) continue;

                    Color e = kv.Value;

                    try
                    {
                        mat.SetColor("_EmissionColor", e);
                        if (e.maxColorComponent > 0.001f) mat.EnableKeyword("_EMISSION");
                        else mat.DisableKeyword("_EMISSION");
                        restoredEmission++;
                    }
                    catch { }
                }

                MelonLogger.Msg("[Blackout] Final snapshot material restore: colors=" +
                                restoredColor + " emission=" + restoredEmission);

                try { RefreshGlobalProbe(); } catch { }

                _blackoutOrigLight.Clear();
                _blackoutTargetLight.Clear();
                _origMatColor.Clear();
                _origMatEmission.Clear();
                _darkMatColor.Clear();
                _darkMatEmission.Clear();
                SetChipLed_OnlyLightMaterial("Blue Chip", false);
            }
            else
            {

                try { RefreshGlobalProbe(); } catch { }
            }

            _blackoutRoutine = null;
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator Co_ProbeRefreshBurst()
        {

            yield return null;
            yield return null;

            float wait = 0.10f;
            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 3; i++)
            {
                try { RefreshGlobalProbe(); } catch { }
                yield return null;
            }
        }

        void RefreshGlobalProbe()
        {
            if (_globalProbe == null)
            {
                var go = GameObject.Find("ModLevel_GlobalReflectionProbe");
                if (go) _globalProbe = go.GetComponent<ReflectionProbe>();
            }

            if (_globalProbe == null)
            {
                MelonLogger.Warning("[Blackout] No global reflection probe to refresh.");
                return;
            }

            try
            {
                MelonLogger.Msg("[Blackout] Forcing RenderProbe on ModLevel_GlobalReflectionProbe.");
                _globalProbe.RenderProbe();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Blackout] RefreshGlobalProbe exception: " + ex.Message);
            }
        }

        void SnapshotEnvIfNeeded()
        {
            if (_envSnapshotValid) return;

            _envSnapshotValid = true;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbientLight = RenderSettings.ambientLight;
            _savedReflectionIntensity = RenderSettings.reflectionIntensity;

            if (_globalProbe != null)
                _savedGlobalProbeIntensity = _globalProbe.intensity;

            MelonLogger.Msg("[Blackout] Captured environment snapshot.");
        }

        void EnsureDarkFallbackReflectionCubemap()
        {
            try
            {
                if (_darkFallbackCube != null) return;

                int s = 16;
                _darkFallbackCube = new Cubemap(s, TextureFormat.RGBAHalf, false);
                _darkFallbackCube.wrapMode = TextureWrapMode.Clamp;
                _darkFallbackCube.hideFlags = HideFlags.DontUnloadUnusedAsset;

                Color c = new Color(0.02f, 0.02f, 0.02f, 1f);
                Color[] px = new Color[s * s];
                for (int i = 0; i < px.Length; i++) px[i] = c;

                _darkFallbackCube.SetPixels(px, CubemapFace.PositiveX);
                _darkFallbackCube.SetPixels(px, CubemapFace.NegativeX);
                _darkFallbackCube.SetPixels(px, CubemapFace.PositiveY);
                _darkFallbackCube.SetPixels(px, CubemapFace.NegativeY);
                _darkFallbackCube.SetPixels(px, CubemapFace.PositiveZ);
                _darkFallbackCube.SetPixels(px, CubemapFace.NegativeZ);
                _darkFallbackCube.Apply(false, false);

                MelonLogger.Msg("[Blackout] Created dark fallback reflection cubemap.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Blackout] EnsureDarkFallbackReflectionCubemap failed: " + ex.Message);
            }
        }

        void DumpShinyMaterialDebug()
        {
            try
            {
                MelonLogger.Msg("[BlackoutDebug] ---- Dump start ----");

                if (_mergedRoot == null)
                {
                    MelonLogger.Warning("[BlackoutDebug] _mergedRoot is null.");
                    return;
                }

                var renderers = _mergedRoot.GetComponentsInChildren<Renderer>(true);
                int loggedRenderers = 0;

                for (int r = 0; r < renderers.Length; r++)
                {
                    var rend = renderers[r];
                    if (rend == null) continue;

                    var mats = rend.sharedMaterials;
                    if (mats == null || mats.Length == 0) continue;

                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null) continue;

                        string shaderName = (mat.shader != null) ? mat.shader.name : "NO_SHADER";

                        bool looksPhoenix =
                            shaderName.IndexOf("Phoenix", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            shaderName.IndexOf("SH_Shared", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            shaderName.IndexOf("Metallic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            shaderName.IndexOf("PBR", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!looksPhoenix)
                            continue;

                        MelonLogger.Msg(
                            "[BlackoutDebug] Renderer '" + rend.gameObject.name +
                            "' mat[" + m + "] shader='" + shaderName + "'");

                        var sh = mat.shader;
                        if (sh == null)
                        {
                            MelonLogger.Msg("[BlackoutDebug]  (no shader object)");
                            continue;
                        }

                        try
                        {
                            int propCount = sh.GetPropertyCount();

                            for (int i = 0; i < propCount; i++)
                            {
                                string propName = sh.GetPropertyName(i);
                                var propType = sh.GetPropertyType(i);

                                if (propType == ShaderPropertyType.Float ||
                                    propType == ShaderPropertyType.Range)
                                {
                                    float val = 0f;
                                    try { val = mat.GetFloat(propName); } catch { }
                                    MelonLogger.Msg(
                                        "[BlackoutDebug]   Float " + propName + " = " + val);
                                }
                                else if (propType == ShaderPropertyType.Color ||
                                         propType == ShaderPropertyType.Vector)
                                {
                                    Color c = Color.black;
                                    try { c = mat.GetColor(propName); } catch { }
                                    MelonLogger.Msg(
                                        "[BlackoutDebug]   Color " + propName + " = " + c);
                                }
                            }
                        }
                        catch (Exception exInner)
                        {
                            MelonLogger.Warning(
                                "[BlackoutDebug]  Error reading shader props: " + exInner.Message);
                        }

                        loggedRenderers++;
                        if (loggedRenderers >= 5)
                        {
                            MelonLogger.Msg("[BlackoutDebug] ---- Dump end (limit reached) ----");
                            return;
                        }
                    }
                }

                MelonLogger.Msg("[BlackoutDebug] ---- Dump end (logged " + loggedRenderers + " renderer(s)) ----");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[BlackoutDebug] Exception: " + ex);
            }
        }

       public void fixFlashlight()
        {
            GameObject light = bank.P_Shop_INT_Flashlight.transform.GetChild(3).gameObject;
            light.SetActive(true);
            light.GetComponent<Light>().intensity = 2f;
        }

        int HardEnableDraw(GameObject root)
        {
            if (root == null) return 0;
            root.SetActive(true);
            int count = 0;
            var rends = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue; count++;
                try
                {
                    r.enabled = true;
#if UNITY_2019_4_OR_NEWER
                    try { r.forceRenderingOff = false; } catch {}
#endif
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    r.allowOcclusionWhenDynamic = false;

                    var mats = r.sharedMaterials; bool changed = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m]; if (mat == null) continue;
                        if (mat.HasProperty("_Color"))
                        {
                            var col = mat.color;
                            if (col.a < 0.99f) { col.a = 1f; mat.color = col; changed = true; }
                        }
                    }
                    if (changed) r.sharedMaterials = mats;
                }
                catch { }
            }
            return count;
        }

        void ForceAllOpaque()
        {
            if (_mergedRoot == null) return;
            var rends = _mergedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i]; if (r == null) continue;
                var mats = r.sharedMaterials; if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m]; if (mat == null) continue;
                    if (ShouldPreserveSpecial(mat)) continue;

                    SafeSetInt(mat, "_SrcBlend", (int)BlendMode.One);
                    SafeSetInt(mat, "_DstBlend", (int)BlendMode.Zero);
                    SafeSetInt(mat, "_ZWrite", 1);
                    try { mat.DisableKeyword("_ALPHATEST_ON"); } catch { }
                    try { mat.DisableKeyword("_ALPHABLEND_ON"); } catch { }
                    try { mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); } catch { }
                    mat.renderQueue = -1;

                    if (mat.HasProperty("_Color"))
                    {
                        var c = mat.color;
                        if (c.a < 0.99f) { c.a = 1f; mat.color = c; }
                    }
                }
                r.sharedMaterials = mats;
            }

            if (RenderSettings.ambientMode != AmbientMode.Skybox && RenderSettings.ambientLight.maxColorComponent < 0.06f)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.12f);
            }
        }

        void CleanHostRoots()
        {
            var active = SceneManager.GetActiveScene();
            var roots = Resources.FindObjectsOfTypeAll<Transform>();
            int deleted = 0, kept = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null) continue;
                var go = t.gameObject;
                if (!go.scene.IsValid() || go.scene != active) continue;
                if (t.parent != null) continue;

                if (go == _mergedRoot)
                {
                    kept++;
                    continue;
                }

                if (go.name == "IEYTD2_Tools_ROOT")
                {
                    kept++;
                    continue;
                }

                string n = go.name.ToLowerInvariant();
                bool keepByName = false;
                for (int k = 0; k < KeepNameContains.Length; k++)
                {
                    if (n.Contains(KeepNameContains[k]))
                    {
                        keepByName = true;
                        break;
                    }
                }

                if (keepByName || go.GetComponentInChildren<ReflectionProbe>(true) != null)
                {
                    kept++;
                    continue;
                }

                if (go.GetComponentInChildren<Camera>(true) != null)
                {
                    kept++;
                    continue;
                }

                try
                {
                    UnityEngine.Object.Destroy(go);
                    deleted++;
                }
                catch { }
            }

            MelonLogger.Msg("[Clean] Deleted " + deleted + " host roots (kept rig/probes/merged/tools).");
        }

        bool EnsureBundle() { return _bundle != null && !string.IsNullOrEmpty(_sceneName) ? true : TryReadBundle(); }
        bool TryReadBundle()
        {
            try
            {
                var path = System.IO.Path.Combine(MelonUtils.UserDataDirectory, BundleFileName);

                if (!File.Exists(path))
                {
                    MelonLogger.Error("[Bundle] Not found at: " + path);
                    _scenePath = FallbackSceneAssetPath;
                    _sceneName = System.IO.Path.GetFileNameWithoutExtension(_scenePath);
                    return false;
                }

                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null) { MelonLogger.Error("[Bundle] LoadFromFile returned null."); return false; }

                _scenePath = FallbackSceneAssetPath;
                try
                {
                    var ps = _bundle.GetAllScenePaths();
                    if (ps != null && ps.Length > 0) _scenePath = ps[0];
                    if (ps != null && ps.Length > 0) MelonLogger.Msg("[Bundle] Scenes: " + string.Join(", ", ps));
                }
                catch { }
                _sceneName = System.IO.Path.GetFileNameWithoutExtension(_scenePath);

                MelonLogger.Msg("[Bundle] Scene ready: name='" + (_sceneName ?? "null") + "'  path='" + _scenePath + "'");
                return true;
            }
            catch (Exception ex) { MelonLogger.Error("[Bundle] " + ex.GetType().Name + ": " + ex.Message); return false; }
        }

        static bool HasKw(Material m, string kw) { try { return m.IsKeywordEnabled(kw); } catch { return false; } }
        static void SafeSetInt(Material m, string prop, int v) { try { if (m.HasProperty(prop)) m.SetInt(prop, v); } catch { } }

        static void CopyTexST(Material src, Material dst, string srcProp, string dstProp)
        {
            try
            {
                if (src.HasProperty(srcProp) && dst.HasProperty(dstProp))
                {
                    var t = src.GetTexture(srcProp);
                    if (t != null) dst.SetTexture(dstProp, t);
                    var st = src.GetTextureScale(srcProp);
                    var of = src.GetTextureOffset(srcProp);
                    dst.SetTextureScale(dstProp, st);
                    dst.SetTextureOffset(dstProp, of);
                }
            }
            catch { }
        }

        static void TryCopyEmission(Material src, Material dst)
        {
            try
            {
                if (src.IsKeywordEnabled("_EMISSION"))
                {
                    dst.EnableKeyword("_EMISSION");
                    if (src.HasProperty("_EmissionColor") && dst.HasProperty("_EmissionColor"))
                        dst.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                    if (src.HasProperty("_EmissionMap") && dst.HasProperty("_EmissionMap"))
                    {
                        var t = src.GetTexture("_EmissionMap");
                        if (t != null) dst.SetTexture("_EmissionMap", t);
                        var st = src.GetTextureScale("_EmissionMap");
                        var of = src.GetTextureOffset("_EmissionMap");
                        dst.SetTextureScale("_EmissionMap", st);
                        dst.SetTextureOffset("_EmissionMap", of);
                    }
                }
            }
            catch { }
        }

        static Camera FindHMDCamera()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                if (!c.gameObject.scene.IsValid()) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.name == "HMD") return c;
            }
            return null;
        }

        static void EnsureLayerVisibleToCamera(GameObject go, Camera cam)
        {
            if (go == null || cam == null) return;
            int layer = go.layer;
            int mask = cam.cullingMask;
            if (((mask >> layer) & 1) == 0)
            {
                for (int i = 0; i < 32; i++)
                {
                    if (((mask >> i) & 1) != 0) { SetLayerRecursive(go, i); break; }
                }
            }
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }

        Type FindTypeBySuffix(string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;

            string[] guesses = {
                "SG.Phoenix.Assets.Code" + suffix,
                "Phoenix.Assets.Code" + suffix,
                suffix.TrimStart('.')
            };
            for (int i = 0; i < guesses.Length; i++)
            {
                try { var exact = Type.GetType(guesses[i], false); if (exact != null) return exact; } catch { }
            }

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < asms.Length; a++)
                {
                    var asm = asms[a];
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }
                    catch { continue; }
                    if (types == null) continue;

                    for (int t = 0; t < types.Length; t++)
                    {
                        var ty = types[t]; if (ty == null) continue;
                        string fn = ty.FullName ?? ty.Name;
                        if (fn != null && fn.EndsWith(suffix, StringComparison.Ordinal))
                            return ty;
                    }
                }
            }
            catch { }
            return null;
        }

        void DumpEnvDebug()
        {
            string sky = "(none)";
            try
            {
                if (RenderSettings.skybox != null)
                    sky = (RenderSettings.skybox.shader != null) ? RenderSettings.skybox.shader.name : "(no shader)";
            }
            catch { }

            MelonLogger.Msg("[ENV] AmbientMode=" + RenderSettings.ambientMode
                + " AmbientInt=" + RenderSettings.ambientIntensity.ToString("0.##")
                + " RefInt=" + RenderSettings.reflectionIntensity.ToString("0.##")
                + " DefaultRefl=" + RenderSettings.defaultReflectionMode
                + " Skybox=" + sky);

            if (_hmd != null)
            {
                try { MelonLogger.Msg("[ENV] HMD allowHDR=" + _hmd.allowHDR + " cullingMask=0x" + _hmd.cullingMask.ToString("X8")); } catch { }
            }
            if (_globalProbe != null)
            {
                try { MelonLogger.Msg("[ENV] Probe pos=" + _globalProbe.transform.position + " size=" + _globalProbe.size + " mask=0x" + _globalProbe.cullingMask.ToString("X8")); } catch { }
            }
            MelonLogger.Msg("[ENV] PixelLights=" + QualitySettings.pixelLightCount);
        }

        static readonly string[] kPreserveShaderHints =
            {
                "Water/",
                "Stylized Ocean",
                "StylizedWater",
                "Custom/Puddle_Procedural",
                "Custom/UnlitAdditiveFire",
                "Legacy Shaders/Particles",
                "Particles/"
            };

        static bool ShouldPreserveSpecial(Material m)
        {
            if (m == null) return false;
            var sh = m.shader;
            if (sh == null) return false;
            string name = sh.name ?? "";

            for (int i = 0; i < kPreserveShaderHints.Length; i++)
                if (name.IndexOf(kPreserveShaderHints[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            if (m.renderQueue >= 3000) return true;

            var mn = m.name ?? "";
            if (mn.IndexOf("[KEEP]", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        void EnableDepthTexturesOnAllCameras()
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            int n = 0;
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (!c || !c.gameObject.scene.IsValid()) continue;
                c.depthTextureMode |= DepthTextureMode.Depth;
                n++;
            }
            MelonLogger.Msg($"[Water] DepthTextureMode.Depth enabled on {n} camera(s).");
        }
    }
}
