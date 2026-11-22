PYRAMIDS CUSTOM MAPS 0.4.0 — the Great Cat and Sobek
For How They Built the Pyramids, game version 0.43.0
==========================================================

Instead of a pyramid, build a giant statue, brick by brick. Building it is the whole objective,
ending included. Both statues use blocks half the size of a pyramid block, so their curves come out smooth.

- THE GREAT CAT: a stone cat crouched to pounce. 24,052 blocks, 37.5 m tall.
- SOBEK (new in 0.4.0): a giant crocodile lying in an oval pool, jaws open to the sky.
  19,538 blocks, 21 m tall, about 130 m nose to tail.

INSTALL (everyone who plays together needs this, same version)
1. Download BepInEx 5.4.23.6 for Windows x64 (BepInEx_win_x64_5.4.23.6.zip) from the official page:
   https://github.com/BepInEx/BepInEx/releases
2. Find the game folder: in Steam, right-click How They Built the Pyramids > Manage > Browse local files.
3. Extract the BepInEx zip into that folder (winhttp.dll ends up next to Pyramid.exe).
4. Extract THIS zip into the same folder (it adds BepInEx\plugins\PyramidsMaps\PyramidsMaps.dll).
   Updating from 0.3.0: just let it replace the old file. Your cat worlds keep working.
5. Start the game once and quit: BepInEx creates its folders and the settings file.

START A STATUE WORLD (the host)
- On the main menu, scroll to the empty plot. Next to Pyramid and Grand Pyramid there are Great Cat and
  Sobek buttons: pick one (the plot shows the statue), then start a new world there.
- Pick Pyramid or Grand Pyramid to go back to normal worlds. The choice is remembered
  (it's the NewWorldMap setting in BepInEx\config\pyramidsmaps.custommaps.cfg).
- Worlds you already have are never changed.
- Friends joining a statue world see the statue automatically (they need the mod, same version).

HOW BUILDING A STATUE WORKS
- Every block is open from the start: build it in any order.
- A block must touch a block you've already laid (or sit on the ground layer): no floating bricks.
  Overhangs like the cat's chin or Sobek's raised upper jaw grow outward from their neighbours.
- You can place a block up to one layer (1.25 m) above your feet, so stack on the bricks beside you,
  step up, and keep going.
- Small markers show the spots next to your bricks where you can build. F7 turns them on/off.
- Stone ramps, scaffold platforms and the sledge lift aren't sold in statue worlds (they only fit pyramids).
- Shabti builders: put a Work Post up ON the statue next to a brick crate; they build that level.
  A Work Post on the ground builds the bottom layer. Keep crates off spots that still need a block.
- Statue worlds stay off the game's online world rankings.

THE GREAT CAT
- The last brick is the tip of her left ear. Climb up together and crown her.

SOBEK
- He lies in a shallow oval pool (0.6 m deep) dug around the build site. You can wade through it,
  and machines can stand in the shallows.
- The hard part is his head: climb into the open mouth and build the upper jaw up and out.
- The last brick is the tip of his raised snout, the highest point of the statue.
- Sobek is new and hasn't had a full playthrough yet. If a spot can't be built, please report it (F8 report).

KEYS
- F7  build markers on/off
- F8  writes a placement report to BepInEx\LogOutput.log (for bug reports)

IF SOMETHING GOES WRONG
- Send BepInEx\LogOutput.log and %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\Player.log
- To remove the mod: delete BepInEx\plugins\PyramidsMaps. Statue worlds won't load correctly without it.

This is an unofficial fan mod, not made or supported by PattezGames.
