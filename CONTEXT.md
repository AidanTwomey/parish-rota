# Parish Rota

A scheduling service for Catholic parish liturgical ministries, starting with Readers. Volunteers interact entirely by messaging on their phone — there is no volunteer-facing UI.

## Language

**Parish**:
A tenant of the system — one church community with its own Masses, Readers, Rota, and Coordinator. (First tenant: Holy Innocents, Orpington.)
_Avoid_: Church, customer, tenant (in domain language)

**Mass**:
A recurring scheduled liturgy at a Parish that Readers are rostered for (e.g. Sun 10:30).
_Avoid_: Service, event

**Reader**:
A volunteer who proclaims the Scripture readings at a Mass. Each Reader has a Home Mass (~8 Readers per Mass at Holy Innocents).
_Avoid_: Lector, volunteer (too generic)

**Home Mass**:
The Mass a Reader habitually attends and is rostered at. The generator only rosters Readers at their Home Mass; covering another Mass happens ad hoc via a widened Cover Request, and is not tracked as a preference.

**Rota**:
The published schedule of which Reader serves at which Mass, covering one Rota Period. Drafted by the system, approved by the Coordinator, then published to Readers.
_Avoid_: Schedule, roster

**Rota Period**:
The span of the liturgical year one Rota covers: Advent & Christmastide, Lent & Eastertide, or a block of Ordinary Time (long seasons are subdivided into roughly 8-week blocks). Boundaries are computed from the liturgical calendar.
_Avoid_: Month, quarter, term

**Unavailability**:
Dates a Reader cannot serve, recorded before a Rota is drafted. Declared by the Reader at any time, in reply to an Availability Prompt, or relayed by the Coordinator. Distinct from a Drop, which cancels an already-published Slot.

**Availability Prompt**:
The message sent to every Reader shortly before a Rota Period is drafted, asking for dates they cannot do. Silence means fully available.

**One-off Mass**:
A Mass occurrence added by the Coordinator outside the weekly pattern (Christmas Day, the Triduum, holy days of obligation). It joins the period's Rota like any other Mass but draws on the whole Parish pool — no Home Mass applies. Reader count is configurable per occurrence (e.g. the Easter Vigil's seven readings).

**Volunteer Call**:
The inverted availability question used for high-absence occasions (Triduum, Christmas): Readers are asked who *can* serve, and silence means unavailable. The opposite polarity to an Availability Prompt.
_Avoid_: Sign-up sheet

**Reminder**:
A nudge sent to a Reader a few days before their Mass. No reply is expected; silence means all is well. Replying "can't make it" turns it into a Drop.
_Avoid_: Confirmation request

**Slot**:
A single assignment of one Reader to one Position at one specific Mass on the Rota. Each Mass has two Slots.
_Avoid_: Shift, booking

**Position**:
The bundle of readings a Slot carries: "First Reading & Psalm" or "Second Reading & Bidding Prayers". A Position tells the Reader what to prepare; it does not restrict who may fill the Slot — any Reader can take either Position.
_Avoid_: Role, capability

**Drop**:
A Reader declaring they cannot fulfil one of their Slots.
_Avoid_: Cancellation, unavailability (that's about future scheduling, not an existing Slot)

**Cover Request**:
A system-initiated ask to Readers to take over a dropped Slot. First to accept gets it. It works up a ladder: Home Mass pool first → after a delay (or when the Mass is imminent), the whole Parish pool → finally escalation to the Coordinator.
_Avoid_: Swap request, broadcast

**Swap**:
A Reader-arranged exchange of a Slot, reported to the system after the fact ("Pat is covering my 10:30"). The system records it; it does not orchestrate it.

**Coordinator**:
The parish person who owns the Rota, receives escalations, and handles Readers who aren't on WhatsApp.
_Avoid_: Admin, organiser
