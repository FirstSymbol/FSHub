# FirstSymbol Hub

One editor window for FirstSymbol packages ([Lettera](https://github.com/FirstSymbol/lettera-unity), [Vectra](https://github.com/FirstSymbol/vectra-unity)): what is installed and at which version, what does not fit together, each product's settings and a status card, links to docs and changelogs, and update checks that stay off until you turn them on.

[Русская версия](README.ru.md)

## Installation

Unity Package Manager → **+** → **Add package from git URL...**:

```
https://github.com/FirstSymbol/FSHub.git
```

Requires Unity 6.3 LTS (6000.3) or newer. The hub is editor-only: it adds nothing to a build.

The hub works as a package or as a plain folder under `Assets`: as a folder, it adds `FIRSTSYMBOL_HUB` to the scripting define symbols by itself (and takes it away when the folder goes), so the products' pages appear. A symbol you set yourself is left alone.

## Using it

**Window > FirstSymbol > Hub** (also **Tools > FirstSymbol > Hub**).

- **Overview**: installed products with version, source (git, local, registry), commit and links; products that are not installed, with the git URL to copy; a compatibility list when two installed versions do not fit together.
- **A page per product**: a status card the product fills in, and the product's own Project Settings page, hosted in the window. The same page is under **Project Settings > FirstSymbol > <Product>**.
- **Hub settings**: light, dark or follow the editor; update checks; the first-run card.

## Network

The hub sends nothing by default. Turning on **Check GitHub for new tags once a day** makes it ask GitHub's public tags list, once per installed product, when the window opens and a day has passed. **Check now** does it once. **Update** and **Install** ask first and hand the address to the Package Manager. No sign-in, no telemetry.

## For a product's author

A product ships a small editor assembly that exists only when the hub is installed:

```json
{
    "name": "MyCompany.MyProduct.HubPage",
    "references": ["FirstSymbol.Hub.Editor"],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["FIRSTSYMBOL_HUB"],
    "versionDefines": [{ "name": "com.firstsymbol.hub", "expression": "0.1.0", "define": "FIRSTSYMBOL_HUB" }]
}
```

with one class:

```csharp
internal sealed class MyProductHub : FirstSymbol.Hub.HubProduct
{
    public override string PackageName => "com.firstsymbol.myproduct";
    public override string DisplayName => "My Product";
    public override string AccentHex => "#4c8dff";
    public override IEnumerable<HubStatusRow> StatusRows() { yield return new HubStatusRow("Engine", "Auto", HubTone.Good); }
}
```

The hub finds it by type. The product's settings page is a normal `[SettingsProvider]` at `Project/FirstSymbol/My Product`; the hub hosts it. Without the hub the product compiles and works as before.

## License

MIT.
