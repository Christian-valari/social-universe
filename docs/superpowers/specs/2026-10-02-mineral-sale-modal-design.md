# Mineral sale popup and shared transaction popup: design

**Date:** 2026-10-02 · **Milestone:** M6 (mineral inventory + sale) · **Status:** implemented 2026-10-02 (plan: `docs/superpowers/plans/2026-10-02-mineral-sale-modal.md`)
**Builds on:** `2026-10-02-drone-purchase-modal-design.md` (the drone purchase popup)

## Goal

Selling minerals from the Mineral Inventory (a row's **Sell**, or **Sell all**) asks for confirmation in a popup, then shows the result in the same popup, like drone purchases. The drone popup becomes one shared popup used by both panels.

## Player flow

1. **A row's Sell** (sells every unit of that mineral) opens the popup: the mineral's icon, title `Sell <qty> <DisplayName>?`, body `<SellValue> coins each`, amount line `You'll get <qty × SellValue> coins`, buttons **Cancel** and **Sell**.
2. **Sell all** opens it with title `Sell all minerals?`, body `<n> minerals` (`1 mineral` when n is 1), amount line `You'll get <total value> coins`, buttons **Cancel** and **Sell all**. No icon.
3. Confirm is disabled when the quantity is 0. (The panel already disables both Sell buttons then; this is a guard.)
4. **Sell / Sell all** shows `Selling…` and locks the buttons.
5. Result in the same popup:
   - **Success:** title `Sold <qty> <DisplayName>` or `Sold <n> minerals`, body `+<value> coins` (the estimate from the confirm screen; the server pays the real amount and the HUD shows the new balance), button **OK**.
   - **Failure:** the confirm screen stays and the status line shows the readable reason from `ServerFailureMessages` (`Not enough minerals to sell`, `Something changed. Try again.`, `Couldn't reach the server. Try again.`), with Sell and Cancel enabled again.
6. Sale failures no longer raise the toast. The sale's quantity is the one captured when the popup opened.

## Shared popup

| Unit | Change |
|---|---|
| `TransactionModal` (`UI/`) | Renamed from `DronePurchaseModal` (file renamed; same `.meta` GUID `75923df341654945878c0e8a096f06bc`, so prefab and scene references survive). A passive view with no knowledge of drones, minerals or EventBus: `Open(TransactionScreen confirm, Sprite icon, string busyText, Action onConfirm)`, `bool IsBusy`, `ShowSuccess(TransactionScreen result)`, `ShowFailure(string message)`. Keeps the busy lock, Cancel/OK closing and sounds (`[Inject] IAudioManager`). `_costText` becomes `_amountText` (`[FormerlySerializedAs("_costText")]`); other field names unchanged. |
| `TransactionScreen` (`UI/TransactionScreen.cs`) | Renamed from `DronePurchaseScreen` and moved to its own file; `Cost` becomes `Amount` (the money line: a price or proceeds). |
| `DronePurchasePrompt` | Returns `TransactionScreen`. Wording unchanged. |
| `DroneGarageView` | `_purchaseModal` is a `TransactionModal`. Opens it with `DronePurchasePrompt.Confirm`, the purchase's icon, `DronePurchasePrompt.Busy` and an `onConfirm` that publishes the purchase request; remembers the pending purchase; on `DronePurchaseCompletedEvent` while the popup is busy (and for the pending drone id), calls `ShowSuccess(DronePurchasePrompt.Success(...))` or `ShowFailure(message)`. |
| `PlanetSceneScope` | Replaces `RegisterComponentInHierarchy<DronePurchaseModal>()` (which injects only one instance) with a build callback that injects every `TransactionModal` under this scope's scene. |
| `TransactionModal.prefab` | Renamed from `DronePurchaseModal.prefab` (same `.meta`); root renamed `TransactionModal` and re-saved by the scratch builder. |

## Mineral sale

| Unit | Folder / namespace | Responsibility |
|---|---|---|
| `MineralSaleCompletedEvent` | `Mining/`, `SocialUniverse.Mining` | App → UI: `Success`, `Message` (`""` on success). |
| `MineralSaleHandler` (changed) | `App/` | Publishes `MineralSaleCompletedEvent` for every sale request it handles (success, failure, or missing reply → "Couldn't reach the server. Try again."), instead of `ServerActionFailedEvent`. Keeps the in-flight guard. |
| `MineralSale` | `UI/MineralSale.cs` | Pure. One mineral (id, name, icon, qty, unit value, total) or everything (`ForAll(qty, value)`). |
| `MineralSalePrompt` | `UI/MineralSalePrompt.cs` | Pure. `Busy = "Selling…"`, `Confirm(MineralSale)`, `Success(MineralSale)` → `TransactionScreen`. |
| `MineralInventoryView` (changed) | `UI/` | New `[SerializeField] TransactionModal _saleModal`. Row Sell and Sell all open the popup with a `MineralSale` (qty and value read when pressed); `onConfirm` publishes the existing `SellMineralsRequestedEvent`; on `MineralSaleCompletedEvent` while the popup is busy, calls `ShowSuccess` / `ShowFailure`. |
| `Planet.unity` | (scratch build) | A second `TransactionModal` instance, last child of the mineral panel, inactive, assigned to `MineralInventoryView._saleModal`. Copied back only if the project's `Planet.unity` is unchanged since the sync. |

Architecture rules: the server still validates and pays every sale (Rule 1); the panels and handlers talk only through EventBus (Rule 4); values come from `MineralDefinition.SellValue` (Rule 3).

## Testing

- **EditMode:** `MineralSalePromptTests` (row and all confirm text, singular unit, zero disables confirm, both success texts); `MineralSaleHandlerTests` (success publishes a completion); `ServerFailureFeedbackTests` (a rejected sale goes to the popup, not the toast; a missing reply still completes); `TransactionModalPrefabTests` (renamed from `DronePurchaseModalPrefabTests`); `MineralInventorySceneWiringTests` (every `MineralInventoryView` reference wired, including `_saleModal`); `DronePurchasePromptTests` (`Amount` rename).
- **PlayMode:** `MineralSaleFlowTests`: a row's Sell asks first, Confirm sends `SellMinerals` with the shown quantity, success shows `Sold …`, OK closes; a rejected sale shows `Not enough minerals to sell` and leaves Sell enabled; every `TransactionModal` in the scene gets its audio injected. `DroneGarageFlowTests` re-run with the renamed type.
- Full EditMode and PlayMode suites in the isolated scratch copy.

## Out of scope

- Per-mineral breakdown on the Sell all result.
- Reworking `LandPurchaseModal` onto the shared popup (possible later).
