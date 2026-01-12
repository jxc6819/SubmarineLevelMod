using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    public class SubLoopAmbience : MonoBehaviour
    {
        public SubLoopAmbience(IntPtr p) : base(p) { }
        public SubLoopAmbience() : base(ClassInjector.DerivedConstructorPointer<SubLoopAmbience>())
            => ClassInjector.DerivedConstructorBody(this);
        public AudioSource source;
        public AudioClip clip;

        public float loopStart = 54.449f;

        public float loopEnd = 73.011f;

        public float tailStart = 77.260f;

        public bool playOnStart = true;
        public bool loopingEnabled = true;

        public float seamFadeSeconds = 0.015f;
        public float jumpCooldownSeconds = 0.05f;

        float _baseVolume = 1f;
        float _lastJumpTime = -999f;

        public bool _debugStartLoop = false;
        public bool _debugDieDown = false;
        public bool _debugKill = false;

        void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false; 

            if (clip != null && source.clip != clip)
                source.clip = clip;

            _baseVolume = source.volume;
        }

        void Start()
        {
            if (playOnStart)
                PlayLoop();
        }

        void Update()
        {
            if (!loopingEnabled) return;
            if (source == null || source.clip == null) return;
            if (!source.isPlaying) return;

            if (loopEnd <= loopStart) return;

            if (source.time >= loopEnd && (Time.unscaledTime - _lastJumpTime) >= jumpCooldownSeconds)
            {
                if (seamFadeSeconds > 0f)
                {
                    source.volume = 0f;
                    source.time = loopStart;
                    CancelInvoke(nameof(RestoreVolume));
                    Invoke(nameof(RestoreVolume), seamFadeSeconds);
                }
                else
                {
                    source.time = loopStart;
                }

                _lastJumpTime = Time.unscaledTime;
            }

        }

        void RestoreVolume()
        {
            if (source != null)
                source.volume = _baseVolume;
        }

        public void PlayLoop()
        {
            if (source == null) return;

            if (clip != null && source.clip != clip)
                source.clip = clip;

            _baseVolume = source.volume;

            source.time = Mathf.Max(0f, loopStart);
            source.loop = false;
            loopingEnabled = true;
            source.Play();
        }

        public void StopLoop()
        {
            if (source == null || source.clip == null) return;

            loopingEnabled = false;
            source.time = Mathf.Clamp(tailStart, 0f, source.clip.length - 0.01f);
            if (!source.isPlaying) source.Play();
        }
    }

}
