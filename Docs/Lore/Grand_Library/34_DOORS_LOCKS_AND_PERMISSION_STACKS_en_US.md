<!-- localization_status: source_authority_en_US -->
# DOORS, LOCKS AND PERMISSION STACKS

> **Source:** Deep Reach access-control primer DR-ACP-9 rev.4, Black Keel salvage annotation layer.  
> **Speaker:** Colony infrastructure training voice, recovered and redacted.  
> **Reviewer Note:** Quoted clauses are original DR-ACP-9 text. Everything else was added by Black Keel intake during the 2190 return-action cycle, and only the breach finding in section 3 carries a signature.

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

CREW-CONF returns PASS because the crew record is closed. CLOSED is a different field from EVACUATED, and the node was never built to tell them apart. IDENT passes a badge forty-six years stale for a maintenance grade that has no living holder.

PRESS is the layer that decides whether the frame kills whoever cycles it. OWNER-REL and CUSTODY decide what the entry gets called afterwards, and that is the pair that comes off a lien. A cut on a frame flagged by the Asset Silence Board is filed as interference with a silenced asset and assessed against the tonne-window at recovery rate. Transit rate would have been cheaper by two thirds. The gauge does not know that and will not say it.

## 3. Forced Entry

A forced frame keeps the tool that opened it.

An induction cutter leaves a bead with a rolled lip and cooks the paint back two hand-widths. Pry work walks the frame out of square, and square never comes back, so the seal never seats again and the next crew inherits a room they cannot close. Burned hinges mean somebody had time. The mark worth reading twice is a clean override, because it means the stack believed whoever stood there belonged.

Intake pays for these marks. It also prices them:

    BLACK KEEL BREACH FINDING BK-BF-2190-0417
    FRAME             44-3-D07
    ENTRY METHOD      induction cut, hinge side, two passes
    NODE STATE        OWNER-REL FAIL, CUSTODY FAIL at time of entry
    RECOVERED         1 valve cage / 2 cassettes / 1 suit locker, empty
    CLAIM CLASS       interference with silenced asset / schedule KM-2147-C4
    ASSESSED          0.9 tonne-window
    SIGNED            V. SATO-REN / RECOVERY COMPLIANCE OFFICE / RAQ 2

The cut took eleven minutes. The claim class was chosen afterwards, in an office, by a desk that has never had to guess what is standing behind a plate. Second pass of the cutter also took out the latch reed, and the reed was the only part that could have shown whether the frame was closed before the water reached it or after.

The class code is older than the cut it prices. Schedule KM-2147-C4 was written by a Keelmark adjuster named Ibarra in the quarter the colony drowned, to value objects nobody then expected a living person to be standing next to. Recovery Compliance Office signs the findings now, off its own return-action queue, and it has not needed a new word to do it.

## 4. Manners At A Frame

Clause 4.1 is the only clause in DR-ACP-9 written in the second person. Intake left it in:

> Do not stand in the swing arc. Do not crowd a frame that is still cycling. Do not hurry a slow latch.

The rest was never in any primer. If a frame hisses, everybody listens. Nobody steps through first when a frame opens easier than its plate says it should. "Hold" gets obeyed before it gets explained, and the explanation is usually a gauge.

Marauders cut their own marks when they can spare the current: one line for bad air, two for wet floor, a crossed notch for living fauna beyond, a small dot under the handle for worth opening quietly. BK-BF-2190-0417 has no field for any of it.
