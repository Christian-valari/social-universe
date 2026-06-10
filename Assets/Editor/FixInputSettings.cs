using UnityEditor;

public static class FixInputSettings
{
    [MenuItem("SocialUniverse/Fix Input: Enable Both Systems")]
    public static void Fix()
    {
        var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        so.FindProperty("activeInputHandler").intValue = 2;
        so.ApplyModifiedProperties();
        UnityEngine.Debug.Log("Input handling set to Both (legacy + Input System).");
    }
}
