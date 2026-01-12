using System;
using System.Collections;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;

namespace IEYTD2_SubmarineCode
{
    public class DamageOverlayDriver : MonoBehaviour
    {
        public DamageOverlayDriver(IntPtr ptr) : base(ptr) { }
        public DamageOverlayDriver()
            : base(ClassInjector.DerivedConstructorPointer<DamageOverlayDriver>())
            => ClassInjector.DerivedConstructorBody(this);

        public float Distance = 0.25f;
        public float Size = 2.0f;

        public LoopingSfx alarmAudio;

        private GameObject overlayRoot;
        private RectTransform canvasRect;
        private Image overlayImage;

        private static Sprite onePixelSprite;

        private Color tintRgb = new Color(0.38f, 0.015f, 0.015f, 1f);

        private bool running;
        private object routine;

        public void Start()
        {
            var audioObj = new GameObject("Alarm Audio");
            audioObj.transform.position = ObjectBank.Instance.Reactor.transform.position;

            alarmAudio = audioObj.AddComponent<LoopingSfx>();
            alarmAudio.InitAndPlay("alarm_loop", 0);
            alarmAudio.TurnOff();
            alarmAudio.SetVolume(0.6f);
        }

        public void SetVolume(float volume)
        {
            alarmAudio.SetVolume(volume);
        }

        public void InitIfNeeded()
        {
            if (overlayImage != null) return;

            Transform hmd = FindHmdTransform();
            if (hmd == null) return;

            BuildOnePixelSpriteIfNeeded();

            overlayRoot = new GameObject("IEYTD2_CustomDamageOverlay");
            overlayRoot.layer = LayerMask.NameToLayer("UI");
            overlayRoot.transform.SetParent(hmd, false);
            overlayRoot.transform.localPosition = new Vector3(0f, 0f, Distance);
            overlayRoot.transform.localRotation = Quaternion.identity;
            overlayRoot.transform.localScale = Vector3.one;

            var canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32767;
            canvas.pixelPerfect = false;

            var scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            overlayRoot.AddComponent<GraphicRaycaster>();

            canvasRect = overlayRoot.GetComponent<RectTransform>();
            if (canvasRect == null) canvasRect = overlayRoot.AddComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(Size, Size);

            var imageObj = new GameObject("OverlayImage");
            imageObj.layer = overlayRoot.layer;
            imageObj.transform.SetParent(overlayRoot.transform, false);

            var imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.localScale = Vector3.one;

            overlayImage = imageObj.AddComponent<Image>();
            overlayImage.sprite = onePixelSprite;
            overlayImage.type = Image.Type.Simple;
            overlayImage.raycastTarget = false;
            overlayImage.material = null;

            ApplyAlpha(0f);
        }

        public void SetTint(Color rgb)
        {
            tintRgb = new Color(rgb.r, rgb.g, rgb.b, 1f);
            ApplyAlpha(overlayImage.color.a);
        }

        public void SetLevel(float level01)
        {
            InitIfNeeded();
            level01 = Mathf.Clamp01(level01);

            StopRoutine();
            ApplyAlpha(level01);
        }

        public void FadeTo(float target01, float duration)
        {
            InitIfNeeded();
            target01 = Mathf.Clamp01(target01);

            StopRoutine();
            routine = MelonCoroutines.Start(CoFadeTo(target01, duration));
        }

        public void Pulse(float intensity = 1f, float fadeIn = 0.06f, float hold = 0.08f, float fadeOut = 0.35f)
        {
            InitIfNeeded();
            intensity = Mathf.Clamp01(intensity);

            StopRoutine();
            routine = MelonCoroutines.Start(CoPulse(intensity, fadeIn, hold, fadeOut));
        }

        public void Debug_SolidOn(float seconds = 1.0f)
        {
            InitIfNeeded();

            StopRoutine();
            ApplyAlpha(1f);
            routine = MelonCoroutines.Start(CoHoldThen(seconds, 0f));
        }

        public void Die(float redFadeIn = 0.5f, float redHold = 0.4f, float blackFade = 1.0f)
        {
            InitIfNeeded();

            StopRoutine();
            routine = MelonCoroutines.Start(CoDie(redFadeIn, redHold, blackFade));
        }

        public void Undie(float fadeOutBlack = 0.6f)
        {
            InitIfNeeded();

            StopRoutine();
            routine = MelonCoroutines.Start(CoUndie(fadeOutBlack));
        }

        private void ApplyAlpha(float alpha)
        {
            var c = overlayImage.color;
            c.r = tintRgb.r;
            c.g = tintRgb.g;
            c.b = tintRgb.b;
            c.a = Mathf.Clamp01(alpha);
            overlayImage.color = c;

            overlayRoot.transform.localPosition = new Vector3(0f, 0f, Distance);
            canvasRect.sizeDelta = new Vector2(Size, Size);
        }

        private void StopRoutine()
        {
            if (routine == null) return;
            MelonCoroutines.Stop(routine);
            routine = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator CoFadeTo(float target01, float duration)
        {
            float startAlpha = overlayImage.color.a;

            if (duration <= 0f)
            {
                ApplyAlpha(target01);
                routine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(startAlpha, target01, elapsed / duration);
                ApplyAlpha(alpha);
                yield return null;
            }

            ApplyAlpha(target01);
            routine = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator CoPulse(float intensity, float fadeIn, float hold, float fadeOut)
        {
            yield return CoFadeTo(intensity, fadeIn);

            float elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return CoFadeTo(0f, fadeOut);
            routine = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator CoHoldThen(float holdSeconds, float endAlpha)
        {
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ApplyAlpha(endAlpha);
            routine = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator CoDie(float redFadeIn, float redHold, float blackFade)
        {
            yield return CoFadeTo(1f, redFadeIn);

            float elapsed = 0f;
            while (elapsed < redHold)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetTint(Color.black);
            yield return CoFadeTo(1f, blackFade);

            ApplyAlpha(1f);
            routine = null;
        }

        [HideFromIl2Cpp]
        private IEnumerator CoUndie(float fadeOutBlack)
        {
            SetTint(Color.black);
            yield return CoFadeTo(0f, fadeOutBlack);

            SetTint(new Color(0.38f, 0.015f, 0.015f, 1f));
            ApplyAlpha(0f);
            routine = null;
        }

        private static void BuildOnePixelSpriteIfNeeded()
        {
            if (onePixelSprite != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);

            onePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }

        private static Transform FindHmdTransform()
        {
            var go = GameObject.Find("VRRig/HMD");
            if (go != null) return go.transform;

            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name == "HMD") return g.transform;
            }

            return null;
        }
    }
}
