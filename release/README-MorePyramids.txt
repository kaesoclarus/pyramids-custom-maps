PYRAMIDS CUSTOM MAPS: MORE PYRAMIDS 0.7.1 — 9 pyramids, and your own
For How They Built the Pyramids, game version 0.43.0
==========================================================

Instead of the usual pyramid, build a different one: upside down, stepped, bent, a whole field of
them, or one with over a million blocks. Built from the game's own pyramid blocks, brick by brick.
Building it is the whole objective, ending included.

THE MAPS
- UPSIDE-DOWN: the classic pyramid balanced on its tip; crown the plaza 65 m up. 23,427 blocks.
- UPSIDE-DOWN GRAND: the Grand Pyramid on its tip; a 170 m plaza 107 m in the air. 105,996 blocks.
- DIAMOND: an upside-down pyramid with a pyramid on its plaza, 127 m tall. 44,251 blocks.
- STEP PYRAMID: Djoser's six tiers, 60 m on a 122 m base. 38,921 blocks.
- BENT PYRAMID: steep below, gentler above. 38,235 blocks.
- NUBIAN FIELD: a dozen steep Nubian pyramids, like those at Meroe; finish on the tallest. 7,684 blocks.
- ZIGGURAT: three terraces, a grand stairway and a shrine, 45 m high. 11,943 blocks.
- COLOSSAL PYRAMID: the pyramid at about 4.5 times the Grand Pyramid, 178 m tall. 477,191 blocks.
- MILLION-BLOCK PYRAMID: the pyramid with over a million blocks, about 9.5 times the Grand Pyramid,
  227 m tall on a 362 m base. 1,004,731 blocks. The longest build there is.

WANT STATUES? The statue maps (the Great Cat, Sobek, Anubis, Horus, Giza, the Colossi and Sphinx
Avenue) are the sister mod, Pyramids Custom Maps: Statues,
https://www.nexusmods.com/howtheybuiltthepyramids/mods/3
Install both if you like: they work together, and the newer one runs every map of both.

INSTALL (everyone who plays together needs this, same version)
1. Download the BepInEx 5 Pack (BepInEx 5.4.23.6 for Windows x64) from Nexus:
   https://www.nexusmods.com/howtheybuiltthepyramids/mods/4
2. Find the game folder: in Steam, right-click How They Built the Pyramids > Manage > Browse local files.
3. Extract the BepInEx zip into that folder (winhttp.dll ends up next to Pyramid.exe).
4. Extract THIS zip into the same folder (it adds BepInEx\plugins\PyramidsMorePyramids\PyramidsMorePyramids.dll).
   These maps used to come with Pyramids Custom Maps 0.6.0 and earlier: worlds you started there keep working.
5. Start the game once and quit: BepInEx creates its folders and the settings file.

START A CUSTOM MAP WORLD (the host)
- On the main menu, scroll to the empty plot. Next to Pyramid and Grand Pyramid there is a
  "Custom map..." dropdown: pick a map (the plot shows it), then start a new world there.
- Pick Pyramid or Grand Pyramid, or "Custom map..." itself, to go back to normal worlds. The choice is
  remembered (the NewWorldMap setting in BepInEx\config\pyramidsmaps.morepyramids.cfg).
- Worlds you already have are never changed.
- Friends joining see the right map automatically (they need this mod or Statues, same version).
- The first time you pick Upside-Down Grand in a session, the game may pause for a few seconds while
  it works out the shape.

HOW BUILDING WORKS
- Every block is open from the start: build it in any order.
- A block must touch a block you've already laid (or sit on the ground layer): no floating bricks.
  Overhangs grow outward from their neighbours.
- You can place a block about one layer above your feet, so stack on the bricks beside you,
  step up, and keep going.
- Small markers show the spots next to your bricks where you can build. F7 turns them on/off.
- Stone ramps, scaffold platforms and the sledge lift aren't sold on most of these maps (they only fit
  the game's pyramid), except on the Colossal and Million-Block Pyramids, which are the game's own
  pyramid made bigger. There, the lift's top Height level climbs every layer (71 and 91; the Grand
  Pyramid's is 43).
- Shabti builders: put a Work Post up ON the build next to a brick crate; they build that level.
  A Work Post on the ground builds the bottom layer. Keep crates off spots that still need a block.
- An empty spot that gets walled in (blocks on every side, above and below, so nothing is left to
  place it against) fills itself in, so the build can always be finished.
- The highest block is the last brick. Climb up together and crown it.
- Custom map worlds stay off the game's online world rankings.

A FEW MAPS IN DETAIL
- THE UPSIDE-DOWN MAPS start from a single point and widen as you climb: the ground under the
  overhang is kept clear so machines can still work there.
- THE COLOSSAL PYRAMID is moved south onto a cleared plain, and the play area is opened up around it.
  It's a very long build; a finished one still runs smoothly and saves small.
- THE MILLION-BLOCK PYRAMID is too big for the map, so the desert is extended south with new dunes.
  The game may freeze for several seconds the first time the menu shows it, and again while the
  world loads: give it time. A finished one still runs at a playable frame rate and saves small.

MAKE YOUR OWN MAP
- Put a MagicaVoxel model (.vox) in BepInEx\plugins\PyramidsMorePyramids\maps and restart the game: it
  appears in the dropdown. That folder's README.txt (written on first run) explains the options; add
  blocks=pyramid in a <name>.txt next to it to use the game's pyramid blocks.
- Everyone in a multiplayer game needs the same file.

KEYS
- F7  build markers on/off
- F8  writes a placement report to BepInEx\LogOutput.log (for bug reports)
- F11 free camera for screenshots (arrows move, PageUp/PageDown, Shift faster), F10 saves a clean shot

HONEST NOTE
Every map has been checked by the mod's own build simulation and filled in game with a test tool,
but nobody has built them all by hand yet. If a spot can't be built, please report it (F8 report and
the log).

IF SOMETHING GOES WRONG
- Send BepInEx\LogOutput.log and %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\Player.log
- To remove the mod: delete BepInEx\plugins\PyramidsMorePyramids. Custom map worlds won't load correctly
  without it (or without Statues).

This is an unofficial fan mod, not made or supported by PattezGames.
