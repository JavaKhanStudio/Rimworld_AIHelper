using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using Verse;

namespace PromptGenerator
{
    [Serializable] public class OAChatResponse { public OAChoice[] choices; }
    [Serializable] public class OAChoice { public OAMessage message; }
    [Serializable] public class OAMessage { public string role; public string content; }

    public static class OpenAIClient
    {
        private const string ChatUrl = "https://api.openai.com/v1/chat/completions";

        public static string CreateChatBody(string userMessage, string model = "gpt-4o-mini", float temperature = 0.7f)
        {
            string safe = (userMessage ?? "")
                .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            return "{"
                 + $"\"model\":\"{model}\","
                 + "\"messages\":[{\"role\":\"user\",\"content\":\"" + safe + "\"}],"
                 + $"\"temperature\":{temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)},"
                 + "\"stream\":false"
                 + "}";
        }

        public static string PostChat(string token, string bodyJson, int timeoutMs = 30000)
        {
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Missing OpenAI token.");

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var req = (HttpWebRequest)WebRequest.Create(ChatUrl);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.UserAgent = "RimWorld-Mod-AI_Utils/1.0";
            req.Headers["Authorization"] = "Bearer " + token;
            req.Timeout = timeoutMs;

            var bytes = Encoding.UTF8.GetBytes(bodyJson);
            using (var rs = req.GetRequestStream())
                rs.Write(bytes, 0, bytes.Length);

            string raw;
            HttpWebResponse resp = null;
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    raw = sr.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                // Read error body to see what happened (401, 400, etc.)
                string err;
                var er = wex.Response?.GetResponseStream();
                if (er != null)
                {
                    using (var sr = new StreamReader(er))
                    {
                        err = sr.ReadToEnd();
                    }
                }
                else
                {
                    err = wex.Message;
                }

                Log.Error($"[AI_Utils] OpenAI HTTP error: {wex.Status} {(wex.Response as HttpWebResponse)?.StatusCode}\n{err}");
                return $"Error: {(wex.Response as HttpWebResponse)?.StatusCode}\n{err}";
            }
            finally
            {
                resp?.Close();
            }

            // Optional: log once to inspect shape
            // Log.Message("[AI_Utils] OpenAI raw: " + raw);

            // First try strict parse
            try
            {
                var parsed = JsonUtility.FromJson<OAChatResponse>(raw);
                if (parsed?.choices != null && parsed.choices.Length > 0)
                {
                    var text = parsed.choices[0].message?.content;
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch { /* fall through to heuristic */ }

            // Heuristic fallback (grab first "content":"...") if schema differs or error shape
            string content = TryExtractContent(raw);
            return string.IsNullOrEmpty(content) ? "(no content)" : content;
        }

        // ultra-light content extractor (not a full JSON parser, but works for typical replies)
        private static string TryExtractContent(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            int i = raw.IndexOf("\"content\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i = raw.IndexOf(':', i);
            if (i < 0) return null;

            // skip spaces/quotes
            while (++i < raw.Length && (raw[i] == ' ' || raw[i] == '\t')) ;
            if (i >= raw.Length || raw[i] != '\"') return null;
            i++;

            var sb = new StringBuilder();
            bool esc = false;
            for (; i < raw.Length; i++)
            {
                char c = raw[i];
                if (esc)
                {
                    // basic unescape
                    if (c == 'n') sb.Append('\n');
                    else if (c == 'r') sb.Append('\r');
                    else if (c == 't') sb.Append('\t');
                    else sb.Append(c);
                    esc = false;
                }
                else
                {
                    if (c == '\\') esc = true;
                    else if (c == '\"') break;
                    else sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
