namespace BnlCommunityFixes
{
    public static class MatchReplayRecorderRuntime
    {
        private static bool enabled;
        private static bool capturePayload;
        private static int maxPayloadBytes = 262144;
        private static string sessionId;
        private static string outputPath;
        private static System.IO.StreamWriter writer;
        private static int failureCount;

        public static void Configure(bool isEnabled, int payloadByteLimit, bool shouldCapturePayload)
        {
            enabled = isEnabled;
            maxPayloadBytes = UnityEngine.Mathf.Clamp(payloadByteLimit, 0, 1048576);
            capturePayload = shouldCapturePayload && maxPayloadBytes > 0;

            if (!enabled || !string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                sessionId = System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string baseDir = System.IO.Path.GetDirectoryName(typeof(MatchReplayRecorderRuntime).Assembly.Location);
                if (string.IsNullOrEmpty(baseDir))
                {
                    baseDir = ".";
                }

                string replayDir = System.IO.Path.Combine(System.IO.Path.Combine(baseDir, ".."), "bnl-match-replays");
                replayDir = System.IO.Path.GetFullPath(replayDir);
                System.IO.Directory.CreateDirectory(replayDir);
                outputPath = System.IO.Path.Combine(replayDir, "zone-capture-" + sessionId + ".jsonl");
                writer = new System.IO.StreamWriter(new System.IO.FileStream(outputPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read));
                writer.AutoFlush = true;

                WriteLine("{\"schema\":\"bnl-zone-capture-v0\",\"kind\":\"session_start\",\"utc\":\"" +
                    Escape(System.DateTime.UtcNow.ToString("o")) + "\",\"unityTime\":" +
                    UnityEngine.Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"capturePayload\":" + (capturePayload ? "true" : "false") +
                    ",\"maxPayloadBytes\":" + maxPayloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");

                UnityEngine.Debug.Log("BNL match replay recorder writing to: " + outputPath);
            }
            catch (System.Exception ex)
            {
                enabled = false;
                UnityEngine.Debug.LogException(ex);
            }
        }

        public static void RecordPacket(string eventName, System.IO.BinaryReader reader)
        {
            if (!enabled || string.IsNullOrEmpty(outputPath) || failureCount > 5)
            {
                return;
            }

            try
            {
                long position = -1;
                long length = -1;
                long remaining = -1;
                string payload = string.Empty;
                int payloadBytes = 0;

                System.IO.Stream stream = reader == null ? null : reader.BaseStream;
                if (stream != null && stream.CanSeek)
                {
                    position = stream.Position;
                    length = stream.Length;
                    remaining = length - position;

                    if (capturePayload && remaining > 0)
                    {
                        int bytesToRead = (int)System.Math.Min(System.Math.Min(remaining, maxPayloadBytes), int.MaxValue);
                        byte[] buffer = reader.ReadBytes(bytesToRead);
                        payloadBytes = buffer.Length;
                        payload = System.Convert.ToBase64String(buffer);
                        stream.Position = position;
                    }
                }

                string line = "{\"kind\":\"zone_packet\",\"utc\":\"" +
                    Escape(System.DateTime.UtcNow.ToString("o")) + "\",\"t\":" +
                    UnityEngine.Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"event\":\"" + Escape(eventName) + "\",\"pos\":" +
                    position.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"len\":" +
                    length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"remaining\":" +
                    remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"payloadBytes\":" +
                    payloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(payload))
                {
                    line += ",\"payloadBase64\":\"" + payload + "\"";
                }

                WriteLine(line + "}");
            }
            catch (System.Exception ex)
            {
                failureCount++;
                if (failureCount <= 3)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        private static void WriteLine(string line)
        {
            if (writer != null)
            {
                writer.WriteLine(line);
                return;
            }

            System.IO.File.AppendAllText(outputPath, line + System.Environment.NewLine);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
