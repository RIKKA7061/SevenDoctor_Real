using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.Dialogue;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 대화 기록창. 게임을 시작하고 나온 대사를 처음부터 전부 위에서 아래로 늘어놓습니다.
    /// 상단바 '기록' 버튼, L 키, 대사 중 마우스 휠을 위로 굴리면 열립니다. L·Esc 로 닫습니다.
    ///
    /// 열면 맨 아래(가장 최근 대사)부터 보입니다. 휠·드래그·스크롤바·↑↓·Home/End 로 넘깁니다.
    ///
    ///   · 장소 줄   — 대사가 나온 방이 바뀔 때마다 가운데에 '— 1층 로비 —'
    ///   · 대사      — 금색 이름 + 본문. 지문(화자 없음)은 흐린 글씨
    ///   · 선택지    — 고른 것만 '▶ 선택'
    ///   · 증거      — 얻은 증거 이름
    ///
    /// 기록은 DialogueRunner.Log 에 행 단위로 쌓입니다. 열 때마다 새로 생긴 줄만 덧붙이고,
    /// 언어가 바뀌면 전부 다시 그립니다.
    /// </summary>
    public class LogView
    {
        const float WheelStep = 140f;   // 휠 한 칸에 넘기는 높이(px)

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        ScrollRect _scroll;
        RectTransform _viewport, _content;
        Text _title, _closeLabel, _help, _empty;
        int _built;          // 지금 그려 둔 기록 줄 수
        bool _dirty = true;  // 전부 다시 그려야 하는지 (언어 변경 등)

        LogView() { }

        public static LogView Create(RectTransform screen)
        {
            // 예전에 구운 UI 프리팹에 같은 이름이 남아 있으면 치웁니다 — 겹쳐서 두 개가 뜨지 않게.
            var old = screen.Find("Log");
            if (old != null) Object.Destroy(old.gameObject);

            var v = new LogView { Root = UIFactory.Rect("Log", screen) };

            var scrim = UIFactory.Btn("Scrim", v.Root, string.Empty, 1, UIFactory.Scrim, UIFactory.Ink);
            UIFactory.Anchor(scrim.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var blank = scrim.GetComponentInChildren<Text>();
            if (blank != null) blank.gameObject.SetActive(false);
            scrim.transition = Selectable.Transition.None;
            scrim.onClick.AddListener(v.Hide);

            var panel = UIFactory.Box("Panel", v.Root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-760, -470), new Vector2(760, 470));
            panel.raycastTarget = true;   // 판 안을 눌러도 닫히지 않게

            v._title = UIFactory.Label("Title", panel.transform, "", 36, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(v._title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -84), new Vector2(-40, -28));

            var close = UIFactory.Btn("Close", panel.transform, "", 24, new Color(0.16f, 0.17f, 0.23f), UIFactory.Ink);
            v._closeLabel = UIFactory.EnsureLabel(close, 24, UIFactory.Ink);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-200, -84), new Vector2(-32, -28));
            close.onClick.AddListener(v.Hide);

            v._help = UIFactory.Label("Help", panel.transform, "", 22, TextAnchor.MiddleLeft, UIFactory.InkDim);
            UIFactory.Anchor(v._help.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(40, 20), new Vector2(-40, 64));

            // 스크롤 판 — ScrollRect 는 끌기·관성·스크롤바를 맡고, 휠과 키는 LogScroller 가 맡습니다.
            // 입력 모듈마다 휠 한 칸 값이 1 이기도 120 이기도 해서 ScrollRect 에 맡기면 속도가 들쭉날쭉합니다.
            var frame = UIFactory.Box("Frame", panel.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            UIFactory.Anchor(frame.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(32, 72), new Vector2(-32, -100));

            var viewport = UIFactory.Box("Viewport", frame.transform, new Color(0, 0, 0, 0));
            viewport.raycastTarget = true;   // 투명해도 휠·끌기를 받아야 합니다
            UIFactory.Anchor(viewport.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-28, 0));
            viewport.gameObject.AddComponent<RectMask2D>();
            v._viewport = viewport.rectTransform;

            v._content = UIFactory.Rect("Content", v._viewport);
            v._content.anchorMin = new Vector2(0, 1);
            v._content.anchorMax = new Vector2(1, 1);
            v._content.pivot = new Vector2(0.5f, 1f);
            v._content.sizeDelta = Vector2.zero;
            v._content.anchoredPosition = Vector2.zero;
            UIFactory.VLayout(v._content, 20, new RectOffset(32, 32, 28, 28), TextAnchor.UpperLeft);
            v._content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            v._scroll = frame.gameObject.AddComponent<ScrollRect>();
            v._scroll.viewport = v._viewport;
            v._scroll.content = v._content;
            v._scroll.horizontal = false;
            v._scroll.vertical = true;
            v._scroll.movementType = ScrollRect.MovementType.Clamped;
            v._scroll.scrollSensitivity = 0f;
            v._scroll.verticalScrollbar = BuildScrollbar(frame.rectTransform);
            v._scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            viewport.gameObject.AddComponent<LogScroller>().View = v;

            v._empty = UIFactory.Label("Empty", v._viewport, "", 26, TextAnchor.MiddleCenter, UIFactory.InkDim);

            v.Relocalize();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        static Scrollbar BuildScrollbar(RectTransform frame)
        {
            var track = UIFactory.Box("Scrollbar", frame, new Color(1f, 1f, 1f, 0.05f));
            UIFactory.Anchor(track.rectTransform, new Vector2(1, 0), new Vector2(1, 1),
                             new Vector2(-20, 12), new Vector2(-8, -12));

            var area = UIFactory.Rect("Sliding Area", track.transform);
            var handle = UIFactory.Box("Handle", area, new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.6f));

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            // 눌러서 선택된 채로 ↑↓ 를 누르면 LogScroller 와 같이 움직여 두 배로 넘어갑니다.
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        public void Relocalize()
        {
            if (_title != null) _title.text = Loc.T("ui.log.title");
            if (_closeLabel != null) _closeLabel.text = Loc.T("ui.log.close");
            if (_help != null) _help.text = Loc.T("ui.log.help");
            if (_empty != null) _empty.text = Loc.T("ui.log.empty");

            // 지난 대사도 바뀐 언어로 다시 그립니다.
            _dirty = true;
            if (IsVisible) Refresh();
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Refresh();
            ScrollToEnd();
        }

        public void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        // ── 그리기 ────────────────────────────────────────────────────────────

        void Refresh()
        {
            var log = Game.Dialogue != null ? Game.Dialogue.Log : null;
            int count = log != null ? log.Count : 0;

            // 재시작으로 기록이 비었을 때도 처음부터 다시 그립니다.
            if (_dirty || count < _built)
            {
                UIFactory.Clear(_content);
                _built = 0;
                _dirty = false;
            }

            for (; _built < count; _built++) AddRow(log.Entries[_built]);
            _empty.gameObject.SetActive(count == 0);
        }

        void AddRow(DialogueLog.Entry e)
        {
            switch (e.Kind)
            {
                case DialogueLog.Kind.Place:
                    var place = Row($"— {e.Room.DisplayName} —", 22, TextAnchor.MiddleCenter, UIFactory.InkDim);
                    // 장소가 바뀌는 곳은 위를 조금 더 띄웁니다.
                    place.GetComponent<LayoutElement>().minHeight = 56f;
                    break;

                case DialogueLog.Kind.Line:
                    string body = TagUtil.StripTags(e.Line.Text);
                    if (string.IsNullOrEmpty(e.Line.SpeakerId))
                    {
                        Row(body, 26, TextAnchor.UpperLeft, UIFactory.InkDim);
                    }
                    else
                    {
                        string name = Game.Db.CharacterName(e.Line.SpeakerId);
                        Row($"<color=#{Hex(UIFactory.Accent)}>{name}</color>\n{body}", 26, TextAnchor.UpperLeft, UIFactory.Ink);
                    }
                    break;

                case DialogueLog.Kind.Choice:
                    Row(Loc.T("ui.log.choice", e.Choice.Text), 24, TextAnchor.UpperLeft, ChoiceInk);
                    break;

                case DialogueLog.Kind.Evidence:
                    Row(Loc.T("ui.evidence.gained", e.Evidence.DisplayName), 22, TextAnchor.UpperLeft, UIFactory.Accent);
                    break;
            }
        }

        static readonly Color ChoiceInk = new Color(0.56f, 0.76f, 0.89f);

        Text Row(string text, int size, TextAnchor anchor, Color color)
        {
            var label = UIFactory.Label($"Row{_built}", _content, text, size, anchor, color);
            label.lineSpacing = 1.25f;
            label.gameObject.AddComponent<LayoutElement>();
            return label;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ── 넘기기 ────────────────────────────────────────────────────────────

        /// <summary>맨 아래(가장 최근 대사)로. 줄 높이가 아직 안 정해졌으면 먼저 계산시킵니다.</summary>
        void ScrollToEnd()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.velocity = Vector2.zero;
            _scroll.verticalNormalizedPosition = 0f;
        }

        /// <summary>양수면 위(옛 대사)로, 음수면 아래로 px 만큼 넘깁니다.</summary>
        public void ScrollBy(float px)
        {
            float overflow = _content.rect.height - _viewport.rect.height;
            if (overflow <= 0f) return;
            _scroll.velocity = Vector2.zero;
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + px / overflow);
        }

        public void ScrollWheel(float delta) { if (delta != 0f) ScrollBy(Mathf.Sign(delta) * WheelStep); }
        public void ScrollToTop() { _scroll.velocity = Vector2.zero; _scroll.verticalNormalizedPosition = 1f; }
        public void ScrollToBottom() { _scroll.velocity = Vector2.zero; _scroll.verticalNormalizedPosition = 0f; }
    }
}
