// ClaimYield — computes and grants accrued visitor-driven land income for an
// owned tile, then resets its yield-accrual state in the planet's global land
// registry (Custom Data).
// Formula (constants below must match EconomyConfig's [Header("Yield")] values):
//   elapsedHours = min((now - entry.lastYieldClaimTs) / 3600000, MAX_YIELD_ACCRUAL_HOURS)
//   buildBonus   = entry.buildLevel * BUILD_LEVEL_YIELD_MULTIPLIER
//   visitBonus   = min(entry.visitCount, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS
//   granted      = floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours)
// Known Issue #19: the accrual reset is written under the registry's writeLock *before* coins are
// granted, so two concurrent claims can't both pay, and this write can no longer put back a tile
// SellLand removed in between (the retry re-reads and answers NOT_OWNER). If the grant fails the
// accrual state is restored.
const { CurrenciesApi }           = require("@unity-services/economy-2.5");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CURRENCY_ID  = "COINS";
const REGISTRY_KEY = "land_registry";

const BASE_YIELD_PER_TILE_PER_HOUR = 2;    // must match EconomyConfig.BaseYieldPerTilePerHour
const BUILD_LEVEL_YIELD_MULTIPLIER = 0.25; // must match EconomyConfig.BuildLevelYieldMultiplier
const VISIT_YIELD_BONUS            = 0.1;  // must match EconomyConfig.VisitYieldBonus
const MAX_YIELD_ACCRUAL_HOURS      = 24;   // must match EconomyConfig.MaxYieldAccrualHours
const MAX_VISIT_COUNT              = 50;   // must match EconomyConfig.MaxVisitCount

const MS_PER_HOUR = 3600000;

/**
 * @param {string} tileId - ID of the hex tile to claim yield for.
 * @param {string} planetId - ID of the planet the tile belongs to.
 */
module.exports = async ({ params, context, logger }) => {
  await requireActiveSession(context, params.sessionId);
  const { tileId, planetId } = params;

  if (!tileId || !planetId) {
    throw new Error("Invalid params: tileId and planetId are required");
  }

  const { projectId, playerId, accessToken } = context;
  const econApi       = new CurrenciesApi({ accessToken });
  const customDataApi = new DataApi(context);
  const customId      = planetId.toLowerCase();

  try {
    // 1. Validate ownership, compute accrued yield and reset the accrual state — one locked write.
    const now = Date.now();
    const step = await updateRegistry(customDataApi, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId) {
        return { write: false, reason: "NOT_OWNER" };
      }

      const elapsedHours = Math.min((now - (entry.lastYieldClaimTs ?? now)) / MS_PER_HOUR, MAX_YIELD_ACCRUAL_HOURS);
      const buildBonus   = (entry.buildLevel ?? 0) * BUILD_LEVEL_YIELD_MULTIPLIER;
      const visitBonus   = Math.min(entry.visitCount ?? 0, MAX_VISIT_COUNT) * VISIT_YIELD_BONUS;
      const granted      = Math.floor(BASE_YIELD_PER_TILE_PER_HOUR * (1 + buildBonus + visitBonus) * elapsedHours);
      const previous     = { lastYieldClaimTs: entry.lastYieldClaimTs, visitCount: entry.visitCount ?? 0 };

      entry.lastYieldClaimTs = now;
      entry.visitCount       = 0;
      return { write: true, granted, previous };
    });

    if (step.conflict) return { success: false, reason: "CONFLICT" };
    if (!step.write)   return { success: false, reason: step.reason };
    const { granted, previous } = step;

    // 2. Grant. If it fails, restore the accrual state this claim reset.
    let newBalance;
    try {
      if (granted > 0) {
        const grantRes = await econApi.incrementPlayerCurrencyBalance({
          projectId, playerId, currencyId: CURRENCY_ID,
          currencyModifyBalanceRequest: { amount: granted }
        });
        newBalance = grantRes.data.balance;
      } else {
        newBalance = await currentBalance(econApi, projectId, playerId);
      }
    } catch (err) {
      logger.error(`ClaimYield: grant failed for ${playerId}; restoring accrual on ${tileId} (${planetId}). ${err?.message}`);
      await restoreAccrual(customDataApi, projectId, customId, tileId, playerId, now, previous, logger);
      throw err;
    }

    logger.info(`ClaimYield: player ${playerId} claimed tile ${tileId} (${planetId}) — granted ${granted} → ${newBalance}`);
    return { success: true, granted, newBalance };
  } catch (err) {
    logger.error("ClaimYield failed", { "error.message": err.message });
    throw err;
  }
};

// CurrenciesApi has no getPlayerCurrencyBalance (Known Issue #12) — read all balances and pick one.
async function currentBalance(econApi, projectId, playerId) {
  const res = await econApi.getPlayerCurrencies({ projectId, playerId });
  const c   = res.data.results.find(x => x.currencyId === "COINS");
  return c ? c.balance : 0;
}

// Puts back lastYieldClaimTs and the visits this claim consumed, unless the tile has changed
// hands or been claimed again since. Visits recorded meanwhile are kept.
async function restoreAccrual(api, projectId, customId, tileId, playerId, claimTs, previous, logger) {
  try {
    const step = await updateRegistry(api, projectId, customId, registry => {
      const entry = registry[tileId];
      if (!entry || entry.ownerId !== playerId || entry.lastYieldClaimTs !== claimTs) return { write: false };
      entry.lastYieldClaimTs = previous.lastYieldClaimTs;
      entry.visitCount       = Math.min((entry.visitCount ?? 0) + previous.visitCount, MAX_VISIT_COUNT);
      return { write: true };
    });
    if (step.conflict) throw new Error(`${MAX_ATTEMPTS} write conflicts`);
  } catch (err) {
    logger.error(`ClaimYield: RESTORE FAILED for tile ${tileId} on ${customId} (player ${playerId}) — accrual lost: ${err?.message}`);
  }
}

// ---- land_registry access (Known Issue #19) — identical in every script that writes the registry,
// pinned by LandRegistryLockTests. The registry is one Custom Data item holding every tile on the
// planet, so each write is a read-modify-write of the whole map: it must carry the writeLock it
// read, or it silently undoes a concurrent write (e.g. puts back a tile SellLand just sold).
const MAX_ATTEMPTS = 3;

// Reads the registry and its writeLock. Only a missing registry reads as {}: any other failure
// throws, so a transient read error can never turn into a write that wipes every tile.
async function loadRegistry(api, projectId, customId) {
  try {
    const res  = await api.getCustomItems(projectId, customId, [REGISTRY_KEY]);
    const item = res.data.results.find(r => r.key === REGISTRY_KEY);
    return item?.value ? { registry: item.value, writeLock: item.writeLock } : { registry: {}, writeLock: undefined };
  } catch (err) {
    if (statusOf(err) === 404) return { registry: {}, writeLock: undefined };
    throw err;
  }
}

// Read-modify-write under the writeLock. `mutate(registry)` validates against a fresh read and
// either edits the registry and returns { write: true, ... } or returns { write: false, ... } to stop.
// A 409 (someone wrote since the read) re-runs mutate on a fresh read, up to MAX_ATTEMPTS; after
// that the result is { write: false, conflict: true }. Returns whatever mutate returned.
async function updateRegistry(api, projectId, customId, mutate) {
  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    const { registry, writeLock } = await loadRegistry(api, projectId, customId);
    const step = await mutate(registry);
    if (!step.write) return step;
    try {
      await api.setCustomItem(projectId, customId,
        writeLock ? { key: REGISTRY_KEY, value: registry, writeLock } : { key: REGISTRY_KEY, value: registry });
      return step;
    } catch (err) {
      if (statusOf(err) !== 409) throw err;
    }
  }
  return { write: false, conflict: true };
}

function statusOf(err) {
  return err?.response?.status ?? err?.status;
}
// ---- end land_registry access

// ---- active session check (copy verbatim into every deployed script; ActiveSessionGuardTests keeps the copies identical) ----
// ClaimSession records the latest sign-in's session in the Protected active_session item. A call
// naming any other session, or none, comes from a replaced or signed-out device. A failed read
// throws rather than letting the call through.
async function requireActiveSession(context, sessionId) {
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const { projectId, playerId } = context;
  let current;
  try {
    const res = await new DataApi(context).getProtectedItems(projectId, playerId, ["active_session"]);
    current = res.data.results.find(r => r.key === "active_session")?.value?.sessionId;
  } catch (e) {
    if (e?.response?.status !== 404) throw e;
  }
  if (!sessionId || sessionId !== current) throw Error("SESSION_REPLACED");
}
// ---- end active session check

// Parameter declarations for the UGS CLI / Cloud Code (types only: each script validates its own
// inputs and returns reasons the client understands). Must stay after `module.exports = ...`,
// which would otherwise overwrite it. Pinned by ServerCodeDeployTests.
module.exports.params = {
  tileId: "String",
  planetId: "String",
  sessionId: "String"
};
