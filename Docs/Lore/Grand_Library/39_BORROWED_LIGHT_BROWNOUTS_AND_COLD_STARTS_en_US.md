<!-- localization_status: source_authority_en_US -->
# BORROWED LIGHT, BROWNOUTS AND COLD STARTS

> **Source:** Deep Reach emergency power course, load priority sheet LP-3, cold start card CS-7, recovered bus traces, and the grease-pencil plate on panel P-63-2.  
> **Scope:** Load priority, brownout behaviour, cold starts, borrowed reserves, and power records used in routes, claims and survival decisions.  
> **Field use:** Read before trusting a lit room, bridging a breaker, waking a dead panel, opening a powered locker, or moving a cell that may be feeding something else.

---

## 1. A Lit Room Is A Load

Light proves a circuit is still spending. It proves nothing about the room.

Emergency reserves push lamp strips at 12 volts and 0.4 amps per metre, which almost any half-dead cell can manage. The door controller bolted beside that strip wants 24 volts and a 6-amp inrush to throw a lock, and it will not get either. Corridor lighting sits above the circulation pumps on the protected list, so a bright corridor can front a district where nothing is moving water. Workstations draw about 90 watts, wake, ask for a login, and die eleven seconds later with the keystrokes still in the buffer. A green status bead only ever reports that the bead's own circuit is alive.

Deep Reach wrote the order down and never revised it after 2141.

```text
LOAD PRIORITY SHEET LP-3 / SECTOR 44 / REV 4
1  Air exchange, scrubber banks 1-4............... protected, no shed
2  Pressure control and hull logic................ protected, no shed
3  Seal heaters, wet-side frames.................. shed at 84 pct bus
4  Refrigeration: sample, medical, morgue......... shed at 80 pct bus
5  Data: log retention and door history........... shed at 78 pct bus
6  Door motors (locks hold engaged on shed)....... shed at 74 pct bus
7  Lighting, work areas........................... shed at 70 pct bus
8  Lighting, corridor and habitat................. shed at 62 pct bus
9  Outlets, general and tool charging............. shed at 62 pct bus
```

Line 4 puts the morgue on the same shed step as the medicine, two steps above the doors and one above the door history. That was liability rather than cruelty. Remains warming in a sealed room generate a loss category Keelmark Mutual prices well above a stuck hatch.

After abandonment the sheet stopped describing the building. A room can read occupied because one line 8 circuit still holds, or dead because a line 1 load upstream has taken every cell it can reach. The question at a doorway is which line is paying for the light.

## 2. Brownout Order

A brownout is the sheet above, executed downward, at whatever speed the bus falls.

At 84 percent of nominal the seal heaters go. Log retention drops six points later, which is why so many Sector 44 door histories stop mid-cycle. At 74 the door motors stop while the locks stay engaged, because the locks fail secure by design and starvation is indistinguishable from procedure afterwards. Below 62 the outlets a repair crew was counting on are gone and the corridor is still lit.

Abandoned systems ignore the sheet. Salted contactors weld shut. Patched corridors back-feed panels that were supposed to die in 2147. A dead sensor keeps its warning lamp burning because the lamp draws 0.3 watts and the loop it reported on draws 40. Pumps run without reporting at all, because telemetry is line 5 and the pump is line 1.

Brownout order dates a room. Which loads died first shows whether a sample held below minus 40, whether a door was sealed by procedure or by starvation, whether a distress beacon still had power on the day the Keel marked it inactive, and whether somebody worked a breaker after the route log closed.

## 3. Cold Starts

Cold-starting a dead room is not the same as turning it on.

A cold start asks old machines to move after pressure, salt and forty-three years have changed their tolerances. Bearings wake dry. Contactors arc through mineral film. Battery stacks take charge unevenly, and one cell in twelve will sit 8 degrees above its neighbours. Fans throw settled dust, mould and chemical vapour into air that read breathable ninety seconds earlier. Safety logic then measures a wrecked compartment against thresholds written for a staffed colony and calls most of it suspect.

Sometimes the logic is right. Doors lock to protect a pressure state that stopped existing in 2147. Heaters soften a gasket whose only surviving virtue was being cold. Servers boot, fail, and overwrite the last useful crash record with the record of the failed boot. Pumps clear one compartment happily and push the water through a cracked tray into the compartment below.

Crews who come back wake a room in the order on the card.

```text
COLD START CARD CS-7 / ANNEX CLASS COMPARTMENT
STEP 1  Instruments only. Bus volts, hull pressure, water depth, gas.
STEP 2  Containment. Seal heaters, valve position, drain state.
STEP 3  Movement. Pumps first, then door motors, one at a time.
STEP 4  Comfort. Light, heat, outlets. Last, always.
NOTE    Any step taken out of order is written on this card with a time.
        The card leaves with the crew. It is the only record that exists.
```

The last two lines of CS-7 have kept more people out of arbitration than the first four have kept alive. One CS-7 card did not leave with its crew. It is still in Shallow Annex P-63, and it carries fourteen out-of-order entries in four different hands, the earliest dated 2151.

## 4. Borrowed Power

Borrowed power is power doing a job its label does not admit.

The colony is full of it: emergency cells cross-fed through patched corridors, drone chargers holding safe-room lamps up, a dead lab pulling trickle current off an antenna array, a medical freezer keeping one sample below minus 40 by starving six door motors of authority. Nothing here failed in clean islands. Loads went on bargaining with each other long after the people who understood the bargain had drowned.

Crews borrow because it turns a dead route into a paid one. Ninety seconds of bridged current is enough to wake a console and price a lot. Twenty amp-hours out of a portable cell will open a locker before the seal dries and salt-welds itself shut. Four minutes of sump pump is often the whole difference between a route and a swim.

That same bridge drains the last reserve under a witness beacon, erases the gap in a power log that proved when a compartment went dark, or drops a safe door closed with the medicine on the wrong side. Black Keel auditors like borrowed power when it raises recoverable value and dislike it when the new power path explains why their old denial was false.

## 5. Breaker Rooms

A breaker room is a map with burn marks.

Deep Reach labels hold until they do not. A breaker marked `Hab Lighting B` may feed a pump after three emergency patches. Taped handles usually hide a jury-rigged life-support cross-feed. A clean breaker in a filthy room means somebody worked it after the flood, and a warm breaker in a cold annex deserves attention before the door beside it does.

Plate notes favour facts that check in seconds: handle position, bus temperature, salt in the hinge, smell at the contactor, which loads flicker when the clamps bite. Long explanations get people killed in breaker rooms.

```text
PANEL P-63-2 / GREASE PENCIL / FOUR HANDS
B1  feeds clinic lock. trips at 11 A. do not bridge during pump cycle
B2  marked Hab Lighting B. IS the level 2 sump. patched 2147
B3  dead. bus cold. hinge packed with salt
B4  taped. cross-feed to scrubber 3. leave it taped
B5  warm at rest. do not touch until B4 is proven
B6  ours. 2190. runs the charger. pull it when we leave
```

Line B2 is worth more than the panel schedule bolted beside it. The printed schedule still reads `Hab Lighting B`, and it has read that since 2141.

## 6. Power As Evidence

Power records prove sequence when rooms cannot.

```text
VOLTAGE TRACE / SECTOR 44 BUS 2 / 2147-05-22 (recovered fragment)
02:41   98 pct   nominal
03:16   84 pct   seal heaters shed
03:52   79 pct   log retention shed -- DOOR HISTORY ENDS HERE
06:20     --     no data
06:31   71 pct   door motor draw, bay 3, 4.2 s
06:34   62 pct   corridor lighting shed
```

Log retention shed at 03:52. That is why the door history stops there, and why the filed report can say bay 3 was sealed before the water came. The trace kept running without it. At 06:31, two hours and thirty-nine minutes after the record the report depends on, something drew a door motor for 4.2 seconds, and a door motor at 71 percent bus does not throw itself.

Other traces do other work. A charge curve puts a portable cell on the wrong side of a custody seal. Refrigeration that held below minus 40 through the whole outage is what makes a sample worth assaying at all. An outage missing from a trace means either an edited archive or a load fed from a line nobody listed.

Power creates liability in the other direction too. Restoring light puts a crew on a receiver log. Keeping a pump alive can destroy a terminal record. Stealing a cell turns a quiet safe room into a dead one and leaves the invoice in the voltage trace for whoever arrives next.

## 7. Field Rule

Find the load before trusting the light.

Before bridging anything, name what will lose power.

Wake the instruments first. They are the only part of a dead room that can say when to stop.

Darkness is not proof of emptiness and light is not proof of safety. Both are power states with an owner, a cost and a record. The plate on P-63-2 is in its fourth hand, and every hand added lines rather than erasing the one before it.
