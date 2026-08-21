# FEMEX — the business model

## Context

`Claude/FEMEX_BusinessAnalysis.md` did the hard part. It tested the converter thesis against what the
market has actually done rather than against what the format can do, and the answer was consistent
across nine players: **nobody monetises the pipe.** Flux died sixteen months after pricing at
$60/month with $29M behind it; Konstru, the closest existing analogue to FEMEX, has been silent for
three years; Autodesk moved the Revit ↔ Robot link *inside the licence* at the 2021 release; SAF is
free, open and backed by roughly thirteen vendors. That document closed with four candidate wedges,
six open questions, and one instruction — *ten conversations, not adapter #2*.

What it could not do was choose between the wedges, because choosing requires constraints it did not
have. Those constraints are now known:

> **Solo, part-time, unfunded. A network of practising engineers, but outside a design office.
> Optimising for durable income at modest scale, not for a venture outcome. Willing to give the format
> away if that is the right move.**

Those four facts eliminate most of the board, and what survives is not quite on the board — it is a
product the analysis kept circling without naming, because it was still measuring FEMEX as a
converter. This document names it, states the order to build it in, and marks what it makes stale. It
changes no schema and no code.

It is the sequel to `FEMEX_BusinessAnalysis.md`, not a replacement for it. Every claim about the
market below rests on that document's §1 evidence table and adds no new vendor research.

---

## 1. What the constraints decide

Each constraint removes a candidate outright. Read together they leave one square standing.

| Constraint | What it eliminates | Why |
|---|---|---|
| **Solo, part-time** | The five-adapter roadmap | Obstacle 4 — maintenance is unbounded. Five native connectors against Autodesk, CSI and Dlubal release cycles is a full-time job for a team. A part-time solo maintaining COM, OAPI and SOAP clients is a treadmill, and the treadmill ends where Konstru ended. |
| **Solo, part-time** | Pivot B — walls and slabs into RCB | It needs results in FEMEX (§6's three costs, the third of which is licence-gated and unsolved), a 30-day trial clock, and a vendor with **no public API, schema or file format of any kind** who can close the gap in any release. The most work and the most risk, from the person with the least capacity. |
| **Unfunded** | Pivot A — BIM ↔ FEMEX, and any platform play | Entrant number ten against seven free incumbents. Speckle's free-connectors/paid-hub playbook is the right idea and it needed $19.2M. |
| **Network outside a firm** | The enterprise sale | Obstacle 2 — the buyer is a technical director or BIM manager. Small deal, slow institutional cycle, no internal champion. Whatever is sold must be self-serve or a service, never procurement. |
| **Durable income, modest scale** | The venture framing | This is explicitly the GeometryGym shape — two people, no burn, sustains indefinitely. Treating it as the target rather than the fallback makes low-dependency choices correct rather than merely cautious. |
| **Open core acceptable** | The last hesitation about the format | The analysis already concedes the format layer has no defensibility against SAF. Giving it away costs nothing real. |

**What survives** is §7's two suggestions: the QA play — *"of the nine players in section 1, not one
sells this"* — and migration consultancy. Neither needs a vendor licence. Neither can be taken away by
a vendor release. Both are priced against **checking** rather than transfer.

The contribution of this document is noticing three things the analysis did not:

1. Those two are not separate businesses. They are **the same product at two levels of automation** —
   a report produced by hand, and the same report produced by software.
2. Beneath both sits a third, simpler claim — *is this model sound?* — which needs no second model, no
   results and no vendor cooperation, and which **is already largely built.**
3. The version of the QA play with the sharpest edge is not cross-program at all.

---

## 2. The recommendation

**Stop selling movement. Sell certainty.**

FEMEX's saleable asset is not the format and not the adapters. It is three things the repository
already contains or has already specified:

- `FemexModel.Validation.cs` — 1,752 lines, thirty-seven check families, with a `ValidationSeverity`
  taxonomy that reports **legal but suspect**, not merely invalid;
- the `TransferMessage` / `LossCategory` discipline of `FEMEX_Adapters.md` §4 — every difference
  between input and output provably declared;
- the uid-keyed equivalence of `FEMEX_Adapters.md` §7.2 — a definition of when two models are the same
  model.

Together those are a **model assurance engine**: something that reads a structural model and tells an
engineer what is wrong with it, what changed in it, and what a transfer did to it — producing a report
they can put in a project file and stand behind.

The format, the library and the SAF adapter are **given away**, to remove the "why not SAF?" objection
and to buy distribution. The money comes from the report, and from services that produce the report by
hand until the software can.

The pitch is the analysis's own sentence, taken literally rather than as a flourish:

> *An engineer will pay to not have to check the model. They will not pay much to move it.*

---

## 3. Model Assurance — three claims, one engine

One engine, three claims. They are listed in increasing order of value and decreasing order of
readiness, which is convenient: the one that ships first is the one that is nearly built.

### Claim 1 — Check. *"Is this model sound?"*

`Validate()` already answers this, and §4 below audits how well. It is the only claim that needs
nothing but one adapter and a report renderer.

- **Incumbent: none.** Every analysis program checks its own model inside its own solver, which is
  precisely the check an independent reviewer cannot rely on. Nobody sells model checking as a
  standalone, program-neutral product.
- **Why first:** no results, no diff, no second model, no vendor cooperation, no licence.
- **Where the report goes:** the viewer's existing `issues[]` panel with click-to-select was built for
  validation messages. It is already the report UI.

### Claim 2 — Compare. *"What changed, and do these two agree?"*

The §7.2 diff, promoted from test infrastructure to product surface. Two uses, and the first is
sharper than the analysis assumed because **it is not interop at all**:

- **Version diff — one program, two dates.** *"What changed between Tuesday's model and Friday's?"*
  Single-program, weekly frequency, needs **one** adapter rather than the N² hub. This directly
  answers obstacle 5: the hub design offers N² pairs of which most are hypothetical, and a version
  diff needs no pair at all. Every engineer has this problem; nobody solves it.
- **Reconciliation — two models, two authors.** Peer review and independent-check workflows, where
  comparison today is spot-checking by hand.

**The honest risk.** §7.2 says *match by `Uid`, never `Id`*. Two exports of the same ETABS model may
carry stable GUIDs; a hand-authored file, a legacy file, or two models from different programs will
not — and `ValidateUidCoverage` exists precisely because partial coverage is the normal state. **A
geometric and topological matching fallback is required, and is not currently designed.** This is the
single largest piece of genuinely new engineering in this plan, and it should be treated as the
product's core intellectual property rather than as a test utility.

### Claim 3 — Certify. *"This crossing lost exactly these things."*

The `TransferMessage` / `LossCategory` audit trail: a dated statement of what a conversion did,
anchored to the objects it did it to.

This is the direct answer to obstacle 3, which the analysis identifies as *"more than any technical
gap, why interop tools underperform their obvious value"*: an engineer is professionally liable for
the model, so if every member must be re-verified after a conversion, the conversion saved less than
it appeared to. A provable loss report is the only thing on the board that attacks that directly.

- **Readiness:** contract designed, no adapter yet exists to produce one.
- It is also the claim that **justifies the format existing at all** — a pivot format that cannot
  declare what it lost is just another file extension.

### The composition is the product

Check → Compare → Certify is one sentence an engineer wants to be able to say: *this model is sound,
it differs from the last issue in these ways, and it came across the boundary losing only these
things.* None of the nine players sells any one of the three. None sells the sequence.

---

## 4. An honest audit of what is already built

Claim 1 rests on `FemexModel.Validation.cs`, so the claim deserves an audit rather than a line count.
Read through, the file splits roughly in half.

**Half is referential integrity** — *"Bar 12 references unknown section 7"*, *"duplicate element id"*,
*"region references unknown node"*. This is table stakes. It catches corrupt files and adapter bugs,
and an engineer will not pay for it, because their own program would not have opened the file.

**Half is engineering judgement**, and this half is the product. A sample, quoted from the source:

- *"Nodes {n} are at the same location. Elements only connect where they reference the same node
  number, so unless the joint is meant to be disconnected they should share one node."*
- *"{load} is projected but its direction lies in the loaded surface's plane, so the projected area is
  zero."*
- *"{owner} has two regions with equal priority and overlapping extents; the outcome depends on list
  order."*
- *"No load case carries self-weight: every selfWeightFactor is zero."*
- *"Section {n} states an area of {a} and its dimensions give {b}; one of the two is wrong."*
- *"{owner} is not planar (max out-of-plane deviation {d}, tolerance {t})."*
- *"Gravity has dx, dy and dz all zero, which is no direction at all."*

Every one of those is a model that **opens cleanly, solves, and is wrong** — the exact category an
engineer either catches by eye or does not catch. That is what a checking product is for, and it is
why the severity taxonomy being three-valued rather than pass/fail matters commercially and not just
technically.

**The consequence for the roadmap:** the checks worth marketing are the judgement half, and new rules
should be added to that half deliberately, sourced from real models and real engagements. The
referential half is finished and should not be extended.

**The consequence for the pitch:** do not say *"254 tests and 1,752 lines of validation"*. Say *"it
finds the load case that carries no self-weight, and the two regions whose overlap resolves by list
order."*

---

## 5. Where the money comes from, in order

**Year one is services. Product revenue compounds behind it.** A part-time solo does not out-build a
market; they bill for judgement while the software learns what to automate. This also solves the
analysis's §2 rule 3 problem — Thornton Tomasetti and Buro Happold could give their tools away because
billable hours had already paid for them, and *"FEMEX has no such internal balance sheet behind it"*.
Services **are** that balance sheet, built deliberately rather than inherited.

1. **Model audit engagements.** A fixed-price report on a firm's model, or on a library of them.
   Delivered with whatever tooling exists on the day and filled in by hand. Every engagement is also a
   corpus of real exported files — the thing `FEMEX_Interop_Status_16082026.md` §4 says has never
   existed — and a requirements interview that costs nothing.
2. **Migration engagements.** A firm moving Robot → ETABS, or a merger consolidating two model
   libraries, with an audit trail. High ticket, one-off, and it validates demand before five adapters
   exist. The analysis already names this; the point here is that it produces the *same report*.
3. **Subscription.** The hosted Check and Compare service, opened once engagements have shown which
   findings people actually act on.

**Pricing.** Not $60 per model per month. That number is a **price for a pipe**, and the analysis
establishes it as a ceiling the industry has now rejected twice. Assurance is priced against the hours
of checking it replaces, which is a different and much higher shelf — IDEA StatiCa charges seat-year
money for a *check* and gives the *transfer* away, at €22M turnover and 40,000 engineers. Concretely:
engagements at four figures; subscription per seat, annual, sold as a checking tool. Keep a genuinely
useful free tier — the checker running locally on your own file — because it is the top of the funnel
and costs nothing to run.

**Shape of the target.** Three or four engagements a year is already a meaningful part-time income.
Subscription is the compounding layer that should eventually exceed it, and it is allowed to take
years. This is the GeometryGym outcome — roughly $200k, two people, fifteen years, sustaining
indefinitely — approached deliberately and with a better wedge than plug-in maintenance.

---

## 6. Open core — the position

**Give away**, MIT or Apache-2.0: the FEMEX specification, the `griffel-femex` library, the SAF
adapter, the conformance suite, and the `LossCategory` taxonomy.

**Keep**: the hosted service, the report, the cross-model matching heuristics of §3 Claim 2, and the
judgement check rules accumulated from real engagements.

Two reasons beyond the analysis's own "no defensibility anyway":

1. **It removes the only objection that cannot be won.** Against SAF — free, open, Nemetschek-backed,
   thirteen vendors — a proprietary format must be *better enough to buy*. A free one only has to be
   *good enough to use*, and FEMEX genuinely is better in the one place `FEMEX_Interop_Review.md` §3.2
   identifies: the priority-based plate region model. Free converts that from a losing argument into a
   quiet advantage.
2. **The audit standard is the only durable asset available.** If other people's adapters declare their
   losses in this taxonomy, then what counts as a clean transfer is defined here, and the certification
   is worth something. That is slow, cheap and compatible with one part-time person. Treat it as
   upside, not as the plan.

---

## 7. What this changes in the existing plans

Very little engineering is wasted, which is the test any reframing of this kind should pass.

**1. `AdaptersPlans/SAF_Adapter.md` — the architecture stands, the pitch changes.** Its four layers
(contract / adapter / web shell / viewer) are right, and all seven of its locked-in decisions survive
unchanged. What changes is what the shell is sold as: not *"convert SAF ↔ FEMEX by subscription"* but
*"check and compare your model"*, with conversion as the plumbing that gets the model in. Its own
observation that *"every customer conversion is a round-trip test"* becomes the business model rather
than a nice side effect.

**2. Phase A is re-ordered — A5, the diff utility, moves to the front.** It currently sits behind the
golden fixture and the reference adapter as test infrastructure. It is a product surface, and it
carries the one unsolved design problem in this plan (matching without uids, §3).
`EnumerateIdentified()` (`FemexModel.Identity.cs:70`) still needs making public — `FEMEX_Adapters.md`
§9 already calls that *"the cheapest additive change on this list"*.

**3. `FEMEX_Adapter_LicenceProcurement.md` Phase 2 shrinks to one item now and three later.** Apply for
ADN — no-cost Open tier, free for start-ups up to three years, covers Revit *and* Robot, long lead
time, so it costs nothing to start. Defer CSI NFR, Dlubal API access and INDUCTA until an engagement
pays for them. **Do not start a 30-day trial clock speculatively**; the Phase 3 record/replay design
stays on the shelf, correct and unbuilt, until a native adapter is customer-funded.

**Adapter policy, stated once.** SAF is the only adapter built on spec. It needs no licence, cannot be
broken unilaterally by any single vendor, and reaches RFEM, SCIA, Archicad, ALLPLAN, RISA, StruSoft,
AxisVM, SOFiSTiK, ConSteel, IDEA StatiCa and Prota. Beyond SAF, **prefer reading exported files to
driving live APIs**: a file reader cannot be broken by a vendor release the way a COM or OAPI client
can, and it needs no seat. Build a native connector only when a paying engagement funds it — which
converts obstacle 4 from an open-ended liability into a cost with a customer attached.

**Results in FEMEX — deferred, not rejected.** Claim 2's strongest form is *"these two models disagree
on column axial load by 12%"*, and that needs results. But Check needs none and geometry-Compare needs
none, and §6 of the analysis is right about all three costs — volume, semantics, and the fact that
verification requires two licensed programs actually running. Decide after §8's conversations. If yes,
build it exactly as §6 argues: a separate `FemexResults` root bound to the model by schema version,
content hash and uids, never a larger `FemexModel`.

---

## 8. The ten conversations, reframed

The analysis is right that this is the cheapest next move and wrong about the questions, because it
was still testing the converter thesis when it wrote them. With the network that exists, this month,
before anything in §7 is built, ask:

1. When you inherit a model from a colleague, what do you check before you trust it, and how long does
   that take?
2. When a model changes between two issues, how do you find out what changed? What has it cost you
   when you found out late?
3. On a peer review or independent check, how do you compare your model against theirs today?
4. What would you want to see in a report you could put in the project file saying this model was
   checked?
5. Has a conversion ever put something wrong into a model you signed?
6. *(Only if 1–5 land.)* Would forces in that comparison change your answer, or is geometry enough?

**The decision rule, written down in advance so it is not negotiated afterwards.** Question 6 decides
whether `FemexResults` is built. Questions 1–3 decide everything else:

- **If 1–3 land** — build Claim 1 immediately and put it in front of the people who answered.
- **If 1–3 are shrugs** — then the QA play has no incumbent because it has no market, and the honest
  conclusion is that FEMEX is an open-source format plus a services business. Under these constraints
  that is still a real outcome, and it is reached two years earlier than by building five adapters
  first.

This is the same discipline `FEMEX_Interop_Status_16082026.md` §5 item 6 applies to the schema — *one
real export, round-tripped, before nine more entities are built from documentation* — transferred from
the format to the business.

---

## 9. What this makes stale

- **`FEMEX_BusinessAnalysis.md`** — not superseded; this is its sequel and rests entirely on its
  evidence. Four of its six open questions are answered here: give the format away (§6); the audit
  trail is **the product**, not a feature (§2, §3); RCBLink's gap is not acceptable risk for a
  part-time solo (§1); results deferred pending §8 question 6 (§7). Its remaining two — which single
  crossing costs a real firm money this month, and the silence of the demand-side research — are what
  §8 exists to close.
- **`FEMEX_Adapter_LicenceProcurement.md`** — Phase 2 items 2–4 deferred until customer-funded; Phase
  4's *"build in whatever order procurement delivers"* no longer applies, because the answer is now
  *build none until one is paid for*. Phases 0, 1 and 3 stand as written.
- **`FEMEX_Interop_Status_16082026.md` §5** — items 7 and 8 (the nine P1 entities, then the first
  native connector) are demoted below the SAF adapter and the diff. Items 4 and 5 (material
  completeness, units as enums) stand: both are small, and both are what make the numbers in a check
  report mean anything.
- **`AdaptersPlans/SAF_Adapter.md`** — architecture and all seven decisions stand; the product framing
  and the Phase A ordering change per §7.

No `.cs` file changes follow from this document.

---

## Still open

- **Whether the judgement half of `Validate()` is what engineers actually want checked.** §4 asserts it
  is, from reading the messages. Nobody has watched an engineer read one. §8 question 4 is the test.
- **How two models are matched when uids do not survive.** Named in §3 as the largest new engineering
  item and deliberately not designed here. It should get its own document before any of it is written,
  because a wrong matching rule produces a diff that is confidently incorrect — the same failure class
  as a wrong sign convention.
- **What a report is worth.** §5 argues assurance prices above the pipe's rejected $60 and points at
  IDEA StatiCa as the shelf. No engineer has been quoted a number. Ask in §8, indirectly, by asking
  what checking costs them now.
- **Whether "certify" survives contact with professional indemnity.** A report saying a model was
  checked is a statement someone may rely on. Whether that is an asset, a liability, or something
  needing different wording is a question for an insurer, not for this document, and it should be
  asked before the word *certify* appears on a website.
- **Whether the free local checker cannibalises the service or feeds it.** §5 assumes it feeds it, on
  the Speckle precedent. Speckle had funding and eventually metered *versions and syncs* rather than
  transfers; the equivalent metered unit here is probably the *report*, not the *check*, and that has
  not been thought through.
- **Whether services and product can be run by one part-time person at the same time.** They compete
  for the same hours, and the failure mode is well known: engagements crowd out the product
  indefinitely. Some rule about hours is needed, and this document does not have one.

## Sources

No new external research. Every market claim above traces to the desk research of August 2026 recorded
in `Claude/FEMEX_BusinessAnalysis.md` §1 and its Sources list — Konstru, Flux, Speckle, IDEA StatiCa,
BuildSoft, GeometryGym, BHoM, Autodesk and SAF. Claims about the repository were checked against the
source in this repository on 21 August 2026: `FemexModel.Validation.cs` (1,752 lines, thirty-seven
check families), `FemexModel.Identity.cs`, and the 254-fact suite in `griffel-femex.Tests`.
