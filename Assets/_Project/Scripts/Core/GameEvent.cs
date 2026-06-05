using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Core
{
    [CreateAssetMenu(menuName = "SocialUniverse/Events/GameEvent", fileName = "NewGameEvent")]
    public class GameEvent : ScriptableObject
    {
        private readonly List<GameEventListener> _listeners = new();

        public void Raise()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i].OnEventRaised();
        }

        public void Register(GameEventListener listener)   => _listeners.Add(listener);
        public void Deregister(GameEventListener listener) => _listeners.Remove(listener);
    }
}
