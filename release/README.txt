PYRAMIDS CUSTOM MAPS 0.3.0 — the Great Cat
For How They Built the Pyramids, game version 0.43.0
==========================================================

Instead of a pyramid, build a giant stone cat crouched to pounce: 24,052 blocks, 37.5 m tall,
in blocks half the size of a pyramid block. Building her is the whole objective, ending included.

INSTALL (everyone who plays together needs this, same version)
1. Download BepInEx 5.4.23.6 for Windows x64 (BepInEx_win_x64_5.4.23.6.zip) from the official page:
   https://github.com/BepInEx/BepInEx/releases
2. Find the game folder: in Steam, right-click How They Built the Pyramids > Manage > Browse local files.
3. Extract the BepInEx zip into that folder (winhttp.dll ends up next to Pyramid.exe).
4. Extract THIS zip into the same folder (it adds BepInEx\plugins\PyramidsMaps\PyramidsMaps.dll).
5. Start the game once and quit: BepInEx creates its folders and the settings file.

START A CAT WORLD (the host)
- On the main menu, scroll to the empty plot. Next to Pyramid and Grand Pyramid there's a Great Cat
  button: pick it (the plot shows the cat), then start a new world there.
- Pick Pyramid or Grand Pyramid to go back to normal worlds. The choice is remembered
  (it's the NewWorldMap setting in BepInEx\config\pyramidsmaps.custommaps.cfg).
- Worlds you already have are never changed.
- Friends joining a cat world see the cat automatically (they need the mod, same version).

HOW BUILDING THE CAT WORKS
- Every block is open from the start: build her in any order.
- A block must touch a block you've already laid (or sit on the ground layer): no floating bricks.
  Overhangs like her chin and belly grow outward from their neighbours.
- You can place a block up to one layer (1.25 m) above your feet, so stack on the bricks beside you,
  step up, and keep going.
- Small markers show the spots next to your bricks where you can build. F7 turns them on/off.
- Stone ramps, scaffold platforms and the sledge lift aren't sold in cat worlds (they only fit pyramids).
- Shabti builders: put a Work Post up ON the cat next to a brick crate; they build that level.
  A Work Post on the ground builds the bottom layer. Keep crates off spots that still need a block.
- The last brick is the tip of her left ear. Climb up together and crown her.
- Cat worlds stay off the game's online world rankings.

KEYS
- F7  build markers on/off
- F8  writes a placement report to BepInEx\LogOutput.log (for bug reports)

IF SOMETHING GOES WRONG
- Send BepInEx\LogOutput.log and %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\Player.log
- To remove the mod: delete BepInEx\plugins\PyramidsMaps. Cat worlds won't load correctly without it.

This is an unofficial fan mod, not made or supported by PattezGames.
