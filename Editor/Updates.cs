using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FirstSymbol.Hub
{
    /// <summary>What this user chose in the hub. EditorPrefs belong to the user across projects; a test run keeps its own in memory.</summary>
    internal static class HubPrefs
    {
        const string UpdatesKey = "FirstSymbol.Hub.CheckUpdates";
        const string ThemeKey = "FirstSymbol.Hub.Theme";
        const string WelcomeKey = "FirstSymbol.Hub.WelcomeDismissed.";
        static Dictionary<string, object> s_Memory;

        internal static bool InMemory
        {
            get => s_Memory != null;
            set => s_Memory = value ? new Dictionary<string, object>() : null;
        }

        static bool GetBool(string key, bool fallback) => s_Memory != null ? s_Memory.TryGetValue(key, out object v) && v is bool b ? b : fallback : EditorPrefs.GetBool(key, fallback);
        static int GetInt(string key, int fallback) => s_Memory != null ? s_Memory.TryGetValue(key, out object v) && v is int i ? i : fallback : EditorPrefs.GetInt(key, fallback);

        static void Set(string key, object value)
        {
            if (s_Memory != null) s_Memory[key] = value;
            else if (value is bool b) EditorPrefs.SetBool(key, b);
            else EditorPrefs.SetInt(key, (int)value);
        }

        /// <summary>Off until the user turns it on.</summary>
        internal static bool CheckUpdates
        {
            get => GetBool(UpdatesKey, false);
            set => Set(UpdatesKey, value);
        }

        /// <summary>0 follows the editor's skin, 1 dark, 2 light.</summary>
        internal static int Theme
        {
            get => GetInt(ThemeKey, 0);
            set => Set(ThemeKey, value);
        }

        internal static bool IsLight => Theme == 2 || Theme == 0 && !EditorGUIUtility.isProSkin;

        static string ProjectKey()
        {
            // FNV-1a: string.GetHashCode differs between runs.
            uint h = 2166136261;
            foreach (char c in Application.dataPath) h = (h ^ c) * 16777619;
            return WelcomeKey + h.ToString("x8");
        }

        /// <summary>Whether the first-run card was dismissed in this project.</summary>
        internal static bool WelcomeDismissed
        {
            get => GetBool(ProjectKey(), false);
            set => Set(ProjectKey(), value);
        }
    }

    /// <summary>
    /// The only place the hub touches the network. Every request goes through here and is counted, so a test can prove that nothing is sent unless asked for.
    /// </summary>
    internal static class HubNet
    {
        /// <summary>Requests started since the editor loaded this assembly.</summary>
        internal static int RequestCount;

        /// <summary>The URLs asked for, newest last.</summary>
        internal static readonly List<string> Requested = new List<string>();

        /// <summary>Answers a request in place of the network, in tests: the body for a URL, or null for a failure.</summary>
        internal static Func<string, string> Interceptor;

        internal static void Get(string url, Action<string, string> done)
        {
            RequestCount++;
            Requested.Add(url);
            if (Interceptor != null)
            {
                string body = Interceptor(url);
                done(body, body == null ? "intercepted: no answer" : null);
                return;
            }
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "FirstSymbolHub");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.timeout = 15;
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ =>
            {
                string error = request.result == UnityWebRequest.Result.Success ? null : request.error ?? request.result.ToString();
                string text = error == null ? request.downloadHandler.text : null;
                request.Dispose();
                done(text, error);
            };
        }
    }

    /// <summary>Looks for newer tags of the installed products on GitHub, only when the user turned it on or pressed Check Now.</summary>
    internal static class UpdateChecker
    {
        internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        [Serializable] class Tag { public string name; }
        [Serializable] class TagList { public Tag[] items; }
        [Serializable] class Entry { public string package, tag; }
        [Serializable] class Cache { public long checkedTicks; public List<Entry> latest = new List<Entry>(); public List<string> errors = new List<string>(); }

        internal static string CachePath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "FirstSymbolHub", "updates.json");

        /// <summary>Replaces the cache file in tests.</summary>
        internal static string CachePathOverride;

        static string Path_ => CachePathOverride ?? CachePath;
        static Cache s_Cache;
        internal static bool Running { get; private set; }

        static Cache Load()
        {
            if (s_Cache != null) return s_Cache;
            try
            {
                if (File.Exists(Path_)) s_Cache = JsonUtility.FromJson<Cache>(File.ReadAllText(Path_));
            }
            catch (Exception)
            {
                s_Cache = null;
            }
            return s_Cache ?? (s_Cache = new Cache());
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path_));
                File.WriteAllText(Path_, JsonUtility.ToJson(s_Cache));
            }
            catch (Exception)
            {
                // The cache is a convenience; a read-only Library only means asking again.
            }
        }

        internal static void ResetForTests()
        {
            s_Cache = null;
            Running = false;
        }

        internal static DateTime LastChecked => Load().checkedTicks > 0 ? new DateTime(Load().checkedTicks, DateTimeKind.Utc) : DateTime.MinValue;

        internal static IReadOnlyList<string> Errors => Load().errors;

        internal static string Latest(string package)
        {
            foreach (Entry e in Load().latest)
                if (e.package == package) return e.tag;
            return null;
        }

        internal static bool Due(DateTime nowUtc) => HubPrefs.CheckUpdates && nowUtc - LastChecked >= Interval;

        /// <summary>The window calls this when it opens. Sends nothing unless the toggle is on and a day has passed.</summary>
        internal static void CheckIfDue(IReadOnlyList<ProductState> products, Action done)
        {
            if (!Due(DateTime.UtcNow)) return;
            CheckNow(products, done);
        }

        /// <summary>One request per installed product that has a repository. Used by Check Now, which is an explicit act, so it ignores the toggle.</summary>
        internal static void CheckNow(IReadOnlyList<ProductState> products, Action done)
        {
            if (Running) return;
            var targets = new List<ProductState>();
            foreach (ProductState p in products)
                if (p.Installed && !string.IsNullOrEmpty(p.RepoUrl) && TagsUrl(p.RepoUrl) != null) targets.Add(p);
            Cache cache = Load();
            cache.errors.Clear();
            if (targets.Count == 0)
            {
                cache.checkedTicks = DateTime.UtcNow.Ticks;
                Save();
                done?.Invoke();
                return;
            }
            Running = true;
            int pending = targets.Count;
            foreach (ProductState p in targets)
            {
                ProductState product = p;
                HubNet.Get(TagsUrl(product.RepoUrl), (body, error) =>
                {
                    if (error != null) cache.errors.Add(product.Display + ": " + error);
                    else
                    {
                        string tag = PickLatest(body);
                        if (tag == null) cache.errors.Add(product.Display + ": no version tags found");
                        else Set(cache, product.Package, tag);
                    }
                    if (--pending > 0) return;
                    cache.checkedTicks = DateTime.UtcNow.Ticks;
                    Running = false;
                    Save();
                    done?.Invoke();
                });
            }
        }

        static void Set(Cache cache, string package, string tag)
        {
            foreach (Entry e in cache.latest)
                if (e.package == package)
                {
                    e.tag = tag;
                    return;
                }
            cache.latest.Add(new Entry { package = package, tag = tag });
        }

        /// <summary>https://github.com/owner/repo(.git) to the tags endpoint, or null for anything else.</summary>
        internal static string TagsUrl(string repoUrl)
        {
            const string host = "https://github.com/";
            if (repoUrl == null || !repoUrl.StartsWith(host, StringComparison.OrdinalIgnoreCase)) return null;
            string path = repoUrl.Substring(host.Length).TrimEnd('/');
            if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - 4);
            string[] parts = path.Split('/');
            return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 ? "https://api.github.com/repos/" + parts[0] + "/" + parts[1] + "/tags?per_page=30" : null;
        }

        /// <summary>The newest release tag; a pre-release only when there is no release.</summary>
        internal static string PickLatest(string json)
        {
            TagList list;
            try
            {
                list = JsonUtility.FromJson<TagList>("{\"items\":" + json + "}");
            }
            catch (Exception)
            {
                return null;
            }
            string best = null, bestPre = null;
            SemVer bv = default, bpv = default;
            if (list?.items == null) return null;
            foreach (Tag t in list.items)
            {
                if (t == null || !SemVer.TryParse(t.name, out SemVer v)) continue;
                if (v.IsPreRelease)
                {
                    if (bestPre == null || v > bpv) { bestPre = t.name; bpv = v; }
                }
                else if (best == null || v > bv) { best = t.name; bv = v; }
            }
            return best ?? bestPre;
        }
    }
}
