using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    public interface ICloudSave
    {
        Task    SaveAsync<T>(string key, T value);

        // A key that doesn't exist yet returns defaultValue; any other failure throws, so a failed
        // read is never mistaken for a new player's empty record.
        Task<T> LoadAsync<T>(string key, T defaultValue = default);

        // Reads a server-written record (Cloud Save's Protected access class: the player can read it,
        // only Cloud Code can write it). LoadAsync never sees these keys.
        Task<T> LoadProtectedAsync<T>(string key, T defaultValue = default);
        Task    DeleteAsync(string key);
    }
}
