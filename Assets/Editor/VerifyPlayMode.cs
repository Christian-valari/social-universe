#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using VContainer.Unity;
using SocialUniverse.Economy;
using SocialUniverse.Mining;
using SocialUniverse.World;
using SocialUniverse.Core;
using SocialUniverse.App;

public static class VerifyPlayMode
{
    static VContainer.IObjectResolver C()
    {
        // Try VContainer's own registry first, then scene search
        var s = LifetimeScope.Find<PlanetSceneScope>();
        if (s == null)
        {
            var all = Object.FindObjectsByType<PlanetSceneScope>(FindObjectsSortMode.None);
            s = all.Length > 0 ? all[0] : null;
        }
        if (s == null) Debug.LogError("VERIFY: PlanetSceneScope not found");
        return s?.Container;
    }

    static T Get<T>(VContainer.IObjectResolver c) => (T)c.Resolve(typeof(T));

    [MenuItem("Verify/1 Wallet Balance")]
    public static void WalletBalance()
    {
        var c = C(); if (c == null) { Debug.LogError("VERIFY: no scope"); return; }
        var w = Get<Wallet>(c);
        Debug.Log($"VERIFY Wallet: Coins={w.Coins} Stardust={w.Stardust}");
    }

    [MenuItem("Verify/2 Tap Mine x10")]
    public static void TapMine()
    {
        var c = C(); if (c == null) { Debug.LogError("VERIFY: no scope"); return; }
        var mc = Get<MiningController>(c);
        var w  = Get<Wallet>(c);
        int before = w.Coins;
        Debug.Log($"VERIFY Mine start: Phase={mc.Phase} Target={mc.CurrentTarget?.Definition?.MineralType ?? "null"} Cargo={mc.Drone?.CargoAmount}/{mc.Drone?.Definition?.CargoCap}");
        for (int i = 0; i < 10; i++)
        {
            var r = mc.Tap();
            if (r != null)
                Debug.Log($"  tap {i+1}: yield={r.YieldAmount} crit={r.IsCrit} cargo={mc.Drone.CargoAmount}/{mc.Drone.Definition.CargoCap}");
            else
                Debug.Log($"  tap {i+1}: null (cargo full or no target)");
            if (mc.Drone != null && mc.Drone.IsCargoFull)
            {
                Debug.Log("  Cargo full — committing");
                _ = mc.CommitCargoAsync();
                break;
            }
        }
        Debug.Log($"VERIFY Mine end: Coins before={before} after={w.Coins}");
    }

    [MenuItem("Verify/3 Buy First Available Tile")]
    public static void BuyTile()
    {
        var c = C(); if (c == null) { Debug.LogError("VERIFY: no scope"); return; }
        var hex = Get<HexasphereManager>(c);
        var w   = Get<Wallet>(c);
        TileData target = null;
        foreach (var kv in hex.Tiles)
            if (kv.Value.State == TileState.Available) { target = kv.Value; break; }
        if (target == null) { Debug.LogError("VERIFY: no available tile"); return; }
        Debug.Log($"VERIFY Buy: tile={target.TileId} coins before={w.Coins}");
        EventBus.Publish(new TileSelectedEvent { Tile = target });
    }

    [MenuItem("Verify/4 Check State")]
    public static void CheckState()
    {
        var c = C(); if (c == null) { Debug.LogError("VERIFY: no scope"); return; }
        var hex = Get<HexasphereManager>(c);
        var w   = Get<Wallet>(c);
        int owned = 0;
        foreach (var kv in hex.Tiles)
            if (kv.Value.State == TileState.OwnedByPlayer) owned++;
        Debug.Log($"VERIFY State: Coins={w.Coins} OwnedTiles={owned}");
    }

    [MenuItem("Verify/5 Simulate Idle (set 2h ago)")]
    public static void SetIdleTime()
    {
        PlayerPrefs.SetString("last_session_end", System.DateTime.UtcNow.AddHours(-2).ToString("O"));
        PlayerPrefs.Save();
        Debug.Log("VERIFY: last_session_end set 2h ago — re-enter Play Mode to trigger idle yield.");
    }
}
#endif
