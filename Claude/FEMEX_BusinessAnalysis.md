# FEMEX — will anyone pay for this?

## Context

FEMEX has had five design rounds, an interop review against five programs, an adapter contract and a
licence-procurement plan. Every one of those documents assumes the product is **a converter**: N
adapters around a pivot format, so a model can cross from Robot to ETABS to RFEM without loss.

Nobody had yet asked whether structural engineers will pay for that. This document is that question,
answered against what the market has actually done rather than against what the format can do. It
changes no schema and no code. Its conclusion is that **the converter is the weakest thing to sell**,
that the format layer has no commercial defensibility at all, and that the two candidate pivots —
BIM ↔ FEMEX, and analysis → FEMEX → design-and-schedule — are not equally good: one is crowded, and
the other has already been built by a vendor and given away.

## 1. The market is real, and nobody has turned the converter into a business

Nine players have attempted some version of what FEMEX proposes. Their commercial outcomes are the
evidence, and they are unusually consistent.

| Player | What it sells | Commercial outcome |
|---|---|---|
| **Konstru** — Thornton Tomasetti, built from 2011, spun out | Model interop, priced per model | **$60/model/month** annual ($75 monthly); free tier 3 models. No visible funding round. Newest blog post **Sep 2023**, everything before it 2018; release notes stop at v3.x (2018); no customer logos; ~400 LinkedIn followers *(estimate)*. Alive, not growing. |
| **Flux** — first Google X spin-out | AEC data exchange | **$29M Series B** (Temasek, Surbana Jurong, DFJ). Monetised Nov 2016 at **$60/month**; firms balked. **Shut down 31 Mar 2018**, pivoted to HelixRE. |
| **Speckle** | Connectors **free**; sells the hub above them | **$12.5M Series A** Oct 2024, **$19.2M total**. Now Workspaces **£12–£60/seat/month**, Team **$99/month**, metering *versions and syncs*; Automate priced on usage. Arup, Mott MacDonald, Aurecon, RHDHV, Ramboll, Bollinger+Grohmann, Herzog & de Meuron, Perkins&Will. |
| **IDEA StatiCa** | The connection **check**; Checkbot links given away | **€22M turnover (2024)**, ~120 staff, 40,000 engineers, ~1.5M calculations/month, family-owned, no debt. Checkbot Free exists; the paid tier unlocks *design*, not transfer. |
| **BuildSoft BIM Expert** — StruSoft group | Paid interop, 50+ point-to-point routes | **From €100/month.** The seller also owns engines at one end (Diamonds, PowerConnect). |
| **GeometryGym** | Rhino/Grasshopper ↔ ETABS/Robot/SAP/GSA/Strand7, commercially, since 2009 | ~**2 people, ~$200k revenue** *(scraped estimate)*. Sustains indefinitely; does not scale. |
| **BHoM** — Buro Happold | ~60 adapters | Open-sourced end-2018. **Free.** |
| **Autodesk** | Revit ↔ Robot | Structural Analysis Toolkit **pulled from the App Store at the 2021 release**; the link is now an entitlement extension delivered with the Revit/Robot seat. Free, bundled, and inside the licence. |
| **SAF** | Open exchange format | Nemetschek initiative (2017), managed by SCIA. Implemented or in progress at SCIA, Graphisoft, Allplan, RISA, FRILO, AxisVM, Dlubal, SOFiSTiK, IDEA StatiCa. |

Two of these rows deserve reading twice. **Konstru is the closest existing thing to FEMEX**, and the
pain was real enough that a large engineering firm funded a whole product to solve it — which also
means the best-fit customers are precisely the ones who can afford to build their own. It then went
quiet: no releases, no marketing, no logos, for roughly three years. It did not fail. It simply never
became a business, which for an independent is the same outcome arriving more slowly.

**Flux is the same experiment run with $29M behind it.** Google X pedigree, tier-one investors, and
the identical conclusion — priced at $60/month, AEC firms declined, and it was gone inside sixteen
months of monetising.

## 2. What the scoreboard says

Four rules fall directly out of the table.

1. **Nobody monetises the pipe. They monetise what is at the end of it.** Speckle sells the hub and
   meters syncs; IDEA StatiCa sells the connection check; BuildSoft sells the analysis engines the
   pipe feeds. The one company that tried to sell the pipe itself is dormant, and the one that tried
   it at scale is dead.
2. **~$60 per unit per month is a price this industry has now rejected twice.** Flux's $60/month was
   fatal; Konstru's $60/model/month coincided with three silent years. The coincidence of the number
   is not evidence on its own, but the direction of both outcomes is. This is the single most useful
   figure in this document, and it sets the ceiling for any FEMEX price list.
3. **Consultancy-built interop gets given away.** Thornton Tomasetti productised Konstru and let it
   stall; Buro Happold open-sourced BHoM outright. Both had already recovered the investment
   internally — the tool paid for itself in billable hours before any external sale. **FEMEX has no
   such internal balance sheet behind it**, so it needs the external sale to work in a way that
   neither of those two ever did.
4. **Paid interop survives in exactly two shapes.** Either you own an engine at one end — BuildSoft,
   €100/month, feeding Diamonds and PowerConnect — or you are two people with no burn rate, which is
   GeometryGym at ~$200k after fifteen years. FEMEX is currently the second shape. That is a viable
   business; it is not the business the five-adapter roadmap is sized for.

## 3. Five obstacles that apply to any converter-first business

1. **The price ceiling is set by free — and free is now pre-installed.** Autodesk gives Revit ↔ Robot
   away, CSI gives CSiXCAD away, SAF is free and backed by Nemetschek with roughly thirteen vendors
   behind it. Vendors subsidise interop to sell seats. Worse for an entrant, Autodesk moved the Robot
   link *inside the licence* at the 2021 release — from a downloadable toolkit to an entitlement
   shipped with the seat. Nothing can be priced below "already installed". FEMEX-the-format competes
   with a free, open, vendor-backed standard, so it has no defensibility on its own.
2. **The buyer is not the engineer.** It is a technical director or BIM manager. Small deal size,
   slow institutional sale — the worst quadrant.
3. **Liability quietly destroys the value.** An engineer is professionally liable for the model. If
   every member must be verified after a conversion, the conversion saved less than it appears to.
   This, more than any technical gap, is why interop tools underperform their obvious value.
4. **Maintenance is unbounded.** Five programs times versions, and any vendor release can break the
   adapter. A vendor absorbs that cost; an independent pays it forever.
5. **Frequency is concentrated, not spread.** Revit → ETABS happens weekly. ETABS → RFEM happens when
   a project changes country or client. The hub design offers N² pairs; most of those pairs are
   hypothetical, and the value sits on one or two edges.

## 4. Pivot A — BIM ↔ FEMEX

Higher frequency, and it is where the daily pain is. It is also the most crowded square on the board,
and **every incumbent is free**: Autodesk's Revit ↔ Robot, CSI's CSiXCAD, Speckle's connectors,
Konstru, BHoM, GeometryGym, IDEA's Checkbot. Entrant number ten against zero-price competition, in the
part of Revit — the analytical model — that everyone has attempted and nobody has made engineers fully
trust.

**Verdict:** more volume, worse economics. Not a better business than the original plan, only a
busier one, unless it carries a differentiator the incumbents lack.

## 5. Pivot B — ETABS → FEMEX → RCB, carrying results

The instinct behind this is correct: analyse where the client demands, design and schedule where the
billable output is produced. Value flows from analysis toward design.

**It is correct enough that INDUCTA already built it, and gave it away.** RCBLink transfers from CSI
SAFE, CSI ETABS and Bentley RAM Concept into RCB — *"an entire model including geometry, material
properties and internal forces"* — explicitly to *"use the results produced by the analytical engines
of the above software and then design the structural elements using INDUCTA's design tools."*

Read both ways:

- **Validating.** A vendor spent real money confirming this workflow matters.
- **Blocking.** This exact pair is served, by the vendor, at zero price.

The stated gap is narrow but real: *"Currently, only the RCB Column Design & Schedule is linked."*
Column-only, one-way, and only from three CSI/Bentley sources — no Robot, no RFEM, no Revit. But it is
a hole in someone else's free product, on the one program of the five with **no public API, schema or
file format**. INDUCTA can close the gap or break the adapter in any release. That is the riskiest
square on the board.

**The transferable lesson.** RCBLink is not a model converter. It is a **forces-to-design pipe**: the
geometry is plumbing, the internal forces are the payload, and the column schedule at the far end is
what someone bills for. Nobody paid for the geometry — confirmed by a vendor who spent money finding
that out.

## 6. Should FEMEX carry results?

This is the real decision, and it is larger than it looks.

**In favour, but narrower than the earlier draft of this document claimed.** SAF's results support is
thin: the entire Results group is two objects, `ResultInternalForce1D` and `ResultInternalForce2DEdge`.
No reactions, no displacements, no modal, no surface results. As a *general-purpose exchange*, results
are genuinely uncommoditised, and this is one of the few places FEMEX could be better than SAF rather
than merely different — as the priority-based plate region model already is. Model-only transfer is a
commodity; a complete results exchange is not.

**But results transfer is not virgin territory.** IDEA StatiCa's Checkbot already carries geometry,
loading **and internal forces** from ETABS, SAP2000, Robot, RFEM and Tekla — free, at a company doing
€22M with 40,000 users. For the connection-design destination this is solved, priced at zero, and
defended by the healthiest firm in the comparison set. The accurate claim is therefore: **results
transfer is open as a general exchange and closed at the connection-design end.** Any FEMEX results
play must aim at a destination Checkbot does not serve — which is an argument *for* the walls-and-slabs
wedge in section 7, not an argument against carrying results at all.

**Against — three costs, the third being the serious one.**

1. **Volume breaks the current design.** Results are 10–100× the model: element × station × load case
   × combination. FEMEX is tuned for the opposite — `WriteIndented = true`, hand-authored examples,
   byte-identity round-trip assertions, `[JsonExtensionData]` on every type. A 200 MB indented JSON
   file satisfies none of the properties four design rounds have earned.
2. **The semantics are harder than the model's.** Sign conventions, station positions, local axes,
   envelope versus per-combination, cracked versus uncracked, staged construction, P-delta. The
   failure mode is nastier too: wrong geometry is visible, a wrong sign convention is invisible and
   gets built.
3. **It cannot be verified without licences.** Record/replay solves model transfer, because the
   assertion is "the model came back the same". For forces the assertion is "these numbers are the
   ones the solver produced", which needs both programs actually running. This is the one place
   `FEMEX_Adapter_LicenceProcurement.md` genuinely does not reach.

**If it is done, do it as a separate document.** Not a larger `FemexModel` — a `FemexResults` root
referencing the model by `schemaVersion`, a content hash, and the object `Uid`s. FEMEX already chose
this shape once for `Mesh`: optional, generated, regenerated wholesale, carrying no uids. Results need
the same separation but a **stronger binding**, because the catastrophic failure here is not losing
data — it is silently designing to another model's forces. Provable binding ("these forces came from
this model, this producer, this version, on this date") is the same auditability argument as the loss
report, and nobody offers it. Keeping it out of `FemexModel` also preserves the byte-identity tests,
the hand-authored examples and the readable-JSON property.

## 7. Where the value actually is

**The loss report is the asset, not the converter.** The `TransferMessage` / `LossCategory` discipline
in `FEMEX_Adapters.md` — every difference between input and output provably declared — is a direct
answer to obstacle 3, and almost nobody does it. It has to be sold as *auditable, certifiable
transfer*, not as conversion. **An engineer will pay to not have to check the model. They will not pay
much to move it.**

Reframe from converter to **forces-to-design pipe with an audit trail**, and pick an edge where no
free vendor link exists. Two candidates:

- **Walls and slabs into a design-and-schedule tool**, not columns — the explicit RCBLink gap, from
  sources CSI and Bentley do not serve, and a destination Checkbot does not reach either.
- **The QA play: multi-model force comparison.** *"These two models disagree on column axial load by
  12%."* It uses the uid-keyed diff utility already designed in `FEMEX_Adapters.md` §7.2, needs no
  vendor cooperation, and no vendor can take it away. **Of the nine players in section 1, not one
  sells this.** It is the only candidate in this document with no incumbent.

Two further framings worth holding: interop as an **on-ramp to something engineers value more** —
noting that RCB, the odd one out among the five, is fundamentally a design and *scheduling* product,
which is a hint about where money sits in this stack; and **migration consultancy** — a firm switching
from Robot to ETABS, or a merger consolidating two model libraries, will pay real money for a one-off
bulk migration with an audit trail. That is a service business with tooling behind it, and it
validates demand before five adapters exist.

## 8. The cheapest next move

Not adapter #2. **Ten conversations.** RCBLink's existence proves the workflow is real, so the
question is now specific and cheap to answer: ask concrete designers whether *column-only and one-way*
costs them time. If yes, there is a wedge and results-in-FEMEX is justified. If they shrug, it would
mean doubling the format's scope on a hypothesis.

None of the engineering so far is wasted under any of these models. The format, the validation and the
loss taxonomy are the reusable core beneath all of them. What is at risk is only the assumption that
all five adapters are equally worth building.

## Still open

- **Which single crossing costs a real firm money this month.** Unanswered, and everything else
  depends on it.
- **Whether FEMEX should be given away.** It competes with SAF, which is free, open and vendor-backed,
  so the format layer has no defensibility. Speckle's playbook — free connectors, paid platform — may
  apply directly, noting that Speckle itself now meters *versions and syncs* rather than transfers.
- **Whether results enter FEMEX at all**, and if so whether as `FemexResults` beside the model or not
  at all. Section 6 argues the shape; the decision waits on section 8.
- **Whether building in RCBLink's gap is acceptable risk**, given that INDUCTA can close it at will
  and RCB has no public interface of any kind.
- **Whether the audit trail is a feature or the product.** If engineers will pay for certifiable
  transfer, that reorders the entire roadmap toward provenance and away from coverage.
- **The desk research is now strong on supply and still silent on demand.** Section 1 establishes with
  reasonable confidence who sells what and how it went. It establishes nothing about whether a
  specific firm will pay, because no buyer has been asked. Konstru's three quiet years make section 8
  more urgent rather than less: the closest analogue to FEMEX stalled at roughly the stage FEMEX is
  approaching, and desk research cannot say why.

## Sources

Desk research, August 2026 — vendor pages, funding announcements and product documentation. Figures
marked *(estimate)* above are third-party scrapes, not reported numbers.

- Konstru — <https://konstru.com/>; pricing <https://konstru.com/pricing/>; blog <https://konstru.com/blog/>
- Konstru origin at Thornton Tomasetti CORE studio — <https://www.thorntontomasetti.com/capability/konstru>
- Flux Series B — <https://techcrunch.com/2015/12/21/google-x-alum-flux-factory-raises-29m-series-b>
- Flux shutdown — <https://www.archpaper.com/2018/02/flux-to-shut-down/>
- Flux post-mortem, including the $60/month rejection — <https://bricks-bytes.com/newsletter/how-flux-burned-through-29m-lessons-for-aec-innovators/>
- Speckle Series A — <https://www.foundamental.com/perspectives/software-sparks-success-open-source-speckle-raises-series-a-for-aec>
- Speckle funding announcement — <https://speckle.systems/blog/speckle-raises-12-5-million-to-build-the-first-aec-data-hub/>
- Speckle pricing and strategy — <https://aecmag.com/bim/speckle-matures/>; plans FAQ <https://docs.speckle.systems/workspaces/new-plans-faq>
- Speckle open-source playbook — <https://bricks-bytes.com/technology/how-speckle-used-open-source-to-crack-aec/>
- IDEA StatiCa company figures — <https://www.ideastatica.com/about-us>; <https://www.ideastatica.com/blog/the-state-of-idea-statica-2023>
- IDEA StatiCa Checkbot, including free tier and internal-force import — <https://www.ideastatica.com/checkbot>; <https://www.ideastatica.com/support-center/checkbot-bulk-bim-workflows>
- BuildSoft BIM Expert — <https://www.buildsoft.eu/en/product/bim-expert/>
- GeometryGym structural analysis plug-ins — <https://technical.geometrygym.com/rhino-grasshopper/structuralanalysis>; size estimate <https://rocketreach.co/geometry-gym-profile_b5cc7ba6f42e0ab4>
- BHoM — <https://aecmag.com/features/bhom-addressing-the-interoperability-challenge/>
- Autodesk Structural Analysis Toolkit retirement and Robot extension delivery — <https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Robot-Structural-Analysis-link-missing-from-the-Analyze-tab-in-Revit.html>
- INDUCTA RCBLink — <https://inductasoftware.com/RCBLink.html>
- SAF specification, Results group — <https://www.saf.guide/en/stable/>
- SAF ecosystem and vendor support — <https://www.scia.net/en/innovations/structural-analysis-format-saf>; <https://github.com/StructuralAnalysisFormat/StructuralAnalysisFormat-Doc/blob/master/docs/getting-started/who-supports-saf.md>
