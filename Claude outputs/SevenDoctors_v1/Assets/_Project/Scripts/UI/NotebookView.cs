using System;
using SevenDoctors.Core;
using SevenDoctors.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 증거 노트. 보유 증거를 보여주고, 방에 인물이 있으면 그 증거를 '들이댈' 수 있습니다.
    /// (AskTopics 탭의 인물 × 증거 매트릭스가 여기서 소비됩니다)
    /// </summary>
    public class NotebookView
    {
        public RectTransform Root { get; }

        readonly RectTransform _grid;
        readonly Text _detailName;
        readonly Text _detailBody;
        readonly Button _askButton;
        readonly Text _askLabel;
        readonly Text _emptyLabel;

        string _selected;

        /// <summary>선택한 증거를 현재 방 인물에게 제시했을 때.</summary>
        public event Action<string> AskRequested;

        public bool IsVisible => Root.gameObject.activeSelf;

        public NotebookView(RectTransform parent)
        {
            Root = UIFactory.Rect("Notebook", parent);

            var scrim = UIFactory.Box("Scrim", Root, UIFactory.Scrim);
            scrim.raycastTarget = true;

            var panel = UIFactory.Box("Panel", Root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-760, -440), new Vector2(760, 440));

            var title = UIFactory.Label("Title", panel.transform, "증거 노트", 36, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -84), new Vector2(-40, -28));

            var close = UIFactory.Btn("Close", panel.transform, "닫기", 24,
                                      new Color(0.16f, 0.17f, 0.23f), UIFactory.Ink);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-160, -84), new Vector2(-32, -28));
            close.onClick.AddListener(Hide);

            // 왼쪽: 증거 그리드
            var left = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(left, new Vector2(0, 0), new Vector2(0.56f, 1),
                             new Vector2(32, 32), new Vector2(-16, -104));

            _grid = UIFactory.Rect("Grid", left);
            UIFactory.Grid(_grid, new Vector2(240, 96), new Vector2(16, 16), 3);

            _emptyLabel = UIFactory.Label("Empty", left, "아직 모은 증거가 없습니다.\n방을 둘러보세요.",
                                          26, TextAnchor.UpperLeft, UIFactory.InkDim);

            // 오른쪽: 상세
            var right = UIFactory.Box("Detail", panel.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            UIFactory.Anchor(right.rectTransform, new Vector2(0.56f, 0), new Vector2(1, 1),
                             new Vector2(16, 32), new Vector2(-32, -104));
            right.raycastTarget = false;

            _detailName = UIFactory.Label("Name", right.transform, "", 30, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(_detailName.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(28, -84), new Vector2(-28, -28));

            _detailBody = UIFactory.Label("Body", right.transform, "", 25, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(_detailBody.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(28, 120), new Vector2(-28, -96));
            _detailBody.lineSpacing = 1.3f;

            _askButton = UIFactory.Btn("Ask", right.transform, "", 26,
                                       new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Anchor(_askButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(28, 28), new Vector2(-28, 100));
            _askLabel = _askButton.GetComponentInChildren<Text>();
            _askButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(_selected)) return;
                Hide();
                AskRequested?.Invoke(_selected);
            });

            Root.gameObject.SetActive(false);
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Rebuild();
        }

        public void Hide() => Root.gameObject.SetActive(false);

        void Rebuild()
        {
            UIFactory.Clear(_grid);

            var owned = Game.Evidence != null ? Game.Evidence.Owned : null;
            bool empty = owned == null || owned.Count == 0;
            _emptyLabel.gameObject.SetActive(empty);
            _grid.gameObject.SetActive(!empty);

            if (empty)
            {
                Select(null);
                return;
            }

            foreach (var id in owned)
            {
                if (Game.Db == null || !Game.Db.Evidences.TryGetValue(id, out var row)) continue;
                var captured = id;

                var btn = UIFactory.Btn($"Ev_{id}", _grid, row.DisplayName, 24,
                                        new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink);
                btn.onClick.AddListener(() => Select(captured));
            }

            Select(_selected != null && Game.Evidence.Has(_selected) ? _selected : owned[0]);
        }

        void Select(string evidenceId)
        {
            _selected = evidenceId;

            if (string.IsNullOrEmpty(evidenceId) || Game.Db == null ||
                !Game.Db.Evidences.TryGetValue(evidenceId, out var row))
            {
                _detailName.text = "";
                _detailBody.text = "";
                _askButton.gameObject.SetActive(false);
                return;
            }

            _detailName.text = row.DisplayName;
            _detailBody.text = string.IsNullOrEmpty(row.Category)
                ? row.Description
                : $"<color=#8C93A5>[{row.Category}]</color>\n\n{row.Description}";

            // 현재 방에 인물이 있을 때만 '보여주기'가 열립니다.
            var character = Game.Room != null ? Game.Db.FindCharacterInRoom(Game.Room.CurrentRoomId) : null;
            bool canAsk = character != null && Game.State == GameState.Exploring;

            _askButton.gameObject.SetActive(canAsk);
            if (canAsk) _askLabel.text = $"{character.DisplayName}에게 보여주기";
        }
    }
}
