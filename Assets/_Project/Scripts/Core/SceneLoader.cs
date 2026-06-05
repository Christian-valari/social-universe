using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace SocialUniverse.Core
{
    public class SceneLoader
    {
        public async Task LoadAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Additive)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = SceneManager.LoadSceneAsync(sceneName, mode);
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;
        }

        public async Task UnloadAsync(string sceneName)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op  = SceneManager.UnloadSceneAsync(sceneName);
            op.completed += _ => tcs.SetResult(true);
            await tcs.Task;
        }
    }
}
