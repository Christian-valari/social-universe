using UnityEngine;
using UnityEngine.UI;
using SocialUniverse.Social;

namespace SocialUniverse.UI
{
    // Single chat history line, instantiated per message by SocialDebugPanel.
    public class ChatMessageItemView : MonoBehaviour
    {
        [SerializeField] private Text _text;

        public void SetMessage(ChatMessage message)
        {
            _text.text = $"{(message.FromSelf ? "me" : message.SenderDisplayName)}: {message.Text}";
        }
    }
}
