# Drone purchase popup: design

**Date:** 2026-10-02 · **Milestone:** M6 (Drone Garage, slot management) · **Status:** implemented 2026-10-02 (plan: `docs/superpowers/plans/2026-10-02-drone-purchase-modal.md`)

## Goal

Buying a drone or a fleet slot from the Drone Garage asks for confirmation first, then shows the result in the same popup. Stat upgrades and **Select** keep today's behaviour: one tap, with a toast on failure.

## Player flow

1. The garage's bottom buy button (`DroneGarageView`, driven by `DroneBuyButton`) opens the popup instead of buying immediately.
   - **Drone:** title `Buy <DisplayName>?`, the drone's icon, the tier line (`DroneComparison.TierLine`, e.g. "Mines up to Tier 2 asteroids"), `Cost <price> · You have <coins>`, buttons **Cancel** and **Buy**.
   - **Slot:** title `Unlock a fleet slot?`, no icon (the icon `Image` is hidden; there is no slot art), `Your fleet grows to <slots + 1> slots`, `Cost <price> · You have <coins>`, buttons **Cancel** and **Unlock**.
   - If the balance doesn't cover the cost when the popup opens, the status line reads "Not enough coins" and Buy/Unlock is disabled (same as `LandPurchaseModal`).
2. **Buy/Unlock** switches the status to "Buying…" and disables every button until the result arrives, so the purchase can't be sent twice.
3. The result appears in the same popup:
   - **Success, drone:** title `<DisplayName> joined your fleet!`, body "Select it on its card to mine with it.", a single **OK** button.
   - **Success, slot:** title `Fleet slot unlocked`, body `You now have <slots + 1> slots.`, a single **OK** button.
   - **Failure:** the confirm screen stays, the status line shows the readable reason from `ServerFailureMessages`, and Buy/Unlock and Cancel are enabled again so the player can retry or leave.
4. Drone and slot purchase failures no longer raise the toast, since the popup shows them. Upgrade and Select failures still do.

## Components

| Unit | Folder / namespace | Responsibility |
|---|---|---|
| `DronePurchaseCompletedEvent` | `Mining/DroneEvents.cs`, `SocialUniverse.Mining` | App → UI result: `DroneId` (null for a slot), `Success`, `Message` (readable failure text, empty on success). |
| `DroneGarageHandler` (changed) | `App/` | For acquire and slot unlock: publish `DronePurchaseCompletedEvent` (success or failure) instead of `ServerActionFailedEvent`. Upgrade and set-active unchanged. Still logs failures. |
| `DronePurchase` | `UI/DronePurchase.cs`, `SocialUniverse.UI` | Pure. What is being bought: a drone (id, name, tier, icon, cost) or a slot (cost, current slot count). |
| `DronePurchaseScreen` + `DronePurchasePrompt` | `UI/DronePurchasePrompt.cs`, `SocialUniverse.UI` | Pure. Turns a purchase plus the coin balance into the confirm screen's text and button state, and a purchase into the success screen's text. Unit-tested. |
| `DronePurchaseModal` | `UI/`, `SocialUniverse.UI` | Thin passive view on the prefab. `Open(DronePurchase, int coins)`, Confirm → publishes the existing `DroneAcquireRequestedEvent` or `DroneSlotUnlockRequestedEvent`, acts on `DronePurchaseCompletedEvent` only while busy. `[Inject]` `IAudioManager`; plays `OpenPanel` / `Confirm` / `Cancel` like `LandPurchaseModal`, and `CoinsReward` on success. Registered in `PlanetSceneScope` for injection. |
| `DroneGarageView` (changed) | `UI/` | New `[SerializeField] DronePurchaseModal _purchaseModal`. `OnBuy` builds a `DronePurchase` from the centered card and opens the modal with the wallet's coins, instead of publishing the request itself. |
| `DronePurchaseModal.prefab` | `Assets/Prefabs/UI/` | Full-screen dim backdrop (blocks taps to the carousel) + the GUI pack's `Popup Base` frame, a drone icon `Image`, TMP title/body/status text, CleanButton instances for Cancel (grey), Buy/Unlock (purple) and OK. |

Architecture rules: the server still validates and commits every purchase (Rule 1); the popup and garage talk to `DroneGarageHandler` only through EventBus events (Rule 4); prices still come from `DroneDefinition` / `EconomyConfig` (Rule 3).

## Building and placing the prefab

The open Editor can't be driven from the agent, so the prefab is built in the scratch project copy by a one-off editor script that is never added to the project:

1. Write `DronePurchaseModal.cs` and `DronePurchasePrompt.cs` in the project **with hand-written `.meta` files** (fixed GUIDs), so the prefab's script references match in both the scratch copy and the project.
2. In the scratch copy, the editor script builds the prefab from the GUI-pack parts, adds and wires `DronePurchaseModal`, saves it to `Assets/Prefabs/UI/DronePurchaseModal.prefab`, then opens `Planet.unity`, instantiates the prefab as the **last child of the garage panel** (inactive), assigns `DroneGarageView._purchaseModal`, and saves the scene.
3. Copy back the prefab, its `.meta` and `Planet.unity` **only if the project's `Planet.unity` is byte-identical to the synced copy**. Otherwise stop and ask.
4. Review the scene diff: it should add only the prefab instance, the garage panel's new child entry and the `_purchaseModal` reference.

## Testing

- **EditMode**
  - `DronePurchasePromptTests`: confirm text for a drone and for a slot, "Not enough coins" disables confirm, success text for each.
  - `ServerFailureFeedbackTests` (updated): a rejected drone purchase publishes `DronePurchaseCompletedEvent` with "Not enough coins" and no toast; slot unlock likewise; upgrade and set-active failures still toast.
  - `DroneGarageHandlerTests` (extended): a successful acquire, and a successful slot unlock, each publish a successful completion event.
  - `DroneGarageSceneWiringTests` (unchanged): now also covers `_purchaseModal`.
  - `DronePurchaseModalPrefabTests`: every serialized reference on the prefab's `DronePurchaseModal` is wired.
- **PlayMode** (`DroneGarageFlowTests`, scripted `FakeBackendClient`)
  - Centre a locked drone, tap the buy button, the popup shows the confirm screen, tap Buy, the scripted `AcquireDrone` success shows the success screen.
  - A scripted rejection (`INSUFFICIENT_FUNDS`) shows "Not enough coins" and leaves Buy enabled.
- Full EditMode and PlayMode suites in the isolated scratch copy (separate product name, so the open Editor's PlayerPrefs are untouched).

## Out of scope

- Confirmation or result popups for stat upgrades and Select.
- Refunds, receipts or purchase history.
- Changing the toast itself.
