# Changelog

All notable changes to this package are documented in this file.

## [0.1.2] - 2026-11-09

### Fixed
- **A hub kept as a folder under `Assets` showed no styling.** The stylesheet was looked up at a fixed `Packages/com.firstsymbol.hub/...` path. It is now found next to the hub's own assembly definition, wherever the hub lives.
- **Products kept as folders under `Assets` showed as not installed.** A product that registers with the hub counts as installed whether or not the Package Manager lists it. Its source reads "assets folder", its version comes from the `package.json` beside its hub page assembly, and the compatibility rules use that version.

## [0.1.1] - 2026-11-09

### Docs
- The README says that packages kept as folders under `Assets` need `FIRSTSYMBOL_HUB` among the scripting define symbols for their pages to appear.

## [0.1.0] - 2026-11-09

### Added
- First version. **Window > FirstSymbol > Hub** and **Tools > FirstSymbol > Hub** open one window for FirstSymbol packages: an overview of what is installed (version, source, commit, docs, changelog and repository links), cards with the git URL for products that are not, and a compatibility list from a rules table (Vectra's Lettera text engine needs Lettera 1.0.0 or newer).
- A page per product: a status card the product fills in, and the product's own Project Settings page hosted in the window. **Project Settings > FirstSymbol** is the node products' pages hang under.
- Products register with a small public API, `FirstSymbol.Hub.HubProduct`, from an editor assembly that compiles only when the hub is installed. The hub has no compile-time dependency on any product.
- Update checks, off by default: **Check GitHub for new tags once a day** and **Check now** ask GitHub's public tags list once per installed product, cache the answer for 24 hours under `Library/FirstSymbolHub`, and never run on a domain reload. **Update** and **Install** ask first. A test proves that nothing is sent while the toggle is off.
- A first-run card once per project; light, dark or follow-the-editor theme, kept per user.

### Limits
- The daily check runs when the hub window opens, not in the background.
