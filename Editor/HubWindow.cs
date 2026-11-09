using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstSymbol.Hub.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

namespace FirstSymbol.Hub
{
    /// <summary>The things the hub does on a click. Seams for tests, so a click never opens a browser or starts the Package Manager there.</summary>
    internal static class HubActions
    {
        internal static Func<string, string, bool> Confirm = (title, message) => EditorUtility.DisplayDialog(title, message, "Continue", "Cancel");
        internal static Action<string> AddPackage = id => Client.Add(id);
        internal static Action<string> OpenUrl = url => Application.OpenURL(url);
    }

    /// <summary>Finds the Project Settings pages of FirstSymbol products and shows them inside the hub.</summary>
    internal sealed class PageHost
    {
        readonly Dictionary<string, SettingsProvider> m_ByPath = new Dictionary<string, SettingsProvider>();
        readonly List<SettingsProvider> m_Active = new List<SettingsProvider>();

        internal PageHost()
        {
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<SettingsProviderAttribute>())
            {
                // Only our own packages are asked: creating another package's provider could be slow, and is none of the hub's business.
                if (!method.IsStatic || method.GetParameters().Length != 0) continue;
                string assembly = method.DeclaringType?.Assembly.GetName().Name;
                if (assembly == null || !assembly.StartsWith("FirstSymbol.", StringComparison.Ordinal) || assembly.StartsWith("FirstSymbol.Hub", StringComparison.Ordinal)) continue;
                try
                {
                    if (method.Invoke(null, null) is SettingsProvider provider && provider.settingsPath != null) m_ByPath[provider.settingsPath] = provider;
                }
                catch (Exception)
                {
                    // A provider that fails to create is skipped; the product page says its settings were not found.
                }
            }
        }

        internal SettingsProvider Find(string path) => path != null && m_ByPath.TryGetValue(path, out SettingsProvider p) ? p : null;

        internal IEnumerable<string> Paths => m_ByPath.Keys;

        /// <summary>Activates the provider into <paramref name="host"/>, which must already be in the window.</summary>
        internal void Activate(SettingsProvider provider, VisualElement host)
        {
            m_Active.Add(provider);
            if (provider.activateHandler == null && provider.guiHandler != null)
            {
                host.Add(new IMGUIContainer(() => provider.guiHandler(string.Empty)));
                return;
            }
            provider.OnActivate(string.Empty, host);
        }

        internal void DeactivateAll()
        {
            foreach (SettingsProvider p in m_Active)
            {
                try
                {
                    p.OnDeactivate();
                }
                catch (Exception)
                {
                    // Closing must not fail on a page that cannot close.
                }
            }
            m_Active.Clear();
        }
    }

    internal sealed class HubWindow : EditorWindow
    {
        internal const string OverviewId = "overview", HubId = "hub";

        /// <summary>Replaces the Package Manager's list in tests.</summary>
        internal static Func<List<PackageRecord>> PackageSource;

        /// <summary>Replaces the registered products in tests.</summary>
        internal static Func<List<HubProduct>> ProductSource;

        List<ProductState> m_Products = new List<ProductState>();
        readonly Dictionary<string, VisualElement> m_Pages = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> m_Hosts = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> m_NavItems = new Dictionary<string, VisualElement>();
        PageHost m_PageHost;
        VisualElement m_Root, m_Nav, m_Content;
        string m_Current;

        [MenuItem("Window/FirstSymbol/Hub", false, 2000)]
        [MenuItem("Tools/FirstSymbol/Hub", false, 0)]
        internal static void Open()
        {
            var window = GetWindow<HubWindow>("FirstSymbol Hub");
            window.minSize = new Vector2(620, 420);
        }

        internal IReadOnlyList<ProductState> Products => m_Products;
        internal string CurrentPage => m_Current;
        internal VisualElement Root => m_Root;
        internal IEnumerable<string> BuiltPages => m_Pages.Keys;

        public void CreateGUI()
        {
            Refresh();
            Select(string.IsNullOrEmpty(m_Current) ? OverviewId : m_Current);
            UpdateChecker.CheckIfDue(m_Products, () => RebuildAfterCheck());
        }

        void OnDisable() => m_PageHost?.DeactivateAll();

        void RebuildAfterCheck()
        {
            if (this == null || m_Content == null) return;
            ApplyLatest();
            foreach (string key in new[] { OverviewId, HubId })
                if (m_Pages.TryGetValue(key, out VisualElement stale))
                {
                    stale.RemoveFromHierarchy();
                    m_Pages.Remove(key);
                }
            BuildNav();
            string current = m_Current ?? OverviewId;
            m_Current = null;
            Select(current);
        }

        void ApplyLatest()
        {
            foreach (ProductState p in m_Products) p.LatestTag = UpdateChecker.Latest(p.Package);
        }

        internal static List<PackageRecord> CollectPackages()
        {
            if (PackageSource != null) return PackageSource();
            var list = new List<PackageRecord>();
            foreach (UnityEditor.PackageManager.PackageInfo info in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (!info.name.StartsWith(Catalog.Prefix, StringComparison.Ordinal)) continue;
                list.Add(new PackageRecord
                {
                    Name = info.name, Version = info.version, Source = info.source.ToString(), GitHash = info.git?.hash ?? "", ResolvedPath = info.resolvedPath,
                    DocsUrl = info.documentationUrl, ChangelogUrl = info.changelogUrl, RepositoryUrl = info.repository?.url,
                });
            }
            return list;
        }

        internal static List<HubProduct> CollectRegistrations()
        {
            if (ProductSource != null) return ProductSource();
            var list = new List<HubProduct>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<HubProduct>())
            {
                if (type.IsAbstract) continue;
                try
                {
                    list.Add((HubProduct)Activator.CreateInstance(type, true));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("FirstSymbol Hub: " + type.FullName + " could not be created: " + e.Message);
                }
            }
            return list;
        }

        /// <summary>Reads the installed packages and registrations again and builds the frame. Pages are built when first shown.</summary>
        internal void Refresh()
        {
            m_PageHost?.DeactivateAll();
            m_Pages.Clear();
            m_Hosts.Clear();
            m_PageHost = new PageHost();
            m_Products = Registry.Build(CollectPackages(), CollectRegistrations());
            ApplyLatest();

            rootVisualElement.Clear();
            m_Root = HubUI.Root(HubPrefs.IsLight);
            m_Nav = HubUI.El("fs-nav");
            m_Content = new ScrollView(ScrollViewMode.Vertical) { name = "hub-content" };
            m_Content.AddToClassList("fs-content");
            m_Content.style.flexGrow = 1;
            m_Root.Add(HubUI.El("fs-shell", m_Nav, m_Content));
            rootVisualElement.Add(m_Root);
            BuildNav();
            m_Current = null;
        }

        void BuildNav()
        {
            m_Nav.Clear();
            m_NavItems.Clear();
            m_Nav.Add(HubUI.El("fs-nav__brand", HubUI.Mark("FS", HubUI.Hex("#8a93a6"), true), HubUI.Text("FirstSymbol", "fs-nav__name")));
            NavItem(OverviewId, "Overview", HubUI.Hex("#8a93a6"), false);
            NavItem(HubId, "Hub settings", HubUI.Hex("#8a93a6"), false);
            bool any = false;
            foreach (ProductState p in m_Products)
            {
                if (!p.Installed) continue;
                if (!any) m_Nav.Add(HubUI.Text("PRODUCTS", "fs-nav__group"));
                any = true;
                NavItem(p.Id, p.Display, HubUI.Hex(p.AccentHex), p.UpdateAvailable);
            }
        }

        void NavItem(string id, string label, Color dot, bool badge)
        {
            VisualElement dotEl = HubUI.El("fs-nav__dot");
            dotEl.style.backgroundColor = dot;
            VisualElement item = HubUI.El("fs-nav__item", dotEl, HubUI.Text(label, "fs-nav__label"));
            if (badge) item.Add(HubUI.El("fs-nav__badge"));
            item.RegisterCallback<ClickEvent>(_ => Select(id));
            m_Nav.Add(item);
            m_NavItems[id] = item;
        }

        /// <summary>Shows a page, building it the first time. A page already built is only unhidden.</summary>
        internal void Select(string id, bool force = false)
        {
            if (!force && id == m_Current) return;
            foreach (VisualElement shown in m_Pages.Values) shown.style.display = DisplayStyle.None;
            foreach (KeyValuePair<string, VisualElement> n in m_NavItems) n.Value.EnableInClassList("fs-nav__item--on", n.Key == id);
            string key = id == OverviewId || id == HubId ? id : "_" + id;
            if (force && m_Pages.TryGetValue(key, out VisualElement stale))
            {
                stale.RemoveFromHierarchy();
                m_Pages.Remove(key);
            }
            if (!m_Pages.TryGetValue(key, out VisualElement page))
            {
                page = HubUI.El("");
                m_Content.Add(page);
                m_Pages[key] = page;
                if (id == OverviewId) Pages.BuildOverview(this, page);
                else if (id == HubId) Pages.BuildHubSettings(this, page);
                else
                {
                    ProductState product = m_Products.Find(p => p.Id == id);
                    if (product != null) Pages.BuildProduct(this, page, product);
                }
            }
            page.style.display = DisplayStyle.Flex;
            m_Current = id;
        }

        /// <summary>The Project Settings page of a product, activated into a host that stays for the window's life. Null when the product has none.</summary>
        internal VisualElement HostedPage(ProductState product, VisualElement parent)
        {
            if (m_Hosts.TryGetValue(product.Id, out VisualElement existing)) return existing;
            SettingsProvider provider = m_PageHost.Find(product.SettingsPath) ?? m_PageHost.Find(product.LegacySettingsPath);
            if (provider == null) return null;
            VisualElement host = HubUI.El("fs-host");
            parent.Add(host);
            m_Hosts[product.Id] = host;
            m_PageHost.Activate(provider, host);
            product.Registration?.ApplyTheme(host, HubPrefs.IsLight);
            return host;
        }

        internal string SettingsPathOf(ProductState product) => m_PageHost.Find(product.SettingsPath) != null ? product.SettingsPath : m_PageHost.Find(product.LegacySettingsPath) != null ? product.LegacySettingsPath : null;

        /// <summary>The hub's own light or dark choice: the hub's colours and every hosted page that knows how to follow it.</summary>
        internal void ApplyTheme()
        {
            bool light = HubPrefs.IsLight;
            m_Root.EnableInClassList("fs--light", light);
            foreach (KeyValuePair<string, VisualElement> h in m_Hosts)
            {
                ProductState p = m_Products.Find(x => x.Id == h.Key);
                p?.Registration?.ApplyTheme(h.Value, light);
            }
        }

        internal void CheckNow()
        {
            UpdateChecker.CheckNow(m_Products, RebuildAfterCheck);
            if (!UpdateChecker.Running) RebuildAfterCheck();
        }

        internal void Dismiss()
        {
            HubPrefs.WelcomeDismissed = true;
            Select(OverviewId, true);
        }

        /// <summary>Whether the folder of a package holds local documentation.</summary>
        internal static string LocalDocs(ProductState p)
        {
            if (string.IsNullOrEmpty(p.ResolvedPath)) return null;
            string path = Path.Combine(p.ResolvedPath, "Documentation~");
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>Project Settings > FirstSymbol: the node the products' pages hang under, with a way into the hub.</summary>
    internal static class HubSettingsProvider
    {
        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider("Project/FirstSymbol", SettingsScope.Project)
        {
            label = "FirstSymbol",
            keywords = new HashSet<string> { "FirstSymbol", "Hub", "Lettera", "Vectra", "Updates" },
            activateHandler = (_, root) =>
            {
                VisualElement page = HubUI.Root(HubPrefs.IsLight);
                page.style.paddingLeft = 10;
                page.style.paddingTop = 8;
                page.style.maxWidth = 640;
                var products = Registry.Build(HubWindow.CollectPackages(), HubWindow.CollectRegistrations());
                VisualElement card = HubUI.Card("FirstSymbol packages");
                foreach (ProductState p in products)
                    if (p.Installed) card.Add(HubUI.Row(p.Display, "v" + p.VersionText));
                card.Add(HubUI.Text("Each product's settings are on its own page under this node.", "fs-hint"));
                card.Add(HubUI.El("fs-links", HubUI.Button("Open the Hub", "Window > FirstSymbol > Hub", HubWindow.Open, true)));
                page.Add(card);
                root.Add(page);
            },
        };
    }
}
