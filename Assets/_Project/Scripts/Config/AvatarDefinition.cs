using UnityEngine;

namespace SocialUniverse.Config
{
    [CreateAssetMenu(menuName = "SocialUniverse/Config/AvatarDefinition", fileName = "NewAvatar")]
    public class AvatarDefinition : ScriptableObject
    {
        [SerializeField] private string _avatarId;
        [SerializeField] private Sprite _sprite;

        public string AvatarId => _avatarId;
        public Sprite Sprite   => _sprite;
    }
}
