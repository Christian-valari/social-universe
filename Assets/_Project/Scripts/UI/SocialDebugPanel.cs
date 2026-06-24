using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Net;
using SocialUniverse.Social;
using TMPro;

namespace SocialUniverse.UI
{
    // M4 manual-verification tool: exercises ChatChannelController and
    // IPresenceService directly, the same services PlanetPresenceController
    // wires up on scene start. Not the M11 ChatScreen (UIManager/MVP) -
    // this is a developer/QA panel for the Planet scene.
    public class SocialDebugPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _activeChannelText;
        [SerializeField] private Toggle _globalChannelButton;
        [SerializeField] private TMP_Text _presenceText;
        [SerializeField] private RectTransform _chatLogContent;
        [SerializeField] private ChatMessageItemView _chatMessageItemPrefab;
        [SerializeField] private TMP_InputField _messageInput;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _statusText;

        [Inject] private ChatChannelController _chat;
        [Inject] private IPresenceService _presence;
        [Inject] private PlanetDefinition _planet;

        private const int MaxLogLines = 12;

        private void Awake()
        {
            _globalChannelButton.onValueChanged.AddListener(value =>
            {
                if (value) _ = SwitchChannelAsync();
            });
            _sendButton.onClick.AddListener(() => _ = SendMessageAsync());
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            _presence.PresenceChanged += RefreshPresence;

            RefreshActiveChannel();
            RefreshChatLog();
            RefreshPresence();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            _presence.PresenceChanged -= RefreshPresence;
        }

        public async void Open()
        {
            gameObject.SetActive(true);
            await _chat.SwitchToPlanetAsync(_planet.name);
            _globalChannelButton.SetIsOnWithoutNotify(true);
            RefreshActiveChannel();
            RefreshChatLog();
        }

        public void Close() => gameObject.SetActive(false);

        // Exercises the same per-planet channel PlanetPresenceController joins
        // on scene start, so this debug toggle mirrors real presence/chat scope.
        private async Task SwitchChannelAsync()
        {
            await _chat.SwitchToPlanetAsync(_planet.name);
            RefreshActiveChannel();
            RefreshChatLog();
        }

        private async Task SendMessageAsync()
        {
            var status = await _chat.SendAsync(_messageInput.text);
            _statusText.text = $"Status: {status}";

            Debug.Log($"#{GetType().Name}# status -> {status}");
            if (status == ChatSendStatus.Sent)
            {
                _messageInput.text = "";
                RefreshChatLog();
                Debug.Log($"#{GetType().Name}# sent message -> {status}");
            }
        }

        private void OnChatMessageReceived(ChatChannelController.ChatMessageReceivedEvent e)
        {
            if (e.Message.ChannelName == _chat.ActiveChannel)
                RefreshChatLog();
        }

        private void RefreshActiveChannel()
        {
            _activeChannelText.text = $"Channel: {_chat.ActiveChannel ?? "(none)"}";
        }

        private void RefreshChatLog()
        {
            var history = _chat.GetHistory(_chat.ActiveChannel);
            Debug.Log($"#{GetType().Name}# History -> {history.Count}");

            for (int i = _chatLogContent.childCount - 1; i >= 0; i--)
                Destroy(_chatLogContent.GetChild(i).gameObject);

            int start = Mathf.Max(0, history.Count - MaxLogLines);
            for (int i = start; i < history.Count; i++)
            {
                var item = Instantiate(_chatMessageItemPrefab, _chatLogContent);
                item.SetMessage(history[i]);
            }
        }

        private void RefreshPresence()
        {
            _presenceText.text = $"{_presence.CurrentChannelName ?? "(none)"} - {_presence.Players.Count} explorers here";
        }
    }
}
