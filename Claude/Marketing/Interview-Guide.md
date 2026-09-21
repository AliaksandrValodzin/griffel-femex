# A4 — Interview guide and capture sheet

Asset A4 of `FEMEX_GoToMarket.md` §5, for steps 0.14 and 1.13: twelve 30-minute conversations,
booked by A3 (`Outreach-Warm.md`), and one capture sheet per conversation. Step 0.15
synthesises the sheets against the rule below.

---

## The decision rule — written before the first conversation, not negotiated after

From `FEMEX_BusinessModel.md` §8, verbatim:

> **Question 6 decides whether `FemexResults` is built. Questions 1–3 decide everything else:**
>
> - **If 1–3 land** — build Claim 1 immediately and put it in front of the people who answered.
> - **If 1–3 are shrugs** — then the QA play has no incumbent because it has no market, and the
>   honest conclusion is that FEMEX is an open-source format plus a services business. Under
>   these constraints that is still a real outcome, and it is reached two years earlier than by
>   building five adapters first.

And the week-12 thresholds from `FEMEX_GoToMarket.md` §4.6:

| By week 12 | Green | Amber | Red |
|---|---|---|---|
| Conversations run | 12+ | 6–11 | <6 |
| Q1–Q3 landed (not shrugs) | 8+ of 12 | 4–7 | <4 → **the QA play has no market; pivot to format + services** |
| Someone asked "can I run this on my model?" unprompted | 3+ | 1–2 | 0 |

### What "landed" and "shrug" mean

Score this on the sheet **straight after the call, before you talk yourself into anything.**
Base it on what they said, not on how friendly they were.

| Score | Meaning | What it sounds like |
|---|---|---|
| **Landed** | A specific story of cost: time, money, rework, a near miss, a late surprise. Or a workaround they built themselves. | *"We lost two days on…"*, *"I've got a spreadsheet I use for…"*, *"That's exactly what went wrong on…"* |
| **Mild** | They agree it's a problem but can't name a time it hurt. | *"Yeah, that can be annoying."* |
| **Shrug** | It's handled, rare, or someone else's job. | *"We just rerun it."*, *"Never really come up."*, *"The software checks that."* |

For the rule, only **Landed** counts as landed. **Mild counts as a shrug.** Politeness produces
a lot of "mild", and that is the thing this rule exists to filter out.

A conversation counts as "Q1–Q3 landed" when **at least two of the three** scored Landed.

---

## Running the conversation

**Do**
- Open with the promise from the email: 30 minutes, no pitch. Then keep it.
- Ask about the **last time**, not in general. *"Tell me about the last model you inherited"*
  gets a story. *"How do you usually…"* gets an opinion.
- Let silences run. The useful sentence usually comes after the pause.
- Write down their words, not your summary of them.
- End on time, and thank them.

**Don't**
- Don't demo, show the report or describe features unless they ask. If they ask, show one
  finding from `Demo-Audit.femex` and record that they asked.
- Don't finish their sentences or suggest answers (*"so would a tool that… help?"*).
- Don't correct them about what their software does.
- Don't ask what they'd pay. Price is learned from the free audit in step 2.11, not from a
  hypothetical.
- Never say *certify*, *guarantee*, *safe to use*, *fit for purpose* or *we confirm*
  (§4.1 rule 1). That holds in speech too.

### Timing

| Min | |
|---|---|
| 0–2 | Thanks, the no-pitch promise, what they do and the program they model in |
| 2–22 | Questions 1–5, about 4 minutes each |
| 22–26 | Question 6, **only if 1–5 landed** |
| 26–30 | "Who else should I talk to?" Thanks. Stop. |

---

## The six questions

Verbatim from `FEMEX_BusinessModel.md` §8. Ask them as written. The follow-ups under each are
optional prompts for when an answer stays general.

**1. When you inherit a model from a colleague, what do you check before you trust it, and how
long does that take?**
- *Tell me about the last one.*
- *What did you find?*
- *Has something ever got past that check?*

**2. When a model changes between two issues, how do you find out what changed? What has it
cost you when you found out late?**
- *What happened the last time you found out late?*
- *Who told you?*

**3. On a peer review or independent check, how do you compare your model against theirs
today?**
- *Do you rebuild it, or work from theirs?*
- *What does the review leave behind in the project file?*

**4. What would you want to see in a report you could put in the project file saying this
model was checked?**
- *Who reads it?*
- *What would make you distrust it?*

**5. Has a conversion ever put something wrong into a model you signed?**
- *Which programs, which direction?*
- *How was it caught?*

**6. *(Only if 1–5 land.)* Would forces in that comparison change your answer, or is geometry
enough?**
- *Where would forces matter most: member design, connections, the review?*

### Closing questions — ask every time

- *Who else do you know who spends a lot of time checking models they didn't build?*
  (This builds the next list of 20.)
- *Would it be all right if I came back to you when there's something to look at?*

---

## Capture sheet — one per conversation

Copy this block into a new file for each conversation, for example
`Conversations/03-[initials]-[yyyy-mm-dd].md`. Fill in the top straight after the call, and
keep it to one page.

```markdown
# Conversation [n] — [initials], [yyyy-mm-dd]

| | |
|---|---|
| Role / firm size | e.g. associate, 40-person consultancy |
| Segment (§4.2) | independent checker / consultancy with a model library / engineer inheriting models |
| Analysis program(s) they model in | e.g. SPACE GASS, Microstran, Strand7, ETABS, RAPT, other: |
| Exports they handle, and in what format | |
| Does statutory review apply? (RPEQ certification, NSW DBP declarations) | yes / no / unsure — details: |

## Scores

| Q | Landed / Mild / Shrug | The words that decided it (verbatim) |
|---|---|---|
| 1 Inherit a model | | |
| 2 Changes between issues | | |
| 3 Peer review / independent check | | |
| 4 Report for the project file | | |
| 5 A conversion put something wrong in | | |
| 6 Forces vs geometry (only if 1–5 landed) | asked / not asked | |

**Q1–Q3 landed (≥2 of 3 scored Landed)?** yes / no

## Unprompted signals

- [ ] Asked "can I run this on my model?", or similar, **without being led there**
- [ ] Offered to send an export
- [ ] Named a colleague to talk to
- [ ] Asked about price

## The best story, in their words

> 

## What surprised me

- 

## Follow-ups

- [ ] Thank-you sent
- [ ] Introductions to chase:
- [ ] Anything I promised to send:
```

---

## After each conversation

1. Fill in the sheet **the same day**, scores first.
2. Tally it on the running table below.
3. After conversation 6, write **one sentence** saying which of the six questions landed. If you
   can't, the conversations are being run as demos rather than as interviews (§7).
   Change how you run them, not the scoring.

## Running tally

| # | Date | Segment | Program | Q1 | Q2 | Q3 | Q1–3 landed? | Q6 | Asked to run it? |
|---|---|---|---|---|---|---|---|---|---|
| 1 | | | | | | | | | |
| 2 | | | | | | | | | |
| 3 | | | | | | | | | |
| 4 | | | | | | | | | |
| 5 | | | | | | | | | |
| 6 | | | | | | | | | |
| 7 | | | | | | | | | |
| 8 | | | | | | | | | |
| 9 | | | | | | | | | |
| 10 | | | | | | | | | |
| 11 | | | | | | | | | |
| 12 | | | | | | | | | |

**After 6 (step 0.15):** which questions landed, in one sentence:

**After 12:** Q1–Q3 landed in __ of 12 → green / amber / red.

### Two things the tally also answers

- **Which program to read next (adapter #2, §2).** Count the "Analysis program" column. The
  plan's AU list (SPACE GASS, Microstran, Strand7, ETABS/SAP2000, RAPT/RCB, Multiframe) is
  a hypothesis for the first five conversations to confirm or break.
- **Whether statutory review is a repeatable market (§4.2 segment 1).** Count the "yes"
  answers on RPEQ and NSW DBP. Treat it as a lead, not a fact, until someone describes the
  workflow.
