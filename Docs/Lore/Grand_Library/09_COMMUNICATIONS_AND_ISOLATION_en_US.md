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

## 5. The Queue, And What Comes Back Out Of It

The Keel listens through the whole orbit. It answers inside the window, by receipt, from one queue.

```text
RETURN ACTION QUEUE / WINDOW 2190-07-14 / 11:40 USABLE
ADMITTED THIS WINDOW......: 214 packets
QUEUE DEPTH...............: 1,140 items
MEDIAN AGE AT DISPOSITION.: 31 h
PRIORITISED...............: manifest upload, material proof, contractor
                            identity, route state, suit solvency
UNSTRUCTURED TEXT.........: logged, not queued
```

`Logged, not queued` is four words doing the work of an entire policy. What a crew gets back looks like this:

```text
BLACK KEEL RECEIPT 2190-07-14 / RAQ-0881
YOUR PACKET..........: 44-9-0774 received, notary valid
CLAIM STATUS.........: active
UPLOAD...............: pending
OXYGEN ADVISORY......: received
RECOVERY ENTITLEMENT.: none implied by this acknowledgement
NEXT WINDOW..........: 14:54
```

Every line on that receipt is true. There are watch officers aboard and some of them are decent; an officer who wants to help a crew at 3,340 m can move one item up a queue of 1,140 and cannot make the moon four hours smaller. Deep Reach called the discipline `orbital silence` during active claim periods and sold it as operational security. What it meant in the watch order was that the tender does not initiate, prefers receipts to speech, and treats free text as a source of liability rather than a source of information.

*[Margin Note: The Keel heard you. That was never the question.]*

## 6. Stale Handles

Failures here rarely arrive as one red light. They arrive as confidence.

A queue fills while the crew watches a transmit lamp that only reports that the suit spoke. The same pressure warning goes out four times and the fourth is suppressed as a duplicate, correctly, under the same rule 6 that suppressed 0662. R-58 sits on its mount with a legible serial plate, a working carrier, and a key that stopped meaning anything in 2147. A beacon wakes after a surge and writes a 2147 route table over a survey taken last season, because the older table carries the higher authority stamp and nothing on the beacon compares dates. And a message can be held at the desk because an evidence flag, a debt flag and a distress flag arrived in an order the schema treats as inconsistent.

Old handles are what actually kill: old contact IDs, old relay trust, old route names, old authorisation stamps. A diver believes they are talking to the Keel while the packet turns around inside a local cache that has not seen orbit in forty-three years. A manifest reaches custody intact and the plea attached to it drops out at the first relay, because the plea is not a field.

This is why crews mark their own routes and keep physical proof. Paint on a hatch outlives a relay account. A tied line outranks a clean coordinate from an unowned key. A body tag can carry a fact that no schema has a column for.

## 7. What A Stale Reply Costs

Ran is 10.5 light years from Sol, and the card rounds the consequence to twenty-one years.

```text
QUERY  DR-Q-441   SENT 2147-05-22, Sector 44 evacuation authority
       "Confirm release authority for queue 3 absent quarantine class."
ANSWER DR-A-441   ARRIVED 2168-04-09, addressed to Sector 44 Operations
       "Authority confirmed. Advise current occupancy status."
```

The answer is correct, courteous, and twenty-one years late, and Sector 44 had been under water for twenty-one of them. It was filed. It sits in the mirror under the same accession run as the queue sheet it answers, and the Recovery Compliance Office still cites pending external queries as grounds to hold an action, because a query in flight is a defensible reason to do nothing for a decade.

The near-side version costs less time and the same amount of blood. Incident 44-IR-3104: route state returned `passable` at 02:10, confirmed against R-44, acted on at 06:31. The fault lip had closed to 180 mm somewhere in the four hours and twenty-one minutes between the reply and the body. One worker was recovered. The other went onto Ibarra's ledger under `asset reassignment`, which is the entry the loss desk uses when there is no recovery and no coordinate.

Revision 9 of the watch card added the 900-second line and the Sol round trip. It did not add a field for the gap between a true answer and a compartment that has already changed. Crews write that one on the plate themselves, in grease pencil, under the last printed line.
