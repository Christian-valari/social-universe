using UnityEngine;
using UnityEngine.UI;

namespace SocialUniverse.UI
{
    // TEMP diagnostic for chat send/receive verification - remove after use.
    public class ChatSendProbe : MonoBehaviour
    {
        [SerializeField] private InputField _input;
        [SerializeField] private Button _sendButton;
        [SerializeField] private string _message = "diagnostic ping";

        private void Start()
        {
            Invoke(nameof(FireSend), 8f);
        }

        private void FireSend()
        {
            if (_input == null || _sendButton == null)
            {
                Debug.LogWarning("#ChatSendProbe# not wired — assign _input and _sendButton in the Inspector");
                return;
            }
            Debug.Log("#ChatSendProbe# firing send (delayed)");
            _input.text = _message;
            _sendButton.onClick.Invoke();
        }
    }
}
