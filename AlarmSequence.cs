using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerBaseLib.Attributes;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class AlarmSequence : MonoBehaviour
    {
        public AlarmSequence(IntPtr ptr) : base(ptr) { }
        public AlarmSequence()
            : base(ClassInjector.DerivedConstructorPointer<AlarmSequence>())
            => ClassInjector.DerivedConstructorBody(this);

        public Vector3 roomCenterWorld = new Vector3(0f, 5f, -2f);

        public float roomZMax = 4f;
        public float roomZMin = -9f;

        public Color alarmColor = new Color(1f, 0.08f, 0.08f, 1f);

        public float pulseHz = 2.0f;
        public float pulseMin = 0.20f;
        public float pulseMax = 1.00f;

        public float existingIntensityMultiplier = 0.2f;

        public string[] ignoreNameContains = new string[] { "sun", "directional", "debug" };

        public bool useTwoXLanes = true;
        public int beaconsPerLane = 7;

        public float beaconY = 5f;
        public float laneXInset = 1.8f;

        public LightType beaconType = LightType.Point;
        public float beaconRange = 12f;
        public float beaconBaseIntensity = 10f;

        public float chaseSpeed = 1.2f;
        public float chaseHotMultiplier = 2.5f;
        public float chaseWarmMultiplier = 1.2f;
        public float chaseFalloff = 1.0f;

        private bool _running;
        private object _routine;

        private readonly List<Light> _roomLights = new List<Light>(256);
        private readonly Dictionary<int, bool> _origEnabled = new Dictionary<int, bool>(256);
        private readonly Dictionary<int, float> _origIntensity = new Dictionary<int, float>(256);
        private readonly Dictionary<int, Color> _origColor = new Dictionary<int, Color>(256);

        private readonly List<Light> _beacons = new List<Light>(64);

        public LoopingSfx alarmAudio;

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

        public void Critical()
        {
            beaconBaseIntensity = 20f;
        }

        public void StartAlarm()
        {
            if (_running) return;
            _running = true;

            CacheRoomLights();
            ApplyRoomLightBaseline();
            BuildBeacons();

            _routine = MelonCoroutines.Start(Co_AlarmLoop());
            alarmAudio.TurnOn();
        }

        public void StopAlarm()
        {
            if (!_running) return;
            _running = false;

            if (_routine != null)
            {
                MelonCoroutines.Stop(_routine);
                _routine = null;
            }

            RestoreRoomLights();
            DestroyBeacons();
            alarmAudio.TurnOff();
        }

        public void ToggleAlarm()
        {
            if (_running) StopAlarm();
            else StartAlarm();
        }

        private void OnDisable()
        {
            if (_running) StopAlarm();
        }

        private bool ShouldIgnore(Light l)
        {
            if (l == null) return true;
            if (l.type == LightType.Directional) return true;

            var n = l.gameObject.name.ToLowerInvariant();
            if (n.Contains("red light")) return true;

            for (int i = 0; i < ignoreNameContains.Length; i++)
            {
                var s = ignoreNameContains[i];
                if (!string.IsNullOrEmpty(s) && n.Contains(s.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private void CacheRoomLights()
        {
            _roomLights.Clear();
            _origEnabled.Clear();
            _origIntensity.Clear();
            _origColor.Clear();

            var all = Resources.FindObjectsOfTypeAll<Light>();
            for (int i = 0; i < all.Length; i++)
            {
                var l = all[i];
                if (l == null) continue;

                if (!l.gameObject.scene.IsValid()) continue;
                if (!l.gameObject.activeInHierarchy) continue;
                if (ShouldIgnore(l)) continue;

                _roomLights.Add(l);

                int id = l.GetInstanceID();
                _origEnabled[id] = l.enabled;
                _origIntensity[id] = l.intensity;
                _origColor[id] = l.color;
            }
        }

        private void ApplyRoomLightBaseline()
        {
            for (int i = 0; i < _roomLights.Count; i++)
            {
                var l = _roomLights[i];

                int id = l.GetInstanceID();
                float origI = _origIntensity[id];

                l.enabled = true;
                l.intensity = origI * existingIntensityMultiplier;
                l.color = alarmColor;
            }
        }

        private void RestoreRoomLights()
        {
            for (int i = 0; i < _roomLights.Count; i++)
            {
                var l = _roomLights[i];
                int id = l.GetInstanceID();

                l.enabled = _origEnabled[id];
                l.intensity = _origIntensity[id];
                l.color = _origColor[id];
            }

            _roomLights.Clear();
            _origEnabled.Clear();
            _origIntensity.Clear();
            _origColor.Clear();
        }

        private void BuildBeacons()
        {
            DestroyBeacons();
            _beacons.Clear();

            int n = Mathf.Max(1, beaconsPerLane);
            float z0 = roomZMax;
            float z1 = roomZMin;

            if (useTwoXLanes)
            {
                float xA = -Mathf.Abs(laneXInset);
                float xB = Mathf.Abs(laneXInset);

                for (int i = 0; i < n; i++)
                {
                    float t = (n == 1) ? 0.5f : (float)i / (float)(n - 1);
                    float z = Mathf.Lerp(z0, z1, t);

                    _beacons.Add(CreateBeacon($"ALARM_Beacon_L{i}", new Vector3(xA, beaconY, z)));
                    _beacons.Add(CreateBeacon($"ALARM_Beacon_R{i}", new Vector3(xB, beaconY, z)));
                }
            }
            else
            {
                float x = roomCenterWorld.x;

                for (int i = 0; i < n; i++)
                {
                    float t = (n == 1) ? 0.5f : (float)i / (float)(n - 1);
                    float z = Mathf.Lerp(z0, z1, t);

                    _beacons.Add(CreateBeacon($"ALARM_Beacon_{i}", new Vector3(x, beaconY, z)));
                }
            }
        }

        private Light CreateBeacon(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var l = go.AddComponent<Light>();
            l.type = beaconType;
            l.color = alarmColor;
            l.range = beaconRange;
            l.intensity = beaconBaseIntensity;

            if (l.type == LightType.Spot)
                l.spotAngle = 110f;

            return l;
        }

        private void DestroyBeacons()
        {
            for (int i = 0; i < _beacons.Count; i++)
                Destroy(_beacons[i].gameObject);

            _beacons.Clear();
        }

        [HideFromIl2Cpp]
        private IEnumerator Co_AlarmLoop()
        {
            float time = 0f;

            while (_running)
            {
                time += Time.deltaTime;

                float wave = 0.5f + 0.5f * Mathf.Sin(time * 6.28318548f * pulseHz);
                float pulseMul = Mathf.Lerp(pulseMin, pulseMax, wave);

                for (int i = 0; i < _roomLights.Count; i++)
                {
                    var l = _roomLights[i];
                    int id = l.GetInstanceID();
                    float origI = _origIntensity[id];

                    l.intensity = origI * existingIntensityMultiplier * pulseMul;
                    l.color = alarmColor;
                }

                int count = _beacons.Count;
                if (count > 0)
                {
                    int hotIndex = (int)((time * chaseSpeed) % count);

                    for (int i = 0; i < count; i++)
                    {
                        var b = _beacons[i];

                        int d = Mathf.Abs(i - hotIndex);
                        d = Mathf.Min(d, count - d);

                        float w;
                        if (d == 0) w = chaseHotMultiplier;
                        else if (d == 1) w = chaseWarmMultiplier;
                        else
                        {
                            float tail = Mathf.Exp(-chaseFalloff * (float)(d - 1));
                            w = 1.0f + 0.35f * tail;
                        }

                        b.range = beaconRange;
                        b.color = alarmColor;
                        b.intensity = beaconBaseIntensity * pulseMul * w;

                        if (b.type == LightType.Spot)
                            b.spotAngle = 110f;
                    }
                }

                yield return null;
            }
        }
    }
}
