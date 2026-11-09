using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace FirstSymbol.Hub
{
    /// <summary>
    /// Lets the products' hub pages see the hub when the hub is a folder under Assets. A package announces itself through versionDefines; a folder cannot, so this adds
    /// <c>FIRSTSYMBOL_HUB</c> to the project's scripting define symbols and takes it away again when the folder goes, before anything is compiled against the missing
    /// assembly. It removes only what it added, and never a symbol the user set.
    /// </summary>
    internal static class AutoDefine
    {
        internal const string Define = "FIRSTSYMBOL_HUB";
        const string HubAssembly = "FirstSymbol.Hub.Editor";
        const string HubFile = "FirstSymbol.Hub.Editor.asmdef";
        internal const string TrackingFile = "ProjectSettings/FirstSymbolAutoDefines.txt";

        internal enum Step { None, Add, Remove, Forget }

        static readonly NamedBuildTarget[] s_Targets =
        {
            NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.WebGL,
            NamedBuildTarget.tvOS, NamedBuildTarget.VisionOS, NamedBuildTarget.WindowsStoreApps,
        };

        /// <summary>What to do for one target. A symbol that is there and that this tool did not add belongs to the user: it is left as it is, even when no longer wanted.</summary>
        internal static Step Plan(bool wanted, bool present, bool tracked)
        {
            if (wanted) return present ? Step.None : Step.Add;
            if (!tracked) return Step.None;
            return present ? Step.Remove : Step.Forget;
        }

        /// <summary>Wanted when the hub is a folder under Assets. A package sets the symbol itself.</summary>
        internal static bool Wanted(string asmdefPath) => !string.IsNullOrEmpty(asmdefPath) && asmdefPath.StartsWith("Assets/", StringComparison.Ordinal);

        internal static HashSet<string> ReadTracking(string file = TrackingFile)
        {
            var set = new HashSet<string>();
            if (File.Exists(file))
                foreach (string line in File.ReadAllLines(file))
                    if (line.Trim().Length > 0) set.Add(line.Trim());
            return set;
        }

        /// <summary>Applies the plan to every target, one write per target that changes. Returns the number of targets changed.</summary>
        internal static int Sync(bool wanted)
        {
            HashSet<string> tracked = ReadTracking();
            int changed = 0;
            bool trackingChanged = false;
            foreach (NamedBuildTarget target in s_Targets)
            {
                string key = Define + "@" + target.TargetName;
                try
                {
                    List<string> symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    switch (Plan(wanted, symbols.Contains(Define), tracked.Contains(key)))
                    {
                        case Step.Add:
                            symbols.Add(Define);
                            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
                            trackingChanged |= tracked.Add(key);
                            changed++;
                            break;
                        case Step.Remove:
                            symbols.Remove(Define);
                            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
                            trackingChanged |= tracked.Remove(key);
                            changed++;
                            break;
                        case Step.Forget:
                            trackingChanged |= tracked.Remove(key);
                            break;
                    }
                }
                catch (Exception)
                {
                    // A platform the editor has no module for: nothing to define there.
                }
            }
            if (trackingChanged) File.WriteAllLines(TrackingFile, tracked.OrderBy(k => k, StringComparer.Ordinal));
            if (changed > 0)
                Debug.Log("FirstSymbol Hub: " + (wanted ? "added " : "removed ") + Define + (wanted ? " to" : " from") + " the scripting define symbols (" + changed + " build targets). "
                          + (wanted ? "The hub is a folder under Assets, so products could not be told by the Package Manager." : "The hub is gone."));
            return changed;
        }

        static bool IsHub(string path) => path != null && path.EndsWith("/" + HubFile, StringComparison.Ordinal);

        static string FindHub(string[] imported, string[] gone)
        {
            string path = imported?.FirstOrDefault(IsHub) ?? CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(HubAssembly);
            return path == null || gone != null && gone.Contains(path) ? null : path;
        }

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            // Now, and again when the editor is idle. delayCall alone is not enough: an editor nobody has focused may not run it for a long time.
            Run();
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/FirstSymbol/Detect Hub Folder", false, 1)]
        static void DetectNow()
        {
            string path = FindHub(null, null);
            bool wanted = Wanted(path);
            int changed = Sync(wanted);
            Debug.Log("FirstSymbol Hub: assembly definition " + (path ?? "not found") + (wanted ? ", needs " + Define + "." : ", " + Define + " not needed from here (a package sets it itself).")
                      + (changed == 0 ? " Nothing to change." : ""));
        }

        static void Run()
        {
            try
            {
                Sync(Wanted(FindHub(null, null)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("FirstSymbol Hub: could not update the scripting define symbols: " + e.Message);
            }
        }

        sealed class Watcher : AssetPostprocessor
        {
            // Runs in the domain that still has the old scripts, before they are compiled again: the symbol goes before the pages it gates are built against a missing hub.
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (!imported.Any(IsHub) && !deleted.Any(IsHub) && !movedFrom.Any(IsHub)) return;
                string[] gone = deleted.Concat(movedFrom).ToArray();
                Sync(Wanted(FindHub(imported.Concat(moved).ToArray(), gone)));
            }
        }
    }
}
