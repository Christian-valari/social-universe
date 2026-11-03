# Backend deploy runbook — Development environment

> **Deploy log:** 2026-10-01 — all 30 scripts on `deploy-list.txt` created and published (v1) in
> `development` with UGS CLI 2.0.0; parameters registered. 2026-10-13 — all 30 redeployed (v2):
> server-side land prices + locked `land_registry` writes (Known Issue #19) and the Cloud Save
> Protected access class (#16), which reset every `development` account's fleet, minerals, fuel,
> travel and owned-tiles records. 2026-10-15 — all 34 deployed: `ClaimSession`, `ReleaseSession`,
> `StartIdleMining` and `GetIdleMining` created (v1), the other 30 updated (single active session #20,
> cross-device consistency #21, server-tracked idle mining #22); verified with
> `ugs cloud-code scripts get --json` — live code and parameters identical to the repo. Not smoke-tested.
> Currencies, OIDC, Vivox, Friends and
> Firebase steps were **not** done by the deploy — see section 2.

How to bring up the UGS **development** environment that test builds use
(`Assets/AppConfig.asset` → `_environment: Development` → UGS environment name `development`),
deploy `ServerCode/`, and check that it works. Everything in `ServerCode/` is the source of truth;
nothing records what is live, so **redeploy from this repo** rather than editing scripts in the
dashboard.

- UGS project: `social-universe`, id `334e88c7-a632-433c-9857-15ac7916335d` (from
  `ProjectSettings/ProjectSettings.asset`, `cloudProjectId`).
- Firebase project: `social-universe-abfdf`; Android package `com.ValariSolutions.SocialUniverse`
  (case matters).

## 1. One-time tooling

1. Install the UGS CLI (needs Node.js): `npm install -g ugs` — or a standalone binary from
   <https://github.com/Unity-Technologies/unity-gaming-services-cli/releases>. Check with `ugs --version`.
   npm's global folder (`npm prefix -g`) may not be on PATH; `deploy.ps1` looks there too, and you
   can always call `& "$(npm prefix -g)\ugs.cmd" <command>`.
2. In the Unity Cloud dashboard → **Administration → Service accounts**, create a service account
   for this project and a key for it. Give it the project roles that allow editing **Cloud Code**
   and **Economy** and viewing **Environments**.
3. Log the CLI in, either interactively (`ugs login`) or by setting these environment variables
   for your shell session — never commit them:
   - `UGS_CLI_SERVICE_KEY_ID`
   - `UGS_CLI_SERVICE_SECRET_KEY`

## 2. Dashboard setup (not in the repo — do once per environment)

In the Unity Cloud dashboard, project `social-universe`:

1. **Environments:** create an environment named exactly `development` if it doesn't exist.
   Select it before each step below — every setting is per environment.
2. **Authentication → Identity providers:** add an **OpenID Connect** provider with ID
   `oidc-firebase` (the name `Net/AuthService.cs` signs in with):
   - Issuer: `https://securetoken.google.com/social-universe-abfdf`
   - Audience / client ID: `social-universe-abfdf`
3. **Vivox:** enable it for the environment (text chat channels).
4. **Friends:** enable it for the environment.
5. **Economy → Currencies:** create `COINS` (Coins, initial 500, max 999999999) and `STARDUST`
   (Stardust, initial 10, max 999999999), then publish — see `ServerCode/ECONOMY_CONFIG.md`.
   UGS CLI 2.0.0 has no Economy support, so `ServerCode/Economy/*.ecc` don't deploy; they stay as
   the reference values `ServerCodeDeployTests` checks against `EconomyConfig`.
6. **Cloud Code:** nothing by hand — step 3 deploys it from the repo.
7. **Cloud Save:** nothing to configure; records are created on first write.

In the **Firebase console** (project `social-universe-abfdf`):

1. **Authentication → Sign-in method:** enable **Email/Password** and **Google**.
2. **Project settings → Android app `com.ValariSolutions.SocialUniverse`:** add the SHA-1
   fingerprints of every key that signs a build testers install — the debug key for Editor/dev
   builds, your upload key, and **Play's app-signing key** if testers install through Play.
   Download the updated `google-services.json` into `Assets/`. (The current file has no Android
   OAuth client and no SHA-1, so Google sign-in will fail on device until this is done.)

## 3. Deploy

From the repo root, in PowerShell:

```powershell
./ServerCode/deploy.ps1 -DryRun      # shows what would change; deploys nothing
./ServerCode/deploy.ps1              # deploys to development
```

The script:

- deploys only the Cloud Code scripts listed in `ServerCode/deploy-list.txt` (currencies are a
  dashboard step — see section 2);
- never deploys `GrantCoins`, `GrantStardust` or `GrantOfflineIncome` — any signed-in player
  could call them (Known Issue #16);
- never passes `--reconcile`, so it never deletes anything already live;
- asks you to type `production` before deploying there (`-Environment production`). Don't, until
  launch.

`ServerCodeDeployTests` (EditMode) keeps the list honest: every function the client calls must
be listed, every script must declare its parameters in `module.exports.params` (the only place
the CLI reads them), and the currency files must match `EconomyConfig`.

**Client and server ship together.** `ValidateMining` changed its request shape (Known Issue
#13): builds from before 2026-09-29 get every mining claim rejected. Since 2026-10-15 every
deployed script also requires the caller's `sessionId` (single active session, Known Issue #20),
so any build older than that gets **every** Cloud Code call rejected with `SESSION_REPLACED`.
Deploy, then give testers a build made from the same commit.

## 4. Smoke test (Editor, signed in, `development`)

Play from `Bootstrap.unity` with a fresh test account, then check, in order:

| # | Do | Expect |
|---|---|---|
| 1 | Sign in (email) | Lands on the Hub. Wallet shows **500** coins / **10** stardust. |
| 2 | Open a planet, idle-mine an Iron asteroid, claim | Reward popup; Mineral Inventory shows iron. No "Mining isn't available" toast. |
| 3 | Sell all minerals | Coins go up by 2 × iron. |
| 4 | Drone Garage → upgrade Yield on Scout | Coins drop by 80; pip fills. |
| 5 | Garage → buy Extractor (600) once you can afford it | Card moves to owned; Scout still owned. |
| 6 | Travel to another planet, land | Fuel drops; landing succeeds; mining there works (checks `current_planet`). |
| 7 | Buy a land tile, then sell it | Coins drop by the price, then rise by the refund. |
| 8 | Open chat (HUD chat button), send a message | Message appears; a second client (ParrelSync) sees it. |
| 9 | Refill fuel with a full tank | A clean "failed" result, not an exception (Known Issue #18). |
| 10 | Sign the same account in on a second client (ParrelSync or a device) | Within a minute — or at once if the first client makes a call or regains focus — the first client shows "Your account was signed in on another device." with a Quit button, and Quit closes the app. It is not signed out. The second keeps playing. |
| 11 | Relaunch the first client | It signs in by itself (newest launch wins), and the second client gets the quit dialog in turn. |
| 12 | Settings → Log out, then sign in again on the same client | The logout succeeds and the new sign-in plays normally. |
| 13 | On client A start a trip to Mars, quit A, then sign in on client B | B opens the Travel scene for the Mars trip, not Earth; Land works once it has arrived (Known Issue #21). |
| 14 | On B, before landing, try to travel elsewhere from the Hub | Refused; B goes to the Travel scene to land the pending trip first. |
| 15 | After landing on Mars on B, sign in on A again | A resumes on Mars. |

For single functions, the Editor-only `CloudCodeTestHarness` (on a GameObject in `Bootstrap.unity`)
has a button per function; its `GrantCoins` / `GrantStardust` / `GrantOfflineIncome` buttons fail
by design because those functions are not deployed.

## 5. What to watch in the Cloud Code logs

These have never run on UGS; if something fails, look here first:

- **Currency writes:** `PurchaseLand`, `PlaceBuild` and the drone functions pass a
  `configAssignmentHash` and `currencyId` when decrementing coins; `ApplyUpkeep`, `SpendCoins` and
  `RefillFuel` do not, and the increment calls omit `currencyId`. If those calls fail, align them
  with `PurchaseLand.js`.
- **Parameters:** each script declares its parameters with types only; the scripts validate their
  own inputs and return reasons (`INVALID_PARAMS`, …) rather than relying on platform validation.
- **Write conflicts (Known Issue #21):** `… write conflict for <player> (attempt n), re-reading`
  warnings are normal under overlapping requests. A `gave up … after 3 write conflicts` warning
  means the call answered `CONFLICT`. Any `REFUND FAILED` error (coins from a drone call, or fuel
  from `StartTravel`) needs a manual fix for that player. `StartTravel: client origin … differs from
  current_planet` means a device showed a stale planet; the server's was used.
- **Active session (Known Issue #20):** a `SESSION_REPLACED` script error is normal when a second
  device signs in. If it shows up for a single client, check that `ClaimSession` returned an id —
  it uses `context.correlationId` as the session id and throws `NO_CORRELATION_ID` without one —
  and that the client log shows `BackendClient: … now active on another device` (the client
  matches `SESSION_REPLACED` in the `ScriptError` message; neither has been seen live yet).
- **`ValidateMining` rejections** are logged with the player, planet, mineral and reason —
  `BUDGET_EXHAUSTED` or `WRONG_PLANET` during normal play means a client/server mismatch, not a
  cheater.
- **Land registry write locks (Known Issue #19):** the nine land functions read `land_registry`
  with `getCustomItems` and write it back with `setCustomItem` passing the `writeLock` they read.
  This assumes Custom Data items return and accept `writeLock` like player items do — if every
  land write fails with 400/409, check that first. `gave up … after 3 write conflicts` warnings
  mean a busy planet; any `RELEASE FAILED`, `RESTORE FAILED`, `CLEAR FAILED`, `RELOCK FAILED` or
  `UNDO FAILED` error means a coin call failed *and* its registry rollback failed, so that tile
  needs a manual fix. `PurchaseLand: client price … differs` / `SellLand: client refund … differs`
  warnings mean `LAND_PRICES` and the planet assets have drifted (or a modified client).
