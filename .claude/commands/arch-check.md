# /arch-check — Architecture Consistency Check

Run this before starting any implementation task. It reads the architecture document and validates the proposed work against all project rules.

## Steps

1. Read `Social_Universe_Architecture.md` in full (or the relevant milestone + script inventory sections if it is very long).

2. Read the current `CLAUDE.md` Pre-Task Protocol, Architecture Rules, Project Structure, Naming Conventions, and Open Decisions sections.

3. For the task described in `$ARGUMENTS`, produce the following checklist — answer each item explicitly:

---

### Architecture Check Report

**Task:** `$ARGUMENTS`

#### 1. Namespace & Assembly
- Which namespace(s) does this work belong in? (match to the Project Structure table)
- Which `.asmdef` assembly? Confirm no cross-assembly dependency violations.

#### 2. Milestone Scope
- Which milestone owns this work (M0–M7)?
- Is it in scope for the **current** milestone? If not, flag it.

#### 3. Architecture Rules
| Rule | Applies? | Compliant? | Notes |
|------|----------|------------|-------|
| 1. Server-authoritative economy | — | — | — |
| 2. Backend behind interfaces | — | — | — |
| 3. ScriptableObjects for data | — | — | — |
| 4. Decouple via events | — | — | — |
| 5. Mobile performance budget | — | — | — |

#### 4. Open Decisions
- Does this task touch backend choice (UGS vs Nakama)? If yes, flag.
- Does this task touch DI framework (VContainer vs Service Locator)? If yes, flag.
- Does this task touch Sky Discovery (AR vs gyroscope)? If yes, flag.

#### 5. Naming Conventions
- List every new type/file this task will create and confirm each follows the naming conventions (interface `I` prefix, SO `Definition`/`Config` suffix, `Service` suffix, `Screen`/`View` suffix).

#### 6. Existing Scripts
- Are any scripts from the Script Inventory already planned or partially implemented that this task must extend rather than duplicate?

#### 7. Verdict
- **PROCEED** — all checks pass, implementation can start.
- **PROCEED WITH FLAGS** — minor issues noted above; flag to user before coding.
- **STOP** — one or more violations require design change before proceeding.

---

After producing the report, wait for the user to confirm before beginning implementation.
