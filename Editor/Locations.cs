using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FirstSymbol.Hub
{
    /// <summary>Where things are. A product, or the hub, may be a package or a plain folder under Assets, so nothing is found by a fixed path.</summary>
    internal static class Locations
    {
        [Serializable] class Manifest { public string version; }

        /// <summary>The project-relative folder of an assembly definition, or null.</summary>
        internal static string AsmdefFolder(string assemblyName)
        {
            string path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path).Replace('\\', '/');
        }

        /// <summary>The folder of the assembly a registration's type lives in.</summary>
        internal static string FolderOf(HubProduct product) => product == null ? null : AsmdefFolder(product.GetType().Assembly.GetName().Name);

        /// <summary>The nearest folder at or above <paramref name="folder"/> that holds a package.json, or null.</summary>
        internal static string PackageRoot(string folder)
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder);
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath)) return info.resolvedPath;
            string dir = folder.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "package.json"))) return dir;
                int slash = dir.LastIndexOf('/');
                if (slash <= 0) break;
                dir = dir.Substring(0, slash);
            }
            return null;
        }

        /// <summary>The version in the package.json of a package folder, or an empty string.</summary>
        internal static string ReadVersion(string root)
        {
            try
            {
                string file = Path.Combine(root, "package.json");
                return File.Exists(file) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(file))?.version ?? "" : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
