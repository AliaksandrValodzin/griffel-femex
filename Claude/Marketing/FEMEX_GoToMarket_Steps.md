# FEMEX go-to-market — step-by-step execution

The executable form of `FEMEX_GoToMarket.md`, beside this file. Every step carries a
reference back to the item in that plan which justifies it, and says whether Claude does the
work, helps with it, or stays out of it.

**Legend for the "Claude?" column**

| | Meaning |
|---|---|
| **Yes** | Claude does the work. Give it the prompt in that phase's *What to ask Claude* list. |
| **Assist** | You do the work; Claude drafts, reviews or prepares the input. |
| **No** | Yours alone — your network, your judgement, your credentials, your money. |

The **Done** column is for tracking — mark a step ✅ when it is finished.

**Order matters in two places only.** G1 before any public launch, and G2 before the repos go
public. Everything else can move.

---

## Phase 0 — Weeks 1–2: conversations, and the P0 fixes

*Nothing is public in this phase. Plan ref: §4.5 weeks 1–2.*

### Engineering

| # | Step | Claude? | Plan ref | Est. | Done |
|---|---|---|---|---|---|
| 0.1 | Review and commit the uncommitted `SAF_example_HOUSE_metric_ZYX_220.femex` in the tree (or discard it — it is test-run residue) | Assist | G4 | 15 m | ✅ |
| 0.2 | README: schema `1.10` → `1.11` | Yes | G4 | 5 m | ✅ |
| 0.3 | Design and author `Examples/Demo-Audit.femex` — a realistic small model that fires the marquee judgement checks and nothing else | **Yes** | **G1** | 3–4 h | ✅ |
| 0.4 | Generate its `.expected.json` via the suite's deliberate-red run, read the diff by hand, add the `<None Include>` line to `griffel-femex.Tests.csproj` | Yes | G1 | 45 m | ✅ |
| 0.5 | Run `dotnet run --project griffel-femex.Cli -- check Examples/Demo-Audit.femex --format text` and confirm ≥6 `judgement`, zero "states neither dimensions nor stiffness" | No | G1, §7 | 10 m | ✅ |
| 0.6 | Run `.\parity-check.ps1` in the viewer repo — must be 9 of 9 | No | G1, §7 | 10 m | ✅ |
| 0.7 | Gate `ValidationParityTests`' baseline rewrite behind `--update-baselines` / an env var; default to assert-and-diff | **Yes** | **G2** | 1 h | ✅ |
| 0.8 | Clone the repo into a clean directory and run `dotnet build && dotnet test`. Green, and `git status` clean afterwards | No | G2, §7 | 20 m | ✅ |
| 0.9 | Commit and push all three repos so they are in lockstep | Assist | G4 | 15 m | ✅ |

### Marketing

| # | Step | Claude? | Plan ref | Est. | Done |
|---|---|---|---|---|---|
| 0.10 | Write down 20 named people from your network who sign, check or inherit structural models | **No** | §4.2 | 1 h |  |
| 0.11 | Draft the warm-intro email (asset A3) | **Yes** | §5 A3 | 30 m | ✅ |
| 0.12 | Draft the six-question interview guide and per-conversation capture sheet (asset A4) | **Yes** | §5 A4 | 45 m |  |
| 0.13 | Send A3 to all 20. Book 6 | No | §4.5 | 1 h |  |
| 0.14 | Run 6 × 30-minute conversations. Capture on the A4 sheet, one page each | **No** | §4.4 ch.1 | 3 h |  |
| 0.15 | Synthesise the six capture sheets against §8's decision rule — did Q1–Q3 land, or were they shrugs? | **Yes** | §4.5 gate, §4.6 | 45 m |  |

### What to ask Claude in this phase

- **0.3 / 0.4** — *"Author `Examples/Demo-Audit.femex` per G1 of `Claude/Marketing/FEMEX_GoToMarket.md`. A realistic small steel-and-concrete frame that triggers, at minimum: coincident nodes at a would-be joint, a projected area load whose direction lies in the loaded plane, two equal-priority overlapping plate regions, no load case carrying self-weight, a section whose stated area contradicts its dimensions by >10%, and a non-planar panel contour. Target ~8 findings, at least 6 categorised Judgement, and zero generic-section findings. Then generate the `.expected.json`, add the csproj line, and show me the diff before it goes green."*
- **0.7** — *"Gate `ValidationParityTests`' baseline rewrite per G2. Default behaviour: assert and fail with a readable diff. Opt-in rewrite via `--update-baselines` or an env var. A fresh clone must be green and leave `git status` clean."*
- **0.11 / 0.12** — *"Write assets A3 and A4 per §5 of the go-to-market plan, into `Claude/Marketing/`. A3 is ≤90 words, asks for 30 minutes, pitches nothing, attaches nothing. A4 carries §8's six questions verbatim and prints the decision rule at the top."*
- **0.15** — *"Here are six conversation capture sheets. Against §8's decision rule, did questions 1–3 land or not? Quote the evidence for each. Do not be generous — a polite 'yeah that's annoying' is a shrug."*

**Gate before Phase 1:** step 0.8 green, step 0.5 clean, and step 0.15 answered in one
sentence.

---

## Phase 1 — Weeks 3–4: make it public

*Plan ref: §4.5 weeks 3–4.*

| # | Step | Claude? | Plan ref | Est. | Done |
|---|---|---|---|---|---|
| 1.1 | Add `.github/workflows/build.yml` — build + test, both TFMs, on push and PR | **Yes** | G3 | 45 m |  |
| 1.2 | Flip `griffel-femex`, `griffel-femex-viewer`, `griffel-femex-models` to public | **No** | G3 | 10 m |  |
| 1.3 | Confirm the Actions badge goes green on a fresh push; add it to the README | Assist | G3, §7 | 20 m |  |
| 1.4 | Create a nuget.org account and API key | **No** | G3 | 20 m |  |
| 1.5 | `dotnet nuget push` the three 0.1.0 packages from `artifacts/`; verify `dotnet add package Griffel.Femex` resolves on a clean machine | Assist | G3, §7 | 30 m |  |
| 1.6 | Hide the viewer's `Open SAF…` / `Save As SAF` buttons when no converter answers | **Yes** | **G5** | 30 m |  |
| 1.7 | Re-run the Phase D offline assertion: `file://` + headless Edge `--dump-dom` → zero `fetch`, zero adapter buttons | No | G5, §7 | 15 m |  |
| 1.8 | Generate the public sample report: `femex check Examples/Demo-Audit.femex --out docs/`; commit it; verify it renders with DNS blackholed | Assist | **G6** | 30 m |  |
| 1.9 | Add the sample-report link to the README | Yes | G6 | 10 m |  |
| 1.10 | Draft the one-pager (asset A1) | **Yes** | §5 A1 | 1 h |  |
| 1.11 | Draft the landing-page copy and build `landing.html` (asset A2) | **Yes** | §5 A2 | 2 h |  |
| 1.12 | Buy a domain and point it at the landing page (GitHub Pages is free and already in the stack) | **No** | §4.4 ch.4 | 1 h |  |
| 1.13 | Run 6 more conversations, same A4 sheet | **No** | §4.4 ch.1 | 3 h |  |

### What to ask Claude in this phase

- **1.1** — *"Add a GitHub Actions workflow per G3: restore, build and test on windows-latest, both `netstandard2.0` and `net8.0` legs, on push to main and on PR. It must be green on a fresh clone — G2 is a prerequisite."*
- **1.6** — *"Per G5, hide the viewer's two SAF buttons entirely when `GET /api/adapters` has not answered. Keep the existing Phase D behaviour — zero fetch calls from `file://` — and do not change the Check or Conversion panels."*
- **1.10 / 1.11** — *"Write assets A1 and A2 per §5. Honour §4.1's four copy rules absolutely: no `certify`/`guarantee`/`fit for purpose`, no test counts, one real finding quoted verbatim from `FemexModel.Validation.cs`, and the non-certification limit stated in the reader's first screen. A2's page is one HTML file, no tracking, no signup form, one email address."*

---

## Phase 2 — Weeks 5–8: say it out loud

*Plan ref: §4.5 weeks 5–8.*

| # | Step | Claude? | Plan ref | Est. | Done |
|---|---|---|---|---|---|
| 2.1 | Draft the LinkedIn launch post (asset A5) | **Yes** | §5 A5 | 30 m |  |
| 2.2 | Take the screenshots the posts need, from the viewer on `Demo-Audit.femex` | **No** | §4.4 ch.2 | 45 m |  |
| 2.3 | Draft four finding-posts (asset A6): self-weight; coincident nodes; equal-priority overlapping regions; the projected load that projects to zero | **Yes** | §5 A6 | 1 h |  |
| 2.4 | Post A5, then one A6 post per week | No | §4.4 ch.2 | 15 m/wk |  |
| 2.5 | Draft the SAF-community post (asset A7) — the conformance suite and the loss taxonomy, addressed to adapter authors | **Yes** | §5 A7, §4.4 ch.5 | 45 m |  |
| 2.6 | Post A7 in the SAF community channels; open one issue or PR conversation on the SAF repos | No | §4.4 ch.5 | 1 h |  |
| 2.7 | Draft the demo script (asset A8), including the three do-nots | **Yes** | §5 A8 | 45 m |  |
| 2.8 | Rehearse the demo once end to end against A8 | No | §5 A8 | 30 m |  |
| 2.9 | Draft a talk abstract for Engineers Australia / ASI / a digital-engineering meetup: *"models that open cleanly, solve, and are wrong"* | Assist | §4.4 ch.3 | 30 m |  |
| 2.10 | Pitch the talk to two or three organisers | **No** | §4.4 ch.3 | 1 h |  |
| 2.11 | Ask one conversation contact for a real export and run the **free** audit on it | **No** | §4.5, §2 route B | 2 h |  |
| 2.12 | Interpret that audit and write it up as a report the firm can keep | **Yes** | §2 route B, §4.3 | 2 h |  |
| 2.13 | Ask the insurer: is a paid findings report on a client's structural model inside existing cover? | **No** | **G7** | 1 h |  |
| 2.14 | Ask whether supplying it constitutes professional engineering services for registration purposes in the states sold into (Qld RPEQ in particular) | **No** | **G7** | 1 h |  |
| 2.15 | Draft the engagement one-pager and terms skeleton (asset A9), non-certification clause quoted verbatim | **Yes** | §5 A9 | 1 h |  |

### What to ask Claude in this phase

- **2.1 / 2.3 / 2.5** — *"Write assets A5, A6 and A7 per §5. Every post opens with a finding, not with a launch. Quote the validator's own sentence verbatim — never reword it. Each A6 post ≤120 words and ends with a question. A7 is in a technical register for SAF adapter authors, with no sales language at all."*
- **2.7** — *"Write asset A8, a 10-minute demo script with exact commands. It must explicitly forbid: running `dotnet test` live (it is designed to go red), clicking the viewer's SAF buttons, and opening `Saf-House.femex` as the headline."*
- **2.12** — *"Here is a real export a firm sent me and the report `femex check` produced. Help me read the findings, mark which are about their structure versus about the crossing, and draft the written summary they will keep in the project file. Do not use `certify`, `guarantee`, `safe`, or `fit for purpose` anywhere."*
- **2.13 / 2.14** — *"Draft the two questions I should put to my insurer and to a professional-registration adviser per G7. One paragraph each, factual, describing exactly what the deliverable is and what it explicitly does not claim."* (Claude drafts the questions; the answers come from a professional.)
- **2.15** — *"Write asset A9. Scope, deliverable, price band, and the non-certification clause lifted verbatim from `README.md:48–63`. Head it with a note that it is unreviewed and must not be issued before G7 is answered."*

---

## Phase 3 — Weeks 9–12: convert and decide

*Plan ref: §4.5 weeks 9–12.*

| # | Step | Claude? | Plan ref | Est. | Done |
|---|---|---|---|---|---|
| 3.1 | Turn the free audit (2.11) into a quoted second engagement | **No** | §4.3 | 1 h |  |
| 3.2 | Deliver the talk | **No** | §4.4 ch.3 | 4 h |  |
| 3.3 | Decide adapter #2 from what conversations 1–12 actually said — ETABS `.e2k`, Microstran `.arc`, or SPACE GASS text | **Yes** | **§2 route C, G9** | 1 h |  |
| 3.4 | Write the adapter #2 plan doc as a proper `Claude/AdaptersPlans/` plan, house style, ending in *Still open* | **Yes** | G9 | 2 h |  |
| 3.5 | Build adapter #2 — **only if 3.1 funded it or 3.3 produced clear evidence** | **Yes** | §2 route C | 2–6 wk |  |
| 3.6 | Review every row of the §4.6 gate table and record the result | **Yes** | §4.6 | 45 m |  |

### What to ask Claude in this phase

- **3.3** — *"Here are twelve conversation capture sheets. Which analysis program appears most often as the source of a model someone has to check or inherit? Rank the three candidate file readers by evidence, and say plainly if there is not enough evidence to pick one."*
- **3.6** — *"Score the §4.6 gate table honestly against what actually happened. If the Q1–Q3 row is red, say so and lay out what the plan's own fallback — open-source format plus services — looks like from here."*

---

## Backlog — not scheduled, and deliberately

| # | Step | Claude? | Plan ref | Trigger | Done |
|---|---|---|---|---|---|
| B.1 | Publish a machine-readable `femex.schema.json` | Yes | **G8** | when a third party asks to write an adapter, or when the SAF channel gains traction |  |
| B.2 | Build `femex serve` — the loopback implementation of the wire contract in `FEMEXViewer.md` | Yes | G5 alt. | only if a conversation asks for in-browser conversion |  |
| B.3 | Phase E — the hosted hub | Yes | §8 "don't" | gated on `FEMEX_BusinessModel.md` §8 questions 1–3; untested |  |
| B.4 | Geometric/topological matching fallback for Compare where uids do not survive | Yes | §4.1 Claim 2 | the business model calls this the product's core IP and it is undesigned. Needs its own plan doc first |  |
| B.5 | Results in FEMEX (`FemexResults`) | Yes | — | gated on §8 question 6 |  |

---

## Summary of where Claude carries the work

**Claude does (Yes):** the demo model and its baseline (0.3, 0.4) · the test gate (0.7) ·
CI (1.1) · hiding the viewer buttons (1.6) · every one of the nine marketing assets
(0.11, 0.12, 1.10, 1.11, 2.1, 2.3, 2.5, 2.7, 2.15) · conversation synthesis (0.15, 3.3, 3.6)
· the audit write-up (2.12) · adapter #2 when it is funded (3.4, 3.5).

**You alone (No):** the twenty names and every conversation in them (0.10, 0.13, 0.14, 1.13,
2.11) · flipping the repos and the NuGet account (1.2, 1.4) · the domain (1.12) · posting
(2.4, 2.6) · pitching and giving the talk (2.10, 3.2) · the insurance and registration
questions (2.13, 2.14) · quoting and closing (3.1).

That split is the point. **Every step Claude cannot do is a step that requires a human who
signs structural models to answer a question nobody has yet been asked.**
