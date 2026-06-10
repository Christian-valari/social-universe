using System.Threading.Tasks;

namespace SocialUniverse.Net
{
    public interface ICloudSave
    {
        Task    SaveAsync<T>(string key, T value);
        Task<T> LoadAsync<T>(string key, T defaultValue = default);
        Task    DeleteAsync(string key);
    }
}
