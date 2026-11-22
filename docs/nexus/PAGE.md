# Nexus Mods page: what to fill in

**Published:** https://www.nexusmods.com/howtheybuiltthepyramids/mods/3, now "Pyramids Custom Maps", version 0.5.0 (22 Nov 2026; 0.4.0 and 0.3.0 archived). Permissions: modification allowed with credit; the rest left at the Nexus defaults.

## Updating to 0.5.0 (13 new maps, player maps): done 22 Nov 2026

Name, version, summary, description, file (as an update of 0.4.0, which was archived) and changelog. Gallery since then: the in-game cat shot with a player for scale (`screens/game-01-front-scale.png`, the thumbnail), then one render per map from [`../maps/`](../maps) (16 images); the other in-game shots and extra Sobek renders were removed. Note: the media editor can show an empty gallery while it loads; wait before changing anything there.

| Field | Value |
|---|---|
| **Mod name** (rename) | Pyramids Custom Maps |
| **Version** | 0.5.0 |
| **Summary** | Build something other than the pyramid: giant statues (a cat, Sobek, Anubis, Horus), upside-down pyramids, Giza, the Step and Bent pyramids, a ziggurat, a colossal pyramid and more. 15 maps, built in any order, with Shabti, build markers and the full ending. Add your own from MagicaVoxel. Requires BepInEx 5. |
| **Description** | Paste the updated [`description.bbcode.txt`](description.bbcode.txt) |
| **Media** | Add in-game Sobek shots from [`screens/`](screens) (`sobek-01-camp.png`, `sobek-02-side.png`, `sobek-03-side-close.png`) |
| **File** | Upload `release/PyramidsCustomMaps-0.5.0.zip` as a new main file, version 0.5.0, and move 0.4.0 to Old versions |
| **File description** | All 15 maps (one plugin DLL) and a player readme. Extract into the game folder after installing BepInEx 5.4.23.6; extract over 0.3.0 or 0.4.0 to update. |
| **Changelog 0.5.0** | 13 new maps: Anubis, Horus, Upside-Down, Upside-Down Grand, Diamond, Step Pyramid, Bent Pyramid, Nubian Field, Ziggurat, Giza, Colossi, Sphinx Avenue, Colossal Pyramid. Player-made maps from MagicaVoxel .vox files. The main menu's map buttons became one Custom map dropdown. Free camera hides the first-person hands. Shapes build 3-8x faster. |

## Updating to 0.4.0 (Sobek): done 22 Nov 2026

Done without a playtest (the page and readme say Sobek hasn't had a full playthrough yet). What was changed on mod #3:

| Field | Value |
|---|---|
| **Mod name** (optional rename) | Pyramids Custom Maps - The Great Cat and Sobek |
| **Version** | 0.4.0 |
| **Summary** | Replace the pyramid with a giant statue built brick by brick: a stone cat crouched to pounce, or Sobek, a crocodile lying in his pool with jaws open. Lay blocks in any order, with Shabti builders, build markers and the full ending. Pick a map on the main menu's empty plot. Requires BepInEx 5. |
| **Description** | Paste the updated [`description.bbcode.txt`](description.bbcode.txt) |
| **Media** | Add Sobek renders from [`images/sobek/`](images/sobek) (`01-hero.png` first); in-game Sobek shots once taken |
| **File** | Upload `release/PyramidsCustomMaps-0.4.0.zip` as a new main file, version 0.4.0, and move 0.3.0 to Old versions |
| **File description** | Both maps (one plugin DLL) and a player readme. Extract into the game folder after installing BepInEx 5.4.23.6; extract over 0.3.0 to update. |
| **Changelog 0.4.0** | New map: Sobek, a crocodile lying in an oval pool, jaws open. Statue code made general for more maps. Multiplayer byte now says which map (0.3.0 players can still join cat worlds). |

Game: **How They Built the Pyramids**. Upload at https://www.nexusmods.com/games/howtheybuiltthepyramids, then **Upload mod**.

## 1. Mod details

| Field | Value |
|---|---|
| **Mod name** | Pyramids Custom Maps - The Great Cat |
| **Version** | 0.3.0 |
| **Category** | The closest fit in the game's list, e.g. *Gameplay*, *Maps* or *Miscellaneous*. Nexus sets the categories per game. |
| **Language** | English |
| **Summary** (max 350 characters) | Replace the pyramid with a giant stone cat crouched to pounce. 24,052 blocks you can lay in any order, Shabti builders, build markers and the full ending. Pick "Great Cat" on the main menu's empty plot. Requires BepInEx 5. |
| **Description** | Paste [`description.bbcode.txt`](description.bbcode.txt) into the editor's **BBCode / source** view. |
| **Contains adult content** | No |

## 2. Media

**In-game screenshots** are in [`screens/`](screens), edited: the build beacon painted out, the first-person hands cropped off.
- **Primary image (thumbnail):** `screens/game-01-front-scale.png`, the finished cat from the front with a player between her paws for scale.
- **Gallery:** `game-02-side.png`, `game-03-side-river.png`, `game-04-wide-river.png`.

**Renders** of the shape are in [`images/`](images) (1920×1080). Good as extra gallery images: `01-hero.png`, `02-front.png`, `03-side.png`, `04-behind.png`, `05-above.png`.

To take more shots in game: F11 is a free camera (arrow keys move, PageUp/PageDown up and down, Shift faster; it hides the HUD, the hands and the build beacons), and F10 saves a clean screenshot to `BepInEx\plugins\PyramidsMaps\screenshots`.

## 3. Files

| Field | Value |
|---|---|
| **File** | `release/PyramidsCustomMaps-0.3.0.zip` |
| **File name** | Pyramids Custom Maps 0.3.0 |
| **Category** | Main files |
| **Version** | 0.3.0 |
| **Description** | The mod (one plugin DLL) and a player readme. Extract into the game folder after installing BepInEx 5.4.23.6. |

## 4. Requirements

- **Off-site requirement:** BepInEx 5.4.23.6, https://github.com/BepInEx/BepInEx/releases. Note: *"Windows x64 build: BepInEx_win_x64_5.4.23.6.zip"*.
- If someone later uploads BepInEx to this game's Nexus section, link that instead.

## 5. Permissions and credits

Your call. A common choice for an open-source mod:
- **Upload permission:** ask first, or "you can upload this file to other sites but you must credit me".
- **Modification permission:** allowed with credit (the source is on GitHub).
- **Asset use, conversion, commercial use:** your preference.
- **Credits:** BepInEx and HarmonyX (the modding framework the mod uses).
- **Mirrors:** GitHub, https://github.com/kaesoclarus/pyramids-custom-maps

## 6. Before you publish

- [ ] The upload zip and the readme say the same version (0.3.0).
- [ ] Your own installed copy matches the uploaded zip.
- [ ] Optional: test multiplayer with a friend first, or keep the "not yet tested" note in the description.
