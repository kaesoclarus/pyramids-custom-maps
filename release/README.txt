PYRAMIDS CUSTOM MAPS 0.6.0 — 16 maps, and your own
For How They Built the Pyramids, game version 0.43.0
==========================================================

Instead of the usual pyramid, build a giant statue, a famous monument or a strange pyramid,
brick by brick. Building it is the whole objective, ending included. Every block is open from
the start: build in any order.

STATUES (blocks half the size of a pyramid block, so their curves come out smooth)
- GREAT CAT: a stone cat crouched to pounce. 24,052 blocks, 37.5 m tall.
- SOBEK: a giant crocodile lying in an oval pool, jaws open to the sky. 19,538 blocks, 21 m tall.
- ANUBIS (new): a jackal keeping watch from his shrine, ears high. 11,588 blocks.
- HORUS (new): a falcon with outstretched wings, wearing the double crown. 47,679 blocks, 100 m tall.

PYRAMIDS AND MONUMENTS
- UPSIDE-DOWN: the classic pyramid balanced on its tip; crown the plaza 65 m up. 23,427 blocks.
- UPSIDE-DOWN GRAND: the Grand Pyramid on its tip; a 170 m plaza 107 m in the air. 105,996 blocks.
- DIAMOND: an upside-down pyramid with a pyramid on its plaza, 127 m tall. 44,251 blocks.
- STEP PYRAMID: Djoser's six tiers, 60 m on a 122 m base. 38,921 blocks.
- BENT PYRAMID: steep below, gentler above. 38,235 blocks.
- NUBIAN FIELD: a dozen steep Nubian pyramids, like those at Meroe; finish on the tallest. 7,684 blocks.
- ZIGGURAT: three terraces, a grand stairway and a shrine, 45 m high. 11,943 blocks.
- GIZA: Khufu, Khafre and Menkaure, and the Great Sphinx. 63,213 blocks.
- COLOSSI: the Colossi of Memnon, two seated pharaohs 38 m tall. 10,776 blocks.
- SPHINX AVENUE: twelve sphinxes lining a road to a 37 m obelisk. 4,346 blocks.
- COLOSSAL PYRAMID: the pyramid at about 4.5 times the Grand Pyramid, 178 m tall. 477,191 blocks.
- MILLION-BLOCK PYRAMID (new in 0.6.0): the pyramid with over a million blocks, about 9.5 times the
  Grand Pyramid, 227 m tall on a 362 m base. 1,004,731 blocks. The longest build there is.

INSTALL (everyone who plays together needs this, same version)
1. Download the BepInEx 5 Pack (BepInEx 5.4.23.6 for Windows x64) from Nexus:
   https://www.nexusmods.com/howtheybuiltthepyramids/mods/4
2. Find the game folder: in Steam, right-click How They Built the Pyramids > Manage > Browse local files.
3. Extract the BepInEx zip into that folder (winhttp.dll ends up next to Pyramid.exe).
4. Extract THIS zip into the same folder (it adds BepInEx\plugins\PyramidsMaps\PyramidsMaps.dll).
   Updating from an earlier version: just let it replace the old file. Your worlds keep working.
5. Start the game once and quit: BepInEx creates its folders and the settings file.

START A CUSTOM MAP WORLD (the host)
- On the main menu, scroll to the empty plot. Next to Pyramid and Grand Pyramid there is a
  "Custom map..." dropdown: pick a map (the plot shows it), then start a new world there.
- Pick Pyramid or Grand Pyramid, or "Custom map..." itself, to go back to normal worlds. The choice is
  remembered (the NewWorldMap setting in BepInEx\config\pyramidsmaps.custommaps.cfg).
- Worlds you already have are never changed.
- Friends joining see the right map automatically (they need the mod, same version).
- The first time you pick Horus or Upside-Down Grand in a session, the game may pause for a few
  seconds while it works out the shape.

HOW BUILDING WORKS
- Every block is open from the start: build it in any order.
- A block must touch a block you've already laid (or sit on the ground layer): no floating bricks.
  Overhangs grow outward from their neighbours.
- You can place a block about one layer above your feet, so stack on the bricks beside you,
  step up, and keep going.
- Small markers show the spots next to your bricks where you can build. F7 turns them on/off.
- Stone ramps, scaffold platforms and the sledge lift aren't sold on custom maps (they only fit the
  game's pyramid), except on the Colossal and Million-Block Pyramids, which are the game's own
  pyramid made bigger. There, the lift's top Height level climbs every layer (71 and 91; the Grand
  Pyramid's is 43).
- Shabti builders: put a Work Post up ON the build next to a brick crate; they build that level.
  A Work Post on the ground builds the bottom layer. Keep crates off spots that still need a block.
- The highest block is the last brick: the cat's left ear, Sobek's snout, Khufu's tip at Giza,
  the obelisk on Sphinx Avenue, and so on. Climb up together and crown it.
- Custom map worlds stay off the game's online world rankings.

A FEW MAPS IN DETAIL
- SOBEK lies in a shallow oval pool (0.6 m deep) dug around the build site. You can wade through it,
  and machines can stand in the shallows. The hard part is his head: climb into the open mouth and
  build the upper jaw up and out.
- THE UPSIDE-DOWN MAPS start from a single point and widen as you climb: the ground under the
  overhang is kept clear so machines can still work there.
- THE COLOSSAL PYRAMID is moved south onto a cleared plain, and the play area is opened up around it.
  It's a very long build; a finished one still runs smoothly and saves small.
- THE MILLION-BLOCK PYRAMID is too big for the map, so the desert is extended south with new dunes.
  The game may freeze for several seconds the first time the menu shows it, and again while the
  world loads: give it time. A finished one still runs at a playable frame rate and saves small.

MAKE YOUR OWN MAP
- Put a MagicaVoxel model (.vox) in BepInEx\plugins\PyramidsMaps\maps and restart the game: it appears
  in the dropdown. That folder's README.txt (written on first run) explains the options.
- Everyone in a multiplayer game needs the same file.

KEYS
- F7  build markers on/off
- F8  writes a placement report to BepInEx\LogOutput.log (for bug reports)
- F11 free camera for screenshots (arrows move, PageUp/PageDown, Shift faster), F10 saves a clean shot

HONEST NOTE
The new maps in 0.5.0, and Sobek, have been checked by the mod's own build simulation and filled in
game with a test tool, but nobody has built them all by hand yet. If a spot can't be built, please
report it (F8 report and the log).

IF SOMETHING GOES WRONG
- Send BepInEx\LogOutput.log and %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\Player.log
- To remove the mod: delete BepInEx\plugins\PyramidsMaps. Custom map worlds won't load correctly without it.

This is an unofficial fan mod, not made or supported by PattezGames.
