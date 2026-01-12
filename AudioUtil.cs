using UnityEngine;
using System;
using UnhollowerRuntimeLib;

namespace IEYTD2_SubmarineCode
{
    public static class AudioUtil
    {
        public static AudioSource PlayAt(
            string clipName,
            Vector3 position,
            float volume = 1f,
            float minDistance = 8f,
            float maxDistance = 250f,
            bool ignoreListenerVolume = true)
        {
            var clip = SubBundle2Manager.GetAudio(clipName);
            if (clip == null) return null;

            var audioObject = new GameObject("OneShotAudio_" + clipName);
            audioObject.transform.position = position;

            var source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);

            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;

            source.ignoreListenerVolume = ignoreListenerVolume;
            source.ignoreListenerPause = true;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.spatialize = false;

            source.Play();
            UnityEngine.Object.Destroy(audioObject, clip.length + 0.1f);
            return source;
        }

        public static void Stop(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            UnityEngine.Object.Destroy(source.gameObject);
        }
    }

    public class LoopingSfx : MonoBehaviour
    {
        public LoopingSfx(IntPtr ptr) : base(ptr) { }
        public LoopingSfx()
            : base(ClassInjector.DerivedConstructorPointer<LoopingSfx>())
            => ClassInjector.DerivedConstructorBody(this);

        private AudioSource _source;

        public void InitAndPlay(
            string clipName,
            float volume = 0.6f,
            float minDistance = 1f,
            float maxDistance = 20f)
        {
            var clip = SubBundle2Manager.GetAudio(clipName);
            if (clip == null) return;

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();

            _source.clip = clip;
            _source.loop = true;
            _source.volume = Mathf.Clamp01(volume);

            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = minDistance;
            _source.maxDistance = maxDistance;

            _source.Play();
        }

        public void TurnOn()
        {
            if (_source != null && !_source.isPlaying)
                _source.Play();
        }

        public void TurnOff()
        {
            if (_source != null)
                _source.Stop();
        }

        public void SetVolume(float volume)
        {
            if (_source != null)
                _source.volume = Mathf.Clamp01(volume);
        }
    }
}
