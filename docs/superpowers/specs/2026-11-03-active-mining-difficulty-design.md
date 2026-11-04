# Active Mining Difficulty and Skill Rewards

**Status:** Approved
**Date:** 2026-11-03
**Builds on:** `2026-07-04-active-mining-minigame-scene-design.md`, `2026-09-29-mining-claim-budget-design.md`
**Reverses:** the 2026-07-04 decision to remove per-target timeouts

Items marked *(drafting call)* were chosen while writing this and have not been reviewed yet.
Everything else was agreed in conversation on 2026-11-03. All numbers are starting values for
playtesting, not final balance.

## 1. Why

Players find active mining too easy. The cause is structural, so raising one number won't fix it:

- **Players almost can't lose.** A run is 5–12 taps with 3 seconds allowed per tap, and targets
  wait until they are tapped. The asteroid turns only 5–15° per second, so targets barely move.
- **Skill doesn't pay.** Any finished run pays 100%, the same as idle mining, which has no risk.
- **Difficulty runs backwards.** Taps come from yield, so the most valuable asteroids are the
  shortest: Iron is 10 taps for 160 coins, Helium-3 is 5 taps for 1,200 coins (average yield,
  before the drone multiplier).

## 2. Goals

- A run can realistically go badly, and a good run feels earned.
- Playing better pays more, on every run.
- Higher-tier asteroids are harder and bring new challenges.
- Active and idle are a real choice: idle is safe and slower, active can pay more but can pay less.

## 3. Non-goals

- Paying active runs above 100% of the asteroid's full quantity.
- Having the server verify how a run went.
- Rebalancing mineral prices, drone prices, land costs or the 4-hour respawn.
- Pinning an asteroid's yield roll between Planet loads. It re-rolls on every load today, and that
  stays open.
- Later ideas from the same discussion: drone abilities in the minigame, asteroid cracking phases,
  combos, personal bests, daily quests and leaderboards.
- A tutorial system. M11 owns onboarding; this design only adds short text on the pre-game panel.

## 4. Anti-goals

This has failed, even if it works as specified, if:

- A new player's first Tier 1 run feels punishing.
- Players lose because of finger size or device rather than skill.
- A Perfect grade is physically out of reach at the top tier.
- Players can avoid a bad result by quitting and retrying.
- The server starts trusting a score sent by the client.

## 5. How a run plays

The pre-game panel, scene swap and Continue flow stay as they are. After Start:

1. A target appears on the asteroid with a ring closing around it. There is no overall countdown.
2. The earlier the player taps, the better the grade *(drafting call: the alternative is timing the
   tap to the moment the ring touches the target, which has more skill but has to be learned)*:

   | Tapped | Grade | Share banked |
   |---|---|---|
   | By the tier's "Perfect by" time | Perfect | 100% |
   | By the tier's "Good by" time | Good | 80% |
   | Before the ring closes | Late | 60% |
   | Ring closes first | Miss | 0% |

3. Each target is worth an equal share of the asteroid's minerals. A counter shows the amount
   banked so far, climbing with each hit.
4. The next target appears 0.25 seconds after the previous one resolves *(drafting call)*.
5. The run ends in one of two ways. The asteroid is used up and respawns after 4 hours either way:
   - **Cleared:** every target has resolved. The player receives what they banked.
   - **Failed:** the tier's miss limit is reached. The player still receives what they banked.
     Only a run that banked nothing pays nothing.

**What counts as a miss.** A closed ring uses up that target, so its share is lost. A wrong tap
(on a decoy or empty space) adds a miss but costs no share: live targets stay where they are and
their rings keep running, so a wrong tap can't reset a nearly closed ring *(drafting call)*.

**Payout.**

```
fullQuantity = round(RemainingYield × EffectiveYieldMult)     // today's MineralQuantity
banked       = sum(share banked per target) / Targets
quantity     = banked > 0 ? clamp(round(fullQuantity × banked), 1, fullQuantity) : 0
```

Example: Iron with yield 80 and the Scout drone, 8 targets.

| Outcome | Banked | Iron |
|---|---|---|
| All Perfect | 100% | 80 |
| 6 Perfect, 1 Good, 1 Late | 92.5% | 74 |
| 4 Perfect, then 3 misses | 50% | 40 |
| Idle mining, for comparison | 70% | 56 |

## 6. Difficulty by tier

Difficulty comes from the asteroid's tier (`AsteroidDefinition.Tier`). Yield only affects the
payout, and the drone doesn't affect difficulty.

| Tier | Targets | Ring time | Perfect by | Good by | Max misses | Decoy chance | Live targets | Spin °/s | Hold and drag |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 8 | 1.6s | 0.8s | 1.2s | 3 | 0 | 1 | 5–15 | none |
| 2 | 10 | 1.3s | 0.75s | 1.05s | 3 | 25% | 1 | 5–15 | none |
| 3 | 12 | 1.1s | 0.7s | 0.9s | 3 | 25% | 2 | 5–15 | none |
| 4 | 14 | 1.0s | 0.65s | 0.85s | 3 | 35% | 2 | 30–45 | none |
| 5 | 16 | 0.9s | 0.6s | 0.75s | 3 | 35% | 2 | 30–45 | every 4th target |

*(Drafting calls: challenges build up rather than replace each other; the per-tier "Perfect by" and
"Good by" columns; Tier 4 and 5 ring times raised from the 0.9s and 0.75s first suggested, because a
player needs roughly 0.5 seconds to see and reach a target at a new spot, which would leave Perfect
unreachable; drift comes from the faster spin alone, with no separate drift movement.)*

**Decoys.** A decoy may appear alongside a real target. It differs in shape as well as colour, so
it stays readable for colour-blind players. Tapping one is a miss. If ignored, it disappears when
its ring closes at no cost, and it doesn't count toward Targets. Decoys give way when the screen is
crowded: one with no clear spot isn't shown, a real target dismisses any decoy it would overlap, and
decoys draw below real targets.

**Two live targets.** The second target joins when the first is about 40% through its ring, so they
never close together and the player has to choose an order *(drafting call)*. The 0.25-second gap
still applies after every resolve, so a tap never brings a replacement at once. Markers keep enough
distance that their tap areas don't overlap.

**Faster spin.** Targets ride the asteroid's surface, so spin moves them across the screen. A target
must not reach the far side before its ring closes. `PickFacingPoint` currently allows any point in
the facing hemisphere, so limit placement to a cone of about `85° − maxSpin × ringTime` around the
viewer.

**Hold and drag (Tier 5).** A vein is a line between two surface points. The player presses the
start, drags to the end, and is graded on finish time. It counts as one target. This is the riskiest
item, so Tier 5 ships without it if it doesn't feel good. *(Drafting calls for all of the following.)*

- **Failed attempt:** lifting early or straying off the line uses up the vein, like a closed ring. It
  is a miss, its share is lost and there is no retry.
- **Screen to itself:** a vein waits for live targets to resolve, and no target or decoy appears until
  it resolves. This keeps it one-finger, with no multi-touch.
- **Spin:** the asteroid eases down to 5°/s while a vein is live, so the player isn't tracing a line
  moving at full speed. Use that slower speed in the placement cone.
- **Ring time:** starts at 1.6s, with Perfect and Good times to be set in playtest.
- **Length:** 300-550 canvas units, so the grade reflects skill and not how long a vein happened to be.
  If veins stay, a time scaled to length is the better fix.
- **Press feedback:** the start marker grows and turns gold while held, so a press that missed is obvious.

The session's target model should allow for a vein from the start, as a target kind that takes the
screen to itself, so Phase 3 doesn't force a rework of Phase 1.

## 7. Idle pays less

Idle mining pays 70% of `fullQuantity` (`_idleYieldFraction`). Its durations don't change. Without
this, players would choose idle for the best asteroids, where it takes only 30–45 seconds at no risk.

The mining prompt must show the choice, or players won't know idle pays less. `MiningModePromptView`
shows no amounts today. Add percentages, for example "Idle: safe, pays 70%" and "Active: up to 100%,
depends on how you play" *(drafting call)*.

Don't show mineral amounts here. `RemainingYield` re-rolls by ±20% on every Planet load and idle pays
from the roll at claim time, so "+56 Iron" could pay 45–67. Exact amounts would also show players
that reloading Planet can get them a better roll.

Players who only idle earn 30% less minerals. Prices aren't changing here, so watch this in playtest.

## 8. Fairness and feedback

A harder game needs these to feel fair, so they are part of this work:

- **Bigger tap area.** Keep the 80×80 marker but make the tappable area about 140×140
  *(drafting call)*, since 80 units on a 1080-wide canvas is small on a phone.
- **No accidental misses.** Today a quick double-tap counts as a miss, because the target moves after
  the first tap and the full-screen `MissArea` catches the second. Ignore off-target taps for
  0.2 seconds after a hit, and whenever no target is live *(drafting call)*.
- **Miss feedback.** Misses and failed runs currently play no sound or effect. Add both, plus a
  floating Perfect, Good or Late label on hits. Add `ActiveMiningMiss` and `ActiveMiningFail` to the
  end of `SfxId`, not the middle, in case the catalog stores its values by number. Reuse an existing
  clip as a placeholder until real audio exists.
- **Correct result screen.** `ActiveMiningMinigameView.ShowResult` shows `RemainingYieldAtStart`, which
  ignores the drone multiplier: a 50-yield asteroid with a ×1.5 drone shows +50 but grants 75. Show
  the actual `quantity`.
- **Claim what the screen showed.** `CompleteActiveMiningAsync` recomputes the reward after Planet has
  reloaded, by which point the asteroid's yield has re-rolled, so the claim can differ from the result
  screen. Compute `fullQuantity` once in `BeginActiveMining`, keep it in the handoff and claim from
  that value.
- **Short instructions.** The pre-game panel says how the ring works and names every challenge this tier
  has. Tiers are per mineral, so a player can reach a tier having skipped the ones before it.

## 9. Quitting mid-run

The run lives only in memory (`ActiveMiningHandoff`), and the claim is sent after returning to Planet.
If the app is closed mid-run or on the result screen, nothing is recorded and the asteroid is still
there. Failure is rare today, but with graded payouts this becomes a free retry.

*(Drafting call)* Record each run locally:

- Save a marker when the player presses Start: player, planet, slot, mineral, full quantity, banked 0.
- Update it when the app is paused or quit, and when the run ends. Don't save on every hit, to avoid
  hitches on phones.
- Clear it when the server answers the claim or forfeit, whether it grants or rejects. If the call
  throws, there is no answer, so keep the marker.
- If a marker exists when Planet next loads, empty its slot straight away and settle it like a failed
  run: pay what was banked and use up the asteroid. A marker kept after a failed call retries this way.
- Drop a marker that names a different player or planet, because PlayerPrefs is shared by every account
  on the device.
- The normal return from the scene and the marker must not both claim the same run.

**Why keep it until the server answers:** clearing it earlier would either lose the reward or bring the
asteroid back after a dropped connection, which is a free retry. If a response is lost after the server
committed, the slot comes back from `GetAsteroidRespawns` as waiting to respawn, so its asteroid isn't
there on the next load and the marker clears without a second claim. That read only covers the 4-hour
respawn window. If the player returns later than that, the slot is back on the field, so the retry claims
again and uses up the fresh asteroid. That is acceptable because each grant uses a claim from the planet's
budget, so it can't raise a player's income above the limit, and a rare double grant beats losing a reward.

A crash loses only what was banked since the last save. This stops normal players from retrying, but
it isn't a security measure and doesn't need to be.

## 10. Constraints

**Server: no changes and no deploy.** The minigame stays client-decided, as it is today, and the
server still clamps each claim. `ValidateMining` grants `min(claimedQty, cap)`, and every quantity
here is at or below today's `fullQuantity`, so the cap still holds.

- `banked > 0` is a normal claim: `GrantMiningAsync(planetId, mineralId, quantity, slotId: ...)`. It
  logs the slot and uses a budget claim, as a success does today.
- `banked == 0` stays a forfeit: `ForfeitMiningAsync`. `ValidateMining` rejects a zero-quantity claim.

**Architecture rules.**

- **Economy (Rule 1):** the client still only requests a quantity, and the server commits it.
- **Data (Rule 3):** all tunables live in `EconomyConfig`, next to the existing active-mining values.
  This includes a five-entry tier table of a new serializable `ActiveMiningTierSettings` type in
  `Config/`, the three grade shares, `_idleYieldFraction`, the spawn gap and the tap grace time
  *(drafting call)*. The ActiveMining scene has no `EconomyConfig`, so `ActiveMiningHandoff` carries
  the tier's settings and `fullQuantity`, as it already does for session values.
- **Layering:** `Core` must not depend on `Mining`. The handoff may hold `Config` types and plain values.
- **Performance (Rule 5):** pool target markers, anchors and hit effects. `SpawnHitVfx` currently
  instantiates and destroys per hit.

**Where the code goes.** Logic stays in plain C# in `Mining/ActiveMiningSession` so EditMode tests
can cover it without a scene. `UI/ActiveMiningMinigameView` stays thin: it positions markers and
forwards taps. Multiple markers need a small new view (`UI/ActiveMiningTargetView`).

**To remove.** `_activeYieldPerTap`, `_minActiveTaps`, `_maxActiveTaps`, `_activeSecondsPerTap`,
`_min/_maxActiveSessionSeconds`, `_activeMaxErrors`, `MiningReward.ActiveTapsRequired`,
`MiningReward.ActiveSessionDurationSeconds`, the session countdown and the scene's `TimeText`, which
can become the banked counter.

**Handoff result.** `SetResult(bool succeeded)` also needs the banked share.
`TryFinalizePendingActiveMining` then chooses between a claim and a forfeit by whether anything was
banked, instead of by `Succeeded`. `Succeeded` remains for the result screen.

**Asteroid spin.** `ActiveMiningAsteroidStage` reads spin from serialized fields today. It needs to
take the tier's range instead.

**Scope.** This isn't on the roadmap: it is post-M6 work ahead of M7. The open decisions (age policy,
land resale) aren't affected.

## 11. Build order

Each phase can be played and tested before the next begins. Work stops after Phase 1 and again after
Phase 2 for a play session, and the project owner decides whether to continue. The targets in section
12 are guides, not pass or fail *(drafting call)*.

1. **Core loop.** Closing rings, grades, banked payouts, tier numbers for Targets, ring times and
   misses, removal of the countdown, idle at 70% with prompt percentages, section 8, section 9 and the
   dev-only test tools in section 12. Playtest Tiers 1 and 2 before moving on.
2. **Tap-based challenges.** Decoys, two live targets, faster spin and the placement cone.
3. **Hold and drag.** Tier 5 veins, with a go or no-go after playtest.

## 12. Testing

- **`ActiveMiningSessionTests`** (rewrite): grade boundaries, ring expiry as a miss that uses up a
  target, wrong taps keeping rings running, the miss limit keeping banked share, clearing with all
  targets resolved, the banked calculation, decoys, the second-target stagger, tap grace, and terminal
  stages ignoring further input.
- **`MiningRewardCalculatorTests`:** remove tap and duration cases; add the idle share.
- **`MiningControllerTests` and `ActiveMiningHandoffTests`:** the new `SetResult`; partial bank claims;
  zero bank forfeits; respawn on both; settling a leftover marker. `PlanetTestRootScope` also
  references the handoff.
- **New config test:** the real `EconomyConfig` asset has five tiers, and Perfect by < Good by < ring
  time in each.
- **`ServerCode/` drift tests** should pass untouched. If one fails, this design has changed something
  it shouldn't have.

**Dev-only test tools**, compiled out of release builds *(drafting call)*:

- A way to start a run at any tier, showing a real asteroid of that tier, since reaching Tier 5
  otherwise takes a 15,000-coin drone.
- One `SULog` line per run with tier, grades, misses and banked share, to measure the targets below.

**Playtest setup.** The project owner plays alone on their own phone, installed over USB with dev mode
on (`RootLifetimeScope._devMode`). So the plan needs no key rotation, backend setup or way to share
builds first. A mouse in the Editor doesn't reflect finger speed or accuracy, so this has to be a phone.

- `_devMode` is saved in `Bootstrap.unity`, so don't commit it switched on.
- This will be the first time M6-era code runs on a device, so allow for unrelated device problems.

**Targets for these sessions**, all guesses *(drafting call)*:

- Tier 1 clears nearly every time and banks at least 60%, even when played casually.
- At Tier 5 the owner usually banks 60–85%, with full-Perfect runs rare but achieved.
- A lost run feels like the player's mistake, not the game's.

**Later, once other people can play** (after the key rotation and backend setup):

- A new player usually clears their first Tier 1 run and banks at least 60%.
- Some players choose active and some choose idle on the same asteroid, so neither is always best.
- The game feels fair on both a small and a large phone.

## 13. Left to the builder

Ring and marker art, HUD layout and wording, class and event names beyond those above, pooling
details, vein visuals and drag tolerance, and whether to rewrite `ActiveMiningSession` or extend it.

**Docs to update when this lands:** the Mining rows in `PROGRESS.md` still describe the older
tap-window design, and the Architecture inventory still lists `ActiveMiningMinigame`.
