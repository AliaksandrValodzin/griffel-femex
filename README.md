# griffel-femex

FEMEX is a structural analysis model format, and this repository is its reference
implementation: the schema, the library that reads and writes it, the validator
that says what is wrong with a model, an adapter for
[SAF](https://www.saf.guide/) workbooks, and a command-line tool that ties the
three together.

The problem it exists for is that structural models move between programs badly.
A model leaves one analysis package and arrives in another with something missing,
something approximated, or — the case nobody reports — something invented, because
the receiving program required a number the sender never said and the adapter
supplied a default. From inside an adapter an invention does not feel like a loss.
It feels like success: everything worked, a number was produced, the user got a
model.

So the format is only half of it. The other half is that **every transfer declares
what it cost**, in a fixed taxonomy — `Dropped`, `Approximated`, `Invented`,
`Unmapped`, `Stale` — and a conformance suite that an adapter cannot pass by
staying quiet.

Current schema version: **1.10**.

## The command line

```
femex check    <file...> [--out DIR] [--format html|json|text]
femex compare  <model> <baseline> [--out DIR] [--format html|json|text]
femex convert  <file...> [--to FILE] [--out DIR] [--format html|json|text]
```

- **`check`** reads a `.femex` or a SAF `.xlsx` and says what is wrong with it.
- **`compare`** says what changed between a model and a baseline, matched by uid.
- **`convert`** turns an `.xlsx` into a `.femex` or the reverse, with a report of
  what the conversion cost.

Wildcards are expanded by `femex` itself, so `femex check *.femex` works in any
shell. Exit codes are `0` nothing to report, `1` findings, `2` the tool could not
run — which is the distinction a build script needs and the one most tools do not
make.

```bash
dotnet build
dotnet test
dotnet run --project griffel-femex.Cli -- check Examples/Example1.femex --format text
```

## What the report is, and is not

**The report states findings and provenance. It does not offer an engineering
opinion, and it does not certify anything.**

That is a deliberate limit rather than an omission, and it is worth being plain
about because it is the first thing a reader will want to know. A finding says
what the model contains and what a transfer cost, with enough provenance to trace
where each statement came from. It does not say the model is correct, fit for
purpose, or safe to build from. Whether a report of this kind could ever carry a
professional judgement is a question about indemnity that has not been answered,
so until it is, the word *certify* appears nowhere in any user-facing string in
this repository — and the absence is stated here rather than left to be noticed.

Read a finding as: *this is what is in your file*. Not: *this is my view of your
engineering*.

## Layout

| Project | What it is |
|---|---|
| `griffel-femex` | The format, the model, and `Validate()` |
| `griffel-femex.Adapters.Saf` | The SAF adapter — import, export, loss declaration |
| `griffel-femex.Reporting` | The report, in HTML, JSON and text |
| `griffel-femex.Cli` | `femex` — the three verbs above |

Each has a test project beside it. The library and the reporting layer multi-target
`netstandard2.0` and `net8.0`, so both can be loaded into a `net48` add-in host;
the CLI is `net8.0` alone, because nothing loads a process into an add-in host.

The viewer lives in a separate repository,
[`griffel-femex-viewer`](https://github.com/AliaksandrValodzin/griffel-femex-viewer):
one self-contained HTML file, no dependencies, opened from disk. It is a preview.
Any report that is handed to anyone is produced by `FemexModel.Validate()` in C#,
never by the viewer's JavaScript, and the viewer says so.

## Licence

Apache License 2.0. See [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE).

The format, this library, the SAF adapter, the conformance suite and the
`LossCategory` taxonomy are given away, and that is the point: an audit standard
is worth something only if other people's adapters declare their losses in it.

`femex.exe` links **EPPlus 4.5.3.3 under the LGPL-3.0** as its Excel reader. The
assembly is unmodified, its licence text ships beside the binary as
`licenses/LGPL-3.0.txt`, and a consumer may replace `EPPlus.dll` with their own
build of the same version. The version is pinned exactly, and `NOTICE` explains
why that pin is a licence control rather than a build preference.
