# FEMEX — going outside: audit, gaps, and a marketing plan

## Context

The decision is to start taking FEMEX to engineering consultancies, leading with the model
check and model audit functionality. This document is the result of an audit of all three
repositories and all 46 markdown documents in them, and it answers three questions in order:

1. **What is actually built** — and which parts of it are sellable today.
2. **What blocks an outsider from seeing it** — ranked, with fixes.
3. **What the marketing plan is** — for Australia/NZ, at 4–6 hrs/week, with the copy drafted.

The short version: **the engine is real and the strategy is already right; the demo is
wrong and the distribution is zero.** `FEMEX_BusinessModel.md` §2 made the correct call
("stop selling movement, sell certainty") and Phases 0→D delivered it. What has not
happened is the one thing every document since Phase C says is still unasked — §8's six
conversations — and the one thing nobody noticed: **the flagship demo model produces
findings that make the tool look broken rather than valuable.**

Constraints taken as given: AU/NZ consultancies as the beachhead, all three repos private
with no domain and no site, 4–6 hrs/week of marketing time protected from engineering.

The step-by-step execution breakdown of this document is `FEMEX_GoToMarket_Steps.md`,
beside it. Drafted collateral lands in this folder.

Written 21 September 2026. This document changes no schema and no code.

---

## 1. Where the product actually stands

**Verified against the working tree at `a4031fc` (2026-08-31), plus one uncommitted file.**

| | |
|---|---|
| Solution | 8 projects, ~36,000 LOC C# (~19k production / ~11.3k test), single `main`, 33 commits |
| Checker | `FemexModel.Validation.cs` — 2,826 lines, **35 named rule families**, `Validate()` at `:36` |
| Severity / category | `Error`/`Warning` × `Referential`/`Judgement`/`Provenance`, orthogonal, no default |
| Diff | `Comparison/` — uid-keyed, exceptions-first member walk, 5 `DifferenceKind`s |
| Loss taxonomy | `Interop/LossCategory.cs` — `Dropped`/`Approximated`/`Invented`/`Unmapped`/`Stale` |
| Conformance | `Interop/Conformance/` — **8 Tier-1 checks**, proven against 8 deliberately broken adapters |
| Adapters | **SAF only.** 13 entities both directions, 81 declared losses, 11 vendor workbooks |
| Report | `griffel-femex.Reporting` — one HTML/JSON/text document from one `AssuranceReport` |
| CLI | `femex check / compare / convert`, exit `0`/`1`/`2`, batch + index page |
| Viewer | separate repo, one 398 KB HTML file, canvas 2D, no dependencies, offline-verified |
| Tests | ~632, four projects; parity harness diffs the viewer's JS mirror against the C# engine |
| Packaging | 3 × `.nupkg` built at 0.1.0 in `artifacts/`, **unpublished**. No CI. Apache-2.0 + NOTICE |
| Schema | **1.11** (`FemexModel.cs:105`); readable 1.1 → 1.11; README still says 1.10 |

### What is genuinely differentiated

Three assets, and they are not the format:

- **The judgement half of the checker.** Coincident nodes by union-find over a spatial hash;
  a projected load whose direction lies in the loaded plane (projects to zero); two regions
  of equal priority and overlapping extent ("the outcome depends on list order"); no load
  case carrying self-weight anywhere; a stated section area disagreeing with its own
  dimensions by >10%; a compression-only sense on a rotational DOF. These are models that
  *open cleanly, solve, and are wrong.*
- **The loss taxonomy, and `Invented` in particular.** Nobody else reports the category where
  an adapter supplied a number the sender never said. `FEMEX_Adapters.md` §4.3 is right that
  from inside an adapter "an invention does not feel like a loss, it feels like success."
- **The conformance harness.** An open, executable definition of what a compliant adapter
  owes. This is the only durable asset in the plan and it is finished.

### The constraint that governs every word of copy

`README.md:48–63` and decision 9: *the report states findings and provenance; it does not
offer an engineering opinion and it does not certify anything.* The reporting tests assert
that *certif\**, *guarantee*, *we confirm*, *safe to use* and *fit for purpose* appear in no
user-facing string. **Every asset in §5 honours this.** It is also a marketing asset in its
own right — it is the sentence that makes an independent reviewer trust the tool.

---

## 2. The one structural problem: AU/NZ does not run SAF

This is the finding that reorders everything, and the repository already states it without
drawing the marketing conclusion. `AdaptersPlans/SAF_Adapter.md` Context:

> **SAF reaches none of ETABS, Robot, SAP2000, Revit or RCB** — the programs this network
> actually runs. So SAF is **the proving ground and the distribution-reach target, not a
> sales corridor.**

SAF reaches SCIA, Dlubal RFEM, Allplan, Archicad, RISA, FRILO, StruSoft, AxisVM, SOFiSTiK,
ConSteel, IDEA StatiCa, Prota. The Australian structural market runs, roughly: **SPACE GASS,
Microstran, Strand7, ETABS/SAP2000, RAPT/RCB (INDUCTA), Multiframe** — with Dlubal and IDEA
StatiCa present but minority. Treat that list as a hypothesis to confirm in the first five
conversations, not as a researched fact.

**The consequence: a Sydney engineer cannot today get their model into `femex check`.** The
free local checker — the top of the whole funnel — has no input path for the chosen beachhead.

There are exactly three ways through, and the plan uses the first two now and defers the third:

| | Route | Needs | Available |
|---|---|---|---|
| **A** | **Conversations.** Ask the six questions; show the report on *your* model, not theirs. | nothing | now |
| **B** | **Engagements.** They send an export in whatever format; the report is produced, filling by hand what the tooling cannot yet read. `FEMEX_BusinessModel.md` §5 item 1 designs exactly this. | nothing | now |
| **C** | **Adapter #2 — a file reader**, not an API client. ETABS `.e2k` is the business model's own nomination; Microstran `.arc` is plain text and heavily used for AU steel; SPACE GASS has a documented text file. | 2–6 weeks of engineering | after A |

**Do not pick C before A.** The repository's own discipline — "build a native connector only
when a paying engagement funds it" — is correct, and which of the three candidate readers to
build is precisely what conversations 1–3 answer. Building the wrong one is a month gone.

---

## 3. Engineering gaps, ranked by what they block

### P0 — blocks any outsider seeing anything (est. 6–10 hrs total)

**G1. The demo model's findings are the wrong findings. This is the single biggest
marketing blocker and it is not recorded in any existing document.**

`Examples/Saf-House.expected.json` — the flagship demo output — is 25 findings:

| Count | Finding | What a prospect reads |
|---|---|---|
| **14** | "Section *n* states neither dimensions nor stiffness…" (Error) | *your converter lost my sections* |
| 3 | "names profile IPE180 with no source" (Warning) | reasonable, but secondary |
| 6 | "Load group *n* names no load case" (Warning) | housekeeping |
| 1 | unresolved `parentUid` (Warning) | meaningless to an engineer |
| **1** | "Load case 3 has nature Live and is in load group 1, typed Permanent; Variable is the type that nature corresponds to, **and the partial factors a code applies are what the disagreement changes**" | ← **this is the product** |

Fourteen red errors about section libraries dominate a screen whose pitch is "it finds the
model that solves and is wrong." One finding in twenty-five is the marquee claim. Item 4 of
`Pre_Release_Hardening.md` already softened the *wording* of that error; it did not change
that it is 56% of the output.

**Fix:** build `Examples/Demo-Audit.femex` — a hand-authored, realistic model (not SAF-derived)
engineered to fire the marquee judgement checks and nothing else: coincident nodes at a
would-be joint, a projected area load whose direction lies in the slab plane, two
equal-priority overlapping regions, no load case carrying self-weight, a section whose stated
area contradicts its dimensions, a non-planar panel. Target: **~8 findings, ≥6 of them
`Judgement`, zero generic-section noise.** It needs a `.expected.json` (the suite writes it),
a `<None Include>` line in `griffel-femex.Tests.csproj`, and a pass through
`parity-check.ps1`. This model becomes the demo, the landing-page screenshot, the sample
report, and the LinkedIn series' raw material.

**G2. `dotnet test` on a fresh clone goes red and rewrites checked-in baselines.**
`ValidationParityTests` is designed to fail-and-regenerate — defensible in private, hostile to
the first outsider who clones a public repo, and the reason
`griffel-femex.Adapters.Saf.Tests/Corpus/SAF_example_HOUSE_metric_ZYX_220.femex` is sitting
modified in the tree. Gate the rewrite behind an env var or `--update-baselines`; default to
asserting and failing with a diff. **Nothing in §4 week 3 starts until `git clone &&
dotnet test` is green for a stranger.**

**G3. Repos private, nothing published, no CI.** The open-core thesis — "an audit standard is
worth something only if other people's adapters declare their losses in it" — is inert while
the taxonomy is on one laptop. Flip all three public; publish the three 0.1.0 packages to
nuget.org; add one GitHub Actions workflow (build + test, both TFMs) so the badge is real.

**G4. Housekeeping a prospect will notice.** README says schema 1.10, code is 1.11. The
corpus file is uncommitted. Last commit was three weeks ago. Fix the version, commit the tree,
and let §4's cadence fix the third.

### P1 — blocks a *good* demo (est. 4–8 hrs)

**G5. The viewer's `Open SAF…` / `Save As SAF` buttons are dead.** They need a
`GET /api/adapters` responder that does not exist. `Pre_Release_Hardening.md` says to "say the
buttons are inactive" — fine for a private demo, embarrassing on a public page. Either hide
them absent a converter (30 min) or ship `femex serve` as a loopback implementation of the
wire contract already written out in full in `FEMEXViewer.md` (the contract is fixed; Phase D
wrote the client). **Recommended: hide them now, build `femex serve` only if a conversation
asks for it.**

**G6. No sample report published anywhere.** The report is "the thing that is actually sold"
and no one outside can see one. Generate `femex check Examples/Demo-Audit.femex --out docs/`
and commit the resulting self-contained HTML as a permanent link. It is the single most
persuasive artefact available and it costs one command.

### P2 — needed before the *first invoice*, not before the first conversation

**G7. Professional indemnity and the word "certify".** `FEMEX_BusinessModel.md` leaves this
explicitly open, and it changes from theoretical to real the moment an Australian consultancy
is invoiced for a report on their model. Two questions for an insurer/accountant, not for this
document: does supplying a paid findings report on a structural model fall inside or outside
existing cover, and does it constitute providing professional engineering services for
registration purposes in the states sold into (Queensland's RPEQ regime in particular).
**Until answered: conversations and free checks only, no invoice.** The README's existing
non-certification stance is the right shield and should appear verbatim in engagement terms.

**G8. No machine-readable schema.** No `*.schema.json` exists; the spec is a `.docx`, a
markdown file and the C# types. This undercuts "other people write adapters in our taxonomy."
Not urgent for AU/NZ sales; genuinely urgent for the SAF-community credibility channel in §4.

**G9. Adapter #2.** Deferred to a conversation outcome per §2. Do not start speculatively.

---

## 4. The marketing plan

### 4.1 Positioning

**One sentence, for a structural engineer:**

> An independent check on a structural model — one that does not belong to the program the
> model was built in.

**The wedge, stated as the business model already states it:** every analysis package checks
its own model inside its own solver, which is exactly the check an independent reviewer cannot
rely on. **Nobody sells model checking as a standalone, program-neutral product.** Of the nine
players in the competitive set, not one sells Check, Compare *or* Certify — let alone the
sequence.

**The three claims, which are already the three CLI verbs:**

| Claim | Question | Status | Lead with it? |
|---|---|---|---|
| **Check** | Is this model sound? | built, 35 rule families | **yes — everything leads here** |
| **Compare** | What changed between Tuesday's model and Friday's? | built, uid-keyed; degrades where uids don't survive | **yes, second** — the question with no incumbent at all |
| **Certify** | What did this crossing cost? | built, 81 declared SAF losses | third, and **never using the word "certify" in public copy** |

**Rules for every piece of copy, non-negotiable:**

1. Never say *certify*, *guarantee*, *safe to use*, *fit for purpose*, *we confirm*. The
   product's own tests enforce this; the marketing must not undo it.
2. **Never lead with numbers of tests or lines of validation.** §4 of the business model:
   don't say "632 tests"; say *"it finds the load case that carries no self-weight, and the
   two regions whose overlap resolves by list order."*
3. Show a finding, not a feature. Every post, every slide, every email contains one real
   sentence the tool emits.
4. State the limit before they ask it. "This is what is in your file. Not: this is my view of
   your engineering." That sentence closes more sceptics than any claim.

### 4.2 Who, specifically, in AU/NZ

**Buyer ≠ user, and the business model already ruled out the wrong one.** Do not sell to BIM
managers or technical directors — small deal, slow institutional cycle, no champion. Sell to
the person who **signs the model**.

Three named segments, in priority order:

1. **The independent checker / peer reviewer.** Someone already paid to form a view on a model
   they did not build. Their current tool is eyeballing and spot-checking. This is the sharpest
   fit for Claim 1 and the only segment where the deliverable — a report for the project file —
   is already a thing they produce. *Includes the statutory review workflows: verify in
   conversation whether Queensland RPEQ certification of others' designs and the NSW Design and
   Building Practitioners declarations create a repeatable version of this. Treat as a lead, not
   a fact.*
2. **The 10–80 person consultancy with a model library.** Buys migration and library-audit
   engagements. One-off, four figures, and every engagement hands over a corpus of real exported
   files — the thing `FEMEX_Interop_Status_16082026.md` §4 says has never existed.
3. **The senior engineer inheriting a colleague's model.** Highest-frequency pain, lowest
   individual willingness to pay, and the top of the free-tier funnel.

### 4.3 The offer ladder

| Tier | What | Price | Ready |
|---|---|---|---|
| Free | `femex check` running locally on your own file; nothing uploaded | $0 | now (after G3) |
| **Audit engagement** | A fixed-price findings report on one model or a model library | **four figures AUD** | now (after G7) |
| **Migration engagement** | Robot→ETABS, or a merger consolidating two libraries, with an audit trail | five figures AUD | now (after G7) |
| Subscription | Hosted Check + Compare, per seat, annual, sold as a checking tool | later | gated on §4.6 |

**Do not publish a price list.** Quote per engagement. The number to hold as a ceiling is the
$60/unit/month the industry has now rejected twice (Flux, Konstru); the number to hold as a
floor is IDEA StatiCa charging seat-year money for a *check* while giving the *transfer* away.

### 4.4 Channels, sized for 4–6 hrs/week

Five channels. Two are the plan; three are cheap amplification.

**1. Warm 1:1 conversations — 2 hrs/wk. This is the plan.**
Twenty named people, six questions, thirty minutes each. Highest signal-per-hour available and
the only channel that answers the question every document since Phase C has deferred: *is the
judgement half of `Validate()` what engineers actually want checked?* Nobody has ever watched
an engineer read a finding.

**2. LinkedIn, one post a week — 1 hr/wk. This is the whole public surface.**
The AU structural community lives there. The content engine writes itself: **each judgement
check is one post.** A short model, a picture, one sentence the tool emits, and the question
"has this bitten you?" That is 20+ posts of material already sitting in
`FemexModel.Validation.cs`, each of which is a finding an engineer will recognise. No
treadmill, no calendar to invent.

**3. One talk — 4 hrs, once.** Engineers Australia structural branch, an Australian Steel
Institute event, or a local digital-engineering meetup. Forty of the right people in a room
beats a quarter of posting. Pitch the talk as *"models that open cleanly, solve, and are
wrong"* — not as a product demo.

**4. Open-source distribution — 0 hrs/wk after setup.** Public repos, three NuGet packages, a
one-page site, a permanent link to a real sample report. Inbound arrives on its own or does
not; either way it costs nothing to keep.

**5. The SAF ecosystem — 1 hr/wk for a few weeks, then nothing.** The underrated one. This
repository holds **the only independent conformance suite for SAF adapters in existence**,
plus the only implementation that declares what a SAF crossing cost in a fixed taxonomy. That
is genuinely novel to SCIA, Dlubal, StruSoft, AxisVM, ConSteel and the SAF working group — a
constituency that will read it on technical merit, that has no reason to see FEMEX as a
competitor, and that is the correct audience for the open-core half of the strategy even
though it is not the sales beachhead. One post in the SAF community channels and one issue/PR
conversation on the SAF repos buys more credibility per hour than anything else public.

**Explicitly not doing, at this capacity:** paid ads, a newsletter, a YouTube channel, SEO
content, conference sponsorship, a Discord/Slack community, cold email at volume.

### 4.5 The twelve weeks

**Weeks 1–2 — Conversations only. Nothing public.**
- Engineering: G1 (demo model), G2 (test gate), G4 (README/commit). ~8 hrs.
- Marketing: list 20 names. Send the warm-intro email (A3). Book 6. Run them.
- Gate: `FEMEX_BusinessModel.md` §8 carries a pre-committed decision rule — **if questions 1–3
  land, build Claim 1 out and put it in front of the people who answered; if they are shrugs,
  FEMEX is an open-source format plus a services business.** Honour it.

**Weeks 3–4 — Make it public.**
- Engineering: G3 (repos public, NuGet, CI), G5 (hide dead buttons), G6 (sample report). ~8 hrs.
- Marketing: one-pager (A1) and landing page (A2) finished. Six more conversations.
- End of week 4: three public repos, three published packages, one live sample report, zero
  marketing spend.

**Weeks 5–8 — Say it out loud.**
- LinkedIn launch post (A5), then one finding-post per week (A6 outlines four).
- The SAF-ecosystem post (A7).
- Pitch the talk.
- First **free** audit on a real model from a conversation — *"send me an export and I'll run
  it and walk you through what it says."* This is the engagement, unpriced, and it is how the
  price is learned.
- G7 in parallel: the insurance and registration questions asked.

**Weeks 9–12 — Convert.**
- Turn the first free audit into a quoted second one.
- Deliver the talk.
- Decide adapter #2 from what conversations 1–12 actually said, and build it only then.
- Review the §4.6 gates.

### 4.6 What counts as working

Pre-committed, so week 12 is a measurement rather than a negotiation:

| By week 12 | Green | Amber | Red |
|---|---|---|---|
| Conversations run | 12+ | 6–11 | <6 |
| Q1–Q3 landed (not shrugs) | 8+ of 12 | 4–7 | <4 → **the QA play has no market; pivot to format + services** |
| Someone asked "can I run this on my model?" unprompted | 3+ | 1–2 | 0 |
| A paid engagement quoted | 1+ | verbal interest | none |
| GitHub stars / NuGet installs | any inbound at all | — | irrelevant if the above is green |

The last row is deliberately near-worthless. Konstru had a whole product and three silent
years; vanity metrics are how that looks from the inside.

---

## 5. Assets to write

All in this folder, except A2's page which is a standalone HTML file.

| | Asset | File | Notes |
|---|---|---|---|
| **A1** | One-pager | `OnePager.md` | Problem → the three claims → one real finding → the non-certification limit → what it costs. One page, no logos, no stock imagery. |
| **A2** | Landing-page copy + page | `LandingPage.md` + `landing.html` | Single page: headline, the sample-report link, the four-line install, the free-tier promise ("nothing is uploaded"), the limit stated plainly, one email address. No signup form. |
| **A3** | Warm-intro email | `Outreach-Warm.md` | 90 words, asks for 30 minutes, does not pitch, does not attach anything. |
| **A4** | Six-question interview guide + capture sheet | `Interview-Guide.md` | The six questions verbatim from `FEMEX_BusinessModel.md` §8, the decision rule printed at the top so it cannot be renegotiated afterwards, and a one-page-per-conversation capture template. |
| **A5** | LinkedIn launch post | `LinkedIn-Launch.md` | Leads with a finding, not with a launch. Links the sample report. |
| **A6** | Four finding-post drafts | `LinkedIn-Findings.md` | Self-weight; coincident nodes; equal-priority overlapping regions; the projected load that projects to zero. Each ≤120 words + one image. |
| **A7** | SAF-community post | `SAF-Community-Post.md` | The conformance suite and the loss taxonomy, addressed to adapter authors. Technical register, no sales language. |
| **A8** | Demo script | `Demo-Script.md` | Exact 10-minute run. **Includes the do-nots: never run `dotnet test` live, never click the viewer's SAF buttons, never open `Saf-House.femex` as the headline.** |
| **A9** | Engagement one-pager + terms skeleton | `Engagement-Terms.md` | Scope, deliverable, price band, and the non-certification clause lifted verbatim from `README.md:48–63`. Marked *needs review before first invoice* per G7. |

---

## 6. Files this plan touches

**Engineering (P0/P1):**
- `Examples/Demo-Audit.femex` + `.expected.json` (new) — the demo model, G1
- `griffel-femex.Tests/griffel-femex.Tests.csproj` — one `<None Include>` line beside the eight existing
- `griffel-femex.Tests/ValidationParityTests.cs` — gate the baseline rewrite, G2
- `README.md` — schema 1.10 → 1.11, plus a sample-report link, G4/G6
- `.github/workflows/build.yml` (new) — build + test both TFMs, G3
- `../griffel-femex-viewer/femex-viewer.html` — hide adapter buttons absent a converter, G5
- `docs/Demo-Audit.report.html` (new, generated) — the public sample report, G6
- `../griffel-femex-viewer/parity-check.ps1` — no edit needed; it enumerates `Examples/*.femex`

**Marketing:** the nine files of §5, all new, all in this folder.

**Reuse rather than reinvent:** every finding quoted in every asset comes from
`FemexModel.Validation.cs` verbatim — never reworded. `HtmlReport.Write` produces the sample
report. `FEMEX_BusinessModel.md` §8 supplies the six questions and the decision rule. The
non-certification paragraph is `README.md:48–63` and is quoted, not paraphrased.

---

## 7. Verification

**G1 — the demo model earns its place.**
```bash
dotnet run --project griffel-femex.Cli -- check Examples/Demo-Audit.femex --format text
```
Assert by eye: ≥6 findings categorised `judgement`, **zero** "states neither dimensions nor
stiffness", and the first three lines on screen are sentences an engineer would recognise as
being about their structure. Then `cd ../griffel-femex-viewer ; .\parity-check.ps1` — 9 of 9.

**G2 — a stranger's first five minutes.**
Clone into a clean directory, `dotnet build && dotnet test`. Green, and `git status` clean
afterwards. This is the acceptance test for going public.

**G3 — distribution is real.**
`dotnet add package Griffel.Femex` resolves from nuget.org on a machine that has never seen
this repo. The Actions badge is green on a fresh push.

**G5/G6 — the demo cannot embarrass you.**
Open `femex-viewer.html` from `file://` in headless Edge with `--dump-dom`: zero `fetch`
calls, zero adapter buttons (the existing Phase D assertion, unchanged). Open
`docs/Demo-Audit.report.html` with DNS blackholed and confirm it renders complete.

**The marketing plan itself** is verified by §4.6's table at week 12, and by one thing before
that: after conversation six, which of the six questions landed should be statable in one
sentence. If it is not, the conversations are being run as demos rather than as interviews.

---

## 8. What not to do

- **Don't build adapter #2 yet**, however obvious ETABS `.e2k` looks. Three candidates, one
  month each, and conversations 1–3 pick between them for free.
- **Don't build `femex serve` or the hub.** Phase E is gated on §8 landing and that gate has
  never been tested. Hiding two buttons costs 30 minutes.
- **Don't publish a price list**, and don't quote $60/anything.
- **Don't launch publicly before the demo model exists.** The current headline output is 14
  red errors about section libraries, and first impressions of a *checking* product are the
  whole product.
- **Don't invoice before G7 is answered.**

---

## Still open

- **Whether the judgement half of `Validate()` is what engineers actually want checked.**
  Unchanged from `FEMEX_BusinessModel.md`. §4.5 week 1 is the first attempt to close it.
- **Which AU/NZ program the second adapter reads.** Three candidates, no evidence, and the
  evidence is two weeks of conversations away.
- **Whether a paid findings report is inside existing professional indemnity cover**, and
  whether it constitutes providing professional engineering services for registration
  purposes in Queensland. G7. Blocks the first invoice, nothing before it.
- **What an audit engagement is actually worth in AUD.** §4.3 gives a band from two
  precedents and no quote has been given to anyone.
- **Whether the free local checker cannibalises the service or feeds it** — inherited from
  `FEMEX_BusinessModel.md` and untouched here, because with no AU input path the free tier is
  not yet reachable by the beachhead at all.
- **Whether 4–6 hrs/week of marketing survives contact with an engagement.** The failure mode
  named in the business model — engagements crowding out the product — applies equally to
  engagements crowding out the conversations that would find the next one.
