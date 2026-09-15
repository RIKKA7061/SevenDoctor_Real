using System.Collections;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 화면 전체를 코드로 짓고, 각 패널의 참조를 들고 있습니다.
    /// 패널 스택은 쓰지 않고 '한 번에 하나만 뜬다'로 단순화했습니다 — 이 게임엔 그걸로 충분합니다.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform Screen { get; private set; }

        public RectTransform BackgroundLayer { get; private set; }
        public Image BackgroundImage { get; private set; }
        public RectTransform HotspotLayer { get; private set; }
        public RectTransform PortraitLayer { get; private set; }

        public Text RoomLabel { get; private set; }
        public Button NotebookButton { get; private set; }
        public Text EvidenceCountLabel { get; private set; }

        public DialogueView Dialogue { get; private set; }
        public ChoiceView Choices { get; private set; }
        public NotebookView Notebook { get; private set; }
        public RectTransform PuzzleLayer { get; private set; }

        RectTransform _toast;
        Text _toastLabel;
        Coroutine _toastRoutine;
        Image _fade;

        // ── 생성 ──────────────────────────────────────────────────────────────

        public void Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            Screen = (RectTransform)canvasGo.transform;

            BuildRoomLayer();
            BuildTopBar();

            Dialogue = new DialogueView(Screen);
            Choices  = new ChoiceView(Screen);
            Notebook = new NotebookView(Screen);

            PuzzleLayer = UIFactory.Rect("PuzzleLayer", Screen);
            PuzzleLayer.gameObject.SetActive(false);

            BuildToast();
            BuildFade();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildRoomLayer()
        {
            BackgroundLayer = UIFactory.Rect("RoomLayer", Screen);

            BackgroundImage = UIFactory.Box("Background", BackgroundLayer, new Color(0.13f, 0.14f, 0.18f, 1f));
            BackgroundImage.raycastTarget = false;

            PortraitLayer = UIFactory.Rect("PortraitLayer", BackgroundLayer);
            HotspotLayer  = UIFactory.Rect("HotspotLayer", BackgroundLayer);
        }

        void BuildTopBar()
        {
            var bar = UIFactory.Box("TopBar", Screen, new Color(0.05f, 0.06f, 0.09f, 0.85f));
            UIFactory.Anchor(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -72), new Vector2(0, 0));
            bar.raycastTarget = false;

            RoomLabel = UIFactory.Label("RoomName", bar.transform, "", 30, TextAnchor.MiddleLeft, UIFactory.Ink);
            UIFactory.Anchor(RoomLabel.rectTransform, new Vector2(0, 0), new Vector2(0.6f, 1),
                             new Vector2(32, 0), new Vector2(0, 0));

            NotebookButton = UIFactory.Btn("NotebookButton", bar.transform, "증거 노트  (Tab)", 24,
                                           new Color(0.16f, 0.17f, 0.23f, 1f), UIFactory.Accent);
            UIFactory.Anchor(NotebookButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                             new Vector2(-268, -22), new Vector2(-24, 22));

            EvidenceCountLabel = UIFactory.Label("EvidenceCount", bar.transform, "", 22, TextAnchor.MiddleRight, UIFactory.InkDim);
            UIFactory.Anchor(EvidenceCountLabel.rectTransform, new Vector2(1, 0), new Vector2(1, 1),
                             new Vector2(-460, 0), new Vector2(-284, 0));
        }

        void BuildToast()
        {
            var box = UIFactory.Box("Toast", Screen, new Color(0.08f, 0.09f, 0.12f, 0.95f));
            _toast = box.rectTransform;
            UIFactory.Anchor(_toast, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                             new Vector2(-320, -180), new Vector2(320, -96));
            box.raycastTarget = false;

            _toastLabel = UIFactory.Label("Label", _toast, "", 26, TextAnchor.MiddleCenter, UIFactory.Accent);
            _toast.gameObject.SetActive(false);
        }

        void BuildFade()
        {
            _fade = UIFactory.Box("Fade", Screen, new Color(0, 0, 0, 0));
            _fade.raycastTarget = false;
            _fade.transform.SetAsLastSibling();
        }

        // ── 동작 ──────────────────────────────────────────────────────────────

        public void SetRoomLabel(string text) { if (RoomLabel != null) RoomLabel.text = text; }

        public void RefreshEvidenceCount()
        {
            if (EvidenceCountLabel == null || Game.Evidence == null) return;
            EvidenceCountLabel.text = Game.Evidence.Count > 0 ? $"증거 {Game.Evidence.Count}" : "";
        }

        public void Toast(string message)
        {
            if (_toast == null) return;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            _toastLabel.text = message;
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            yield return new WaitForSeconds(2.2f);
            _toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        public IEnumerator Fade(float from, float to, float duration)
        {
            if (_fade == null) yield break;
            _fade.transform.SetAsLastSibling();

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                _fade.color = new Color(0, 0, 0, a);
                yield return null;
            }
            _fade.color = new Color(0, 0, 0, to);
        }

        /// <summary>화면 흔들기. [shake] 태그가 이걸 부릅니다.</summary>
        public IEnumerator Shake(float duration = 0.35f, float magnitude = 18f)
        {
            if (BackgroundLayer == null) yield break;

            var origin = BackgroundLayer.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(t / duration);
                BackgroundLayer.anchoredPosition = origin + new Vector2(
                    Random.Range(-1f, 1f) * magnitude * damper,
                    Random.Range(-1f, 1f) * magnitude * damper);
                yield return null;
            }
            BackgroundLayer.anchoredPosition = origin;
        }
    }
}
