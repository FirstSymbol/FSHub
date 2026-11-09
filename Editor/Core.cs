using System;
using System.Collections.Generic;

namespace FirstSymbol.Hub
{
    /// <summary>A semantic version such as 1.0.0 or 1.0.0-rc.3, ordered as semver says: a release is newer than its own pre-releases.</summary>
    internal readonly struct SemVer : IComparable<SemVer>, IEquatable<SemVer>
    {
        public readonly int Major, Minor, Patch;
        readonly string m_Pre;
        public readonly bool Valid;
        public string Pre => m_Pre ?? "";

        SemVer(int major, int minor, int patch, string pre)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            m_Pre = pre ?? "";
            Valid = true;
        }

        public bool IsPreRelease => Pre.Length > 0;

        public static SemVer Parse(string text)
        {
            TryParse(text, out SemVer v);
            return v;
        }

        public static bool TryParse(string text, out SemVer version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);
            int plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus);
            string pre = "";
            int dash = s.IndexOf('-');
            if (dash >= 0)
            {
                pre = s.Substring(dash + 1);
                s = s.Substring(0, dash);
            }
            string[] parts = s.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;
            var numbers = new int[3];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0) return false;
            version = new SemVer(numbers[0], numbers[1], numbers[2], pre);
            return true;
        }

        public int CompareTo(SemVer other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
            if (Pre.Length == 0 || other.Pre.Length == 0)
                return Pre.Length == other.Pre.Length ? 0 : Pre.Length == 0 ? 1 : -1;
            string[] a = Pre.Split('.'), b = other.Pre.Split('.');
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                bool an = int.TryParse(a[i], out int x), bn = int.TryParse(b[i], out int y);
                int c = an && bn ? x.CompareTo(y) : an ? -1 : bn ? 1 : string.CompareOrdinal(a[i], b[i]);
                if (c != 0) return c;
            }
            return a.Length.CompareTo(b.Length);
        }

        public bool Equals(SemVer other) => Valid == other.Valid && CompareTo(other) == 0;
        public override bool Equals(object obj) => obj is SemVer other && Equals(other);
        public override int GetHashCode() => (Major * 397 ^ Minor) * 397 ^ Patch;
        public override string ToString() => Valid ? Major + "." + Minor + "." + Patch + (Pre.Length > 0 ? "-" + Pre : "") : "?";
        public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;
        public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;
        public static bool operator <=(SemVer a, SemVer b) => a.CompareTo(b) <= 0;
        public static bool operator >=(SemVer a, SemVer b) => a.CompareTo(b) >= 0;
    }

    /// <summary>Versions from Min (included) up to Max (left out). No Max means no upper end.</summary>
    internal readonly struct VersionRange
    {
        public readonly SemVer Min;
        public readonly SemVer Max;

        public VersionRange(string min, string max = null)
        {
            Min = SemVer.Parse(min);
            Max = SemVer.Parse(max);
        }

        public static VersionRange Any => new VersionRange("0.0.0");

        public bool Contains(SemVer v) => v.Valid && (!Min.Valid || v >= Min) && (!Max.Valid || v < Max);
    }

    /// <summary>What a package says about itself, taken from the Package Manager. A plain record so the logic can be tested without the editor's package list.</summary>
    internal sealed class PackageRecord
    {
        public string Name, Version, Source, GitHash, ResolvedPath, DocsUrl, ChangelogUrl, RepositoryUrl;
    }

    /// <summary>A product the hub knows by name, so it can describe one that is not installed.</summary>
    internal sealed class CatalogEntry
    {
        public string Package, Display, Description, AccentHex, GitUrl, DocsUrl, ChangelogUrl, LegacySettingsPath;
        public string Id => Package.Substring(Package.LastIndexOf('.') + 1);
        public string RepoUrl => GitUrl.EndsWith(".git") ? GitUrl.Substring(0, GitUrl.Length - 4) : GitUrl;
    }

    internal static class Catalog
    {
        internal static readonly CatalogEntry[] Entries =
        {
            new CatalogEntry
            {
                Package = "com.firstsymbol.lettera", Display = "Lettera", AccentHex = "#9b6dff",
                Description = "A text engine for Unity UI and world space: OpenType shaping, bidirectional text, color emoji, one shared SDF atlas, markup styles, formulas and solid 3D text.",
                GitUrl = "https://github.com/FirstSymbol/lettera-unity.git",
                DocsUrl = "https://github.com/FirstSymbol/lettera-unity/blob/main/Documentation~/Lettera.md",
                ChangelogUrl = "https://github.com/FirstSymbol/lettera-unity/blob/main/CHANGELOG.md",
                LegacySettingsPath = "Project/Lettera",
            },
            new CatalogEntry
            {
                Package = "com.firstsymbol.vectra", Display = "Vectra", AccentHex = "#4c8dff",
                Description = "Vector shapes for uGUI and world space: analytic SDF primitives, paths, boolean operations, gradients, strokes, effects, and SVG import and export.",
                GitUrl = "https://github.com/FirstSymbol/vectra-unity.git",
                DocsUrl = "https://github.com/FirstSymbol/vectra-unity#readme",
                ChangelogUrl = "https://github.com/FirstSymbol/vectra-unity/blob/main/CHANGELOG.md",
                LegacySettingsPath = "Project/Vectra",
            },
        };

        internal const string HubPackage = "com.firstsymbol.hub";
        internal const string Prefix = "com.firstsymbol.";
    }

    /// <summary>One product as the overview shows it.</summary>
    internal sealed class ProductState
    {
        public string Package, Display, Description, AccentHex, GitUrl, DocsUrl, ChangelogUrl, RepoUrl, LegacySettingsPath, SettingsPath;
        public bool Installed;
        public string VersionText = "", Source = "", GitHash = "", ResolvedPath = "";
        public SemVer Version;
        public HubProduct Registration;
        public string LatestTag;

        public string Id => Package.Substring(Package.LastIndexOf('.') + 1);

        public bool UpdateAvailable => Installed && Version.Valid && SemVer.TryParse(LatestTag, out SemVer latest) && latest > Version;
    }

    internal static class Registry
    {
        /// <summary>Joins what the Package Manager lists with what products registered. Catalog products come first, then any other registered product.</summary>
        internal static List<ProductState> Build(IEnumerable<PackageRecord> packages, IEnumerable<HubProduct> registrations, Func<HubProduct, string> folderOf = null)
        {
            var byName = new Dictionary<string, PackageRecord>();
            foreach (PackageRecord p in packages)
                if (p != null && p.Name != null && p.Name.StartsWith(Catalog.Prefix, StringComparison.Ordinal) && p.Name != Catalog.HubPackage)
                    byName[p.Name] = p;
            var regs = new Dictionary<string, HubProduct>();
            foreach (HubProduct r in registrations)
                if (r != null && !string.IsNullOrEmpty(r.PackageName)) regs[r.PackageName] = r;

            var result = new List<ProductState>();
            var seen = new HashSet<string>();
            foreach (CatalogEntry e in Catalog.Entries)
            {
                seen.Add(e.Package);
                result.Add(Make(e.Package, e, byName, regs, folderOf));
            }
            foreach (string name in byName.Keys)
                if (seen.Add(name)) result.Add(Make(name, null, byName, regs, folderOf));
            foreach (string name in regs.Keys)
                if (seen.Add(name)) result.Add(Make(name, null, byName, regs, folderOf));
            return result;
        }

        static ProductState Make(string package, CatalogEntry entry, Dictionary<string, PackageRecord> packages, Dictionary<string, HubProduct> regs, Func<HubProduct, string> folderOf)
        {
            packages.TryGetValue(package, out PackageRecord record);
            regs.TryGetValue(package, out HubProduct reg);
            var s = new ProductState
            {
                Package = package,
                Display = reg?.DisplayName ?? entry?.Display ?? package.Substring(Catalog.Prefix.Length),
                Description = !string.IsNullOrEmpty(reg?.Description) ? reg.Description : entry?.Description ?? "",
                AccentHex = reg?.AccentHex ?? entry?.AccentHex ?? "#8a93a6",
                GitUrl = entry?.GitUrl ?? "",
                DocsUrl = record?.DocsUrl is string d && d.Length > 0 ? d : entry?.DocsUrl ?? "",
                ChangelogUrl = record?.ChangelogUrl is string c && c.Length > 0 ? c : entry?.ChangelogUrl ?? "",
                RepoUrl = entry?.RepoUrl ?? record?.RepositoryUrl ?? "",
                LegacySettingsPath = entry?.LegacySettingsPath,
                Registration = reg,
                // A registration is the source of truth: a product kept as a plain folder under Assets is not in the Package Manager's list.
                Installed = record != null || reg != null,
            };
            s.SettingsPath = reg?.SettingsPath ?? "Project/FirstSymbol/" + s.Display;
            if (record != null)
            {
                s.VersionText = record.Version ?? "";
                s.Version = SemVer.Parse(record.Version);
                s.Source = record.Source ?? "";
                s.GitHash = record.GitHash ?? "";
                s.ResolvedPath = record.ResolvedPath ?? "";
            }
            else if (reg != null)
            {
                string folder = folderOf?.Invoke(reg);
                string root = folder != null ? Locations.PackageRoot(folder) : null;
                s.Source = root != null && root.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) ? "Assets folder" : "Local";
                s.ResolvedPath = root != null ? System.IO.Path.GetFullPath(root) : "";
                s.VersionText = root != null ? Locations.ReadVersion(root) : "";
                s.Version = SemVer.Parse(s.VersionText);
            }
            return s;
        }
    }

    /// <summary>A line in the overview: a mismatch between installed products, or a note about one.</summary>
    internal readonly struct HubNote
    {
        public readonly string Id;
        public readonly HubTone Severity;
        public readonly string Message;

        public HubNote(string id, HubTone severity, string message)
        {
            Id = id;
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>The rules for what does not fit together. A rule fires when its product is installed in its range and the other is installed in its range.</summary>
    internal static class Compat
    {
        internal sealed class Rule
        {
            public string Id, Product, Other, Message;
            public VersionRange ProductRange, OtherRange;
            public HubTone Severity;
        }

        internal static readonly Rule[] Rules =
        {
            new Rule
            {
                Id = "vectra-text-needs-lettera-1", Product = "com.firstsymbol.vectra", ProductRange = new VersionRange("0.3.0"),
                Other = "com.firstsymbol.lettera", OtherRange = new VersionRange("0.0.0", "1.0.0"), Severity = HubTone.Warn,
                Message = "Vectra's Lettera text engine needs Lettera 1.0.0 or newer. Update Lettera, or choose another text engine in Vectra's settings.",
            },
        };

        internal static List<HubNote> Evaluate(IReadOnlyList<ProductState> products)
        {
            var notes = new List<HubNote>();
            ProductState Find(string package)
            {
                foreach (ProductState p in products)
                    if (p.Package == package) return p;
                return null;
            }
            foreach (Rule rule in Rules)
            {
                ProductState a = Find(rule.Product), b = Find(rule.Other);
                if (a != null && b != null && a.Installed && b.Installed && rule.ProductRange.Contains(a.Version) && rule.OtherRange.Contains(b.Version))
                    notes.Add(new HubNote(rule.Id, rule.Severity, rule.Message));
            }
            foreach (ProductState p in products)
                if (p.Installed && p.Registration == null && p.LegacySettingsPath != null)
                    notes.Add(new HubNote("no-hub-page-" + p.Id, HubTone.Normal,
                        p.Display + " " + p.VersionText + " has no hub page of its own. Its settings are shown from " + p.LegacySettingsPath + "."));
            return notes;
        }
    }
}
