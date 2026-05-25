using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BnlCommunityFixes
{
    /// <summary>
    /// MonoBehaviour that loads custom audio files at startup and provides
    /// playback for the audio replacer system.
    /// </summary>
    public sealed class AudioReplacerManager : MonoBehaviour
    {
        private const string CustomAudioFolder = "CustomAudio";
        private const string CustomPrefix = "__CUSTOM__:";
        private const int PoolSize = 32;

        private static AudioReplacerManager instance;
        private static readonly Dictionary<string, AudioClip> customClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static bool loadStarted;
        private static bool loadComplete;
        private static int loadCount;
        private static int loadTotal;
        private static float masterVolume = 1f;
        private static AudioSource[] pool;
        private static int poolIndex;

        public static void EnsureInstance()
        {
            if (instance != null) return;

            GameObject go = new GameObject("BNL_AudioReplacerManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioReplacerManager>();
        }

        public static void RegisterCustomReplacement(string originalEvent, string fileName)
        {
            string key = CustomPrefix + fileName;
            AudioReplacerRuntime.RegisterReplacement(originalEvent, key);
        }

        public static void SetVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            if (pool != null)
            {
                foreach (var src in pool)
                    src.volume = 1f;
            }
        }

        public static bool TryPlayCustomClip(string mappedName, GameObject target, float eventVolume)
        {
            if (string.IsNullOrEmpty(mappedName) || !mappedName.StartsWith(CustomPrefix))
            {
                return false;
            }

            AudioClip clip;
            if (customClips.TryGetValue(mappedName, out clip) && clip != null && pool != null)
            {
                // Simple round-robin pool — fast, no Time.time, no search loop
                AudioSource source = pool[poolIndex];
                poolIndex = (poolIndex + 1) % PoolSize;

                Vector3 position = (target != null) ? target.transform.position : Vector3.zero;
                source.transform.position = position;
                // PlayOneShot avoids reassigning .clip, which causes main-thread
                // decompression and a frame stutter on every shot.
                source.PlayOneShot(clip, masterVolume * eventVolume);
                return true;
            }

            return false;
        }

        public static bool IsLoadComplete
        {
            get { return loadComplete; }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Create pooled AudioSources — avoids per-shot allocations
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject child = new GameObject("AudioPool_" + i);
                child.transform.SetParent(transform, false);
                AudioSource src = child.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.playOnAwake = false;
                src.volume = 1f;
                pool[i] = src;
            }

            StartCoroutine(LoadCustomAudio());
        }

        private IEnumerator LoadCustomAudio()
        {
            if (loadStarted) yield break;
            loadStarted = true;

            string dataPath = Application.dataPath;
            string customPath = Path.Combine(dataPath, CustomAudioFolder);

            if (!Directory.Exists(customPath))
            {
                Debug.Log("[AudioReplacer] CustomAudio folder not found: " + customPath);
                loadComplete = true;
                yield break;
            }

            string[] files = Directory.GetFiles(customPath, "*.wav", SearchOption.TopDirectoryOnly);
            string[] mp3Files = Directory.GetFiles(customPath, "*.mp3", SearchOption.TopDirectoryOnly);
            string[] oggFiles = Directory.GetFiles(customPath, "*.ogg", SearchOption.TopDirectoryOnly);

            List<string> allFiles = new List<string>();
            allFiles.AddRange(files);
            allFiles.AddRange(mp3Files);
            allFiles.AddRange(oggFiles);

            loadTotal = allFiles.Count;
            if (loadTotal == 0)
            {
                Debug.Log("[AudioReplacer] CustomAudio folder exists but is empty.");
                loadComplete = true;
                yield break;
            }

            Debug.Log("[AudioReplacer] Loading " + loadTotal + " custom audio file(s) from " + customPath + "...");

            foreach (string filePath in allFiles)
            {
                string fileName = Path.GetFileName(filePath);
                string url = "file:///" + filePath.Replace('\\', '/');

                using (WWW www = new WWW(url))
                {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {
                        AudioClip clip = null;

                        // Try all loading modes — Unity 5.1 MP3 support varies
                        clip = www.GetAudioClip(false, true);
                        if (clip == null) clip = www.GetAudioClip(false, false);
                        if (clip == null) clip = www.GetAudioClip(true, false);
                        if (clip == null) clip = www.GetAudioClip(true, true);
                        if (clip == null) clip = www.audioClip;

                        if (clip != null)
                        {
                            clip.name = fileName;
                            string key = CustomPrefix + fileName;
                            customClips[key] = clip;

                            // Also register under just the filename without extension
                            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                            customClips[CustomPrefix + nameNoExt] = clip;

                            loadCount++;
                            Debug.Log("[AudioReplacer] Loaded custom audio: " + fileName + " (" + clip.length.ToString("F1") + "s, " + clip.channels + "ch, " + clip.frequency + "Hz)");
                        }
                        else
                        {
                            Debug.LogWarning("[AudioReplacer] Failed to decode audio: " + fileName);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[AudioReplacer] Failed to load " + fileName + ": " + www.error);
                    }
                }
            }

            loadComplete = true;
            Debug.Log("[AudioReplacer] Custom audio loading complete. " + loadCount + "/" + loadTotal + " files loaded.");
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }

        /// <summary>

    /// <summary>
    /// Runtime audio event logger and replacer.
    /// Intercepts all Wwise AkSoundEngine.PostEvent calls, logs them,
    /// and applies replacements (Wwise event swaps or custom audio files).
    /// </summary>
    public static class AudioReplacerRuntime
    {
        private static bool configured;
        private static bool logAllPostEvents = true;
        private static readonly HashSet<string> ignoredEvents = new HashSet<string>();
        private static readonly Dictionary<string, string> replacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> volumeMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public static void Configure(bool enableLogging)
        {
            configured = true;
            logAllPostEvents = enableLogging;

            AudioReplacerManager.EnsureInstance();

            if (logAllPostEvents)
            {
                Debug.Log("[AudioReplacer] Audio event logging ENABLED.");
            }
            else
            {
                Debug.Log("[AudioReplacer] Audio event logging DISABLED.");
            }
        }

        /// Always returns false (the PostEvent always runs). For custom audio,
        /// plays the custom clip and replaces eventName with "" to silence Wwise.
        /// For Wwise swaps, replaces eventName with the mapped event.
        /// </summary>
        public static bool LogAndResolvePostEvent(ref string eventName, GameObject gameObject)
        {
            string resolved = ResolveReplacement(eventName);

            if (resolved.StartsWith("__CUSTOM__:"))
            {
                if (configured && logAllPostEvents && !ignoredEvents.Contains(eventName))
                {
                    string goName = (gameObject != null) ? gameObject.name : "(null)";
                    Debug.Log("[AudioReplacer] CUSTOM: \"" + eventName + "\" -> file \"" + resolved.Substring(11) + "\" (" + goName + ")");
                }
                float vol;
                if (!volumeMap.TryGetValue(eventName, out vol)) vol = 1f;
                AudioReplacerManager.TryPlayCustomClip(resolved, gameObject, vol);
                suppressNextUint = true; // suppress the paired uint PostEvent call
                return true; // SKIP — don't let Wwise play anything
            }

            if (configured && logAllPostEvents && !ignoredEvents.Contains(eventName))
            {
                string goName = (gameObject != null) ? gameObject.name : "(null)";
                if (resolved != eventName)
                {
                    Debug.Log("[AudioReplacer] REPLACE: \"" + eventName + "\" -> \"" + resolved + "\" (" + goName + ")");
                }
                else
                {
                    Debug.Log("[AudioReplacer] PostEvent(\"" + eventName + "\", " + goName + ")");
                }
            }

            eventName = resolved;
            return false;
        }

        /// </summary>
        public static bool LogAndResolvePostEventWithFlags(ref string eventName, GameObject gameObject, uint flags)
        {
            string resolved = ResolveReplacement(eventName);

            if (resolved.StartsWith("__CUSTOM__:"))
            {
                if (configured && logAllPostEvents && !ignoredEvents.Contains(eventName))
                {
                    string goName = (gameObject != null) ? gameObject.name : "(null)";
                    Debug.Log("[AudioReplacer] CUSTOM: \"" + eventName + "\" -> file \"" + resolved.Substring(11) + "\" (" + goName + ", flags=0x" + flags.ToString("X8") + ")");
                }
                float vol;
                if (!volumeMap.TryGetValue(eventName, out vol)) vol = 1f;
                AudioReplacerManager.TryPlayCustomClip(resolved, gameObject, vol);
                suppressNextUint = true; // suppress the paired uint PostEvent call
                return true; // SKIP
            }

            if (configured && logAllPostEvents && !ignoredEvents.Contains(eventName))
            {
                string goName = (gameObject != null) ? gameObject.name : "(null)";
                if (resolved != eventName)
                {
                    Debug.Log("[AudioReplacer] REPLACE: \"" + eventName + "\" -> \"" + resolved + "\" (" + goName + ")");


// second instance:
                }
                else
                {
                    Debug.Log("[AudioReplacer] PostEvent(\"" + eventName + "\", " + goName + ", flags=0x" + flags.ToString("X8") + ")");
                }
            }

            eventName = resolved;
            return false;
        }

        // Suppression for uint-based PostEvent: when a custom audio replacement
        // fires on the string overload, the uint overload may also be called.
        // This flag suppresses the next uint PostEvent call.
        private static bool suppressNextUint;

        public static void SetSuppressNextUint()
        {
            suppressNextUint = true;
        }

        public static bool ShouldSuppressUint()
        {
            if (suppressNextUint)
            {
                suppressNextUint = false;
                Debug.Log("[AudioReplacer] Suppressing uint PostEvent.");
                return true;
            }
            return false;
        }

        public static void RegisterReplacement(string originalEventName, string replacementEventName)
        {
            replacementMap[originalEventName] = replacementEventName;
        }

        public static void RegisterCustomReplacement(string originalEventName, string fileName)
        {
            AudioReplacerManager.RegisterCustomReplacement(originalEventName, fileName);
        }

        public static void RegisterEventVolume(string eventName, float volume)
        {
            volumeMap[eventName] = volume;
        }

        public static void LogRegisteredReplacements()
        {
            if (replacementMap.Count == 0)
            {
                Debug.Log("[AudioReplacer] No replacements registered.");
                return;
            }

            int wwiseCount = 0;
            int customCount = 0;
            foreach (var kvp in replacementMap)
            {
                if (kvp.Value.StartsWith("__CUSTOM__:")) customCount++;
                else wwiseCount++;
            }

            Debug.Log("[AudioReplacer] === " + replacementMap.Count + " replacement(s) (" + wwiseCount + " Wwise, " + customCount + " custom) ===");
            foreach (var kvp in replacementMap)
            {
                string display = kvp.Value.StartsWith("__CUSTOM__:") ? "file:" + kvp.Value.Substring(11) : kvp.Value;
                Debug.Log("[AudioReplacer]   \"" + kvp.Key + "\" -> \"" + display + "\"");
            }
        }

        public static void ClearReplacements()
        {
            replacementMap.Clear();
            Debug.Log("[AudioReplacer] All replacements cleared.");
        }

        public static void IgnoreEvent(string eventName)
        {
            ignoredEvents.Add(eventName);
        }

        public static string ResolveReplacement(string eventName)
        {
            string replacement;
            if (replacementMap.TryGetValue(eventName, out replacement))
            {
                return replacement;
            }
            return eventName;
        }

        public static int ReplacementCount
        {
            get { return replacementMap.Count; }
        }

        public static bool IsLoggingEnabled
        {
            get { return configured && logAllPostEvents; }
        }
    }
}
