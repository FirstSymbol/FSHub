using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FirstSymbol.Hub.UI
{
    /// <summary>Building blocks of the hub, styled by FirstSymbolHub.uss. Internal: products describe themselves with data (<see cref="HubStatusRow"/>), never with these.</summary>
    internal static class HubUI
    {
        const string SheetPath = "Packages/" + Catalog.HubPackage + "/Editor/UI/FirstSymbolHub.uss";
        static StyleSheet s_Sheet;

        static StyleSheet Sheet => s_Sheet != null ? s_Sheet : s_Sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);

        internal static VisualElement Root(bool light)
        {
            var root = new VisualElement();
            if (Sheet != null) root.styleSheets.Add(Sheet);
            root.AddToClassList("fs-root");
            root.EnableInClassList("fs--light", light);
            return root;
        }

        internal static VisualElement El(string classes, params VisualElement[] children)
        {
            var element = new VisualElement();
            foreach (string c in classes.Split(' '))
                if (c.Length > 0) element.AddToClassList(c);
            foreach (VisualElement child in children) element.Add(child);
            return element;
        }

        internal static Label Text(string text, string classes)
        {
            var label = new Label(text) { enableRichText = false };
            foreach (string c in classes.Split(' '))
                if (c.Length > 0) label.AddToClassList(c);
            return label;
        }

        internal static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : new Color(0.54f, 0.58f, 0.65f);
        }

        internal static Label Mark(string letter, Color accent, bool small = false)
        {
            Label mark = Text(letter, small ? "fs-mark fs-mark--small" : "fs-mark");
            mark.style.backgroundColor = accent;
            return mark;
        }

        internal static Label Chip(string text, HubTone tone = HubTone.Normal)
        {
            string cls = tone == HubTone.Good ? "fs-chip fs-chip--good" : tone == HubTone.Warn ? "fs-chip fs-chip--warn" : tone == HubTone.Bad ? "fs-chip fs-chip--bad" : "fs-chip";
            return Text(text, cls);
        }

        internal static VisualElement Card(string title, params VisualElement[] children)
        {
            VisualElement card = El("fs-card");
            if (!string.IsNullOrEmpty(title)) card.Add(Text(title.ToUpperInvariant(), "fs-card__title"));
            foreach (VisualElement child in children) card.Add(child);
            return card;
        }

        internal static VisualElement Header(string title, string subtitle, Color accent, string letter, out VisualElement chips)
        {
            chips = El("fs-chips");
            VisualElement info = El("fs-header__info", Text(title, "fs-title"), Text(subtitle, "fs-subtitle"), chips);
            return El("fs-header", Mark(letter, accent), info);
        }

        internal static Label Button(string text, string tooltip, Action onClick, bool primary = false, Color? accent = null)
        {
            Label button = Text(text, primary ? "fs-btn fs-btn--primary" : "fs-btn");
            button.tooltip = tooltip;
            if (primary && accent.HasValue)
            {
                button.style.backgroundColor = accent.Value;
                button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = accent.Value;
            }
            button.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                onClick?.Invoke();
            });
            return button;
        }

        internal static VisualElement Row(string label, VisualElement value, string tooltip = null)
        {
            VisualElement row = El("fs-row", Text(label, "fs-row__label"), value);
            if (tooltip != null) row.tooltip = tooltip;
            return row;
        }

        internal static VisualElement Row(string label, string value, HubTone tone = HubTone.Normal)
        {
            string cls = tone == HubTone.Good ? "fs-row__value fs-row__value--good" : tone == HubTone.Warn ? "fs-row__value fs-row__value--warn" : tone == HubTone.Bad ? "fs-row__value fs-row__value--bad" : "fs-row__value";
            return Row(label, Text(value, cls));
        }

        internal static VisualElement Switch(Func<bool> get, Action<bool> set, Color? accent = null)
        {
            VisualElement thumb = El("fs-switch__thumb");
            VisualElement track = El("fs-switch", thumb);
            void Paint() => track.EnableInClassList("fs-switch--on", get());
            Paint();
            track.RegisterCallback<ClickEvent>(e =>
            {
                e.StopPropagation();
                set(!get());
                Paint();
            });
            return track;
        }

        internal static VisualElement Segmented(string[] labels, Func<int> get, Action<int> set)
        {
            VisualElement seg = El("fs-seg");
            var items = new Label[labels.Length];
            void Paint()
            {
                for (int i = 0; i < items.Length; i++) items[i].EnableInClassList("fs-seg__item--on", i == get());
            }
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                items[i] = Text(labels[i], "fs-seg__item");
                items[i].RegisterCallback<ClickEvent>(e =>
                {
                    set(index);
                    Paint();
                });
                seg.Add(items[i]);
            }
            Paint();
            return seg;
        }

        internal static VisualElement Note(HubTone tone, string message)
        {
            string dot = tone == HubTone.Warn ? "fs-note__dot fs-note__dot--warn" : tone == HubTone.Bad ? "fs-note__dot fs-note__dot--bad" : "fs-note__dot";
            return El("fs-note", El(dot), Text(message, "fs-text"));
        }
    }
}
