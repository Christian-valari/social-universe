using UnityEngine;

namespace SocialUniverse.Config
{
    public enum AppEnvironment { Local, Development, Production }

    [CreateAssetMenu(menuName = "SocialUniverse/Config/AppConfig", fileName = "AppConfig")]
    public class AppConfig : ScriptableObject
    {
        [SerializeField] private AppEnvironment _environment = AppEnvironment.Local;
        [SerializeField] private string _gameVersion = "0.1.0";
        [SerializeField] private bool _verboseLogging = true;

        public AppEnvironment Environment  => _environment;
        public string          GameVersion => _gameVersion;
        public bool            VerboseLogging => _verboseLogging;
    }
}
