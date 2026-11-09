using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace FirstSymbol.Hub.Tests
{
    sealed class FakeProduct : HubProduct
    {
        readonly string m_Package, m_Name;
        public FakeProduct(string package, string name)
        {
            m_Package = package;
            m_Name = name;
        }
        public override string PackageName => m_Package;
        public override string DisplayName => m_Name;
        public override string Description => "A fake product.";
        public override IEnumerable<HubStatusRow> StatusRows()
        {
            yield return new HubStatusRow("Engine", "fake", HubTone.Good);
            yield return new HubStatusRow("Atlas", "2048");
        }
    }

    public class HubTests
    {
        string m_Cache;

        [SetUp]
        public void SetUp()
        {
            HubPrefs.InMemory = true;
            m_Cache = Path.Combine(Path.GetTempPath(), "fshub-test-" + Guid.NewGuid().ToString("N") + ".json");
            UpdateChecker.CachePathOverride = m_Cache;
            UpdateChecker.ResetForTests();
            HubNet.Interceptor = url => "[{\"name\":\"1.0.0\"},{\"name\":\"1.2.0\"},{\"name\":\"1.3.0-rc.1\"}]";
            HubNet.RequestCount = 0;
            HubNet.Requested.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RemoveFolder();
            HubPrefs.InMemory = false;
            HubNet.Interceptor = null;
            HubWindow.PackageSource = null;
            HubWindow.ProductSource = null;
            UpdateChecker.CachePathOverride = null;
            UpdateChecker.ResetForTests();
            if (File.Exists(m_Cache)) File.Delete(m_Cache);
            foreach (HubWindow w in Resources.FindObjectsOfTypeAll<HubWindow>()) w.Close();
        }

        static PackageRecord Pkg(string name, string version, string source = "Git") => new PackageRecord { Name = name, Version = version, Source = source, GitHash = "0123456789abcdef", ResolvedPath = "" };

        static void Fake(bool lettera, string letteraVersion, bool vectra, string vectraVersion, bool register = true)
        {
            var packages = new List<PackageRecord>();
            var products = new List<HubProduct>();
            if (lettera)
            {
                packages.Add(Pkg("com.firstsymbol.lettera", letteraVersion));
                if (register) products.Add(new FakeProduct("com.firstsymbol.lettera", "Lettera"));
            }
            if (vectra)
            {
                packages.Add(Pkg("com.firstsymbol.vectra", vectraVersion));
                if (register) products.Add(new FakeProduct("com.firstsymbol.vectra", "Vectra"));
            }
            HubWindow.PackageSource = () => packages;
            HubWindow.ProductSource = () => products;
        }

        // ---- Versions ----

        [Test]
        public void VersionsOrderAsSemverSays()
        {
            Assert.IsTrue(SemVer.Parse("1.0.0-rc.3") < SemVer.Parse("1.0.0"));
            Assert.IsTrue(SemVer.Parse("1.0.0-rc.2") < SemVer.Parse("1.0.0-rc.10"));
            Assert.IsTrue(SemVer.Parse("0.9.9") < SemVer.Parse("v1.0.0"));
            Assert.AreEqual(SemVer.Parse("1.0.0+build5"), SemVer.Parse("1.0.0"));
            Assert.IsFalse(SemVer.TryParse("latest", out _));
            Assert.IsFalse(SemVer.TryParse("", out _));
            Assert.IsFalse(default(SemVer).Valid);
        }

        [Test]
        public void ARangeIncludesItsMinAndLeavesOutItsMax()
        {
            var r = new VersionRange("1.0.0", "2.0.0");
            Assert.IsTrue(r.Contains(SemVer.Parse("1.0.0")));
            Assert.IsTrue(r.Contains(SemVer.Parse("1.9.9")));
            Assert.IsFalse(r.Contains(SemVer.Parse("2.0.0")));
            Assert.IsFalse(r.Contains(SemVer.Parse("0.9.0")));
            Assert.IsFalse(r.Contains(default));
        }

        // ---- Products and rules ----

        [Test]
        public void ProductsNotInstalledAreStillListedWithTheirGitUrl()
        {
            List<ProductState> list = Registry.Build(new List<PackageRecord>(), new List<HubProduct>());
            Assert.AreEqual(Catalog.Entries.Length, list.Count);
            Assert.IsTrue(list.All(p => !p.Installed && p.GitUrl.EndsWith(".git")));
        }

        [Test]
        public void AnInstalledProductCarriesVersionSourceAndHash()
        {
            List<ProductState> list = Registry.Build(new[] { Pkg("com.firstsymbol.lettera", "1.2.3-rc.1"), Pkg("com.firstsymbol.hub", "0.1.0") }, new List<HubProduct>());
            ProductState lettera = list.Single(p => p.Package == "com.firstsymbol.lettera");
            Assert.IsTrue(lettera.Installed);
            Assert.AreEqual("1.2.3-rc.1", lettera.VersionText);
            Assert.AreEqual("Git", lettera.Source);
            Assert.AreEqual("Project/FirstSymbol/Lettera", lettera.SettingsPath);
            Assert.IsFalse(list.Any(p => p.Package == "com.firstsymbol.hub"), "the hub is not one of its own products");
        }

        [Test]
        public void AProductOutsideTheCatalogShowsWhenItRegisters()
        {
            List<ProductState> list = Registry.Build(new[] { Pkg("com.firstsymbol.other", "0.2.0") }, new HubProduct[] { new FakeProduct("com.firstsymbol.other", "Other") });
            ProductState other = list.Single(p => p.Package == "com.firstsymbol.other");
            Assert.IsTrue(other.Installed);
            Assert.AreEqual("Other", other.Display);
        }

        static List<HubNote> Notes(string lettera, string vectra, bool register = true)
        {
            var packages = new List<PackageRecord>();
            var regs = new List<HubProduct>();
            if (lettera != null) { packages.Add(Pkg("com.firstsymbol.lettera", lettera)); if (register) regs.Add(new FakeProduct("com.firstsymbol.lettera", "Lettera")); }
            if (vectra != null) { packages.Add(Pkg("com.firstsymbol.vectra", vectra)); if (register) regs.Add(new FakeProduct("com.firstsymbol.vectra", "Vectra")); }
            return Compat.Evaluate(Registry.Build(packages, regs));
        }

        [Test]
        public void VectraTextEngineNeedsLetteraOne()
        {
            Assert.IsTrue(Notes("0.9.5", "0.4.0").Any(n => n.Id == "vectra-text-needs-lettera-1" && n.Severity == HubTone.Warn));
            Assert.IsFalse(Notes("1.0.0", "0.4.0").Any(n => n.Id == "vectra-text-needs-lettera-1"));
            Assert.IsTrue(Notes("1.0.0-rc.4", "0.4.0").Any(n => n.Id == "vectra-text-needs-lettera-1"), "a release candidate is older than 1.0.0");
            Assert.IsFalse(Notes(null, "0.4.0").Any(n => n.Id == "vectra-text-needs-lettera-1"), "no Lettera, no mismatch");
            Assert.IsFalse(Notes("0.9.5", "0.2.2").Any(n => n.Id == "vectra-text-needs-lettera-1"), "Vectra 0.2 has no Lettera engine");
        }

        [Test]
        public void AnInstalledProductWithoutAHubPageIsSaidToBe()
        {
            Assert.IsTrue(Notes("1.0.0", null, false).Any(n => n.Id == "no-hub-page-lettera"));
            Assert.IsFalse(Notes("1.1.0", null, true).Any(n => n.Id == "no-hub-page-lettera"));
        }

        // ---- Updates and the network ----

        [Test]
        public void TheTagsAddressComesFromTheRepositoryAndNothingElse()
        {
            Assert.AreEqual("https://api.github.com/repos/FirstSymbol/lettera-unity/tags?per_page=30", UpdateChecker.TagsUrl("https://github.com/FirstSymbol/lettera-unity"));
            Assert.AreEqual("https://api.github.com/repos/FirstSymbol/FSHub/tags?per_page=30", UpdateChecker.TagsUrl("https://github.com/FirstSymbol/FSHub.git"));
            Assert.IsNull(UpdateChecker.TagsUrl("https://example.com/a/b"));
            Assert.IsNull(UpdateChecker.TagsUrl("https://github.com/only-owner"));
            Assert.IsNull(UpdateChecker.TagsUrl(null));
        }

        [Test]
        public void TheNewestReleaseTagWinsOverAPreRelease()
        {
            Assert.AreEqual("1.2.0", UpdateChecker.PickLatest("[{\"name\":\"1.0.0\"},{\"name\":\"1.3.0-rc.1\"},{\"name\":\"1.2.0\"},{\"name\":\"junk\"}]"));
            Assert.AreEqual("0.5.0-rc.2", UpdateChecker.PickLatest("[{\"name\":\"0.5.0-rc.1\"},{\"name\":\"0.5.0-rc.2\"}]"));
            Assert.IsNull(UpdateChecker.PickLatest("[]"));
            Assert.IsNull(UpdateChecker.PickLatest("not json"));
        }

        [Test]
        public void NothingIsSentWhileUpdateChecksAreOff()
        {
            Fake(true, "1.0.0", true, "0.4.0");
            Assert.IsFalse(HubPrefs.CheckUpdates, "off by default");
            var window = EditorWindow.GetWindow<HubWindow>();
            window.Select("lettera");
            window.Select("vectra");
            window.Select(HubWindow.HubId);
            window.Select(HubWindow.OverviewId);
            window.ApplyTheme();
            window.Refresh();
            UpdateChecker.CheckIfDue(window.Products, null);
            Assert.IsFalse(UpdateChecker.Due(DateTime.UtcNow.AddYears(5)));
            Assert.AreEqual(0, HubNet.RequestCount, string.Join(", ", HubNet.Requested));
        }

        [Test]
        public void WhenOnItAsksOncePerProductAndThenWaitsADay()
        {
            Fake(true, "1.0.0", true, "0.4.0");
            HubPrefs.CheckUpdates = true;
            var window = EditorWindow.GetWindow<HubWindow>();
            Assert.AreEqual(2, HubNet.RequestCount, "one per installed product, from opening the window");
            Assert.IsTrue(HubNet.Requested.All(u => u.StartsWith("https://api.github.com/repos/FirstSymbol/")));
            Assert.AreEqual("1.2.0", UpdateChecker.Latest("com.firstsymbol.lettera"));
            Assert.IsTrue(window.Products.Single(p => p.Id == "lettera").UpdateAvailable);
            int before = HubNet.RequestCount;
            UpdateChecker.CheckIfDue(window.Products, null);
            Assert.AreEqual(before, HubNet.RequestCount, "checked a moment ago");
            Assert.IsTrue(UpdateChecker.Due(DateTime.UtcNow.AddHours(25)));
            Assert.IsFalse(UpdateChecker.Due(DateTime.UtcNow.AddHours(1)));
        }

        [Test]
        public void CheckNowAsksEvenWithTheToggleOffAndOnlyForInstalledProducts()
        {
            Fake(true, "1.0.0", false, null);
            var window = EditorWindow.GetWindow<HubWindow>();
            Assert.AreEqual(0, HubNet.RequestCount);
            window.CheckNow();
            Assert.AreEqual(1, HubNet.RequestCount);
            StringAssert.Contains("lettera-unity", HubNet.Requested[0]);
        }

        [Test]
        public void AFailedCheckIsReportedAndSendsNothingMore()
        {
            Fake(true, "1.0.0", false, null);
            HubNet.Interceptor = _ => null;
            var window = EditorWindow.GetWindow<HubWindow>();
            window.CheckNow();
            Assert.AreEqual(1, HubNet.RequestCount);
            Assert.AreEqual(1, UpdateChecker.Errors.Count);
            Assert.IsNull(UpdateChecker.Latest("com.firstsymbol.lettera"));
        }

        // ---- The window ----

        [Test]
        public void TheWindowListsExactlyTheInstalledProducts()
        {
            Fake(true, "1.0.0", false, null);
            var window = EditorWindow.GetWindow<HubWindow>();
            Assert.IsTrue(window.Products.Single(p => p.Id == "lettera").Installed);
            Assert.IsFalse(window.Products.Single(p => p.Id == "vectra").Installed);
            Assert.IsNotNull(window.Root.Q<VisualElement>(className: "fs-nav"));
            Assert.AreEqual(3, window.Root.Query<VisualElement>(className: "fs-nav__item").ToList().Count, "overview, hub settings and Lettera");
            window.Select("lettera");
            Assert.AreEqual("lettera", window.CurrentPage);
        }

        [Test]
        public void ANotInstalledProductGetsACardWithItsUrlAndNoNavEntry()
        {
            Fake(true, "1.0.0", false, null);
            var window = EditorWindow.GetWindow<HubWindow>();
            var urls = window.Root.Query<Label>(className: "fs-url").ToList().Select(l => l.text).ToList();
            CollectionAssert.Contains(urls, "https://github.com/FirstSymbol/vectra-unity.git");
            Assert.AreEqual(0, HubNet.RequestCount);
        }

        [Test]
        public void ARegisteredProductsStatusRowsAreShown()
        {
            Fake(true, "1.0.0", true, "0.4.0");
            var window = EditorWindow.GetWindow<HubWindow>();
            window.Select("vectra");
            var labels = window.Root.Query<Label>(className: "fs-row__label").ToList().Select(l => l.text).ToList();
            CollectionAssert.IsSupersetOf(labels, new[] { "Engine", "Atlas" });
        }

        [Test]
        public void TheWelcomeCardShowsOnceAndStaysDismissed()
        {
            Fake(true, "1.0.0", false, null);
            Assert.IsFalse(HubPrefs.WelcomeDismissed);
            var window = EditorWindow.GetWindow<HubWindow>();
            Assert.AreEqual(1, window.Root.Query<Label>(className: "fs-card__title").ToList().Count(l => l.text == "WELCOME"));
            window.Dismiss();
            Assert.IsTrue(HubPrefs.WelcomeDismissed);
            Assert.AreEqual(0, window.Root.Query<Label>(className: "fs-card__title").ToList().Count(l => l.text == "WELCOME"));
            window.Refresh();
            window.Select(HubWindow.OverviewId);
            Assert.AreEqual(0, window.Root.Query<Label>(className: "fs-card__title").ToList().Count(l => l.text == "WELCOME"));
        }

        [Test]
        public void TheThemeSwitchTurnsTheLightSkinOnAndOff()
        {
            Fake(true, "1.0.0", false, null);
            var window = EditorWindow.GetWindow<HubWindow>();
            HubPrefs.Theme = 2;
            window.ApplyTheme();
            Assert.IsTrue(window.Root.ClassListContains("fs--light"));
            HubPrefs.Theme = 1;
            window.ApplyTheme();
            Assert.IsFalse(window.Root.ClassListContains("fs--light"));
        }

        [Test]
        public void ProductsInstalledForRealAreFoundAndRegistered()
        {
            var real = HubWindow.CollectPackages();
            var window = EditorWindow.GetWindow<HubWindow>();
            foreach (PackageRecord r in real.Where(r => r.Name != Catalog.HubPackage))
            {
                ProductState p = window.Products.FirstOrDefault(x => x.Package == r.Name);
                Assert.IsNotNull(p, r.Name);
                Assert.IsTrue(p.Installed, r.Name);
            }
            foreach (ProductState p in window.Products.Where(p => p.Installed))
                Assert.IsNotNull(p.Registration, p.Package + " installed but its hub page assembly did not register");
            foreach (CatalogEntry e in Catalog.Entries.Where(e => real.All(r => r.Name != e.Package)))
            {
                ProductState p = window.Products.Single(x => x.Package == e.Package);
                Assert.AreEqual(p.Registration != null, p.Installed, e.Package + ": not in the Package Manager, so installed only when it registered (a folder under Assets)");
            }
        }

        [Test]
        public void ARegisteredProductsSettingsPageIsHostedInItsPage()
        {
            var real = HubWindow.CollectPackages();
            var window = EditorWindow.GetWindow<HubWindow>();
            foreach (ProductState p in window.Products.Where(p => p.Installed))
            {
                window.Select(p.Id);
                Assert.IsNotNull(window.SettingsPathOf(p), p.Package + " has no settings page");
                Assert.IsNotNull(window.Root.Q<VisualElement>(className: "fs-host"), p.Package);
            }
        }

        // ---- Where the hub and its products live ----

        [Test]
        public void TheStylesheetIsFoundWhereverTheHubLives()
        {
            Assert.IsTrue(UI.HubUI.SheetLoaded, "FirstSymbolHub.uss did not load");
            Assert.AreEqual(1, UI.HubUI.Root(false).styleSheets.count);
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator TheHubIsStyledOnceItIsLaidOut()
        {
            Fake(true, "1.0.0", false, null);
            var window = EditorWindow.GetWindow<HubWindow>();
            for (int i = 0; i < 6; i++)
            {
                window.Repaint();
                yield return null;
            }
            VisualElement nav = window.Root.Q<VisualElement>(className: "fs-nav");
            Assert.AreEqual(168f, nav.resolvedStyle.width, 1f, "the sidebar has its width only if the stylesheet applied");
            Assert.IsTrue(window.Root.resolvedStyle.backgroundColor.a > 0.5f);
        }

        string m_Folder;

        void RemoveFolder()
        {
            if (m_Folder == null) return;
            if (Directory.Exists(m_Folder)) Directory.Delete(m_Folder, true);
            if (File.Exists(m_Folder + ".meta")) File.Delete(m_Folder + ".meta");
            m_Folder = null;
        }

        string MakePackageFolder(string name, string version)
        {
            m_Folder = "Assets/" + name;
            Directory.CreateDirectory(m_Folder + "/Editor");
            File.WriteAllText(m_Folder + "/package.json", "{ \"name\": \"x\", \"version\": \"" + version + "\" }");
            return m_Folder + "/Editor";
        }

        [Test]
        public void AProductKeptUnderAssetsIsInstalledWithItsOwnVersion()
        {
            string editor = MakePackageFolder("FSHubTestProduct", "0.9.5");
            List<ProductState> list = Registry.Build(new List<PackageRecord>(), new HubProduct[] { new FakeProduct("com.firstsymbol.lettera", "Lettera") }, _ => editor);
            ProductState p = list.Single(x => x.Package == "com.firstsymbol.lettera");
            Assert.IsTrue(p.Installed, "the registration is the source of truth");
            Assert.AreEqual("0.9.5", p.VersionText);
            Assert.AreEqual("Assets folder", p.Source);
            Assert.IsTrue(p.Version.Valid);
            Assert.IsFalse(list.Single(x => x.Package == "com.firstsymbol.vectra").Installed);
        }

        [Test]
        public void TheCompatRulesUseTheVersionOfAProductKeptUnderAssets()
        {
            string editor = MakePackageFolder("FSHubTestProduct", "0.9.5");
            var regs = new HubProduct[] { new FakeProduct("com.firstsymbol.lettera", "Lettera"), new FakeProduct("com.firstsymbol.vectra", "Vectra") };
            var packages = new List<PackageRecord> { Pkg("com.firstsymbol.vectra", "0.4.0") };
            var notes = Compat.Evaluate(Registry.Build(packages, regs, r => r.PackageName.EndsWith("lettera") ? editor : null));
            Assert.IsTrue(notes.Any(n => n.Id == "vectra-text-needs-lettera-1"));
        }

        [Test]
        public void ThePackageJsonOfTheHubItselfIsFound()
        {
            string folder = Locations.AsmdefFolder("FirstSymbol.Hub.Editor");
            Assert.IsNotNull(folder);
            Assert.IsNotNull(Locations.PackageRoot(folder));
            Assert.IsNotEmpty(Locations.ReadVersion(Locations.PackageRoot(folder)));
        }

        // ---- Timings (Q5) ----

        static double Median(List<double> v)
        {
            v.Sort();
            return v[v.Count / 2];
        }

        [Test]
        public void TimingsOfOpeningAndSwitching()
        {
            var open = new List<double>();
            var first = new List<double>();
            var cached = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                foreach (HubWindow w in Resources.FindObjectsOfTypeAll<HubWindow>()) w.Close();
                var sw = Stopwatch.StartNew();
                var window = EditorWindow.GetWindow<HubWindow>();
                window.Select(HubWindow.OverviewId);
                open.Add(sw.Elapsed.TotalMilliseconds);
                foreach (ProductState p in window.Products.Where(p => p.Installed))
                {
                    sw.Restart();
                    window.Select(p.Id);
                    first.Add(sw.Elapsed.TotalMilliseconds);
                }
                for (int k = 0; k < 4; k++)
                {
                    string id = k % 2 == 0 ? HubWindow.OverviewId : HubWindow.HubId;
                    sw.Restart();
                    window.Select(id);
                    cached.Add(sw.Elapsed.TotalMilliseconds);
                }
                foreach (ProductState p in window.Products.Where(p => p.Installed))
                {
                    sw.Restart();
                    window.Select(p.Id);
                    cached.Add(sw.Elapsed.TotalMilliseconds);
                }
            }
            string line = "HUB-TIMINGS open median " + Median(open).ToString("F1") + " ms; first product page " + (first.Count > 0 ? Median(first).ToString("F1") : "n/a") + " ms; switch median " + Median(cached).ToString("F1") + " ms; products " + HubWindow.CollectPackages().Count;
            Debug.Log(line);
            Assert.LessOrEqual(Median(open), 300, line);
            Assert.LessOrEqual(Median(cached), 50, line);
        }

        // ---- Screenshots (only with FSHUB_SHOTS set) ----

        [UnityEngine.TestTools.UnityTest]
        [Category("Screenshots")]
        public System.Collections.IEnumerator ScreenshotsOfEachPageInBothSkins()
        {
            string dir = Environment.GetEnvironmentVariable("FSHUB_SHOTS");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("FSHUB_SHOTS is not set"); yield break; }
            string tag = Environment.GetEnvironmentVariable("FSHUB_TAG") ?? "shot";
            Directory.CreateDirectory(dir);
            HubPrefs.CheckUpdates = false;
            var window = EditorWindow.GetWindow<HubWindow>();
            window.position = new Rect(80, 80, 900, 640);
            MethodInfo capture = typeof(UnityEditorInternal.InternalEditorUtility).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == "CaptureEditorWindow");
            Debug.Log("HUB-SHOT capture method: " + (capture == null ? "none" : capture + " returns " + capture.ReturnType));
            var pages = new List<string> { HubWindow.OverviewId, HubWindow.HubId };
            pages.AddRange(window.Products.Where(p => p.Installed).Select(p => p.Id));
            foreach (int theme in new[] { 1, 2 })
            {
                HubPrefs.Theme = theme;
                window.ApplyTheme();
                foreach (string page in pages)
                {
                    window.Select(page);
                    for (int frame = 0; frame < 6; frame++)
                    {
                        window.Repaint();
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                        yield return null;
                    }
                    string file = Path.Combine(dir, tag + "_" + page + "_" + (theme == 2 ? "light" : "dark") + ".png");
                    Texture2D tex = null;
                    if (capture != null)
                    {
                        int w = Mathf.Max(64, (int)window.position.width), h = Mathf.Max(64, (int)window.position.height);
                        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                        object ok = capture.Invoke(null, new object[] { window, rt });
                        Debug.Log("HUB-SHOT capture " + ok + " " + w + "x" + h);
                        var previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        RenderTexture.active = previous;
                        rt.Release();
                    }
                    if (tex != null) File.WriteAllBytes(file, tex.EncodeToPNG());
                    else Debug.Log("HUB-SHOT no texture for " + file);
                }
            }
        }
    }
}
