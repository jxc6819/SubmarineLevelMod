using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

public static class SubBundle2Manager
{
    private const string BundleFileName = "subbundle2"; 

    private static AssetBundle _bundle;
    private static bool _tried;

    private static readonly Dictionary<string, Texture2D> _texCache =
        new Dictionary<string, Texture2D>();

    private static readonly Dictionary<string, AudioClip> _audioCache =
        new Dictionary<string, AudioClip>();

    public static bool Init()
    {
        if (_tried) return _bundle != null;
        _tried = true;

        try
        {
            var path = Path.Combine(MelonUtils.UserDataDirectory, BundleFileName);

            if (!File.Exists(path))
            {
                MelonLogger.Warning("[SubBundle2] Bundle not found: " + path);
                return false;
            }

            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null)
            {
                MelonLogger.Error("[SubBundle2] LoadFromFile returned null.");
                return false;
            }

            MelonLogger.Msg("[SubBundle2] Loaded OK. Assets=" + _bundle.GetAllAssetNames().Length);
            return true;
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[SubBundle2] Failed: " + ex);
            return false;
        }
    }

    public static Texture2D GetTexture(string nameContains)
    {
        if (!Init()) return null;

        var key = nameContains.ToLowerInvariant();
        Texture2D cached;
        if (_texCache.TryGetValue(key, out cached)) return cached;

        var names = _bundle.GetAllAssetNames();
        for (int i = 0; i < names.Length; i++)
        {
            var assetName = names[i];
            if (!assetName.Contains(key)) continue;

            var tex = _bundle.LoadAsset<Texture2D>(assetName);
            if (tex != null)
            {
                _texCache[key] = tex;
                return tex;
            }
        }

        MelonLogger.Warning("[SubBundle2] Texture not found containing: " + nameContains);
        return null;
    }

    public static AudioClip GetAudio(string nameContains)
    {
        if (!Init()) return null;

        var key = nameContains.ToLowerInvariant();
        AudioClip cached;
        if (_audioCache.TryGetValue(key, out cached)) return cached;

        var names = _bundle.GetAllAssetNames();
        for (int i = 0; i < names.Length; i++)
        {
            var assetName = names[i];
            if (!assetName.Contains(key)) continue;

            var clip = _bundle.LoadAsset<AudioClip>(assetName);
            if (clip != null)
            {
                _audioCache[key] = clip;
                return clip;
            }
        }

        MelonLogger.Warning("[SubBundle2] Audio not found containing: " + nameContains);
        return null;
    }
}
