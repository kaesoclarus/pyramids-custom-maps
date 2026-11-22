# Map ideas

Ideas for future maps. Each one replaces the pyramid with something else to build brick by brick. Anything that is one solid shape can reuse nearly all of the Great Cat's code: any-order building, markers, Shabti builders, the footprint fixes and the ending.

| Map | Status | What it is | What's new or hard about it |
|---|---|---|---|
| **The Great Cat** | Released, 0.3.0 | A giant cat crouched to pounce | — |
| **Sobek** (crocodile) | Released, 0.4.0 (not yet playtested), see [`PLAN.md`](../PLAN.md#second-map-sobek-planned-2026-11-21) | A crocodile coming out of the water, jaws open | A water pool, open jaws (a big overhang), a long low body with a curved tail |
| **Anubis** (jackal) | In progress (shape done 2026-11-22) | A jackal lying on a shrine chest, ears up | A tall box chest can't be climbed with the 1.25 m reach, so it has a stairway up the front |
| **Horus** (falcon) | In progress (shape done 2026-11-22) | A falcon with outstretched wings and the double crown, 100 m tall, 47,679 blocks | Huge thin wings, and tall: needs the reach check's "step left open" mode. The last free multiplayer map index (3) |
| **Upside-Down** pyramid | In progress (shape done 2026-11-22) | The classic pyramid's own 23,426 blocks balanced on its tip, plus a capstone on the plaza on top | Every layer overhangs. Ground under the overhang stays usable (only the 14 m square around the tip counts as the statue) |
| **Upside-Down Grand** | In progress (shape done 2026-11-22) | The Grand Pyramid's own 105,995 blocks on its tip, plus a capstone: a 170 m plaza 107.5 m up | Its plaza reaches a little past the normal play area, so the play area widens and the boundary walls come down (`MapEdges`) |
| **Diamond** | In progress (shape done 2026-11-22) | An upside-down pyramid with a pyramid on its plaza: an octahedron on its point, 44,251 blocks, 127.5 m | The last free multiplayer map index (7); the top point is the last brick |
| **Step Pyramid** (Djoser) | In progress (2026-11-22) | Six 10 m tiers on a 122 × 110 m base, 38,921 blocks | Straight tier walls: needs the step-left-open building |
| **Bent Pyramid** | In progress (2026-11-22) | 51° below, about 43° above, 38,235 blocks | The slope change uses 1, 1, 2 blocks in per layer |
| **Nubian Field** | In progress (2026-11-22) | A dozen steep (about 68°) small pyramids, 7,684 blocks | Separate pieces in one map; the tallest is the last brick |
| **Ziggurat** | In progress (2026-11-22) | Three terraces, central and side stairways, shrine, 45 m, 11,943 blocks | Stairways built into the shape |
| **Giza** | In progress (2026-11-22) | Khufu, Khafre and Menkaure on a diagonal, the Great Sphinx in front of Khafre, 63,213 blocks | Four separate monuments in one map; statue blocks so the Sphinx has detail |
| **Colossi** of Memnon | In progress (2026-11-22) | Two seated pharaohs on thrones, 38 m, 10,776 blocks | The right one's sun disc makes the last brick unambiguous |
| **Sphinx Avenue** | In progress (2026-11-22) | Twelve small sphinxes lining a road to an obelisk, 4,346 blocks | Pieces turned a quarter to face the road; a thin 37 m obelisk to climb |
| **Scarab** | Idea | A beetle holding up a sun disc | The disc is a thin overhang held up by the legs, so it tests the no-floating-bricks rule hardest |

## Other ideas from the first brainstorm

- **Non-statues:** a stepped ziggurat, an obelisk field, a temple with columns and a roof. These need new work, because a roof over open space brings back floating bricks.
- **Just for fun:** a giant hand rising from the sand.
