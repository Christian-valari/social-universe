using System;
using UnityEngine;
using TMPro;
using SocialUniverse.Social;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // Single chat history line, instantiated per message by SocialDebugPanel.
    public class ChatMessageItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _senderText;
        [SerializeField] private TMP_Text _timestampText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private RectTransform _messageBGRect;
        [SerializeField] private Image _avatarImage;

        public void SetMessage(ChatMessage message, Sprite avatarSprite)
        {
            _senderText.alignment = message.FromSelf ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            _timestampText.alignment = message.FromSelf ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            _messageBGRect.pivot = message.FromSelf ? new Vector2(1,1) : Vector2.zero ;
            _senderText.text    = message.FromSelf ? "Me" : message.SenderDisplayName.Split('#')[0];
            _messageText.text   = message.Text;
            _timestampText.text = message.TimestampMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(message.TimestampMs).LocalDateTime.ToString("HH:mm")
                : "--:--";

            // Null means unresolved (no catalog match / no id yet) — leave the
            // prefab's inspector-default placeholder sprite in place.
            _avatarImage.gameObject.SetActive(!message.FromSelf);
            if (avatarSprite != null) _avatarImage.sprite = avatarSprite;
        }
    }
}
