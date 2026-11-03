#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace SocialUniverse.UI
{
    // A row of Tier 1-5 buttons across the top of the pre-game panel, so a run can be tried at any
    // tier without finding an asteroid of that tier. Drawn with IMGUI, which needs no prefab or scene
    // change, and which is fine for something that never ships.
    // IMGUI doesn't block uGUI, so a tap on a button here also reaches the pre-game panel's
    // full-screen Start button: IsPressed lets that button tell the two apart.
    public class ActiveMiningDevTierPicker : MonoBehaviour
    {
        public const int TierCount = 5;

        // Laid out for a phone 1080 px wide, and scaled up or down to the real screen.
        private const float ReferenceWidth = 1080f;
        private const float ButtonWidth    = 190f;
        private const float ButtonHeight   = 130f;
        private const float ButtonGap      = 12f;
        private const float TopMargin      = 24f;
        private const float FontSize       = 40f;

        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.2f);

        private GUIStyle _style;

        public event Action<int> OnTierPicked;

        public int  SelectedTier { get; set; }

        // True from a press that lands on the row until the pointer is released.
        public bool IsPressed { get; private set; }

        private void OnGUI()
        {
            float scale = Screen.width / ReferenceWidth;
            float width = (TierCount * ButtonWidth + (TierCount - 1) * ButtonGap) * scale;
            float top   = Screen.height - Screen.safeArea.yMax + TopMargin * scale; // IMGUI counts down from the top
            float left  = (Screen.width - width) / 2f;

            _style          ??= new GUIStyle(GUI.skin.button);
            _style.fontSize   = Mathf.RoundToInt(FontSize * scale);

            var row   = new Rect(left, top, width, ButtonHeight * scale);
            var color = GUI.backgroundColor;

            for (int tier = 1; tier <= TierCount; tier++)
            {
                var button = new Rect(left + (tier - 1) * (ButtonWidth + ButtonGap) * scale, top, ButtonWidth * scale, ButtonHeight * scale);

                GUI.backgroundColor = tier == SelectedTier ? SelectedColor : color;
                if (GUI.Button(button, $"Tier {tier}", _style))
                {
                    SelectedTier = tier;
                    OnTierPicked?.Invoke(tier);
                }
            }
            GUI.backgroundColor = color;

            // The raw type, because a button that takes a press or release marks the event used.
            Event current = Event.current;
            if (current.rawType == EventType.MouseDown && row.Contains(current.mousePosition)) IsPressed = true;
            else if (current.rawType == EventType.MouseUp)                                      IsPressed = false;
        }
    }
}
#endif
