using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SocialUniverse.Config;
using SocialUniverse.App;
using SocialUniverse.World;
using SocialUniverse.Mining;

public static class M1AssetCreator
{
    const string SO_FOLDER = "Assets/_Project/ScriptableObjects";

    [MenuItem("SocialUniverse/Create M1 Assets")]
    public static void CreateM1Assets()
    {
        if (!AssetDatabase.IsValidFolder(SO_FOLDER))
            AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

        // EconomyConfig (all defaults are fine)
        if (!AssetDatabase.LoadAssetAtPath<EconomyConfig>(SO_FOLDER + "/EconomyConfig.asset"))
        {
            var eco = ScriptableObject.CreateInstance<EconomyConfig>();
            AssetDatabase.CreateAsset(eco, SO_FOLDER + "/EconomyConfig.asset");
        }

        // Asteroid — Iron
        var iron = AssetDatabase.LoadAssetAtPath<AsteroidDefinition>(SO_FOLDER + "/Asteroid_Iron.asset");
        if (!iron)
        {
            iron = ScriptableObject.CreateInstance<AsteroidDefinition>();
            AssetDatabase.CreateAsset(iron, SO_FOLDER + "/Asteroid_Iron.asset");
        }
        var ironSO = new SerializedObject(iron);
        ironSO.FindProperty("_mineralType").stringValue = "Iron";
        ironSO.FindProperty("_tier").intValue           = 1;
        ironSO.FindProperty("_baseYield").intValue      = 60;
        ironSO.FindProperty("_rarity").floatValue       = 0.5f;
        ironSO.FindProperty("_coinsPerUnit").intValue   = 2;
        ironSO.ApplyModifiedPropertiesWithoutUndo();

        // Drone — Scout
        var scout = AssetDatabase.LoadAssetAtPath<DroneDefinition>(SO_FOLDER + "/Drone_Scout.asset");
        if (!scout)
        {
            scout = ScriptableObject.CreateInstance<DroneDefinition>();
            AssetDatabase.CreateAsset(scout, SO_FOLDER + "/Drone_Scout.asset");
        }
        var scoutSO = new SerializedObject(scout);
        scoutSO.FindProperty("_droneId").stringValue     = "drone_scout";
        scoutSO.FindProperty("_displayName").stringValue = "Scout Drone";
        scoutSO.FindProperty("_travelSpeed").floatValue  = 5f;
        scoutSO.FindProperty("_cargoCap").intValue       = 50;
        scoutSO.ApplyModifiedPropertiesWithoutUndo();

        // Planet — Terra Prime
        var terra = AssetDatabase.LoadAssetAtPath<PlanetDefinition>(SO_FOLDER + "/Planet_TerraPrime.asset");
        if (!terra)
        {
            terra = ScriptableObject.CreateInstance<PlanetDefinition>();
            AssetDatabase.CreateAsset(terra, SO_FOLDER + "/Planet_TerraPrime.asset");
        }
        var terraSO = new SerializedObject(terra);
        terraSO.FindProperty("_planetId").stringValue           = "planet_terra_prime";
        terraSO.FindProperty("_displayName").stringValue        = "Terra Prime";
        terraSO.FindProperty("_tileCount").intValue             = 512;
        terraSO.FindProperty("_landPriceMultiplier").floatValue = 1f;
        terraSO.FindProperty("_asteroidTier").intValue          = 1;
        var asteroidTypes = terraSO.FindProperty("_asteroidTypes");
        asteroidTypes.arraySize = 1;
        asteroidTypes.GetArrayElementAtIndex(0).objectReferenceValue = iron;
        terraSO.ApplyModifiedPropertiesWithoutUndo();

        // DatabaseRegistry
        var registry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(SO_FOLDER + "/DatabaseRegistry.asset");
        if (!registry)
        {
            registry = ScriptableObject.CreateInstance<DatabaseRegistry>();
            AssetDatabase.CreateAsset(registry, SO_FOLDER + "/DatabaseRegistry.asset");
        }
        var regSO = new SerializedObject(registry);
        var planets = regSO.FindProperty("_planets");
        planets.arraySize = 1;
        planets.GetArrayElementAtIndex(0).objectReferenceValue = terra;
        var asteroids = regSO.FindProperty("_asteroids");
        asteroids.arraySize = 1;
        asteroids.GetArrayElementAtIndex(0).objectReferenceValue = iron;
        var drones = regSO.FindProperty("_drones");
        drones.arraySize = 1;
        drones.GetArrayElementAtIndex(0).objectReferenceValue = scout;
        regSO.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[M1AssetCreator] All M1 ScriptableObject assets created successfully.");
    }

    [MenuItem("SocialUniverse/Setup Planet Scene")]
    public static void SetupPlanetScene()
    {
        // Load Planet scene
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Planet.unity", OpenSceneMode.Single);

        // ── PlanetRoot ───────────────────────────────────────────────────────
        var planetRoot       = new GameObject("PlanetRoot");
        var hexasphere       = planetRoot.AddComponent<HexasphereManager>();
        var tileColorizer    = planetRoot.AddComponent<TileColorizer>();
        var tileSelection    = planetRoot.AddComponent<TileSelectionController>();
        var planetController = planetRoot.AddComponent<PlanetController>();

        var pcSO = new SerializedObject(planetController);
        pcSO.FindProperty("_hexasphere").objectReferenceValue = hexasphere;
        pcSO.FindProperty("_colorizer").objectReferenceValue  = tileColorizer;
        pcSO.ApplyModifiedPropertiesWithoutUndo();

        var tscSO = new SerializedObject(tileSelection);
        tscSO.FindProperty("_hexasphere").objectReferenceValue = hexasphere;
        tscSO.ApplyModifiedPropertiesWithoutUndo();

        // ── AsteroidField ─────────────────────────────────────────────────────
        var asteroidField = new GameObject("AsteroidField");
        asteroidField.AddComponent<AsteroidSpawner>();

        // ── Drone ─────────────────────────────────────────────────────────────
        var droneGO = new GameObject("Drone");
        droneGO.AddComponent<DroneController>();
        droneGO.transform.position = new Vector3(12f, 0f, 0f);

        // ── PlanetCameraController on Main Camera ─────────────────────────────
        var mainCam = GameObject.FindWithTag("MainCamera");
        if (mainCam == null)
        {
            mainCam = new GameObject("Main Camera");
            mainCam.tag = "MainCamera";
            mainCam.AddComponent<Camera>();
            mainCam.AddComponent<AudioListener>();
        }
        mainCam.transform.position = new Vector3(0f, 4f, -14f);
        mainCam.transform.LookAt(Vector3.zero);

        var camCtrl = mainCam.AddComponent<PlanetCameraController>();
        var camSO = new SerializedObject(camCtrl);
        camSO.FindProperty("_target").objectReferenceValue = planetRoot.transform;
        camSO.ApplyModifiedPropertiesWithoutUndo();

        // ── PlanetSceneScope ──────────────────────────────────────────────────
        var scopeGO = new GameObject("PlanetSceneScope");
        var scope   = scopeGO.AddComponent<PlanetSceneScope>();

        var ecoConfig  = AssetDatabase.LoadAssetAtPath<EconomyConfig>(SO_FOLDER + "/EconomyConfig.asset");
        var dbRegistry = AssetDatabase.LoadAssetAtPath<DatabaseRegistry>(SO_FOLDER + "/DatabaseRegistry.asset");

        var scopeSO = new SerializedObject(scope);
        scopeSO.FindProperty("_economyConfig").objectReferenceValue   = ecoConfig;
        scopeSO.FindProperty("_databaseRegistry").objectReferenceValue = dbRegistry;
        scopeSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[M1AssetCreator] Planet scene configured and saved.");
    }

    [MenuItem("SocialUniverse/Update Build Settings")]
    public static void UpdateBuildSettings()
    {
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity",   true),
            new EditorBuildSettingsScene("Assets/Scenes/Auth.unity",        true),
            new EditorBuildSettingsScene("Assets/Scenes/SolarSystem.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Planet.unity",      true),
            new EditorBuildSettingsScene("Assets/Scenes/Station.unity",     true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[M1AssetCreator] Build Settings updated: Bootstrap(0) Auth(1) SolarSystem(2) Planet(3) Station(4)");
    }
}
