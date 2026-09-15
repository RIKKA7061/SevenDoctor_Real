using System;
using System.Collections.Generic;
using SevenDoctors.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>화면 하단 대화창. 화자 이름표 + 본문 + 진행 화살표.</summary>
    public class DialogueView
    {
        public RectTransform Root { get; }
        readonly Image _nameBox;
        readonly Text _nameLabel;
        readonly Text _bodyLabel;
        readonly Text _nextArrow;
        readonly Button _clickCatcher;

        public bool IsVisible => Root.gameObject.activeSelf;

        /// <summary>대화창 아무 데나 클릭했을 때. 타이핑 스킵 / 다음 줄 진행에 씁니다.</summary>
        public event Action Clicked;

        public DialogueView(RectTransform parent)
        {
            Root = UIFactory.Rect("DialoguePanel", parent);
            UIFactory.Anchor(Root, new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(0, 0), new Vector2(0, 340));

            // 대화창 전체가 클릭 판정 — 어디를 눌러도 다음으로 넘어갑니다.
            var bg = UIFactory.Box("Body", Root, new Color(0.05f, 0.06f, 0.09f, 0.94f));
            UIFactory.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(48, 36), new Vector2(-48, -8));
            bg.raycastTarget = true;

            _clickCatcher = bg.gameObject.AddComponent<Button>();
            _clickCatcher.transition = Selectable.Transition.None;
            _clickCatcher.onClick.AddListener(() => Clicked?.Invoke());

            _nameBox = UIFactory.Box("NameBox", Root, UIFactory.Accent);
            UIFactory.Anchor(_nameBox.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(48, -4), new Vector2(340, 56));
            _nameBox.raycastTarget = false;

            _nameLabel = UIFactory.Label("Name", _nameBox.transform, "", 28, TextAnchor.MiddleCenter,
                                         new Color(0.06f, 0.07f, 0.10f));

            _bodyLabel = UIFactory.Label("Text", bg.transform, "", 32, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(_bodyLabel.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(40, 40), new Vector2(-40, -36));
            _bodyLabel.lineSpacing = 1.35f;

            _nextArrow = UIFactory.Label("NextArrow", bg.transform, "▼", 28, TextAnchor.LowerRight, UIFactory.Accent);
            UIFactory.Anchor(_nextArrow.rectTransform, new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-64, 16), new Vector2(-24, 56));

            Root.gameObject.SetActive(false);
        }

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
        public void SetArrow(bool visible) => _nextArrow.gameObject.SetActive(visible);
        public void Hide() => Root.gameObject.SetActive(false);
    }

    /// <summary>선택지 목록. 대화창 위에 세로로 쌓입니다.</summary>
    public class ChoiceView
    {
        public RectTransform Root { get; }
        readonly RectTransform _list;

        public ChoiceView(RectTransform parent)
        {
            Root = UIFactory.Rect("ChoicePanel", parent);

            var scrim = UIFactory.Box("Scrim", Root, new Color(0, 0, 0, 0.35f));
            scrim.raycastTarget = true;

            _list = UIFactory.Rect("List", Root);
            UIFactory.Anchor(_list, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-460, -260), new Vector2(460, 260));
            UIFactory.VLayout(_list, 18, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            Root.gameObject.SetActive(false);
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
