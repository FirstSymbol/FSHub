using System.Collections.Generic;
using UnityEngine.UIElements;

namespace FirstSymbol.Hub
{
    /// <summary>How a status row reads: plain, fine, worth a look, or wrong.</summary>
    public enum HubTone { Normal, Good, Warn, Bad }

    /// <summary>One line of a product's status card: a label, a value and how the value reads.</summary>
    public readonly struct HubStatusRow
    {
        public readonly string Label;
        public readonly string Value;
        public readonly HubTone Tone;

        public HubStatusRow(string label, string value, HubTone tone = HubTone.Normal)
        {
            Label = label;
            Value = value;
            Tone = tone;
        }
    }

    /// <summary>
    /// How a product introduces itself to the hub. A product ships a small editor assembly with a subclass; the assembly compiles only when the hub is installed
    /// (a <c>versionDefines</c> entry on <c>com.firstsymbol.hub</c>, and that define as a <c>defineConstraints</c> entry). The hub finds subclasses by type, so neither side
    /// needs the other at compile time of its own package. Give the subclass a parameterless constructor; it may be internal.
    /// </summary>
    public abstract class HubProduct
    {
        /// <summary>The UPM package name, for example <c>com.firstsymbol.lettera</c>.</summary>
        public abstract string PackageName { get; }

        /// <summary>The name shown in the hub.</summary>
        public abstract string DisplayName { get; }

        /// <summary>One line on what the product is.</summary>
        public virtual string Description => "";

        /// <summary>The product's accent as #rrggbb.</summary>
        public virtual string AccentHex => "#8a93a6";

        /// <summary>The Project Settings page the hub hosts on the product's page. By convention <c>Project/FirstSymbol/&lt;Name&gt;</c>.</summary>
        public virtual string SettingsPath => "Project/FirstSymbol/" + DisplayName;

        /// <summary>The lines of the status card, read each time the page opens.</summary>
        public virtual IEnumerable<HubStatusRow> StatusRows() => System.Array.Empty<HubStatusRow>();

        /// <summary>Lets a hosted settings page follow the hub's own light or dark choice. <paramref name="pageRoot"/> holds whatever the settings page added.</summary>
        public virtual void ApplyTheme(VisualElement pageRoot, bool light)
        {
        }
    }
}
