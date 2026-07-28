<!-- localization_status: source_authority_en_US -->
# COMMUNICATIONS, TELEMETRY, AND ORBITAL SILENCE

> **Source:** Black Keel communications watch manual, revision 9. Packet desk log extracts, relay roster, and Marauder plate annotations. Contractor mirror, shelf copy RAN-B:H8 / COMM-09.  
> **Scope:** Timing, payload limits, relay custody, queue behaviour, and the difference between an answer and a receipt.  
> **Reader note:** There is no faster-than-light channel. Every figure printed here was measured on this moon, and the figures are the argument.

---

## 1. The Card On The Bulkhead

Watch card COMM-09/A is printed on plate and bolted beside the packet desk. It settles most arguments in eight lines.

```text
COMMUNICATIONS WATCH CARD COMM-09/A          BLACK KEEL / PACKET DESK
ONE-WAY LEG, SUIT TO SURFACE BUOY...: 2.1 s from 3,200 m (1,510 m/s)
ONE-WAY LEG, BUOY TO HULL...........: 0.008 s
RELAY HOLD, SPINE 0-100 m...........: 90 s slot interval
RELAY HOLD, 100-1,500 m.............: 240 s slot interval
RELAY HOLD, BELOW 2,500 m...........: 900 s slot interval
KEEL RECEIVE WINDOW.................: 11 min 40 s every 3 h 14 min
PAYLOAD CAP, NOTARISED PACKET.......: 480 bytes
CORE QUERY ROUND TRIP, SOL..........: 21 years
```

The water leg is two seconds. Nobody has died of the two seconds. Everything below that line is scheduling, and scheduling is where the years are kept.

A distress packet leaving a suit at 3,400 m clears the water in about two seconds, waits up to fifteen minutes for a relay slot, waits up to three hours and two minutes for the Keel to come back over the horizon, and then enters a queue. The card does not print the queue.

*[Margin Note: The contract clause is "continuous operational awareness". Continuous modifies the awareness. It does not modify the answering.]*

## 2. What The Water Does To A Carrier

The survey put numbers on the failures, which is more than the brochure did.

At 2 MHz the useful radio range in HECTON-8 brine is under 3 m. At 30 kHz it is about 40 m in clean water and 6 m through the Cable Reef, where trunk mass, repair clamps and conductive biofilm all load the field. Optical links hold 4 Mbit/s across a clean 200 m sightline and collapse to nothing in a particulate bloom; the basin gives a clean 200 m sightline perhaps two days in nine. Magnetic induction works at contact range only: a suit handshake, a docked tool, a hatch plate. Nothing has reached orbit from below 1,000 m except low-frequency acoustics.

Acoustics carry, and charge for it in certainty. The thermocline at 340 m bends ray paths downward. The brine layer near 1,480 m reflects most of what hits it at a shallow angle. Machinery still running in the factory levels raises the noise floor by 14 dB inside a 200 m radius, and a density boundary can throw a packet sideways far enough that the receiver's bearing solution places the sender 600 m east of where the sender is standing.

`Blackout` is the wrong word for what the desk logs. Three consecutive lines from the same window:

```text
PKT 44-9-0771  RX 04:12:18  pressure alarm; sector tag present; route field null
PKT 44-9-0774  RX 04:19:02  distress code valid; coordinate checksum FAIL
PKT 44-9-0662  RX 04:31:55  duplicate (first RX 19 d prior); suppressed per rule 6
```

0771 reports that something is wrong and cannot say where. 0774 says where and cannot prove it. 0662 was nineteen days old, and the desk suppressed it correctly under rule 6; the crew that sent the original had re-entered the flooded compartment it described eleven days before the copy arrived.

## 3. What Fits In 480 Bytes

The channel is 1.1 kHz centre, 340 bit/s on the spine, 40 bit/s below 2,500 m. At 40 bit/s a full packet takes 96 seconds to leave the suit, and the suit cannot listen while it sends.

The 480 bytes buy a status code, a suit pressure figure, a route tag, a manifest hash, a claim signature, one evidence flag, and 60 characters of free text. They do not buy a helmet feed, a conversation, or the description of a compartment that has become complicated. Crews who come back prepare their tags before the compartment becomes complicated.

Admissibility is a separate matter from arrival. A packet the Keel will later treat as a record carries a notary block, applied at the first relay still holding a valid key:

```text
NOTARY BLOCK / PACKET NOTARY INTERFACE
PKT.........: 44-9-0774
ORIGIN KEY..: suit 44-S-311, Class-IV, solvency current
FIRST RELAY.: R-19, spine mast 4, key valid to 2191
HASH........: 8c4f 21b0 (truncated on card)
CUSTODY.....: Aegir Reclamation Pool
ADMISSIBLE..: yes
```

An unnotarised packet still arrives, still gets read by a human at the desk, and is not a record. Deck shorthand for one of those is a shout. Packet 44-9-0774 was fully admissible; its free-text field read `H16 dogs binding do not send 2nd crew`, thirty-seven characters, in a field the schema stores and does not index.

*[Margin Note: The manual says send the distress code. It does not say what to do with the 900 seconds after you send it.]*

## 4. The Relay Chain And Its Ghosts

Deep Reach never relied on one transmitter. It built layers, and the layers aged unevenly.

Upper routes used buoy masts, service pylons, tether nodes and platform repeaters. The Cable Reef became a dense communication skeleton of power trunks, data umbilicals, repair clamps and relay housings, most of it under biofilm that still conducts when the right voltage reaches it. Deeper systems used acoustic pingers, maintenance caches, pressure-rated memory spools and route beacons that hold a message until a receiver passes close enough to take it.

```text
RELAY ROSTER EXTRACT / RAN-B:H8 / rev 9
ID     DEPTH     LAST FORWARD   KEY OWNER                   STATE
R-04      40 m   2190 current   Aegir Reclamation Pool      forwards
R-19      90 m   2190 current   Aegir Reclamation Pool      forwards
R-31     410 m   2147-06        Deep Reach Sector 44        accepts, holds
R-44   1,180 m   2148-02        Atlas continuity            forwards inward
R-58   2,510 m   2147-05        unassigned                  answers, no path
R-63   3,340 m   --             Recovery Compliance Office  spool only
```

R-31 has accepted 4,700 packets since 2147 and forwarded none. Its spool filled in the first fortnight and the acknowledgement it still returns is genuine, correctly formed, and worth nothing. R-44 forwards, but inward, to Atlas continuity logic rather than to the packet desk. R-58 is the expensive one: it answers on the correct carrier with a route table from 2147, which is how a crew ends up trusting a coordinate that predates four collapses. R-63 stores until a receiver comes within 200 m, and the Return Action Queue has an open item asking for whatever is on it.

A relay proves a path. Something that answers proves only that a battery and a key survived. The difference is one column on the roster and roughly six hours of a crew's air.

## 5. The Black Keel Listening Regime

The Black Keel does listen. That is not the same as answering.

As a claim tender, the Keel prioritizes custody events: manifest upload, material proof, contractor identity, route state, suit solvency, recoverable evidence, and signals that affect liability. It acknowledges what the system can price. It escalates what might damage the claim structure. It records more than it comforts.

There are human watch officers aboard, but they do not sit in a drama channel waiting to save one diver. They handle windows, queues, corrupted packet review, arbitration holds, security flags, and the constant work of proving that the Keel responded according to policy. A watch officer may care. The queue does not. Policy is where caring goes to become admissible or useless.

Deep Reach called this discipline "orbital silence" during active claim periods. The term sounded like operational security. In practice it meant the tender would avoid initiating unnecessary contact, would prefer receipts over conversation, and would treat unstructured speech as a liability source.

That is why a Marauder can scream into a channel and receive only a clean acknowledgement number.

*[Margin Note: The Keel heard you. That was never the question.]*

## 6. Failure Paths

Communication failures on HECTON-8 rarely arrive as a single red light.

A packet queue can fill while a crew thinks the relay is transmitting. A suit can resend the same pressure warning until the receiver suppresses it as duplicate noise. A relay can be physically present but keyed to an old custody owner. A route beacon can wake after a power surge and overwrite a newer map with a pre-Tide path. A watch system can quarantine a message because an evidence flag, debt flag, and distress flag arrived in the wrong order.

Bad data is not always silence. Sometimes bad data is confidence.

The most dangerous failures are stale handles: old contact IDs, old relay trust, old route names, old authorization stamps. A diver thinks they are speaking to the Black Keel. The packet is really bouncing through a local cache that has not seen orbit in twenty years. A crew follows a reply that was valid before a fault lip moved. A salvage manifest reaches custody, but the attached plea for help drops because it is not part of the accepted schema.

This is why crews mark their own routes and keep physical proofs. Paint on a hatch can outlive a relay account. A tied line can outrank a clean coordinate. A body tag can carry a truth that telemetry refused to classify.

## 7. Isolation As Player Pressure

Isolation should not feel like a lore excuse. It should feel like a pressure system.

The player can receive pings, fragments, receipts, delayed warnings, corrupted messages, old route ghosts, Black Keel acknowledgements, Atlas-local replies, and crew-made marks. None of them should feel like a perfect narrator. Every signal asks for judgment. Who sent it? When? Through what relay? What does it omit? Who benefits if the player trusts it?

This gives the setting a specific loneliness. The player is not alone because the universe forgot them. The player is alone because the available systems can see parts of them and still fail to become help.

A working comm link can be more frightening than a dead one. A dead link tells the truth clearly. A working link can tell you your oxygen warning was received, your claim remains active, your upload is pending, and no rescue entitlement is implied.

That is HECTON-8's silence. Not the absence of sound. The presence of systems that heard enough to bill the moment, but not enough to save it.
