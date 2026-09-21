using System;
using SevenDoctors.Core;
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
        public RectTransform Root { get; private set; }

        RectTransform _grid;
        Text _detailName;
        Text _detailBody;
        Button _askButton;
        Text _askLabel;
        Text _emptyLabel;
        Text _titleLabel, _closeLabel;
        Button _closeButton;
        Image _detailIcon;
        RectTransform _detailPanel;

        string _selected;

        /// <summary>선택한 증거를 현재 방 인물에게 제시했을 때.</summary>
        public event Action<string> AskRequested;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        NotebookView() { }

        // ── 코드로 생성 ───────────────────────────────────────────────────────

        public static NotebookView Create(RectTransform parent)
        {
            var v = new NotebookView();
            v.Root = UIFactory.Rect("Notebook", parent);

            var scrim = UIFactory.Box("Scrim", v.Root, UIFactory.Scrim);
            scrim.raycastTarget = true;

            var panel = UIFactory.Box("Panel", v.Root, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-760, -440), new Vector2(760, 440));

            v._titleLabel = UIFactory.Label("Title", panel.transform, Loc.T("ui.notebook.title"), 36, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(v._titleLabel.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(40, -84), new Vector2(-40, -28));

            v._closeButton = UIFactory.Btn("Close", panel.transform, Loc.T("ui.notebook.close"), 24,
                                           new Color(0.16f, 0.17f, 0.23f), UIFactory.Ink);
            v._closeLabel = UIFactory.EnsureLabel(v._closeButton, 24, UIFactory.Ink);
            UIFactory.Anchor(v._closeButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-160, -84), new Vector2(-32, -28));

            // 왼쪽: 증거 그리드
            var left = UIFactory.Rect("List", panel.transform);
            UIFactory.Anchor(left, new Vector2(0, 0), new Vector2(0.56f, 1),
                             new Vector2(32, 32), new Vector2(-16, -104));

            v._grid = UIFactory.Rect("Grid", left);
            UIFactory.Grid(v._grid, new Vector2(240, 96), new Vector2(16, 16), 3);

            v._emptyLabel = UIFactory.Label("Empty", left, Loc.T("ui.notebook.empty"),
                                            26, TextAnchor.UpperLeft, UIFactory.InkDim);

            // 오른쪽: 상세
            var right = UIFactory.Box("Detail", panel.transform, new Color(0.07f, 0.08f, 0.11f, 1f));
            UIFactory.Anchor(right.rectTransform, new Vector2(0.56f, 0), new Vector2(1, 1),
                             new Vector2(16, 32), new Vector2(-32, -104));
            right.raycastTarget = false;

            v._detailPanel = right.rectTransform;
            v._detailIcon = UIFactory.Box("Icon", right.transform, Color.white);
            UIFactory.Anchor(v._detailIcon.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                             new Vector2(-148, -156), new Vector2(-28, -36));
            v._detailIcon.preserveAspect = true;
            v._detailIcon.raycastTarget = false;

            v._detailName = UIFactory.Label("Name", right.transform, "", 30, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(v._detailName.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(28, -84), new Vector2(-160, -28));

            v._detailBody = UIFactory.Label("Body", right.transform, "", 25, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(v._detailBody.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(28, 120), new Vector2(-28, -96));
            v._detailBody.lineSpacing = 1.3f;

            v._askButton = UIFactory.Btn("Ask", right.transform, "", 26,
                                         new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Anchor(v._askButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(28, 28), new Vector2(-28, 100));
            v._askLabel = UIFactory.EnsureLabel(v._askButton, 26, UIFactory.Accent);

            v.Wire();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        // ── 프리팹에 다시 연결 ────────────────────────────────────────────────

        public static NotebookView Bind(RectTransform canvasRoot)
        {
            var root = UIFactory.FindRect(canvasRoot, "Notebook");
            if (root == null) return null;

            var v = new NotebookView
            {
                Root         = root,
                _closeButton = UIFactory.Find<Button>(root, "Panel/Close"),
                _grid        = UIFactory.FindRect(root, "Panel/List/Grid"),
                _emptyLabel  = UIFactory.Find<Text>(root, "Panel/List/Empty"),
                _detailName  = UIFactory.Find<Text>(root, "Panel/Detail/Name"),
                _detailBody  = UIFactory.Find<Text>(root, "Panel/Detail/Body"),
                _askButton   = UIFactory.Find<Button>(root, "Panel/Detail/Ask"),
            };

            if (v._grid == null || v._askButton == null || v._closeButton == null) return null;
            v._askLabel   = UIFactory.EnsureLabel(v._askButton, 26, UIFactory.Accent);
            v._closeLabel = UIFactory.EnsureLabel(v._closeButton, 24, UIFactory.Ink);
            v._titleLabel = UIFactory.Find<Text>(root, "Panel/Title");
            v.Relocalize();

            v._detailPanel = UIFactory.FindRect(root, "Panel/Detail");
            v._detailIcon = root.Find("Panel/Detail/Icon")?.GetComponent<Image>();
            if (v._detailIcon == null && v._detailPanel != null)
            {
                v._detailIcon = UIFactory.Box("Icon", v._detailPanel, Color.white);
                UIFactory.Anchor(v._detailIcon.rectTransform, new Vector2(1, 1), new Vector2(1, 1),
                                 new Vector2(-148, -156), new Vector2(-28, -36));
                v._detailIcon.preserveAspect = true;
                v._detailIcon.raycastTarget = false;
            }

            v.Wire();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        void Wire()
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(Hide);

            _askButton.onClick.RemoveAllListeners();
            _askButton.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(_selected)) return;
                Hide();
                AskRequested?.Invoke(_selected);
            });
        }

        // ── 동작 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 언어가 바뀌었을 때. 증거 목록은 Rebuild 가 시트에서 다시 읽어 오지만,
        /// 한 번 찍고 마는 제목·닫기·빈 목록 문구는 여기서 다시 씁니다.
        /// </summary>
        public void Relocalize()
        {
            if (_titleLabel != null) _titleLabel.text = Loc.T("ui.notebook.title");
            if (_closeLabel != null) _closeLabel.text = Loc.T("ui.notebook.close");
            if (_emptyLabel != null) _emptyLabel.text = Loc.T("ui.notebook.empty");
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

                var btn = UIFactory.Btn($"Ev_{id}", _grid, row.DisplayName, 22,
                                        new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink);

                var icon = LoadIcon(row.IconKey);
                if (icon != null)
                {
                    var iconImg = UIFactory.Box("Icon", btn.transform, Color.white);
                    UIFactory.Anchor(iconImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                                     new Vector2(10, -34), new Vector2(78, 34));
                    iconImg.sprite = icon;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;

                    // 아이콘 자리를 비켜 글자를 오른쪽으로
                    var label = btn.transform.Find("Label") as RectTransform;
                    if (label != null) label.offsetMin = new Vector2(84, 8);
                }

                btn.onClick.AddListener(() => Select(captured));
            }

            Select(_selected != null && Game.Evidence.Has(_selected) ? _selected : owned[0]);
        }

        /// <summary>Resources/Art/Evidence/{아이콘키}.png — 없으면 null 이고, 그럼 글자만 나옵니다.</summary>
        static Sprite LoadIcon(string iconKey)
        {
            return string.IsNullOrEmpty(iconKey) ? null : Resources.Load<Sprite>($"Art/Evidence/{iconKey}");
        }

        void Select(string evidenceId)
        {
            _selected = evidenceId;

            if (string.IsNullOrEmpty(evidenceId) || Game.Db == null ||
                !Game.Db.Evidences.TryGetValue(evidenceId, out var row))
            {
                _detailName.text = "";
                _detailBody.text = "";
                if (_detailIcon != null) _detailIcon.gameObject.SetActive(false);
                _askButton.gameObject.SetActive(false);
                return;
            }

            if (_detailIcon != null)
            {
                var icon = LoadIcon(row.IconKey);
                _detailIcon.sprite = icon;
                _detailIcon.gameObject.SetActive(icon != null);
            }

            _detailName.text = row.DisplayName;
            _detailBody.text = string.IsNullOrEmpty(row.Category)
                ? row.Description
                : $"<color=#8C93A5>[{Loc.Category(row.Category)}]</color>\n\n{row.Description}";

            // 현재 방에 인물이 있을 때만 '보여주기'가 열립니다.
            var character = Game.Room != null ? Game.Db.FindCharacterInRoom(Game.Room.CurrentRoomId) : null;
            bool canAsk = character != null && Game.State == GameState.Exploring;

            _askButton.gameObject.SetActive(canAsk);
            if (canAsk) _askLabel.text = Loc.T("ui.notebook.show_to", character.DisplayName);
        }
    }
}
