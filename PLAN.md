# Custom map mod for How They Built the Pyramids: plan

## Phase 0 results (2026-11-20)
- **The running game is 0.43.0.** The README's patch notes stop at 0.23.2 and are out of date.
- **Saves are in `%USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids`**, not `DefaultCompany` as the README says. They're backed up in `Backups\`.
- **BepInEx 5.4.23.6 works** on Unity 6000.4.10f1. The `PyramidsMaps` test plugin loads and writes the map layout to `BepInEx\LogOutput.log`.
- **UnityExplorer 4.13.6 does not work** on Unity 6000.4. It fails with `Scene.get_handle()` MissingMethodException because `Scene.handle` became a `SceneHandle`. Replacement for Phase 1: our own scene and component dump in the plugin.
- **Scene layout:** the scene is `Pyramid`. Under `Map/` are `PyramidSite` at (0,0,-44), `ClayPit` at (0,0,44), `RiverEdge_South` at z=47.3, `MapDecorations` with 11 `MapDecor` items, `MoldYard/` and `GymCorner/`. Under `Environment/` are `Terrain`, which is active (640×60×640, heightmap 1025, 4 layers, origin (-320,-6,-300)), `Terrain_Grand`, which is inactive (the game's own second terrain), and `Dressing/`. `BuildArea` is on `Systems/WorldSystems`.
- **There is no NileQuay in the scene.** Players build it at runtime, so the map only has to provide the river bank.

## Phase 1 results (2026-11-20)
Sources:
- `decompiled\` holds all 842 game types, made with `tools\Decomp`.
- `recon\0.43.0\` holds the plugin's scene dump: `hierarchy.txt`, `components.txt`, `classic.json` and both terrain heightmaps as 16-bit `.raw` files at 1025².

**The game already has a map-switching system, and we can build on it.** Version 0.43 added a "Grand Pyramid" New Game+ world (`Pyramid.Core.WorldKind`: `Classic` = 0, `Grand` = 1). Each save stores its kind, and switching works like this:
1. `WorldSave` picks the slot and calls `PyramidState.ServerSetWorldKind(kind)`. The kind is a FishNet SyncVar, so clients get it in `OnStartClient` / `OnWorldKindChanged`.
2. `PyramidState.ApplyWorldKind(kind)` runs on the host and on every client:
   - `PyramidSite.ApplyKind` moves the site (classic (0,0,-44) to grand (0,0,-100)) and swaps the `PyramidDefinition` (base 51 to 85).
   - The layout is rebuilt and `LayoutSwapped` / `Changed` are raised.
   - `WorldKinds.SetCurrent` is called.
3. `WorldKinds.Changed` → `WorldKindVariant` (`Map/WorldVariant`) turns its `classicOnly` objects off and its `grandOnly` objects on. Classic-only objects are the `Terrain`, `Map/Boundaries`, 4 obelisks and 36 palms, plants, cliffs and dead trees. Grand-only objects are `Terrain_Grand`, `Boundaries_Grand` and 4 grand obelisks.
4. `PlayerTuning.PlayAreaMin/Max` read `WorldKinds.IsGrand`. Classic is x ±81, z −124…46.4; Grand is x ±97, z −199…46.4.
5. The `BuildArea` keep-out comes from the pyramid's position, so it follows the site on its own.

**Hook point (confirmed in game):** a Harmony postfix on `PyramidState.ApplyWorldKind` runs right after "Hosting save slot …" and **before** belts, Shabti, buildings and the trader are restored from the save. That's the right moment to lay out a custom map.

**Fixed values from the classic map:**
- The river bank (`RiverEdge_South`) has a box 156×4×1.8 at z 47.3 and land face z 46.4. The river mesh `Environment/River` is at z 54, scale (64,1,1.7). The river runs along +x on the north edge.
- The clay pit box is 36×2×4.6 at (0,0,44.2), with 5 `ClayMound` mesh piles.
- The terrain is 640×60×640 at (−320,−6,−300), heightmap 1025, layers SandFine, SandLarge, Mud and SandFine_gravel.
- Other things on the map: `ShopStall` (14,0,34), `ShabtiMarket` (21,0,30), `MoldYard` (0,0,32), `GymCorner` (−15,0,35), `SpawnPoints` and `MapDecorations` (11 removable decorations with ids 0–10).
- **The game uses URP** (Universal Render Pipeline), so Phase 3 AssetBundles must use URP shaders.
- Multiplayer: FishNet's `VersionAuthenticator` checks the game version only. Clients with a different map would still be let in, so we need our own check (Phase 4).
- **Pyramid size:** the pyramid's layer count is (baseSize+1)/2, so 26 layers for base 51 and 43 for base 85. Its `brickValueByLayer`, `milestones` and `layerTitles` arrays are tuned to that. A custom size has to copy the definition (`Object.Instantiate`) and stretch those arrays to match. Simplest for the first version: use the classic (51) or grand (85) definition as is.

## Revised Phase 2 design
A custom map works like a third world kind, applied on top of Classic or Grand:
- **Which map a world uses:** a small sidecar file next to the save slot (e.g. `slot_1.map.json` holding the map id and hash). For now the host picks the map with a config entry when starting a new world.
- **In the `ApplyWorldKind` postfix**, when the slot has a custom map:
  1. Pick the base kind (Classic or Grand) for the pyramid definition, then move `PyramidSite` to the map's position.
  2. Hide everything in `classicOnly` and `grandOnly` (via `WorldKindVariant`) and show our own objects.
  3. Terrain: clone `Terrain`'s TerrainData (never edit the original asset), apply the map's heightmap with `SetHeights`, and use it.
  4. Move or resize `RiverEdge_South` and `ClayPit` (box plus mounds) and the `River` mesh. Move `Map/Boundaries` walls to the map's play area.
  5. Harmony-patch the `PlayerTuning.PlayAreaMin/Max` getters to return the map's play area.
  6. Move `ShopStall`, `ShabtiMarket`, `MoldYard`, `GymCorner` and `SpawnPoints` to the map's spots.
- **Undo:** when the world closes (`OnStopNetwork` → Classic), put everything back so the menu showcase and other slots look normal.

## First map: the Sphinx (decided 2026-11-20)
**Goal:** a "Sphinx" world where the main build is sphinx-shaped instead of a pyramid, with the normal progress, climbing and ending. The river and terrain stay as they are.

**Why it's a big job:** `PyramidLayout` stores the build as layers of rectangular sections and doesn't care about shape. Most of the code around it, though, treats the pyramid as square **rings** centred on the site: ring = max(|x|,|z|) and `LayerHalfExtent(layer)` gives each layer's size. That covers:
- `PyramidPlacement`: walking and tread height (`_topOfRing`), "inside footprint", ramps and platforms
- `PyramidRenderer`: ring shells, solid cores, holed boxes and ring-run colliders
- `PyramidState`: the builder plane (`BuilderLayerHalf` / `BuilderPlaneRadius` / notches)
- `LiftGeometry` / `SledgeLiftPlacement`: lifts on a face
- `ShabtiRamps` / `ShabtiPaths`
- `BuildArea`'s square keep-out
- `EndingDirector` camera framing

**Approach:** describe the sphinx as a **heightmap of columns** (layers per 2 m × 2 m column). Each column is solid from the ground up, so every block has a block under it. A built-in stepped stair (the rear haunches, then the back of the head) gives a route up to every level one 2.5 m step at a time. Wherever the game asks "which ring / how big is this layer", it gets column-based answers instead.

**Stages:**
1. **Shape and layout:** a sphinx heightmap generator and a preview tool. Patch `PyramidLayout.BuildWhole` to emit the sphinx's sections when the site uses our cloned `SphinxDefinition`. Scale milestones and per-layer brick values to the sphinx's brick count. Each world gets a sidecar file marking it as a Sphinx world. *Test: see what renders and what breaks.*
   - *Status (2026-11-20):* **working.** New world `slot_3` became a Sphinx world: 7,298 cells, 16 layers, 131 sections, 64 blueprint unlocks plus upgrade and market gates scaled ×0.312. In game the sphinx outline shows and bricks can be placed. A fix that stops the hook throwing on the main menu, plus the menu showcase drawing Sphinx saves (and the empty plot when `NewWorldMap = sphinx`), is built and waiting to be copied in once the game is closed.
   - Shape preview: `tools\ShapePreview` writes `preview\sphinx.html` / `.svg`.
   - *Update 7 (0.3.0): in-game map chooser.*
     - The main menu's empty plot gets one button per custom map (copies of the Grand Pyramid button) next to Pyramid and Grand Pyramid. All buttons are refitted into the card's original width.
     - Choosing a map sets `NewWorldMap` and redraws the preview; choosing Pyramid or Grand clears it.
     - **To add a map:** its world logic, plus one entry in `Maps\MapRegistry.cs`.
     - **Tested in game:** the layout looks right and switching works.
     - **Friend zip:** `release\PyramidsCustomMaps-0.3.0.zip`.
   - *Update 6 (0.2.1):*
     - **Footprint, not square:** only her real footprint counts as "on the cat" (`InsideLocal`, `FootprintOverlaps` and `OutsideFootprint` are patched). Before, the whole 129 m square counted, so nearby ground said "Future bricks go here".
     - **Shabti routes** go around her bounding rectangle, not the square.
     - **Ground builders: tested and working** (Work Post beside the base, Shabti lay base bricks).
     - **Fixes:** a cat-layout memory leak (now a weak table); the dead `Heightmap.cs` and old SVG preview code removed; the reach checker uses the cat's 1.3 m rule (the whole cat is still buildable by hand).
     - **Friend package:** `release\PyramidsCustomMaps-0.2.1.zip` (the mod and a README; BepInEx comes from its official page).
   - *Update 5 (Shabti):*
     - **On the cat: tested and working.** A Work Post on layer 1's top reads step 2 correctly, and its Shabti lay bricks from a nearby crate.
     - **Ground builders:** they now start at the Work Post, not the square site's edge. Not yet tested in game: the dev fills had already finished the ground layer.
     - **"Needs ramps" messages** now say "Put the Work Post up on the cat, next to a brick crate".
     - **Patch safety:** patch groups apply one by one, and a failure is logged by name.
     - **Dev panel:** the game's F1 panel needs the `-devpanel` launch option.
   - *Update 4 (0.2.0):*
     - **Multiplayer:** the host syncs "cat, shape 5" through the world-kind byte (0x40 | version = 69), and modded clients build the cat. Not yet tested with a second player.
     - **Shabti builders:** builder-plane squares count as floor only above laid bricks, and a Work Post on the cat builds its own level. Not yet tested in game.
     - **Rankings:** cat-world records aren't rankable.
     - **Ending:** **tested** with the F9 dev fill (`DevTools` setting) in a throwaway world. The final brick on the left ear tip was placed and the full ending played with no errors. That run earned the real Steam achievement ACH_CROWN.
   - *Update 3 (2026-11-20, in game):* the cat is **playable in a new world (slot_4)**.
     - **Building:** every block is open from the start, in any order, but a block must touch a laid block or sit on the ground layer. A simulation shows the whole cat can be built by hand this way; layer by layer, 1,795 blocks couldn't be.
     - **Place rule:** a block's base may be up to 1.3 m above your feet (the game allows 1 m), so you can stack one 1.25 m cat layer above where you stand.
     - **Rendering:** every layer stays in exact per-section mode, so there are no square slabs or cores.
     - **Markers:** small markers show the spots next to laid bricks. F7 toggles them; config `ShowMarkers`.
     - **Other changes:** a footprint-shaped keep-out zone; stone ramps, scaffold platforms and the sledge lift are off sale; the menu's empty plot previews the cat.
     - **Speed:** the aim only scans sections within reach (the lag is gone).
     - **Debug:** F8 logs a placement report.
     - **Not done:** Shabti builders on the cat, the ending/crowning, multiplayer clients, and a full-session performance check.
   - *Update 2 (current design):* a **crouched cat about to pounce**, shape version 5, built brick by brick as the whole objective in place of the pyramid. It uses **smaller blocks: 1 m × 1 m × 1.25 m** (half the pyramid's each way, set via the cloned definition's `cellSize`) for smooth curves. Size: 23,712 blocks (pyramid: 23,426), 30 layers (37.5 m), on one oval base; 1,929 blocks overhang (chin, belly, tail), which the game's layer-by-layer opening allows. The footprint is ±64 m, larger than the classic ±51 m site but inside the play area. Preview: `preview\statue.html` (ray-traced PNGs from `tools\ShapePreview`).
   - *Earlier:* a seated Bastet (version 4, dropped as too blocky and unreadable from the front), and before that the lying sphinx.
   - *Update 1:* the design changed from a lying sphinx to a **seated cat statue (Bastet)**, shape version 4. It's modelled from smooth solids (`Shapes\SphinxShape.cs`) and sampled into a full 3D block grid (`Shapes\Voxels.cs`), so it can have overhangs: 5,821 blocks, 36 layers (90 m), 205 overhanging blocks. Preview: `preview\statue.html`. The test world `slot_3` used shape version 1 and no longer matches. **In-game work is paused** while the look is settled. The plugin compiles against the new shape but hasn't been redeployed.
2. **Standing and placing:** column-based replacements for `PyramidPlacement` surface and tread queries and the `PyramidState` builder plane, so players can climb the sphinx and lay bricks on every level.
3. **Rendering and colliders:** column-based shells, cores and colliders in `PyramidRenderer`.
4. **Building aids:** stone ramps, sledge lift, scaffolds and Shabti builders on the sphinx's faces (or turn individual ones off at first).
5. **Ending and menu:** capstone, crowning camera, and the save-slot showcase drawing the sphinx.
6. **Multiplayer:** clients learn the world is a Sphinx (lobby key or RPC) and build the same layout.

## What we're working with (checked on this PC)

| | |
|---|---|
| Engine | Unity **6000.4.10f1**, **Mono** scripting backend (`MonoBleedingEdge`, `Managed\Assembly-CSharp.dll`), so BepInEx + Harmony can be used |
| Developer | PattezGames, Steam app 5458210 |
| Networking | FishNet over Steam (Facepunch.Steamworks). The host is the server |
| Map | One scene (`level0`). The developer builds it with editor tools (`EconomyBuilder.BuildMap`, `Tools/Pyramid/Build/...`) |
| Saves | `%USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids` (one slot per world) |

### Game types that make up "the map"
- `Pyramid.Build.PyramidSite`: where the pyramid goes and its `PyramidDefinition`. It already switches between a "classic" and a "grand" definition and position (`grandPosition`), so moving the pyramid is something the game already does.
- `Pyramid.Data.PyramidDefinition` (ScriptableObject): shape, `baseSize`, `tileSize`, `cellSize`, layer values, milestones, `groundMask`.
- `Pyramid.World.BuildArea`: the buildable area and its keep-out zones (the "Can't build past the edge of the map" message comes from here).
- `Pyramid.World.RiverEdge` (BoxCollider `zone`, `bankLandZ`, `toWaterYaw`, `flowDir`): the river banks that bank machines snap to. `EnvironmentRiverSafeZ` is a related constant.
- `Pyramid.World.ClayPit` (BoxCollider `zone` + pile colliders): where players scoop clay.
- `Pyramid.Quay.NileQuay` / `NileTrader`: the trader dock and boat path (`berth`, `landPoint`).
- `Pyramid.World.MapDecorations` + `MapDecor` (byte `id`, `worldBounds`): decorations that can be removed. Which ones are removed is stored in the save by id.
- Environment: a Unity `Terrain`, plus BK Pure Nature (`BK_EnvironmentManager`) and COZY (`DistantLands.Cozy`) for weather and sky.

## The approach

Keep the game's own networked objects (FishNet scene objects) and move or reshape them. Swap only the parts that don't run game logic: terrain, art and props. Adding new networked objects is hard, because FishNet scene IDs have to match on every player's machine, so we avoid it.

The map is described in a data file (`maps/<name>.json`), so we can change it without recompiling:
```json
{
  "id": "delta-oasis", "version": 1,
  "pyramid":   { "position": [0,0,0], "baseSize": 26 },
  "river":     [{ "center": [..], "size": [..], "yaw": 90, "flowDir": 1 }],
  "clayPit":   { "center": [..], "size": [..] },
  "quay":      { "berth": [..], "land": [..] },
  "buildArea": { "center": [..], "size": [..], "keepOut": [..] },
  "spawn":     [..],
  "terrain":   { "heightmap": "delta-oasis.raw", "splat": "..." },
  "removeDecor": [3, 7, 12],
  "props":     [{ "bundle": "props", "asset": "PalmGrove", "pos": [..], "rot": [..] }]
}
```

## Phases

### Phase 0: Tools (about an evening)
1. Back up the game's Saves folder.
2. Install **BepInEx 5.4.23.x (x64, Mono)** in the game folder. First check it starts on Unity 6000.4; if it doesn't, use BepInEx 6 bleeding-edge (Mono).
3. Install the **.NET SDK** for building the plugin (it targets `netstandard2.1`/`net472` and references `Managed\*.dll` + `BepInEx.Harmony`).
4. Install **ILSpy / dnSpyEx** to read the game's code, and **UnityExplorer** (a fork that works with Unity 6) to inspect the running game: the scene tree, component values, and moving objects live.
5. Make a "hello world" plugin that logs on scene load. Phase 0 is done when it shows up in `BepInEx\LogOutput.log`.

### Phase 1: Explore (1–2 sessions)
- Use UnityExplorer to map out the `level0` hierarchy: the Terrain, the river mesh and water, RiverEdge zones, ClayPit, NileQuay plus the boat path, PyramidSite, BuildArea, the Bazaar and Shabti Market, player spawn, and MapDecorations.
- Use ILSpy to find the right **hook point**: something that runs after the scene loads but **before** FishNet starts the network and `WorldSave` restores the save. Likely candidates are `SceneManager.sceneLoaded`, a Harmony prefix on `WorldSave.OnEnable` / `PyramidSite.Awake`, or `MapDecorations.Awake`.
- Write down which objects are networked (have a `NetworkObject`) and which are only visuals.
- Have the plugin write the current layout to `maps/classic.json`. That's the reference to build new maps from.

### Phase 2: A rearranged map, the first version you can play (main work)
The plugin reads a map JSON and, at the hook point:
- moves PyramidSite and changes its definition (base size)
- moves and resizes the RiverEdge and ClayPit zones, and moves the river visuals to match
- moves NileQuay's berth and landing point, plus the trader boat route
- reshapes BuildArea and its keep-out zones
- flattens or reshapes the Terrain heightmap (`TerrainData.SetHeights`) and refreshes its colliders
- hides decorations and moves the player spawn

Phase 2 is done when the existing world, with the pyramid, river and clay pit moved, plays through a full loop: scoop, mold, haul, build a layer, trader visit, save and reload.

### Phase 3: A whole new map
- Build an **AssetBundle** in **Unity 6000.4.10f1** (it has to be the same version) with the new terrain, river/water mesh and props. Empty marker objects in it (`Marker_RiverEdge`, `Marker_ClayPit`, ...) say where gameplay zones go.
- When the game loads, hide the original environment's visual parts, load the bundle, and snap the game's existing gameplay objects onto the markers.
- Shaders: build with URP or HDRP to match the game (check in Phase 1), otherwise materials show up pink.

### Phase 4: Making it safe and shareable
- **Separate saves:** put modded-map worlds in a separate save folder (or tag them per slot) so a classic save never loads on a different map. Removed decorations and building positions are saved by id and world position.
- **Multiplayer:** every player needs the same mod and the same map file. Send a map hash in the lobby or connection data, and refuse to join if it doesn't match.
- **Rankings:** the game uploads results to an online leaderboard (`/v1/pyramids`, `maps=1`). Turn off uploads on modded maps so they don't mix with classic scores.
- Package it as a Thunderstore-style zip: `BepInEx/plugins/PyramidsMaps/*.dll` plus `maps/`.

## Risks
- **Every game update can break the mod.** The game patches often (0.23.x). Lock to one build, check `Application.version`, and switch off cleanly when the version doesn't match.
- Some positions may be baked in when the map is built (the bazaar, ramps, nav data for Shabti). Phase 1 will show how much.
- Unity 6 support in BepInEx and UnityExplorer is new, so check it first in Phase 0.
- Getting permission, or a mod API, from PattezGames would make all of this much easier. Worth asking in the Steam discussions in parallel.

## Second map: Sobek (planned 2026-11-21)

**Goal:** a crocodile lying in a pool, jaws open, built brick by brick like the Great Cat. It should push past what the cat did in three ways: water, a big overhang (the upper jaw), and a long, low, curving body.

### The design
- **Pose:** he lies in a pool, gaping: the lower jaw rests in the water and the head is tipped back with the upper jaw raised about 34°. The body lies low, legs splayed, and the tail curls round behind him in an S.
- **Details that make it read as a crocodile:** raised eyes and nostrils on top of the head, rows of ridge bumps (scutes) down the back and tail, teeth along both jaws, and splayed legs with the elbows out.
- **Size (first renders):** 19,538 blocks, 17 layers (21 m tall), about 130 m nose to tail on a 161 × 161 m grid, modelled then scaled by 1.3. Cells stay 1 × 1.25 m. Every block passes the reach check.
- **The climb is the jaws:** the body is easy, but to finish the upper jaw players stand in the mouth on the lower jaw and build upward and out. The gap between the jaws must be tall enough to walk in, and every tooth and the jaw tip must pass the 1.25 m reach rule.
- **The last brick:** the tip of the snout, or one eye. To decide when we see renders.

### The water (decided 2026-11-21: his whole body lies in an oval pool at the build site)
- **An oval pool, 92 × 144 m** (`SobekShape.InPool` / `PoolCells`): centred 7 m behind the site centre, about 10,400 m², world z −123 to 21 and x ±46. That's inside the play area (z −124) and short of the mold yard (z 32). His whole body is in it, at least 4 m from the shore (the closest point is the end of the tail curl). Water level 0.9 m, so the bottom layer of blocks is half in the water.
- **Rejected options:** a pool just around his front end; a pond that follows his outline; the full 168 m oval, which doesn't fit the classic map (it would need the Grand map's bigger play area, z −199 … 46, with the site at z −100).
- **How:** build a flat oval mesh and give it the river's own water material (`Environment/River`). The real river, the clay pit, the trader quay and the water wheels stay where they are and keep working. Remove the mesh when the world closes.
- **Shabti Work Posts:** the ground beside his base is now water. Either allow Work Posts in the shallow water, or only on him. To decide once we see how the water behaves in game.
- **To check in game:** whether the water material needs anything the river mesh has (a scrolling shader or a script), how wading looks and feels, and which machines may stand in the pool.

### Engineering
1. **Make the cat code general (no change in behaviour).** Most of `Sphinx/` isn't cat-specific: layout, any-order building, markers, footprint, Shabti and the ending work for any voxel shape. Move them behind a small map definition (id, title, text, shape version, `Build()`, last brick, extras such as the pool). The cat's id stays `sphinx` so existing worlds keep loading. Test the cat again after this step.
2. **Multiplayer sync byte:** today it's `0x40 | shape version`, which only says "cat". New layout: `0x40 | map index << 4 | version`. The cat stays map 0, so its byte is still 0x45 and 0.3.0 players can still join cat worlds. A 0.3.0 client joining a Sobek world sees a version mismatch and is told to update.
3. **`Shapes/SobekShape.cs`:** model it with smooth solids like the cat (ellipsoids and capsules, the tail as a chain of tapering capsules along a curve, jaws as wedges with a cut-out between them). Render it with `tools/ShapePreview`, run the reach check, and iterate on the look with you before it goes in game.
4. **Footprint and keep-out:** the cat used a rectangle around her body. The curved tail would make a rectangle block off a lot of empty ground, so use the per-cell footprint for the keep-out instead.
5. **The pool:** an oval mesh with the river's water material, removed on world close, plus rules for which machines may stand in it.
6. **Release 0.4.0:** both maps on the main menu, updated README, Nexus page and screenshots.

### Phases
| Phase | What | Done when |
|---|---|---|
| A | Shape and renders | You like the renders, and every block passes the reach check |
| B | General map code | The cat plays exactly as before |
| C | Sobek in game | You can build it by hand, Shabti work, and the ending fires |
| D | The pool | It looks right in game, and machines follow the pool rules |
| E | Release 0.4.0 | Published on GitHub and Nexus |

### Risks
- **The jaws:** the gap and the teeth may need reshaping to stay reachable, or a slightly closed mouth.
- **Water:** the river mesh may be tied to the river (a scrolling shader, or a script). A plain plane with the same material is the fallback.
- **A long thin shape** puts sections far from the centre, so check aim performance and Shabti paths around the tail.
