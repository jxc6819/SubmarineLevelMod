using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using MelonLoader;
using IEYTD2_SubmarineCode;
using UnhollowerRuntimeLib;
using System.Collections;
using UnhollowerBaseLib.Attributes;
using SG.Phoenix.Assets.Code.InputManagement;
using SG.Phoenix.Assets.Code.Utility;
using SG.Phoenix.Assets.Code.Interactables;

namespace IEYTD2_SubmarineCode
{
    public class SubmarineLevelLogic : MonoBehaviour
    {
        public SubmarineLevelLogic(IntPtr ptr) : base(ptr) { }
        public SubmarineLevelLogic() : base(ClassInjector.DerivedConstructorPointer<SubmarineLevelLogic>())
            => ClassInjector.DerivedConstructorBody(this);

        private const float YellowThreshold = 0.25f;
        private const float ThresholdEpsilon = 0.002f; 

        NeedleScript needle;
        WaterSprayDriver waterSpray;
        Rigidbody wheelRB;
        RigidbodyConstraints _wheelConstraints;
        SpinFan fan;
        WheelScript wheel;
        

        public bool _reactorVentSabotaged;
        public bool _reactorCoolantSabotaged;
        public bool _terminalStopperSabotaged;
        public bool _ventPopped = false;
        public bool _blackoutTriggered = false;

        private object _needleRoutine;
        private bool _isAnimatingNeedle;

        private float _lastNeedleValue = 0f;
        private bool _isDead = false;
        public ObjectBank bank;
        SteamDriver steam;
        MyMod myMod;
        int guardsDead = 0;

        bool _redHit = false;
        bool _stopHit = false;
        GameObject HMD;

        public LoopingSfx coolantAudio;
        SubLoopAmbience ambience;
        RotationalMotion panelRot;
        bool _panelOpen = false;

        //WireManager wireManager;

        public void OnEnable()
        {
            bank = ObjectBank.Instance;
            steam = bank.ReactorVentTrigger.GetComponent<SteamDriver>();
            myMod = MyMod.Instance;
            panelRot = bank.ELV_RocketThrusterControlBox.transform.GetChild(1).GetChild(0).gameObject.GetComponent<RotationalMotion>();
            GameObject coolAudioObj = new GameObject("coolAudioObj");
            coolAudioObj.transform.position = new Vector3(6.5f, 1.5f, 2.6f);
            coolantAudio = coolAudioObj.AddComponent<LoopingSfx>();
            MelonLogger.Msg("Checkpoint 1");
            AudioClip ambienceClip = SubBundle2Manager.GetAudio("Large Industrial Gas Heater 1");
            MelonLogger.Msg("Checkpoint 2");
            MelonLogger.Msg("AmbienceClip Null: " + (ambienceClip == null));
            HMD = GameObject.Find("HMD");
            AudioSource audSource = bank.Reactor.GetComponent<AudioSource>();
            if (audSource == null) audSource = bank.Reactor.AddComponent<AudioSource>();
            audSource.clip = ambienceClip;
            audSource.volume = 0.3f;
            MelonLogger.Msg("Checkpoint 3");
            ambience = bank.Reactor.GetComponent<SubLoopAmbience>();
            if (ambience == null) ambience = bank.Reactor.AddComponent<SubLoopAmbience>();
            MelonLogger.Msg("Checkpoint 4");
            ambience.clip = ambienceClip;
            MelonLogger.Msg("Checkpoint 5");
            ambience.source = audSource;
            MelonLogger.Msg("Checkpoint 6");
            ambience.PlayLoop();
            MelonLogger.Msg("Checkpoint 7");
            MelonCoroutines.Start(IntroSequence());


            coolantAudio.InitAndPlay("propeller_loop", 0.6f);


            // wireManager = gameObject.GetComponent<WireManager>();
            //GameObject.Find("PickUp_HOST_ReactorPipeGrabbable").SetActive(false);
            try
            {
                var needleGo = GameObject.Find("SM_needle");
                needle = needleGo ? needleGo.GetComponent<NeedleScript>() : null;

                var fanGo = GameObject.Find("_Fan");
                fan = fanGo ? fanGo.GetComponent<SpinFan>() : null;

                var wheelGo = GameObject.Find("SM_wheelTerminal");
                wheelRB = wheelGo ? wheelGo.GetComponent<Rigidbody>() : null;

                wheel = wheelGo ? wheelGo.GetComponent<WheelScript>() : null;

                waterSpray = GameObject.Find("CoolantNozzle").GetComponent<WaterSprayDriver>();

                if (wheelRB != null)
                    _wheelConstraints = wheelRB.constraints;

                if (needle != null) _lastNeedleValue = needle.value01;

                if (needle == null) MelonLogger.Warning("SubmarineLevelLogic: NeedleScript missing (SM_needle).");
                if (fan == null) MelonLogger.Warning("SubmarineLevelLogic: SpinFan missing (_Fan).");
                if (wheelRB == null) MelonLogger.Warning("SubmarineLevelLogic: Rigidbody missing (SM_wheelTerminal).");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SubmarineLevelLogic OnEnable exception: {ex}");
            }
        }

        private void Update()
        {
            if (needle == null) return;

            float now = needle.value01;

            if (!_isAnimatingNeedle && !_reactorVentSabotaged)
            {
                bool wasBelow = _lastNeedleValue < (YellowThreshold - ThresholdEpsilon);
                bool nowAtOrAbove = now >= (YellowThreshold + ThresholdEpsilon);

                if (wasBelow && nowAtOrAbove)
                {
                    MelonLogger.Msg("yellow threshold crossed from below");
                    triggerReactorVent();
                }
            }

            _lastNeedleValue = now;

            if(!_panelOpen && panelRot._currentRotation > 220f && _blackoutTriggered)
            {
                _panelOpen = true;
                playHandler("Handler_panelFried", 5f);
            }
        }
        public void yellowTempHit()
        {
            if (_reactorVentSabotaged || _isAnimatingNeedle)
            {
                if (_reactorVentSabotaged && !_ventPopped)
                {
                    _ventPopped = true;
                    popVent();
                }
            }
            else triggerReactorVent();
        }

        public void orangeTempHit()
        {
            if (_reactorCoolantSabotaged || _isAnimatingNeedle) return;
            triggerReactorCoolant();
        }

        public void redTempHit() 
        {
            if (!_redHit)
            {
                _redHit = true;
                triggerArmedGuard();
            }
        }

        public void stopperHit() 
        {
            if(bank.Stopper.GetComponent<StopperScript>().popped == false && _blackoutTriggered == false)
            {
                _blackoutTriggered = true;
                MelonCoroutines.Start(beginBlackoutSequence());
            }
        }
        public void armedGuardDead() 
        {
            guardsDead++;
            if(guardsDead >=2)
            {
                playStinger();
                UnfreezeWheel();
                playHandler("Handler_guardsDead", 0.8f);
            }
        }
        bool _critTempHit = false;
        public void criticalTempHit()
        {
            if (!_critTempHit)
            {
                _critTempHit = true;
                MelonLogger.Msg("[SLL] - Critical Temp Hit");
                MelonCoroutines.Start(MeltdownSequence());
            }
            
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator MeltdownSequence()
        {
            AlarmSequence Alarm = gameObject.GetComponent<AlarmSequence>();
            AudioUtil.PlayAt("vault_explosion_02", bank.Reactor.transform.position, 6);
            bank.VRRig.GetComponent<TransformShaker>().ShakeDefault();
            yield return new WaitForSeconds(1f);
            Alarm.SetVolume(9f);
            Alarm.Critical();
            Alarm.StartAlarm();
            playSubVoice("SubVoice_getOut");
            yield return new WaitForSeconds(7f);
            bank.Hatch.GetComponent<HatchScript>().Unlock();
            playHandler("Handler_getOut", 0.8f);
        }

        bool _ventHintPlayed = false;
        public void triggerReactorVent()
        {
            playSubVoice("SubVoice_ventingSteam");
            steam.emitting = true;
            AudioUtil.PlayAt("ventSteam", steam.transform.position);
            StartNeedleRoutine(0f, 4f);
            Invoke("turnOffVentSteam", 4.0f);
        }

        public void triggerReactorCoolant()
        {
            playSubVoice("SubVoice_deployingCoolant");
            StartNeedleRoutine(0f, 6f);
            AudioUtil.PlayAt("waterSpray", waterSpray.transform.position, 0.6f);
            waterSpray.enabled = true;
            waterSpray.emitting = true;
            Invoke("turnOffWaterSpray", 6.0f);
        }

        private void StartNeedleRoutine(float targetValue, float duration)
        {
            if (_isAnimatingNeedle) return;

            if (needle != null && Mathf.Abs(needle.value01 - targetValue) < 0.001f)
            {
                MelonLogger.Msg("needle already at target; skipping animation");
                return;
            }

            if (_needleRoutine != null) MelonCoroutines.Stop(_needleRoutine);
            _needleRoutine = MelonCoroutines.Start(FreezeWheelAndMoveNeedle(targetValue, duration));
        }

        public void triggerArmedGuard()
        {
            FreezeWheel();
            //intercom: sending guards 
            MelonCoroutines.Start(armedGuardSequence());
            //bank.Henchman1.transform.parent.gameObject.SetActive(true);
        }

        [HideFromIl2Cpp]
        private IEnumerator armedGuardSequence()
        {
            gameObject.GetComponent<AlarmSequence>().StartAlarm();
            playSubVoice("SubVoice_sendingGuards", 0.5f);
            yield return new WaitForSeconds(5f);
            playHandler("Handler_guardsComing", 0.8f);
            yield return new WaitForSeconds(12f);
            Vector3 pos = bank.Henchman1.transform.position;
            AudioUtil.PlayAt("VO_Vault_Henchmen_Room_Lockdown_03", pos, 20f);
            yield return new WaitForSeconds(8.5f);
            AudioUtil.PlayAt("vault_door_kick", pos, 20f);
            yield return new WaitForSeconds(2.3f);
            AudioUtil.PlayAt("hatch_open", pos, 20f);
            yield return new WaitForSeconds(2f);
            bank.Henchman1.transform.parent.gameObject.SetActive(true);
            bank.Henchman1.GetComponent<HenchmanController>().StartHenchman();
            bank.Henchman2.GetComponent<HenchmanController>().StartHenchman();
        }

        [HideFromIl2Cpp]
        private IEnumerator FreezeWheelAndMoveNeedle(float targetValue, float duration)
        {
            MelonLogger.Msg("freeze wheel move needle");
            _isAnimatingNeedle = true;

            bool haveRB = (wheelRB != null);
            RigidbodyConstraints saved = default;

            if (haveRB)
            {
                saved = wheelRB.constraints;
                wheelRB.constraints = saved | RigidbodyConstraints.FreezeRotation; 
            }

            try
            {
                MelonLogger.Msg("starting MoveNeedleOverTime");
                yield return MoveNeedleOverTime(targetValue, duration);
                MelonLogger.Msg("finished MoveNeedleOverTime");
            }
            finally
            {
                if (haveRB)
                    wheelRB.constraints = saved;

                _needleRoutine = null;
                _isAnimatingNeedle = false;

                if (needle != null)
                    _lastNeedleValue = needle.value01;
            }
        }

        public void FreezeWheel()
        {
            if (wheel != null)
                wheel.SetLocked(true);
        }

        public void UnfreezeWheel()
        {
            if (wheel != null)
                wheel.SetLocked(false);
        }


        [HideFromIl2Cpp]
        private IEnumerator MoveNeedleOverTime(float moveTo, float seconds)
        {
            MelonLogger.Msg($"move needle over time -> {moveTo} in {seconds}s");

            float start = (needle != null) ? needle.value01 : 0f;
            if (seconds <= 0.0001f) seconds = 0.0001f;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (needle != null)
                    needle.value01 = Mathf.Lerp(start, moveTo, t / seconds);
                yield return null;
            }

            if (needle != null)
                needle.value01 = moveTo;
        }

        private void popVent()
        {
            playStinger();
            GameObject ventTrigger = bank.ReactorVentTrigger;
            Transform ventTf = ventTrigger.transform;
            Transform parent1 = ventTf.parent;
            Transform parent2 = parent1.parent;
            GameObject staticPipe = parent2.gameObject;

            GameObject rpg = bank.ReactorPipeGrabbable;

            GameObject explosionObj = new GameObject("explosion");
            explosionObj.transform.position = ventTrigger.transform.position;
            explosionObj.AddComponent<ExplosionDriver>().TriggerExplosion();

            rpg.transform.position = staticPipe.transform.position;
            rpg.transform.rotation = staticPipe.transform.rotation;

            UnityEngine.Object.Destroy(ventTrigger);
            UnityEngine.Object.Destroy(staticPipe);

            rpg.SetActive(true);
            MyMod.Instance.MakeGrabbable("ReactorPipeGrabbable");
            Rigidbody rb = rpg.transform.parent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = rpg.transform.forward;
                float force = 0.2f;
                rb.AddForce(dir * force, ForceMode.Impulse);
            }

            bank.VentSponge.SetActive(false);
            playHandler("Handler_ventSabotaged", 1.8f);
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator beginBlackoutSequence()
        {
            FreezeWheel();
            playSubVoice("SubVoice_blackout", 0.3f);
            yield return new WaitForSeconds(4.5f);
            beginBlackout();
        }

        void beginBlackout()
        {
            gameObject.GetComponent<AlarmSequence>().StopAlarm();
            myMod.SetBlackout(true);
          //  AudioUtil.PlayAt("propeller_deactivation", coolantAudio.transform.position);
            ambience.StopLoop();
           // coolantAudio.TurnOff();
            bank.WireSparkPoint.GetComponent<SparkDriver>().EnableLoop(true);
            toggleChip("Red Chip", true);
            toggleChip("Blue Chip", true);
            toggleChip("Green Chip", false);
            playHandler("Handler_blackout", 0.8f, true);
            //myMod.fixFlashlight();

        }

        public void toggleChip(string name, bool on)
        {
            myMod.SetChipLed_OnlyLightMaterial(name, on);
        }

        public void endBlackout()
        {
            playStinger();
            myMod.SetBlackout(false);
            AudioUtil.PlayAt("propeller_activation", coolantAudio.transform.position);
            ambience.PlayLoop();
           // gameObject.GetComponent<AlarmSequence>().StartAlarm();
            // coolantAudio.TurnOn();
            UnfreezeWheel();
            playHandler("Handler_blackoutOver", 0.8f);
            toggleChip("Blue Chip", false);
        }

        public void playStinger()
        {
            AudioUtil.PlayAt("puzzle_step_stinger", wheel.transform.position, 0.7f);
        }

        void playHandler(string clip)
        {
            // AudioUtil.PlayAt(clip, HMD.transform.position);
            playHandler(clip, 0);
        }

        void playSubVoice(string clip)
        {
            AudioUtil.PlayAt(clip, wheel.transform.position);
        }

        void playHandler(string clip, float delay, bool priority = false)
        {
            //AudioUtil.PlayAt(clip, HMD.transform.position);
            MelonCoroutines.Start(HandlerDelay(clip, delay, priority));
        }

        bool handlerTalking = false;

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator HandlerDelay(string clipName, float delay, bool priority = false)
        {
            if (priority)
            {
                while (handlerTalking) yield return null;
                handlerTalking = true;

                if (delay > 0f) yield return new WaitForSeconds(delay);
            }
            else
            {
                if (delay > 0f) yield return new WaitForSeconds(delay);

                while (handlerTalking) yield return null;
                handlerTalking = true;
            }

            var src = AudioUtil.PlayAt(clipName, HMD.transform.position);

            float len = 0f;
            if (src != null && src.clip != null) len = src.clip.length;
            else
            {
                var clip = SubBundle2Manager.GetAudio(clipName);
                if (clip != null) len = clip.length;
            }

            if (len > 0.01f) yield return new WaitForSeconds(len + 0.8f);
            else yield return new WaitForSeconds(1.0f);

            handlerTalking = false;
        }



        void playSubVoice(string clip, float delay)
        {
            //AudioUtil.PlayAt(clip, HMD.transform.position);
            MelonCoroutines.Start(SubVoiceDelay(clip, delay));
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator SubVoiceDelay(string clip, float delay)
        {
            yield return new WaitForSeconds(delay);
            AudioUtil.PlayAt(clip, wheel.transform.position);
        }

        public void playIntroClip()
        {
            playHandler("Handler_intro");
        }

        [HideFromIl2Cpp]
        private System.Collections.IEnumerator IntroSequence()
        {
            yield return new WaitForSeconds(1f);
            playSubVoice("start_resume_stinger", 0);
            yield return new WaitForSeconds(4f);
            playHandler("Handler_intro");
        }

        public void KillPlayer()
        {
            myMod.KillPlayer();
        }

        public int damage = 0;
        public void DamagePlayer()
        {
            if(damage == 4)
            {
                KillPlayer();
                return;
            }
            myMod.DamagePlayer();
            damage++;
        }

        bool _coolantHintPlayed = false;

        private void turnOffWaterSpray() 
        { 
            waterSpray.emitting = false; 
            if(!_coolantHintPlayed)
            {
                _coolantHintPlayed = true;
                playHandler("Handler_coolantHint");
            }
        }
        private void turnOffVentSteam() 
        { 
            steam.emitting = false; 
            if(!_ventHintPlayed)
            {
                _ventHintPlayed = true;
                playHandler("Handler_ventHint", 0.8f);
            }
        }

        public void reactorVentSabotaged() { _reactorVentSabotaged = true; }
        public void fanSabotaged() { if (fan != null) { fan.stopFan(); playStinger(); } }
        public void coolantSabotaged() 
        { _reactorCoolantSabotaged = true; 
            playStinger();
            playHandler("Handler_coolantSabotaged", 1f);
        }
        public void terminalStopperSabotaged() 
        {
            _terminalStopperSabotaged = true;
            AudioUtil.PlayAt("memento_get_stinger", wheel.transform.position);
            playHandler("Handler_stopperSabotaged", 0.8f);
            //voiceover/
        }
    }
}


