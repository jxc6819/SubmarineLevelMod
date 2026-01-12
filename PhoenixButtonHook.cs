using System;
using HarmonyLib;
using SG.Phoenix.Assets.Code.Interactables;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class PhoenixButtonHook : MonoBehaviour
    {
        public PhoenixButtonHook(IntPtr ptr) : base(ptr) { }
        public PhoenixButtonHook()
            : base(ClassInjector.DerivedConstructorPointer<PhoenixButtonHook>())
            => ClassInjector.DerivedConstructorBody(this);

        public string targetButtonName = "P_WinRoom_INT_DebrielCaseButton_01";
        public float pressCooldownSeconds = 0.25f;
        public bool blockDefaultPress = false;

        public static bool restarting = false;

        private static bool _patchInstalled;
        private static int _targetButtonId;
        private static float _cooldownSeconds;
        private static float _lastPressTime;

        private Button _targetButton;

        private void OnEnable()
        {
            InstallPatch();
            HookTargetButton();
        }

        public void HookTargetButton()
        {
            GameObject targetObject = null;

            if (!string.IsNullOrEmpty(targetButtonName))
                targetObject = GameObject.Find(targetButtonName);

            if (targetObject == null)
                targetObject = gameObject;

            _targetButton = targetObject.GetComponent<Button>();
            if (_targetButton == null)
                return;

            _targetButtonId = _targetButton.gameObject.GetInstanceID();
            _cooldownSeconds = pressCooldownSeconds;
        }

        public static void HandlePressed(Button button)
        {
            float now = Time.unscaledTime;
            if (now - _lastPressTime < _cooldownSeconds)
                return;

            _lastPressTime = now;
            restarting = true;
        }

        private static void InstallPatch()
        {
            if (_patchInstalled) return;
            _patchInstalled = true;

            var harmony = new Harmony("IEYTD2_SubmarineCode.PhoenixButtonHook");
            harmony.PatchAll(typeof(PhoenixButtonHookPatches));
        }

        [HarmonyPatch]
        private static class PhoenixButtonHookPatches
        {
            [HarmonyPatch(typeof(Button), "PressButton")]
            [HarmonyPrefix]
            private static bool PressButton_Prefix(Button __instance)
            {
                if (__instance == null) return true;
                if (_targetButtonId == 0) return true;

                if (__instance.gameObject.GetInstanceID() != _targetButtonId)
                    return true;

                HandlePressed(__instance);

                if (blockDefaultPress)
                    return false;

                return true;
            }
        }
    }
}
