# Sephiria Merchant Choice

Replace merchant rerolls with a searchable catalog of eligible artifacts and tablets.

**[Download v1.0.0 for BepInEx 6](https://github.com/liaodongxu/SephiriaMerchantChoice/releases/tag/v1.0.0)** · [简体中文](README.md)

Under **Assets**, download `SephiriaMerchantChoice-1.0.0-BepInEx6.zip`. The automatically generated `Source code` archives are not install packages.

## Features

- Filter artifacts, tablets and favorites; search item names and browse pages.
- View native item tooltips and purchase through the game's own transaction flow.
- Preserve Negotiation pricing, trade vouchers and inventory validation.
- Keep the catalog on the left side of the screen, using native fonts and sprites.

## Install

Target: **Sephiria 1.0.30, Windows x64, BepInEx 6 Unity Mono** (developed against 6.0.0-be.697). BepInEx 5 and IL2CPP are not supported; other game versions are unverified.

Install BepInEx separately. Exit the game, then merge the ZIP's `BepInEx` folder into the game directory. The resulting file must be `BepInEx/plugins/SephiriaMerchantChoice.dll`.

If upgrading from the combined plugin, move **SephiriaRunQoL.dll** outside `BepInEx/plugins`, including its subfolders. The old and new plugins are incompatible.

Click the native merchant reroll button after unlocking the game's replenishment feature. No other custom mod is required.

## Rules

Each merchant offers up to **2 artifact purchases and 1 tablet purchase** through replenishment. Closing the catalog does not reset these slots. First opening costs 2 sapphires if that merchant has not attempted replenishment yet; later openings are free. Item purchases still require gold or a valid trade voucher.

The catalog is not an all-item spawner: unlocks, dual-category activation, main-inventory duplicates, currently sold items, unique-item conflicts and weapon requirements are checked. Both associated combos must be active for dual artifacts. The native duplicate check does not include the sub-bag.

Choosing items directly changes the original random-selection experience; this is not a cosmetic-only mod.

## Status

The earlier combined version received positive single-player purchase feedback. This standalone version passes compilation, installed-game reference checks and behavior comparison, but complete standalone in-game regression testing remains pending. **Multiplayer client purchase synchronization is unverified.**

UI labels are currently Simplified Chinese; item names follow the game's language. Catalog tooltips pass mouse input through, so their internal keyword buttons are not clickable. Normal inventory tooltip behavior is unchanged.

This plugin does not include Quick Restart, Preset Favorites repair or native shop-list favorite hearts.

## Configuration and build

Set `[Shop] EnableChoiceCatalog = false` in `BepInEx/config/com.codex.sephiria.merchantchoice.cfg`, then restart to use native rerolls. Remove the DLL while the game is closed to uninstall.

Build with Windows .NET Framework 4's C# compiler and your installed game/BepInEx dependencies:

```powershell
./Build.ps1 -GameRoot 'D:\Games\Sephiria'
```

Output: `build/SephiriaMerchantChoice.dll`. No game binaries or assets are bundled. See [MIT License](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).
