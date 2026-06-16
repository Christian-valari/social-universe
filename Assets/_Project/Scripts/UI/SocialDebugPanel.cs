using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using SocialUniverse.Core;
using SocialUniverse.Config;
using SocialUniverse.Net;
using SocialUniverse.Social;

namespace SocialUniverse.UI
{
    // M4 manual-verification tool: exercises ChatChannelController and
    // IPresenceService directly, the same services PlanetPresenceController
    // wires up on scene start. Not the M11 ChatScreen (UIManager/MVP) -
    // this is a developer/QA panel for the Planet scene.
    public class SocialDebugPanel : MonoBehaviour
    {
        [SerializeField] private Text _activeChannelText;
        [SerializeField] private Button _globalChannelButton;
        [SerializeField] private Button _localChannelButton;
        [SerializeField] private Text _presenceText;
        [SerializeField] private RectTransform _chatLogContent;
        [SerializeField] private ChatMessageItemView _chatMessageItemPrefab;
        [SerializeField] private InputField _messageInput;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Text _statusText;

        [Inject] private ChatChannelController _chat;
        [Inject] private IPresenceService _presence;
        [Inject] private PlanetDefinition _planet;

        private const int MaxLogLines = 12;

        private void Awake()
        {
            _globalChannelButton.onClick.AddListener(() => _ = SwitchChannelAsync(global: true));
            _localChannelButton.onClick.AddListener(() => _ = SwitchChannelAsync(global: false));
            _sendButton.onClick.AddListener(() => _ = SendMessageAsync());
        }

        private void Start()
        {
            EventBus.Subscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            _presence.PresenceChanged += RefreshPresence;

            RefreshActiveChannel();
            RefreshChatLog();
            RefreshPresence();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ChatChannelController.ChatMessageReceivedEvent>(OnChatMessageReceived);
            _presence.PresenceChanged -= RefreshPresence;
        }

        private async Task SwitchChannelAsync(bool global)
        {
            if (global)
                await _chat.SwitchToGlobalAsync();
            else
                await _chat.SwitchToLocalAsync(_planet.name);

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
            var sb = new StringBuilder();
            sb.AppendLine($"Shard: {_presence.CurrentShardId ?? "(none)"}");
            sb.AppendLine($"Players ({_presence.Players.Count}):");
            foreach (var p in _presence.Players)
                sb.AppendLine($" - {p.DisplayName} ({p.PlayerId})");
            _presenceText.text = sb.ToString();
        }
    }
}
