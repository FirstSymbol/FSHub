using System;
using System.Collections.Generic;
using FirstSymbol.Hub.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FirstSymbol.Hub
{
    /// <summary>The pages of the hub window.</summary>
    internal static class Pages
    {
        static string HubVersion()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Pages).Assembly);
            return info != null ? info.version : "";
        }

        static string SourceWord(string source)
        {
            switch (source)
            {
                case "Git": return "git";
                case "Local": return "local folder";
                case "Embedded": return "embedded";
                case "Registry": return "registry";
                case "LocalTarball": return "tarball";
                default: return source.ToLowerInvariant();
            }
        }

        static void Chips(VisualElement chips, ProductState p)
        {
            chips.Add(HubUI.Chip("v" + p.VersionText));
            if (p.Source.Length > 0) chips.Add(HubUI.Chip(SourceWord(p.Source)));
            if (p.GitHash.Length >= 7) chips.Add(HubUI.Chip(p.GitHash.Substring(0, 7)));
            if (p.UpdateAvailable) chips.Add(HubUI.Chip("update " + p.LatestTag, HubTone.Good));
        }

        static VisualElement Links(ProductState p)
        {
            VisualElement links = HubUI.El("fs-links");
            if (p.DocsUrl.Length > 0) links.Add(HubUI.Button("Docs", p.DocsUrl, () => HubActions.OpenUrl(p.DocsUrl)));
            if (p.ChangelogUrl.Length > 0) links.Add(HubUI.Button("Changelog", p.ChangelogUrl, () => HubActions.OpenUrl(p.ChangelogUrl)));
            if (p.RepoUrl.Length > 0) links.Add(HubUI.Button("Repository", p.RepoUrl, () => HubActions.OpenUrl(p.RepoUrl)));
            string local = HubWindow.LocalDocs(p);
            if (local != null) links.Add(HubUI.Button("Local docs", local, () => EditorUtility.RevealInFinder(local)));
            return links;
        }

        static void UpdateButton(VisualElement into, ProductState p)
        {
            if (!p.UpdateAvailable || p.GitUrl.Length == 0) return;
            string id = p.GitUrl + "#" + p.LatestTag;
            into.Add(HubUI.Button("Update to " + p.LatestTag, "Asks first, then the Package Manager changes the package to " + id, () =>
            {
                if (HubActions.Confirm("Update " + p.Display, p.Display + " " + p.VersionText + " becomes " + p.LatestTag + ". The Package Manager resolves it and scripts recompile.")) HubActions.AddPackage(id);
            }, true, HubUI.Hex(p.AccentHex)));
        }

        internal static void BuildOverview(HubWindow window, VisualElement page)
        {
            page.Add(HubUI.Header("FirstSymbol Hub", "Your FirstSymbol packages in this project", HubUI.Hex("#8a93a6"), "FS", out VisualElement chips));
            string hubVersion = HubVersion();
            if (hubVersion.Length > 0) chips.Add(HubUI.Chip("hub v" + hubVersion));

            if (!HubPrefs.WelcomeDismissed)
            {
                VisualElement welcome = HubUI.Card("Welcome",
                    HubUI.Text("This is where FirstSymbol packages meet: versions, what does not fit together, and each product's settings. Nothing here uses the network unless you turn on update checks or press a button that says so.", "fs-text"),
                    HubUI.El("fs-links", HubUI.Button("Got it", "Hide this card for this project", window.Dismiss, true)));
                welcome.AddToClassList("fs-card--note");
                page.Add(welcome);
            }

            List<HubNote> notes = Compat.Evaluate(window.Products);
            if (notes.Count > 0)
            {
                bool warn = notes.Exists(n => n.Severity != HubTone.Normal);
                VisualElement card = HubUI.Card("Compatibility");
                if (warn) card.AddToClassList("fs-card--warn");
                foreach (HubNote n in notes) card.Add(HubUI.Note(n.Severity, n.Message));
                page.Add(card);
            }

            bool installed = false;
            foreach (ProductState p in window.Products)
            {
                if (!p.Installed) continue;
                if (!installed) page.Add(HubUI.Text("INSTALLED", "fs-section"));
                installed = true;
                ProductState product = p;
                VisualElement card = HubUI.Card(null);
                card.AddToClassList("fs-card--accent");
                card.style.borderLeftColor = HubUI.Hex(p.AccentHex);
                VisualElement header = HubUI.Header(p.Display, p.Description, HubUI.Hex(p.AccentHex), p.Display.Substring(0, 1), out VisualElement pc);
                header.AddToClassList("fs-header--bare");
                Chips(pc, p);
                card.Add(header);
                VisualElement links = Links(p);
                links.Insert(0, HubUI.Button("Open page", "Settings and status of " + p.Display, () => window.Select(product.Id), true, HubUI.Hex(p.AccentHex)));
                UpdateButton(links, p);
                card.Add(links);
                page.Add(card);
            }
            if (!installed) page.Add(HubUI.Card(null, HubUI.Text("No FirstSymbol product is installed in this project yet.", "fs-text")));

            bool header2 = false;
            foreach (ProductState p in window.Products)
            {
                if (p.Installed || p.GitUrl.Length == 0) continue;
                if (!header2) page.Add(HubUI.Text("NOT INSTALLED", "fs-section"));
                header2 = true;
                ProductState product = p;
                VisualElement card = HubUI.Card(null);
                card.AddToClassList("fs-card--accent");
                card.style.borderLeftColor = HubUI.Hex(p.AccentHex);
                card.Add(HubUI.Header(p.Display, p.Description, HubUI.Hex(p.AccentHex), p.Display.Substring(0, 1), out _));
                card[0].AddToClassList("fs-header--bare");
                card.Add(HubUI.Text("Package Manager > + > Add package from git URL:", "fs-hint"));
                card.Add(HubUI.Text(p.GitUrl, "fs-url"));
                card.Add(HubUI.El("fs-links",
                    HubUI.Button("Copy URL", "Copies the address; nothing is sent", () => GUIUtility.systemCopyBuffer = product.GitUrl),
                    HubUI.Button("Install", "Asks first, then the Package Manager downloads it from GitHub", () =>
                    {
                        if (HubActions.Confirm("Install " + product.Display, "The Package Manager downloads " + product.GitUrl + " from GitHub.")) HubActions.AddPackage(product.GitUrl);
                    }, true, HubUI.Hex(p.AccentHex)),
                    HubUI.Button("Repository", product.RepoUrl, () => HubActions.OpenUrl(product.RepoUrl))));
                page.Add(card);
            }
        }

        internal static void BuildHubSettings(HubWindow window, VisualElement page)
        {
            page.Add(HubUI.Header("Hub settings", "Your choices, kept for you on this machine", HubUI.Hex("#8a93a6"), "FS", out VisualElement chips));
            string hubVersion = HubVersion();
            if (hubVersion.Length > 0) chips.Add(HubUI.Chip("hub v" + hubVersion));
            chips.Add(HubUI.Chip("Unity " + Application.unityVersion));

            page.Add(HubUI.Card("Look",
                HubUI.Row("Theme", HubUI.Segmented(new[] { "Auto", "Dark", "Light" }, () => HubPrefs.Theme, i =>
                {
                    HubPrefs.Theme = i;
                    window.ApplyTheme();
                }), "Auto follows the editor's skin"),
                HubUI.Text("The hub and the product pages in it follow this choice. Auto follows the editor.", "fs-hint")));

            Label last = HubUI.Text("", "fs-row__value");
            VisualElement results = HubUI.El("");
            void Show()
            {
                DateTime t = UpdateChecker.LastChecked;
                last.text = UpdateChecker.Running ? "checking..." : t == DateTime.MinValue ? "never" : t.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                results.Clear();
                foreach (ProductState p in window.Products)
                    if (p.Installed && p.LatestTag != null)
                        results.Add(HubUI.Row(p.Display, "latest tag " + p.LatestTag + (p.UpdateAvailable ? ", newer than v" + p.VersionText : ", you have v" + p.VersionText), p.UpdateAvailable ? HubTone.Good : HubTone.Normal));
                foreach (string error in UpdateChecker.Errors) results.Add(HubUI.Note(HubTone.Warn, error));
            }
            Show();
            page.Add(HubUI.Card("Updates",
                HubUI.Row("Check GitHub for new tags once a day", HubUI.Switch(() => HubPrefs.CheckUpdates, on => HubPrefs.CheckUpdates = on), "Off by default. When on, the hub asks GitHub for the tags of each installed product when it opens, at most once a day."),
                HubUI.Text("One request per installed product to the public tags list on GitHub, no sign-in, nothing about your project. Off, the hub sends nothing at all.", "fs-hint"),
                HubUI.Row("Last check", last),
                results,
                HubUI.El("fs-links", HubUI.Button("Check now", "Asks GitHub once for each installed product", () =>
                {
                    window.CheckNow();
                }))));

            page.Add(HubUI.Card("Welcome card",
                HubUI.Text("The first-run card on the overview is hidden per project once dismissed.", "fs-hint"),
                HubUI.El("fs-links", HubUI.Button("Show it again", "Shows the card on the overview", () =>
                {
                    HubPrefs.WelcomeDismissed = false;
                    window.Select(HubWindow.OverviewId, true);
                }))));
        }

        internal static void BuildProduct(HubWindow window, VisualElement page, ProductState p)
        {
            Color accent = HubUI.Hex(p.AccentHex);
            page.Add(HubUI.Header(p.Display, p.Description, accent, p.Display.Substring(0, 1), out VisualElement chips));
            Chips(chips, p);

            VisualElement status = HubUI.Card("Status");
            status.AddToClassList("fs-card--accent");
            status.style.borderLeftColor = accent;
            bool rows = false;
            if (p.Registration != null)
            {
                try
                {
                    foreach (HubStatusRow row in p.Registration.StatusRows())
                    {
                        status.Add(HubUI.Row(row.Label, row.Value, row.Tone));
                        rows = true;
                    }
                }
                catch (Exception e)
                {
                    status.Add(HubUI.Note(HubTone.Warn, p.Display + " could not report its status: " + e.Message));
                }
            }
            if (!rows) status.Add(HubUI.Row("Version", p.VersionText));
            VisualElement links = Links(p);
            UpdateButton(links, p);
            status.Add(links);
            page.Add(status);

            VisualElement settings = HubUI.Card("Settings");
            page.Add(settings);
            string path = window.SettingsPathOf(p);
            if (path == null)
            {
                settings.Add(HubUI.Text(p.Display + " has no settings page in Project Settings.", "fs-hint"));
                return;
            }
            settings.Add(HubUI.El("fs-links", HubUI.Button("Open in Project Settings", path, () => SettingsService.OpenProjectSettings(path))));
            window.HostedPage(p, settings);
        }
    }
}
