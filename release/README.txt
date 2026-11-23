PYRAMIDS CUSTOM MAPS: STATUES 0.7.0 — 7 maps, and your own
For How They Built the Pyramids, game version 0.43.0
==========================================================

Instead of the usual pyramid, build a giant statue or a famous monument, brick by brick. Building
it is the whole objective, ending included. Every block is open from the start: build in any order.
Statue blocks are half the size of a pyramid block, so the curves come out smooth.

THE MAPS
- GREAT CAT: a stone cat crouched to pounce. 24,052 blocks, 37.5 m tall.
- SOBEK: a giant crocodile lying in an oval pool, jaws open to the sky. 19,538 blocks, 21 m tall.
- ANUBIS: a jackal keeping watch from his shrine, ears high. 11,588 blocks.
- HORUS: a falcon with outstretched wings, wearing the double crown. 47,679 blocks, 100 m tall.
- GIZA: Khufu, Khafre and Menkaure, and the Great Sphinx. 63,213 blocks.
- COLOSSI: the Colossi of Memnon, two seated pharaohs 38 m tall. 10,776 blocks.
- SPHINX AVENUE: twelve sphinxes lining a road to a 37 m obelisk. 4,346 blocks.

WANT PYRAMIDS? The pyramid maps (Upside-Down, Diamond, Step, Bent, Nubian, Ziggurat, the Colossal
and the Million-Block Pyramid) are a separate mod since 0.7.0: Pyramids Custom Maps: More Pyramids,
https://www.nexusmods.com/howtheybuiltthepyramids/mods/7
Install both if you like: they work together, and the newer one runs every map of both.

INSTALL (everyone who plays together needs this, same version)
1. Download the BepInEx 5 Pack (BepInEx 5.4.23.6 for Windows x64) from Nexus:
   https://www.nexusmods.com/howtheybuiltthepyramids/mods/4
2. Find the game folder: in Steam, right-click How They Built the Pyramids > Manage > Browse local files.
3. Extract the BepInEx zip into that folder (winhttp.dll ends up next to Pyramid.exe).
4. Extract THIS zip into the same folder (it adds BepInEx\plugins\PyramidsMaps\PyramidsMaps.dll).
   Updating from an earlier version: just let it replace the old file. Your worlds keep working,
   pyramid-map worlds too (you need More Pyramids only to START new pyramid-map worlds).
5. Start the game once and quit: BepInEx creates its folders and the settings file.

START A CUSTOM MAP WORLD (the host)
- On the main menu, scroll to the empty plot. Next to Pyramid and Grand Pyramid there is a
  "Custom map..." dropdown: pick a map (the plot shows it), then start a new world there.
- Pick Pyramid or Grand Pyramid, or "Custom map..." itself, to go back to normal worlds. The choice is
  remembered (the NewWorldMap setting in BepInEx\config\pyramidsmaps.custommaps.cfg).
- Worlds you already have are never changed.
- Friends joining see the right map automatically (they need this mod or More Pyramids, same version).
- The first time you pick Horus in a session, the game may pause for a few seconds while it works
  out the shape.

HOW BUILDING WORKS
- Every block is open from the start: build it in any order.
- A block must touch a block you've already laid (or sit on the ground layer): no floating bricks.
  Overhangs grow outward from their neighbours.
- You can place a block about one layer above your feet, so stack on the bricks beside you,
  step up, and keep going.
- Small markers show the spots next to your bricks where you can build. F7 turns them on/off.
- Stone ramps, scaffold platforms and the sledge lift aren't sold on these maps (they only fit the
  game's pyramid).
- Shabti builders: put a Work Post up ON the statue next to a brick crate; they build that level.
  A Work Post on the ground builds the bottom layer. Keep crates off spots that still need a block.
- The highest block is the last brick: the cat's left ear, Sobek's snout, Khufu's tip at Giza,
  the obelisk on Sphinx Avenue, and so on. Climb up together and crown it.
- Custom map worlds stay off the game's online world rankings.

SOBEK lies in a shallow oval pool (0.6 m deep) dug around the build site. You can wade through it,
and machines can stand in the shallows. The hard part is his head: climb into the open mouth and
build the upper jaw up and out.

MAKE YOUR OWN MAP
- Put a MagicaVoxel model (.vox) in BepInEx\plugins\PyramidsMaps\maps and restart the game: it appears
  in the dropdown. That folder's README.txt (written on first run) explains the options.
- Everyone in a multiplayer game needs the same file.

KEYS
- F7  build markers on/off
- F8  writes a placement report to BepInEx\LogOutput.log (for bug reports)
- F11 free camera for screenshots (arrows move, PageUp/PageDown, Shift faster), F10 saves a clean shot

HONEST NOTE
Anubis, Horus, Giza, the Colossi, Sphinx Avenue and Sobek have been checked by the mod's own build
simulation and filled in game with a test tool, but nobody has built them all by hand yet. If a spot
can't be built, please report it (F8 report and the log).

IF SOMETHING GOES WRONG
- Send BepInEx\LogOutput.log and %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\Player.log
- To remove the mod: delete BepInEx\plugins\PyramidsMaps. Custom map worlds won't load correctly without it
  (or without More Pyramids).

This is an unofficial fan mod, not made or supported by PattezGames.
