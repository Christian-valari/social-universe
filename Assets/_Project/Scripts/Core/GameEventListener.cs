using UnityEngine;
using UnityEngine.Events;

namespace SocialUniverse.Core
{
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent _event;
        [SerializeField] private UnityEvent _response;

        private void OnEnable()  => _event.Register(this);
        private void OnDisable() => _event.Deregister(this);

        public void OnEventRaised() => _response?.Invoke();
    }
}
