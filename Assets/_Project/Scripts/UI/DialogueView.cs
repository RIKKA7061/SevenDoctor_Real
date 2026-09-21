using System;
using System.Collections.Generic;
using SevenDoctors.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 화면 하단 대화창. 화자 이름표 + 본문 + 진행 화살표.
    ///
    /// 두 가지 방식으로 만들어집니다:
    ///   · Create(parent)  — 코드로 새로 만듭니다 (프리팹이 없을 때)
    ///   · Bind(root)      — UI 프리팹에 이미 들어 있는 오브젝트에 다시 연결합니다
    /// 버튼 리스너는 직렬화되지 않으므로, Bind 쪽에서도 반드시 다시 걸어줍니다.
    /// </summary>
    public class DialogueView
    {
        public RectTransform Root { get; private set; }

        Image _nameBox;
        Text _nameLabel;
        Text _bodyLabel;
        Text _nextArrow;
        Button _clickCatcher;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        /// <summary>대화창 아무 데나 클릭했을 때. 타이핑 스킵 / 다음 줄 진행에 씁니다.</summary>
        public event Action Clicked;

        DialogueView() { }

        // ── 코드로 생성 ───────────────────────────────────────────────────────

        public static DialogueView Create(RectTransform parent)
        {
            var v = new DialogueView();

            v.Root = UIFactory.Rect("DialoguePanel", parent);
            UIFactory.Anchor(v.Root, new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(0, 0), new Vector2(0, 340));

            // 대화창 전체가 클릭 판정 — 어디를 눌러도 다음으로 넘어갑니다.
            var bg = UIFactory.Box("Body", v.Root, new Color(0.05f, 0.06f, 0.09f, 0.94f));
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(48, 36), new Vector2(-48, -8));
            bg.raycastTarget = true;

            v._clickCatcher = bg.gameObject.AddComponent<Button>();
            v._clickCatcher.transition = Selectable.Transition.None;

            v._nameBox = UIFactory.Box("NameBox", v.Root, UIFactory.Accent);
            UIFactory.Anchor(v._nameBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(48, -4), new Vector2(340, 56));
            v._nameBox.raycastTarget = false;

            v._nameLabel = UIFactory.Label("Name", v._nameBox.transform, "", 28, TextAnchor.MiddleCenter,
                                           new Color(0.06f, 0.07f, 0.10f));

            v._bodyLabel = UIFactory.Label("Text", bg.transform, "", 32, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(v._bodyLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(40, 40), new Vector2(-40, -36));
            v._bodyLabel.lineSpacing = 1.35f;

            v._nextArrow = UIFactory.Label("NextArrow", bg.transform, "▼", 28, TextAnchor.LowerRight, UIFactory.Accent);
            UIFactory.Anchor(v._nextArrow.rectTransform, new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-64, 16), new Vector2(-24, 56));

            v.Wire();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        // ── 프리팹에 다시 연결 ────────────────────────────────────────────────

        public static DialogueView Bind(RectTransform canvasRoot)
        {
            var root = UIFactory.FindRect(canvasRoot, "DialoguePanel");
            if (root == null) return null;

            var v = new DialogueView
            {
                Root          = root,
                _clickCatcher = UIFactory.Find<Button>(root, "Body"),
                _nameBox      = UIFactory.Find<Image>(root, "NameBox"),
                _nameLabel    = UIFactory.Find<Text>(root, "NameBox/Name"),
                _bodyLabel    = UIFactory.Find<Text>(root, "Body/Text"),
                _nextArrow    = UIFactory.Find<Text>(root, "Body/NextArrow"),
            };

            if (v._clickCatcher == null || v._bodyLabel == null || v._nameBox == null) return null;

            v.Wire();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        void Wire()
        {
            _clickCatcher.onClick.RemoveAllListeners();
            _clickCatcher.onClick.AddListener(() => Clicked?.Invoke());
        }

        // ── 동작 ──────────────────────────────────────────────────────────────

        public void Show(string speakerName, string body, bool showArrow)
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling(); // 퍼즐 패널 위에서도 클릭이 먹도록

            bool hasSpeaker = !string.IsNullOrEmpty(speakerName);
            _nameBox.gameObject.SetActive(hasSpeaker);
            if (hasSpeaker) _nameLabel.text = speakerName;

            _bodyLabel.text = body;
            _nextArrow.gameObject.SetActive(showArrow);
        }

        public void SetBody(string body) => _bodyLabel.text = body;

        /// <summary>
        /// 타이핑 중 본문. 아직 안 찍힌 부분도 투명하게 같이 넣습니다 — 안 그러면
        /// 글자가 늘 때마다 줄바꿈 위치가 바뀌어서, 이미 찍힌 문장이 덜그럭거립니다.
        /// </summary>
        public void SetBodyTyping(string full, int shown)
        {
            if (string.IsNullOrEmpty(full)) { _bodyLabel.text = string.Empty; return; }

            shown = Mathf.Clamp(shown, 0, full.Length);
            _bodyLabel.text = shown >= full.Length
                ? full
                : full.Substring(0, shown) + "<color=#00000000>" + full.Substring(shown) + "</color>";
        }
        public void SetArrow(bool visible) => _nextArrow.gameObject.SetActive(visible);
        public void Hide() => Root.gameObject.SetActive(false);
    }

    /// <summary>선택지 목록. 대화창 위에 세로로 쌓입니다.</summary>
    public class ChoiceView
    {
        public RectTransform Root { get; private set; }
        RectTransform _list;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        ChoiceView() { }

        public static ChoiceView Create(RectTransform parent)
        {
            var v = new ChoiceView();
            v.Root = UIFactory.Rect("ChoicePanel", parent);

            var scrim = UIFactory.Box("Scrim", v.Root, new Color(0, 0, 0, 0.35f));
            scrim.raycastTarget = true;

            v._list = UIFactory.Rect("List", v.Root);
            UIFactory.Anchor(v._list, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-460, -260), new Vector2(460, 260));
            UIFactory.VLayout(v._list, 18, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            v.Root.gameObject.SetActive(false);
            return v;
        }

        public static ChoiceView Bind(RectTransform canvasRoot)
        {
            var root = UIFactory.FindRect(canvasRoot, "ChoicePanel");
            if (root == null) return null;

            var v = new ChoiceView
            {
                Root  = root,
                _list = UIFactory.FindRect(root, "List"),
            };
            if (v._list == null) return null;

            v.Root.gameObject.SetActive(false);
            return v;
        }

        public void Show(List<ChoiceRow> choices, Action<ChoiceRow> onPick)
        {
            UIFactory.Clear(_list);
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();

            foreach (var choice in choices)
            {
                var captured = choice;
                var btn = UIFactory.Btn($"Choice_{choice.Id}", _list, choice.Text, 28,
                                        new Color(0.11f, 0.12f, 0.17f, 0.97f), UIFactory.Ink);
                UIFactory.Height(btn.gameObject, 84);
                btn.onClick.AddListener(() =>
                {
                    Hide();
                    onPick?.Invoke(captured);
                });
            }
        }

        public void Hide()
        {
            Root.gameObject.SetActive(false);
            UIFactory.Clear(_list);
        }
    }
}
