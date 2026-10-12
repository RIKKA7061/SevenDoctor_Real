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
        Image _detailIcon;           // 상세 칸의 큰 그림. 누르면 EvidenceZoomView 로 크게 봅니다
        Button _detailZoom;
        Text _zoomHint;
        RectTransform _detailPanel;

        const float DetailImageHeight = 300f;
        const float DetailTop = 96f;       // 이름 아래부터

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

            v._detailName = UIFactory.Label("Name", right.transform, "", 30, TextAnchor.UpperLeft, UIFactory.Accent);
            v._detailBody = UIFactory.Label("Body", right.transform, "", 25, TextAnchor.UpperLeft, UIFactory.Ink);
            v._detailBody.lineSpacing = 1.3f;
            v.BuildDetailImage();

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
            if (v._detailPanel != null)
            {
                // 예전 프리팹의 작은 아이콘은 치우고 큰 그림 칸으로 바꿉니다.
                var oldIcon = v._detailPanel.Find("Icon");
                if (oldIcon != null) UnityEngine.Object.Destroy(oldIcon.gameObject);
                v.BuildDetailImage();
            }

            v.Wire();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        /// <summary>
        /// 상세 칸 위쪽의 큰 그림. 누르면 화면 가득 확대됩니다 — 작은 단서는 그림 안에만
        /// 있어서 여기서 들여다보는 게 추리의 일부입니다. 이름·설명 자리도 여기서 맞춥니다.
        /// </summary>
        void BuildDetailImage()
        {
            UIFactory.Anchor(_detailName.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(28, -84), new Vector2(-28, -28));
            UIFactory.Anchor(_detailBody.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(28, 120), new Vector2(-28, -DetailTop));

            _detailZoom = UIFactory.Btn("Image", _detailPanel, string.Empty, 1,
                                        new Color(0.11f, 0.12f, 0.16f, 1f), UIFactory.Ink);
            UIFactory.Anchor(_detailZoom.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(28, -DetailTop - DetailImageHeight), new Vector2(-28, -DetailTop));
            var blank = _detailZoom.GetComponentInChildren<Text>();
            if (blank != null) blank.gameObject.SetActive(false);

            _detailIcon = UIFactory.Box("Picture", _detailZoom.transform, Color.white);
            UIFactory.Anchor(_detailIcon.rectTransform, Vector2.zero, Vector2.one,
                             new Vector2(8, 8), new Vector2(-8, -8));
            _detailIcon.preserveAspect = true;
            _detailIcon.raycastTarget = false;

            _zoomHint = UIFactory.Label("ZoomHint", _detailZoom.transform, Loc.T("ui.notebook.zoom_hint"), 20,
                                        TextAnchor.LowerRight, UIFactory.InkDim);
            UIFactory.Anchor(_zoomHint.rectTransform, Vector2.zero, new Vector2(1, 0),
                             new Vector2(12, 8), new Vector2(-14, 40));
            _zoomHint.raycastTarget = false;

            _detailZoom.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_selected)) EvidenceZoomView.Open(_selected);
            });
            _detailZoom.gameObject.SetActive(false);
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
            if (_zoomHint != null) _zoomHint.text = Loc.T("ui.notebook.zoom_hint");
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Rebuild();
        }

        public void Hide()
        {
            EvidenceZoomView.Close();
            Root.gameObject.SetActive(false);
        }

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
                if (_detailZoom != null) _detailZoom.gameObject.SetActive(false);
                _askButton.gameObject.SetActive(false);
                return;
            }

            var picture = LoadIcon(row.IconKey);
            bool hasPicture = picture != null && _detailZoom != null;
            if (_detailZoom != null)
            {
                _detailIcon.sprite = picture;
                _detailZoom.gameObject.SetActive(hasPicture);
            }

            // 그림이 있으면 설명은 그 아래로 내려갑니다.
            float top = hasPicture ? DetailTop + DetailImageHeight + 20f : DetailTop;
            _detailBody.rectTransform.offsetMax = new Vector2(-28, -top);

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
