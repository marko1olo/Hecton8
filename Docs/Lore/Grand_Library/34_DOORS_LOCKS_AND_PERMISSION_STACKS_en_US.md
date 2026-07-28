<!-- localization_status: source_authority_en_US -->
# DOORS, LOCKS AND PERMISSION STACKS

> **Source:** Deep Reach access-control primer DR-ACP-9 rev.4, Black Keel salvage annotation layer.  
> **Speaker:** Colony infrastructure training voice, recovered and redacted.  
> **Reviewer Note:** Indented blocks are original DR-ACP-9 clauses. Everything outside them was added by Black Keel intake during the 2190 return-action cycle and carries no signature.

---

## 1. The Rating Plate

Clause 1.1, unaltered:

> A Class-3 pressure frame separates two volumes of differing rating. The frame certifies nothing about
> either volume. Atmospheric fitness is certified by the volume's air ledger, not by the door.

Every colony frame carries a rivet plate on the hinge side, four fields deep: FRAME CLASS / WORKING DIFFERENTIAL / LAST SERVICE / SERVICE INITIALS. The plate lifted off frame 44-3-D07 reads Class 3, 1.8 MPa, LAST SERVICE 2147-03-11, and three sets of initials in a column ruled for twelve. Behind the plate the hinge motor housing is packed to a depth of four centimetres with grey brine paste.

Nobody revoked the rating. Revocation needed a countersignature from a desk that stopped answering in 2147, so the plate is still legally current and the motor is still full of mud. Intake has cycled eleven frames in this district on command. Not one of them has a service column that continues past that year.

## 2. The Permission Stack

Clause 2.3:

> Access is granted only when every applicable permission layer returns PASS. A refusal shall name the
> refusing layer.

Eight layers, in the order the node reports them: IDENT, ROUTE, PRESS, CONTAM, MAINT-OVR, OWNER-REL, CREW-CONF, CUSTODY. Any one of them can pass while the other seven refuse.

Node dump off 44-3-D07, fourth attempt, copied by hand because the port would not hold a session:

    ACCESS NODE 44-3-D07              ATTEMPT 4 OF 4
    IDENT      PASS   badge 8841-K / holder: SITE MAINTENANCE 2 / issued 2144-06
    ROUTE      PASS   route line 44-3 open
    PRESS      FAIL   differential unresolved; return gauge hunting 1.1-1.4 MPa
    CONTAM     PASS   last sample logged 2147-02-28
    MAINT-OVR  ----   no override on file
    OWNER-REL  FAIL   custody pool: Aegir Reclamation Pool
    CREW-CONF  PASS   crew status: CLOSED
    CUSTODY    FAIL   asset flagged / Asset Silence Board

CREW-CONF returns PASS because the crew record is closed. CLOSED is a different field from EVACUATED, and the node was never built to tell them apart. IDENT passes a badge forty-three years stale for a maintenance grade that has no living holder.

PRESS is the layer that decides whether the frame kills whoever cycles it. OWNER-REL and CUSTODY decide what the entry gets called afterwards, and that is the pair that comes off a lien. A cut on a frame flagged by the Asset Silence Board is filed as interference with a silenced asset and assessed against the tonne-window at recovery rate. Transit rate would have been cheaper by two thirds. The gauge does not know that and will not say it.

## 3. Forced Entry

Every forced door leaves a story in the metal.

Cut marks show impatience, but also tool type, hand angle and whether the person cutting expected pressure behind the panel. Pried frames show panic. Burned hinges show someone had time and hated the lock personally. A clean override can be more suspicious than a rough breach because it means the system believed the intruder belonged there.

Black Keel values these marks. A broken door can prove a route, a death, a claim dispute or a contamination path. It can also prove that the player destroyed the only record that would have made the recovery valuable. The ocean is not the only thing that punishes brute force. Paperwork does too, just later and with better memory.

## 4. Door Culture

People who work under pressure develop manners around doors.

Do not stand in the swing arc. Do not crowd a hatch that is still thinking. Do not mock a slow lock; slow locks have saved more lives than fast workers. If a door hisses, everybody listens. If a door opens too easily, nobody steps through first without checking why. A worker who says "hold" near a pressure frame is obeyed before being questioned.

Marauders leave marks for each other when they dare: one line for bad air, two for wet floor, a crossed notch for living fauna beyond, a small dot under the handle for "worth opening quietly." Black Keel does not recognize these marks. That is part of why they matter.
