using System;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IEYTD2_SubmarineCode
{
    public class SubmarineRunManager : MonoBehaviour
    {
        public SubmarineRunManager(IntPtr ptr) : base(ptr) { }
        public SubmarineRunManager()
            : base(ClassInjector.DerivedConstructorPointer<SubmarineRunManager>())
            => ClassInjector.DerivedConstructorBody(this);

        public static SubmarineRunManager Instance;

        GameObject _templateRoot;

        public GameObject ActiveRoot { get; private set; }

        public bool TemplateReady => _templateRoot != null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                try { Destroy(this); } catch { }
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            MelonLogger.Msg("[RunManager] SubmarineRunManager awake.");
        }

        public void RegisterInitialRunRoot(GameObject mergedRoot)
        {
            if (mergedRoot == null)
            {
                MelonLogger.Error("[RunManager] RegisterInitialRunRoot called with null mergedRoot.");
                return;
            }

            if (_templateRoot == null)
            {
                _templateRoot = UnityEngine.Object.Instantiate(mergedRoot);
                _templateRoot.name = mergedRoot.name + "_TEMPLATE";
                _templateRoot.SetActive(false);

                UnityEngine.Object.DontDestroyOnLoad(_templateRoot);

                MelonLogger.Msg("[RunManager] Created submarine template root.");
            }

            ActiveRoot = mergedRoot;
        }

        public GameObject RestartRunFromTemplate()
        {
            if (_templateRoot == null)
            {
                MelonLogger.Error("[RunManager] Restart requested but template not ready.");
                return null;
            }

            if (ActiveRoot != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(ActiveRoot);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("[RunManager] Exception destroying old ActiveRoot: " + ex);
                }
                ActiveRoot = null;
            }

            GameObject newRoot = null;
            try
            {
                newRoot = UnityEngine.Object.Instantiate(_templateRoot);
                newRoot.name = "SubmarineRun_ROOT";
                newRoot.SetActive(true);

                var activeScene = SceneManager.GetActiveScene();
                SceneManager.MoveGameObjectToScene(newRoot, activeScene);

                ActiveRoot = newRoot;
                MelonLogger.Msg("[RunManager] Spawned new run from template.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[RunManager] Failed to spawn new run from template: " + ex);
            }

            return newRoot;
        }
    }
}
