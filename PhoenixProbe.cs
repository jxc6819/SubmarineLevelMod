/*
  THIS CODE IS AI-GENERATED
  USED TO 'PROBE' THROUGH SHADERS AND LOG THEM FOR DEBUGGING PURPOSES
  */



// PhoenixProbe.cs
// Unity 2019.4.18f1 • IL2CPP • MelonLoader 0.5.7
using System;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace IEYTD2_SubmarineCode
{
    /// <summary>
    /// Runtime recon for IEYTD2's shipped shaders & materials.
    /// Use: PhoenixProbe.Initialize() once, then call the Dump* methods via hotkeys or your scene-load hooks.
    /// Outputs go to Application.persistentDataPath, e.g.
    ///   %USERPROFILE%\AppData\LocalLow\Schell Games\I Expect You To Die 2\
    /// </summary>
    public static class PhoenixProbe
    {
        // ---------------- Public API ----------------

        public static void Initialize()
        {
            MelonLogger.Msg("[PhoenixProbe] Ready.");
        }

        /// <summary>
        /// Warms up shader variants (broadens what's in memory), then dumps ALL loaded shaders with property metadata.
        /// Also attempts to scan loaded AssetBundles for Shader assets and lists them.
        /// </summary>
        public static void WarmupAndDumpAllShadersDetailed()
        {
            TryWarmupAllShaders();
            DumpAllLoadedShadersDetailed();
            DumpShadersFromLoadedAssetBundles();
        }

        /// <summary>
        /// Dump Phoenix/* materials currently in memory (any scene).
        /// Shows: shader, queue, RenderType, blends, ZWrite, common props, keywords.
        /// </summary>
        public static void DumpAllPhoenixMaterials(int max = 500)
        {
            var sb = new StringBuilder(1024 * 256);
            Header(sb, "PHOENIX MATERIALS (IN MEMORY)");

            var mats = Resources.FindObjectsOfTypeAll<Material>();
            SortByName(mats);

            int count = 0;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m || m.shader == null) continue;
                var sname = m.shader.name ?? "";
                if (!StartsWith(sname, "Phoenix/")) continue;

                DumpMaterialBlock(sb, m);
                count++;
                if (max > 0 && count >= max) break;
            }

            WriteFile("Phoenix_MaterialDump", sb);
        }

        /// <summary>
        /// Dump all materials in active memory that belong to scenes whose name contains sceneNameHint (optional).
        /// Set onlyPhoenix=false to see both Phoenix and non-Phoenix materials (useful after your merge).
        /// </summary>
        public static void DumpSceneMaterials(string sceneNameHint = null, bool onlyPhoenix = false, int max = 2000)
        {
            var sb = new StringBuilder(1024 * 256);
            Header(sb, "SCENE MATERIALS");

            var renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            ArraySortByName(renderers);

            int written = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r || !r.enabled) continue;
                var sc = r.gameObject.scene;
                if (!sc.IsValid()) continue;

                if (!string.IsNullOrEmpty(sceneNameHint))
                {
                    var sn = sc.name ?? "";
                    if (sn.IndexOf(sceneNameHint, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (!mat || mat.shader == null) continue;

                    var sname = mat.shader.name ?? "";
                    if (onlyPhoenix && !StartsWith(sname, "Phoenix/")) continue;

                    sb.AppendLine("Renderer: " + Safe(r.name) + "   Scene: " + Safe(sc.name));
                    DumpMaterialBlock(sb, mat);
                    written++;
                    if (max > 0 && written >= max) goto DONE;
                }
            }

        DONE:
            WriteFile("Scene_MaterialDump", sb);
        }

        /// <summary>
        /// Heuristic scan for refraction/grab/screen properties on all loaded shaders.
        /// Advisory only (we'll set multiple globals at runtime anyway).
        /// </summary>
        public static void DumpLikelyRefractionProps()
        {
            var sb = new StringBuilder(8192);
            Header(sb, "LIKELY REFRACTION/GRAB PROPERTIES");

            var shaders = Resources.FindObjectsOfTypeAll<Shader>();
            SortByName(shaders);

            for (int i = 0; i < shaders.Length; i++)
            {
                var s = shaders[i];
                if (!s) continue;

                bool printed = false;
                int pc = GetPropCount(s);
                for (int p = 0; p < pc; p++)
                {
                    string pname = GetPropName(s, p);
                    if (NameLike(pname, "grab") || NameLike(pname, "refract") || NameLike(pname, "scene") || NameLike(pname, "screen"))
                    {
                        if (!printed) { sb.AppendLine("Shader: " + Safe(s.name)); printed = true; }
                        sb.AppendLine("  Prop: " + Safe(pname) + "  Type: " + GetPropType(s, p));
                    }
                }
                if (printed) sb.AppendLine();
            }

            WriteFile("Phoenix_LikelyRefractionProps", sb);
        }

        // ---------------- Internals ----------------

        private static void TryWarmupAllShaders()
        {
            try
            {
                Shader.WarmupAllShaders();
                MelonLogger.Msg("[PhoenixProbe] Shader.WarmupAllShaders() called.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[PhoenixProbe] Warmup failed: " + Safe(ex.Message));
            }
        }

        private static void DumpAllLoadedShadersDetailed()
        {
            var sb = new StringBuilder(1024 * 512);
            Header(sb, "ALL LOADED SHADERS (DETAILED)");

            var shaders = Resources.FindObjectsOfTypeAll<Shader>();
            SortByName(shaders);

            for (int i = 0; i < shaders.Length; i++)
            {
                var s = shaders[i];
                if (!s) continue;

                sb.AppendLine("Shader: " + Safe(s.name));
                sb.AppendLine("  DefaultRenderQueue: " + s.renderQueue);
                int pc = GetPropCount(s);
                sb.AppendLine("  PropertyCount: " + pc);

                for (int p = 0; p < pc; p++)
                {
                    string n = GetPropName(s, p);
                    string t = GetPropType(s, p);
                    string d = GetPropDesc(s, p);
                    string f = GetPropFlags(s, p);
                    string dim = GetPropTexDim(s, p);
                    string def = GetPropTexDefault(s, p);

                    sb.Append("    - ").Append(n).Append(" : ").Append(t);
                    if (!string.IsNullOrEmpty(d)) sb.Append("  // ").Append(d);
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(f)) sb.AppendLine("       Flags: " + f);
                    if (!string.IsNullOrEmpty(dim)) sb.AppendLine("       TexDim: " + dim);
                    if (!string.IsNullOrEmpty(def)) sb.AppendLine("       TexDefault: " + def);
                }
                sb.AppendLine();
            }

            WriteFile("Phoenix_Shaders", sb);
        }

        private static void DumpShadersFromLoadedAssetBundles()
        {
            try
            {
                // Returns Il2CppSystem.Collections.Generic.IEnumerable<AssetBundle>
                var il2cppEnumerable = AssetBundle.GetAllLoadedAssetBundles();
                if (il2cppEnumerable == null)
                {
                    MelonLogger.Msg("[PhoenixProbe] No loaded AssetBundles detected.");
                    return;
                }

                // Materialize into an Il2Cpp List so we can index it safely
                var list = new Il2CppSystem.Collections.Generic.List<AssetBundle>(il2cppEnumerable);

                int idx = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var ab = list[i];
                    if (ab == null) continue;

                    string abName = "(bundle-" + (idx++) + ")";
                    var sb = new System.Text.StringBuilder(1024 * 64);
                    Header(sb, "ASSETBUNDLE SHADERS - " + abName);

                    // Non-generic LoadAllAssets(); returns Il2CppReferenceArray<UnityEngine.Object>
                    UnityEngine.Object[] all = null;
                    try
                    {
                        var il2cppArr = ab.LoadAllAssets(); // Il2CppReferenceArray<UnityEngine.Object>
                        if (il2cppArr != null)
                        {
                            // Copy into a managed array for easy indexing (or just index il2cppArr directly)
                            all = new UnityEngine.Object[il2cppArr.Length];
                            for (int k = 0; k < il2cppArr.Length; k++) all[k] = il2cppArr[k];
                        }
                    }
                    catch { }

                    if (all != null)
                    {
                        // List shader names
                        SortByName(all);
                        for (int j = 0; j < all.Length; j++)
                        {
                            var s = all[j] as Shader;
                            if (s != null) sb.AppendLine("Shader: " + Safe(s.name));
                        }
                    }

                    WriteFile("Phoenix_AB_Shaders_" + Sanitize(abName), sb);
                }

                MelonLogger.Msg("[PhoenixProbe] Finished scanning AssetBundles.");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("[PhoenixProbe] Bundle scan failed: " + Safe(ex.Message));
            }
        }



        private static void DumpMaterialBlock(StringBuilder sb, Material m)
        {
            sb.AppendLine("Material: " + Safe(m.name));
            sb.AppendLine("  Shader: " + Safe(m.shader ? m.shader.name : "(null)"));
            sb.AppendLine("  RenderQueue: " + m.renderQueue);
            sb.AppendLine("  Tag(RenderType): " + Safe(m.GetTag("RenderType", false)));

            AppendInt(sb, m, "_SrcBlend");
            AppendInt(sb, m, "_DstBlend");
            AppendInt(sb, m, "_ZWrite");

            AppendFloat(sb, m, "_Cutoff");
            AppendFloat(sb, m, "_Metallic");
            AppendFloat(sb, m, "_Glossiness");
            AppendFloat(sb, m, "_Smoothness");
            AppendFloat(sb, m, "_Roughness");

            AppendColor(sb, m, "_Color");
            AppendColor(sb, m, "_BaseColor");
            AppendColor(sb, m, "_EmissionColor");

            AppendTex(sb, m, "_MainTex");
            AppendTex(sb, m, "_BaseMap");
            AppendTex(sb, m, "_BumpMap");
            AppendTex(sb, m, "_NormalMap");
            AppendTex(sb, m, "_MetallicGlossMap");
            AppendTex(sb, m, "_MetallicMap");
            AppendTex(sb, m, "_EmissionMap");

            // Common “water-ish” names if present
            AppendTex(sb, m, "_FoamTexture");
            AppendTex(sb, m, "_Noise");
            AppendTex(sb, m, "_Higlight");

            var kws = m.shaderKeywords ?? new string[0];
            sb.Append("  Keywords: ");
            if (kws.Length == 0) sb.AppendLine("(none)");
            else
            {
                for (int i = 0; i < kws.Length; i++)
                {
                    sb.Append(kws[i]);
                    if (i < kws.Length - 1) sb.Append(", ");
                }
                sb.AppendLine();
            }
        }

        // -------------- Safe wrappers for Unity 2019 shader reflection --------------

        private static int GetPropCount(Shader s) { try { return s.GetPropertyCount(); } catch { return 0; } }
        private static string GetPropName(Shader s, int i) { try { return s.GetPropertyName(i); } catch { return ""; } }
        private static string GetPropType(Shader s, int i) { try { return s.GetPropertyType(i).ToString(); } catch { return ""; } }
        private static string GetPropDesc(Shader s, int i) { try { return s.GetPropertyDescription(i); } catch { return ""; } }
        private static string GetPropFlags(Shader s, int i) { try { return s.GetPropertyFlags(i).ToString(); } catch { return ""; } }
        private static string GetPropTexDim(Shader s, int i) { try { return s.GetPropertyTextureDimension(i).ToString(); } catch { return ""; } }
        private static string GetPropTexDefault(Shader s, int i) { try { return s.GetPropertyTextureDefaultName(i); } catch { return ""; } }

        // -------------- Small utilities --------------

        private static void SortByName(UnityEngine.Object[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
            {
                var key = arr[i]; string kn = key ? key.name : "";
                int j = i - 1;
                while (j >= 0)
                {
                    var aj = arr[j]; string an = aj ? aj.name : "";
                    if (string.Compare(an, kn, StringComparison.OrdinalIgnoreCase) <= 0) break;
                    arr[j + 1] = arr[j]; j--;
                }
                arr[j + 1] = key;
            }
        }
        private static void ArraySortByName(Renderer[] arr)
        {
            for (int i = 1; i < arr.Length; i++)
            {
                var key = arr[i]; string kn = key ? key.name : "";
                int j = i - 1;
                while (j >= 0)
                {
                    var aj = arr[j]; string an = aj ? aj.name : "";
                    if (string.Compare(an, kn, StringComparison.OrdinalIgnoreCase) <= 0) break;
                    arr[j + 1] = arr[j]; j--;
                }
                arr[j + 1] = key;
            }
        }

        private static void AppendInt(StringBuilder sb, Material m, string prop)
        {
            if (m.HasProperty(prop)) sb.AppendLine("  " + prop + ": " + m.GetInt(prop));
        }
        private static void AppendFloat(StringBuilder sb, Material m, string prop)
        {
            if (m.HasProperty(prop)) sb.AppendLine("  " + prop + ": " + m.GetFloat(prop).ToString("0.###"));
        }
        private static void AppendColor(StringBuilder sb, Material m, string prop)
        {
            if (m.HasProperty(prop))
            {
                var c = m.GetColor(prop);
                sb.AppendLine($"  {prop}: ({c.r:0.###}, {c.g:0.###}, {c.b:0.###}, {c.a:0.###})");
            }
        }
        private static void AppendTex(StringBuilder sb, Material m, string prop)
        {
            if (m.HasProperty(prop))
            {
                var t = m.GetTexture(prop);
                if (t) sb.AppendLine("  " + prop + ": " + Safe(t.name));
            }
        }

        private static string Safe(string s) => s ?? "(null)";
        private static bool StartsWith(string s, string prefix)
        {
            if (s == null) return false;
            return s.Length >= prefix.Length && string.Compare(s, 0, prefix, 0, prefix.Length, StringComparison.Ordinal) == 0;
        }
        private static bool NameLike(string s, string sub)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static void Header(StringBuilder sb, string title)
        {
            sb.AppendLine("==== " + title + " ====");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Unity: " + Application.unityVersion + "   Platform: " + Application.platform);
            sb.AppendLine();
        }
        private static void WriteFile(string baseName, StringBuilder sb)
        {
            string dir = Application.persistentDataPath;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(dir, baseName + "_" + stamp + ".txt");
            try { File.WriteAllText(path, sb.ToString()); MelonLogger.Msg("[PhoenixProbe] Wrote: " + path); }
            catch (Exception ex) { MelonLogger.Warning("[PhoenixProbe] Write failed: " + Safe(ex.Message)); }
        }
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "bundle";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}
