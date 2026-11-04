using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SocialUniverse.Mining;

namespace SocialUniverse.UI
{
    // A live hold-and-drag vein: a line between two markers, with a closing ring at the start. The
    // player presses the start and drags to the end, and lifting or straying first fails it.
    // ActiveMiningMinigameView places the ends and reports the outcome to the session.
    // The root covers the screen and sits above the miss area, so it takes every press while a vein
    // is live, and a press away from the start simply does nothing. Positions here are screen pixels;
    // the distances Show is given are canvas units, as they are on the config.
    public class ActiveMiningVeinView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform _line;
        [SerializeField] private RectTransform _startMarker;
        [SerializeField] private RectTransform _endMarker;
        [SerializeField] private RectTransform _ring;

        private Action<ActiveMiningTarget> _onCompleted;
        private Action<ActiveMiningTarget> _onFailed;
        private Canvas  _canvas;
        private float   _tapRadius;
        private float   _dragTolerance;
        private float   _completeProgress;

        private static readonly Color HeldColor = new Color(1f, 0.84f, 0.3f);
        private const float HeldScale = 1.25f;

        // The start marker's look at rest, taken the first time it is needed: Hide can run before Awake.
        private Graphic _startGraphic;
        private Color   _startColor;
        private Vector3 _startScale;
        private bool    _startLookKnown;
        private bool    _held;
        private int     _heldPointerId;
        private Vector2 _heldAt;

        // Null while no vein is live.
        public ActiveMiningTarget Target { get; private set; }

        public Vector2 StartPosition { get; private set; }
        public Vector2 EndPosition   { get; private set; }

        private void Awake() => _canvas = GetComponentInParent<Canvas>();

        public void Show(ActiveMiningTarget vein, float tapRadius, float dragTolerance, float completeProgress,
            Action<ActiveMiningTarget> onCompleted, Action<ActiveMiningTarget> onFailed)
        {
            Target            = vein;
            _tapRadius        = tapRadius;
            _dragTolerance    = dragTolerance;
            _completeProgress = completeProgress;
            _onCompleted      = onCompleted;
            _onFailed         = onFailed;
            SetHeld(false);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Target       = null;
            _onCompleted = null;
            _onFailed    = null;
            SetHeld(false);
            gameObject.SetActive(false);
        }

        // Called each frame with the ends' places on screen. The rock turns under a finger that isn't
        // moving, and no drag event says so, so the hold is checked here too. That can end the vein,
        // so it comes last.
        public void UpdateLine(Vector2 start, Vector2 end)
        {
            StartPosition = start;
            EndPosition   = end;

            _startMarker.position = start;
            _endMarker.position   = end;
            _ring.position        = start;

            float scale = Mathf.Lerp(ActiveMiningTargetView.RingStartScale, 1f, Target.Progress);
            _ring.localScale = new Vector3(scale, scale, 1f);

            Vector2 along = end - start;
            _line.position      = (start + end) / 2f;
            _line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            _line.sizeDelta     = new Vector2(along.magnitude / _canvas.scaleFactor, _line.sizeDelta.y);

            if (_held) CheckHold();
        }

        // The tap area is a circle, kept inside the stray limit: a press in the corner of a square one,
        // or past a tighter limit, would be off the line before the finger had moved.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (Target == null || _held) return;

            float reach = Mathf.Min(_tapRadius, _dragTolerance) * _canvas.scaleFactor;
            if (Vector2.Distance(eventData.position, StartPosition) > reach) return;

            SetHeld(true);
            _heldPointerId = eventData.pointerId;
            _heldAt        = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsHeldBy(eventData)) return;

            _heldAt = eventData.position;
            CheckHold();
        }

        // The input module delivers a move and a release from the same frame as a pointer-up alone,
        // so a flick off the end of the line is finished here rather than counted as a lift.
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsHeldBy(eventData)) return;

            _heldAt = eventData.position;
            CheckHold();
            if (_held) Resolve(_onFailed);
        }

        // Unity sends no pointer-up on pause, so the hold ends with no penalty. The start can be
        // pressed again while the ring has time.
        private void OnApplicationPause(bool paused)
        {
            if (paused) SetHeld(false);
        }

        private bool IsHeldBy(PointerEventData eventData) => _held && eventData.pointerId == _heldPointerId;

        // Straying comes first: a finger far past the end of the line hasn't traced it.
        private void CheckHold()
        {
            float distance = ActiveMiningVeinPath.DistanceToSegment(_heldAt, StartPosition, EndPosition);
            if (distance > _dragTolerance * _canvas.scaleFactor)
                Resolve(_onFailed);
            else if (ActiveMiningVeinPath.ProgressAlong(_heldAt, StartPosition, EndPosition) >= _completeProgress)
                Resolve(_onCompleted);
        }

        // A held press shows on the start marker, so the player can tell it landed.
        private void SetHeld(bool held)
        {
            if (!_startLookKnown)
            {
                _startGraphic   = _startMarker.GetComponent<Graphic>();
                _startColor     = _startGraphic != null ? _startGraphic.color : Color.white;
                _startScale     = _startMarker.localScale;
                _startLookKnown = true;
            }

            _held = held;
            _startMarker.localScale = held ? _startScale * HeldScale : _startScale;
            if (_startGraphic != null) _startGraphic.color = held ? HeldColor : _startColor;
        }

        private void Resolve(Action<ActiveMiningTarget> outcome)
        {
            SetHeld(false);
            outcome?.Invoke(Target);
        }
    }
}
