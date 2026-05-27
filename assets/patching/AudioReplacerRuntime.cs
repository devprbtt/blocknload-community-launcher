using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private const int PoolSize = 64;

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
                AudioSource source = AcquireSource();
                if (source == null)
                {
                    return false;
                }

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
                src.spatialBlend = 0f;
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
                src.panStereo = 0f;
                src.priority = 0;
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
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                AudioClip clip = null;

                if (extension == ".wav")
                {
                    try
                    {
                        clip = LoadWaveClip(filePath, fileName);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[AudioReplacer] Failed to decode wav " + fileName + ": " + ex.Message);
                    }
                }
                else
                {
                    string url = "file:///" + filePath.Replace('\\', '/');
                    using (WWW www = new WWW(url))
                    {
                        yield return www;

                        if (string.IsNullOrEmpty(www.error))
                        {
                            // Keep compressed formats as a fallback, but prefer wav
                            // for weapon sounds because it avoids decode stalls later.
                            clip = www.GetAudioClip(false, false);
                            if (clip == null) clip = www.GetAudioClip(true, false);
                            if (clip == null) clip = www.GetAudioClip(false, true);
                            if (clip == null) clip = www.GetAudioClip(true, true);
                            if (clip == null) clip = www.audioClip;
                        }
                        else
                        {
                            Debug.LogWarning("[AudioReplacer] Failed to load " + fileName + ": " + www.error);
                        }
                    }
                }

                if (clip != null)
                {
                    clip.name = fileName;
                    RegisterLoadedClip(fileName, clip);
                    loadCount++;
                    Debug.Log("[AudioReplacer] Loaded custom audio: " + fileName + " (" + clip.length.ToString("F1") + "s, " + clip.channels + "ch, " + clip.frequency + "Hz)");
                }
                else
                {
                    Debug.LogWarning("[AudioReplacer] Failed to decode audio: " + fileName);
                }

                yield return null;
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

        private static AudioSource AcquireSource()
        {
            // Prefer an idle source so overlapping rapid-fire shots do not
            // stomp currently playing clips on the shared pool.
            for (int i = 0; i < PoolSize; i++)
            {
                int index = (poolIndex + i) % PoolSize;
                AudioSource source = pool[index];
                if (source != null && !source.isPlaying)
                {
                    poolIndex = (index + 1) % PoolSize;
                    return source;
                }
            }

            // If all sources are busy, fall back to the next source in a
            // stable round-robin order instead of allocating on the hot path.
            AudioSource fallback = pool[poolIndex];
            poolIndex = (poolIndex + 1) % PoolSize;
            return fallback;
        }

        private static void RegisterLoadedClip(string fileName, AudioClip clip)
        {
            string key = CustomPrefix + fileName;
            customClips[key] = clip;

            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            customClips[CustomPrefix + nameNoExt] = clip;
        }

        private static AudioClip LoadWaveClip(string filePath, string clipName)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            WaveData wave = ParseWave(bytes);
            AudioClip clip = AudioClip.Create(clipName, wave.Samples.Length / wave.Channels, wave.Channels, wave.SampleRate, false);
            clip.SetData(wave.Samples, 0);
            return clip;
        }

        private static WaveData ParseWave(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44)
            {
                throw new InvalidDataException("WAV file is too small.");
            }

            if (ReadAscii(bytes, 0, 4) != "RIFF" || ReadAscii(bytes, 8, 4) != "WAVE")
            {
                throw new InvalidDataException("Not a RIFF/WAVE file.");
            }

            int offset = 12;
            ushort formatTag = 0;
            ushort channels = 0;
            int sampleRate = 0;
            ushort bitsPerSample = 0;
            byte[] sampleBytes = null;

            while (offset + 8 <= bytes.Length)
            {
                string chunkId = ReadAscii(bytes, offset, 4);
                int chunkSize = BitConverter.ToInt32(bytes, offset + 4);
                int chunkDataOffset = offset + 8;
                if (chunkSize < 0 || chunkDataOffset + chunkSize > bytes.Length)
                {
                    throw new InvalidDataException("Invalid WAV chunk size.");
                }

                if (chunkId == "fmt ")
                {
                    formatTag = BitConverter.ToUInt16(bytes, chunkDataOffset);
                    channels = BitConverter.ToUInt16(bytes, chunkDataOffset + 2);
                    sampleRate = BitConverter.ToInt32(bytes, chunkDataOffset + 4);
                    bitsPerSample = BitConverter.ToUInt16(bytes, chunkDataOffset + 14);
                }
                else if (chunkId == "data")
                {
                    sampleBytes = new byte[chunkSize];
                    Buffer.BlockCopy(bytes, chunkDataOffset, sampleBytes, 0, chunkSize);
                }

                offset = chunkDataOffset + chunkSize + (chunkSize & 1);
            }

            if (channels == 0 || sampleRate <= 0 || bitsPerSample == 0 || sampleBytes == null)
            {
                throw new InvalidDataException("Incomplete WAV data.");
            }

            return new WaveData
            {
                Channels = channels,
                SampleRate = sampleRate,
                Samples = DecodeWaveSamples(sampleBytes, formatTag, bitsPerSample)
            };
        }

        private static float[] DecodeWaveSamples(byte[] data, ushort formatTag, ushort bitsPerSample)
        {
            if (bitsPerSample == 0)
            {
                throw new InvalidDataException("Invalid bits-per-sample value.");
            }

            int bytesPerSample = bitsPerSample / 8;
            if (bytesPerSample <= 0 || data.Length % bytesPerSample != 0)
            {
                throw new InvalidDataException("Unsupported WAV sample layout.");
            }

            int sampleCount = data.Length / bytesPerSample;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                int index = i * bytesPerSample;
                if (formatTag == 3 && bitsPerSample == 32)
                {
                    samples[i] = Mathf.Clamp(BitConverter.ToSingle(data, index), -1f, 1f);
                    continue;
                }

                switch (bitsPerSample)
                {
                    case 8:
                        samples[i] = (data[index] - 128) / 128f;
                        break;
                    case 16:
                        samples[i] = BitConverter.ToInt16(data, index) / 32768f;
                        break;
                    case 24:
                        int sample24 = data[index] | (data[index + 1] << 8) | (data[index + 2] << 16);
                        if ((sample24 & 0x800000) != 0)
                        {
                            sample24 |= unchecked((int)0xFF000000);
                        }
                        samples[i] = sample24 / 8388608f;
                        break;
                    case 32:
                        samples[i] = BitConverter.ToInt32(data, index) / 2147483648f;
                        break;
                    default:
                        throw new InvalidDataException("Unsupported WAV bit depth: " + bitsPerSample);
                }
            }

            return samples;
        }

        private static string ReadAscii(byte[] bytes, int offset, int count)
        {
            return Encoding.ASCII.GetString(bytes, offset, count);
        }

        private sealed class WaveData
        {
            public int Channels;
            public int SampleRate;
            public float[] Samples;
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
        private static bool bootstrapApplied;
        private static bool logAllPostEvents = true;
        private static readonly HashSet<string> ignoredEvents = new HashSet<string>();
        private static readonly Dictionary<string, string> replacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> volumeMap = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, SuppressionState> pendingUintSuppressions = new Dictionary<int, SuppressionState>();
        private static readonly Dictionary<string, int> recentCustomEvents = new Dictionary<string, int>(StringComparer.Ordinal);

        public static void Configure(bool enableLogging)
        {
            configured = true;
            logAllPostEvents = enableLogging;
            bootstrapApplied = true;
            replacementMap.Clear();
            volumeMap.Clear();
            ignoredEvents.Clear();
            pendingUintSuppressions.Clear();
            recentCustomEvents.Clear();

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

        public static bool BeginBootstrap(bool enableLogging)
        {
            if (bootstrapApplied)
            {
                return false;
            }

            Configure(enableLogging);
            return true;
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
                if (ShouldPlayCustomEventThisFrame(eventName, gameObject))
                {
                    AudioReplacerManager.TryPlayCustomClip(resolved, gameObject, vol);
                }
                RegisterUintSuppression(gameObject);
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
                if (ShouldPlayCustomEventThisFrame(eventName, gameObject))
                {
                    AudioReplacerManager.TryPlayCustomClip(resolved, gameObject, vol);
                }
                RegisterUintSuppression(gameObject);
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

        public static bool ShouldSuppressUint(GameObject gameObject)
        {
            int emitterId = (gameObject != null) ? gameObject.GetInstanceID() : 0;
            SuppressionState state;
            if (pendingUintSuppressions.TryGetValue(emitterId, out state))
            {
                if (state.Frame >= Time.frameCount - 1 && state.RemainingCount > 0)
                {
                    state.RemainingCount--;
                    if (state.RemainingCount <= 0)
                    {
                        pendingUintSuppressions.Remove(emitterId);
                    }
                    else
                    {
                        pendingUintSuppressions[emitterId] = state;
                    }

                    Debug.Log("[AudioReplacer] Suppressing uint PostEvent for emitter " + emitterId + ".");
                    return true;
                }

                pendingUintSuppressions.Remove(emitterId);
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

        private static void RegisterUintSuppression(GameObject gameObject)
        {
            int emitterId = (gameObject != null) ? gameObject.GetInstanceID() : 0;
            SuppressionState state;
            if (pendingUintSuppressions.TryGetValue(emitterId, out state) && state.Frame >= Time.frameCount - 1)
            {
                state.RemainingCount++;
                state.Frame = Time.frameCount;
            }
            else
            {
                state = new SuppressionState
                {
                    Frame = Time.frameCount,
                    RemainingCount = 1
                };
            }

            pendingUintSuppressions[emitterId] = state;
        }

        private static bool ShouldPlayCustomEventThisFrame(string eventName, GameObject gameObject)
        {
            int emitterId = (gameObject != null) ? gameObject.GetInstanceID() : 0;
            string key = emitterId.ToString() + "|" + eventName;
            int frame;
            if (recentCustomEvents.TryGetValue(key, out frame) && frame == Time.frameCount)
            {
                return false;
            }

            recentCustomEvents[key] = Time.frameCount;
            return true;
        }

        private struct SuppressionState
        {
            public int Frame;
            public int RemainingCount;
        }
    }
}
