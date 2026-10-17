using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;

namespace GenesisLoader
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/incharilla1/ShibaGT-Genesis-Reborn/main/Core/PluginInfo.cs";
        private const string DownloadUrl = "https://github.com/incharilla1/ShibaGT-Genesis-Reborn/releases/latest/download/ShibaGTGenesisReborn.dll";
        private const string DllName = "ShibaGTGenesisReborn.dll";

        private static string GenesisDir => Path.Combine(Paths.GameRootPath ?? ".", "Genesis");
        private static string VersionFile => Path.Combine(GenesisDir, "version.txt");
        private static string TargetDll => Path.Combine(GenesisDir, DllName);
        private static string LegacyDll => Path.Combine(Paths.PluginPath, DllName);

        private void Awake()
        {
            if (!Directory.Exists(GenesisDir))
                Directory.CreateDirectory(GenesisDir);

            if (File.Exists(LegacyDll))
            {
                try { File.Delete(LegacyDll); } catch { }
            }

            StartCoroutine(checkthenupdate());
        }

        private IEnumerator checkthenupdate()
        {
            string remoteVersion = null;
            using (UnityWebRequest req = UnityWebRequest.Get(VersionUrl))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    Match m = Regex.Match(req.downloadHandler.text, @"Version\s*=\s*""([^""]+)""");
                    if (m.Success)
                        remoteVersion = m.Groups[1].Value.Trim();
                }
            }

            string cachedVersion = File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;
            bool dllExists = File.Exists(TargetDll);

            if (string.IsNullOrEmpty(remoteVersion))
            {
                if (dllExists) loadmenu(TargetDll);
                yield break;
            }

            if (!dllExists || cachedVersion != remoteVersion)
                yield return download(remoteVersion);
            else
                loadmenu(TargetDll);
        }

        private IEnumerator download(string version)
        {
            using UnityWebRequest req = UnityWebRequest.Get(DownloadUrl);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (File.Exists(TargetDll))
                    loadmenu(TargetDll);
                yield break;
            }

            byte[] bytes = req.downloadHandler.data;
            if (bytes == null || bytes.Length == 0)
            {
                if (File.Exists(TargetDll))
                    loadmenu(TargetDll);
                yield break;
            }

            try
            {
                if (File.Exists(TargetDll))
                    File.Delete(TargetDll);

                File.WriteAllBytes(TargetDll, bytes);
                File.WriteAllText(VersionFile, version);
            }
            catch { }

            init(Assembly.Load(bytes));
        }

        private void loadmenu(string path)
        {
            try
            {
                init(Assembly.Load(File.ReadAllBytes(path)));
            }
            catch { }
        }

        private void init(Assembly assembly)
        {
            Type pluginType = assembly?.GetTypes().FirstOrDefault(t => typeof(BaseUnityPlugin).IsAssignableFrom(t) && !t.IsAbstract);
            if (pluginType == null) return;

            GameObject holder = new GameObject(pluginType.Name);
            DontDestroyOnLoad(holder);
            holder.AddComponent(pluginType);
        }
    }
}
