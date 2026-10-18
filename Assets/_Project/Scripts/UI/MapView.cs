using System.Collections;
using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 저택 지도. 건물 단면도처럼 층마다 방을 늘어놓고, 누르면 그 방으로 갑니다.
    /// 방 사이 이동은 전부 여기서 합니다 — 방 안에 출구 버튼을 따로 두지 않습니다.
    ///
    /// 배치는 Rooms 탭의 '지도층 · 지도칸 · 지도폭' 을 그대로 씁니다 (위에서부터 5층 … 1층, B1).
    /// 그래서 방을 추가하거나 옮겨도 코드는 안 바뀝니다. 지도층이 빈 방(연구실)은 안 나옵니다.
    ///
    ///   · 지금 있는 방   — 금색
    ///   · 갈 수 있는 방  — 누르면 이동
    ///   · 아직 잠긴 방   — 어둡게. 누르면 '아직 갈 수 없는 곳' 알림
    ///   · 지도연결       — 그 방이 열린 뒤에만 빨간 길(비밀 통로)로 이어 그립니다.
    ///                      처음부터 보이면 반전이 새어 나갑니다.
    /// 개발자 방 이동 키를 한 번 쓰면(RoomController.IgnoreLocks) 잠긴 방도 누를 수 있습니다.
    ///
    /// 지금 있는 방에는 박사 얼굴 말(Props/prop_token_doctor)이 놓입니다. 다른 방을 누르면
    /// 말이 건물 안을 걸어가듯 움직인 뒤 도착하면 방이 바뀝니다 —
    ///   같은 층    옆으로
    ///   다른 층    가운데 계단 줄(방 중심들의 가운데 x)까지 갔다가 오르내린 뒤 옆으로
    ///   지도연결   빨간 비밀 통로를 따라 건물 바깥으로 돌아서
    /// 걷는 동안에는 다른 방을 누르거나 지도를 닫을 수 없습니다.
    /// </summary>
    public class MapView
    {
        const float Unit = 150f;       // 한 칸 폭
        const float Gap = 8f;
        const float RowHeight = 100f;
        const float RowGap = 12f;
        const float LabelWidth = 90f;
        const float PathMargin = 70f;  // 오른쪽 바깥으로 도는 비밀 통로 자리
        const float TokenSize = 64f;
        const float WalkSpeed = 720f;  // px/초
        const string TokenKey = "Art/Props/prop_token_doctor";

        static readonly Color Current = new Color(0.36f, 0.29f, 0.12f);
        static readonly Color Open = new Color(0.16f, 0.18f, 0.24f);
        static readonly Color Locked = new Color(0.08f, 0.09f, 0.12f);
        static readonly Color Passage = new Color(0.86f, 0.16f, 0.16f);

        public RectTransform Root { get; private set; }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        RectTransform _board;
        Text _title, _closeLabel, _help;

        // 이번에 그린 지도의 자리 정보 — 말이 걸어갈 길을 짤 때 씁니다 (판 왼쪽 위 기준 좌표).
        readonly Dictionary<string, Vector2> _centers = new Dictionary<string, Vector2>();
        readonly Dictionary<string, RoomRow> _rooms = new Dictionary<string, RoomRow>();
        float _stairX, _outsideX;
        RectTransform _token;
        bool _walking;

        public bool IsWalking => _walking;

        MapView() { }

        public static MapView Create(RectTransform screen)
        {
            var v = new MapView { Root = UIFactory.Rect("Map", screen) };
            UIFactory.Anchor(v.Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

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

            v._board = UIFactory.Rect("Board", panel.transform);
            UIFactory.Anchor(v._board, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 72), new Vector2(-40, -100));

            v.Relocalize();
            v.Root.gameObject.SetActive(false);
            return v;
        }

        public void Relocalize()
        {
            if (_title != null) _title.text = Loc.T("ui.map.title");
            if (_closeLabel != null) _closeLabel.text = Loc.T("ui.map.close");
            if (_help != null) _help.text = Loc.T("ui.map.help");
        }

        public void Toggle() { if (IsVisible) Hide(); else Show(); }

        public void Show()
        {
            if (Game.Db == null) return;
            if (Game.UI.Notebook != null && Game.UI.Notebook.IsVisible) Game.UI.Notebook.Hide();
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Rebuild();
        }

        public void Hide()
        {
            if (_walking) return;   // 걷는 중에 닫으면 도착을 못 합니다
            if (Root != null) Root.gameObject.SetActive(false);
        }

        // ── 그리기 ────────────────────────────────────────────────────────────

        void Rebuild()
        {
            UIFactory.Clear(_board);

            var rooms = new List<RoomRow>();
            foreach (var r in Game.Db.Rooms.Values) if (r.OnMap) rooms.Add(r);
            if (rooms.Count == 0) return;

            // 위층부터 한 줄씩. 층 번호가 큰 쪽이 위입니다.
            var levels = new List<int>();
            int columns = 0;
            foreach (var r in rooms)
            {
                if (!levels.Contains(r.MapLevel)) levels.Add(r.MapLevel);
                columns = Mathf.Max(columns, r.MapColumn + r.MapSpan);
            }
            levels.Sort((a, b) => b.CompareTo(a));

            float gridWidth = columns * Unit;
            float gridHeight = levels.Count * RowHeight + (levels.Count - 1) * RowGap;
            float boardWidth = LabelWidth + gridWidth + PathMargin;

            // 판 가운데에 놓습니다. 좌표는 왼쪽 위 기준으로 계산합니다.
            var origin = UIFactory.Rect("Origin", _board);
            origin.anchorMin = origin.anchorMax = new Vector2(0.5f, 0.5f);
            origin.sizeDelta = new Vector2(boardWidth, gridHeight);
            origin.anchoredPosition = Vector2.zero;

            var cells = new Dictionary<string, RectTransform>();
            _centers.Clear();
            _rooms.Clear();
            _outsideX = LabelWidth + gridWidth + PathMargin * 0.5f;
            string here = Game.Room != null ? Game.Room.CurrentRoomId : null;
            bool ignoreLocks = Game.Room != null && Game.Room.IgnoreLocks;

            for (int i = 0; i < levels.Count; i++)
            {
                float top = i * (RowHeight + RowGap);
                var label = UIFactory.Label($"Floor{levels[i]}", origin, FloorName(levels[i]), 28,
                                            TextAnchor.MiddleLeft, UIFactory.InkDim);
                Place(label.rectTransform, 0f, top, LabelWidth, RowHeight);
            }

            foreach (var r in rooms)
            {
                float top = levels.IndexOf(r.MapLevel) * (RowHeight + RowGap);
                float left = LabelWidth + r.MapColumn * Unit;
                bool isHere = r.Id == here;
                bool open = ignoreLocks || Game.Flags.Check(r.RequiredFlag);

                var btn = UIFactory.Btn($"Room_{r.Id}", origin, r.DisplayName, 24,
                                        isHere ? Current : open ? Open : Locked,
                                        isHere ? UIFactory.Accent : open ? UIFactory.Ink : UIFactory.InkDim);
                var rt = btn.GetComponent<RectTransform>();
                Place(rt, left + Gap * 0.5f, top, r.MapSpan * Unit - Gap, RowHeight);
                cells[r.Id] = rt;
                _centers[r.Id] = new Vector2(left + r.MapSpan * Unit * 0.5f, top + RowHeight * 0.5f);
                _rooms[r.Id] = r;

                var caption = btn.GetComponentInChildren<Text>();
                if (caption != null && !open) caption.text = $"{r.DisplayName}\n<size=18>{Loc.T("ui.map.locked")}</size>";

                string id = r.Id;
                btn.onClick.AddListener(() => OnRoomClicked(id, isHere, open));
            }

            foreach (var r in rooms)
            {
                if (string.IsNullOrEmpty(r.MapLink) || !cells.ContainsKey(r.MapLink) || !cells.ContainsKey(r.Id)) continue;
                if (!(ignoreLocks || Game.Flags.Check(r.RequiredFlag))) continue;   // 열리기 전엔 비밀입니다
                DrawPassage(origin, cells[r.MapLink], cells[r.Id], _outsideX);
            }

            // 계단 줄 — 방 중심 x 들의 가운데값. 이 지도에선 복도 칸 줄이 됩니다.
            var xs = new List<float>();
            foreach (var c in _centers.Values) xs.Add(c.x);
            xs.Sort();
            _stairX = xs[xs.Count / 2];

            // 박사 말 — 마지막에 만들어야 칸과 빨간 길 위에 올라옵니다.
            _token = UIFactory.Rect("Token", origin);
            var tokenImage = _token.gameObject.AddComponent<Image>();
            tokenImage.sprite = Resources.Load<Sprite>(TokenKey);
            tokenImage.color = tokenImage.sprite != null ? Color.white : UIFactory.Accent;
            tokenImage.raycastTarget = false;
            _token.anchorMin = _token.anchorMax = new Vector2(0f, 1f);
            _token.pivot = new Vector2(0.5f, 0.5f);
            _token.sizeDelta = new Vector2(TokenSize, TokenSize);
            bool onMap = here != null && _centers.ContainsKey(here);
            _token.gameObject.SetActive(onMap);
            if (onMap) PutToken(_centers[here]);
        }

        /// <summary>
        /// 판 왼쪽 위 기준 좌표(칸 가운데)에 말을 놓습니다. 방 이름을 가리지 않게
        /// 칸 위쪽 테두리에 핀처럼 걸쳐 앉힙니다.
        /// </summary>
        void PutToken(Vector2 p, float lift = 0f)
        {
            float y = p.y - RowHeight * 0.5f + 4f;
            _token.anchoredPosition = new Vector2(p.x, -y + lift);
        }

        // ── 걷기 ──────────────────────────────────────────────────────────────

        /// <summary>from 에서 to 까지 말이 지나갈 꺾은선. 판 왼쪽 위 기준 좌표.</summary>
        List<Vector2> Route(string from, string to)
        {
            var a = _centers[from];
            var b = _centers[to];
            var path = new List<Vector2> { a };

            bool secret = _rooms[from].MapLink == to || _rooms[to].MapLink == from;
            if (secret)
            {
                path.Add(new Vector2(_outsideX, a.y));
                path.Add(new Vector2(_outsideX, b.y));
            }
            else if (!Mathf.Approximately(a.y, b.y))
            {
                path.Add(new Vector2(_stairX, a.y));
                path.Add(new Vector2(_stairX, b.y));
            }
            path.Add(b);

            // 같은 점이 이어지면 빼 둡니다 (복도 칸에서 출발할 때 등).
            for (int i = path.Count - 1; i > 0; i--)
                if ((path[i] - path[i - 1]).sqrMagnitude < 1f) path.RemoveAt(i);
            return path;
        }

        IEnumerator Walk(string from, string to)
        {
            _walking = true;
            var path = Route(from, to);

            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1], path[i]);
            float duration = Mathf.Clamp(total / WalkSpeed, 0.45f, 1.8f);

            _token.localScale = Vector3.one * 1.12f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = t / duration;
                float eased = u * u * (3f - 2f * u);            // 출발·도착에서 천천히
                float along = eased * total;
                // 통통 튀는 걸음 — 걸은 거리에 맞춰 위아래로.
                float hop = Mathf.Abs(Mathf.Sin(along / 38f * Mathf.PI)) * 9f;
                PutToken(PointAlong(path, along), hop);
                yield return null;
            }
            PutToken(path[path.Count - 1]);
            _token.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(0.15f);
            _walking = false;
            Hide();
            Game.Room.Enter(to);
        }

        static Vector2 PointAlong(List<Vector2> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float leg = Vector2.Distance(path[i - 1], path[i]);
                if (distance <= leg) return Vector2.Lerp(path[i - 1], path[i], leg > 0f ? distance / leg : 1f);
                distance -= leg;
            }
            return path[path.Count - 1];
        }

        /// <summary>
        /// 두 방을 건물 바깥(오른쪽)으로 도는 빨간 길로 잇습니다. 기획 지도의 빨간 선 그대로 —
        /// 위쪽 방 오른쪽 끝에서 나와 바깥 벽을 타고 내려가 아래쪽 방 오른쪽 끝으로 들어갑니다.
        /// </summary>
        static void DrawPassage(RectTransform origin, RectTransform from, RectTransform to, float outsideX)
        {
            const float Thickness = 12f;
            Vector2 a = RightMiddle(from), b = RightMiddle(to);

            Segment(origin, new Vector2(a.x, a.y), new Vector2(outsideX, a.y), Thickness);
            Segment(origin, new Vector2(outsideX, a.y), new Vector2(outsideX, b.y), Thickness);
            Segment(origin, new Vector2(outsideX, b.y), new Vector2(b.x, b.y), Thickness);
        }

        static Vector2 RightMiddle(RectTransform rt)
        {
            // Place 가 왼쪽 위 기준으로 놓았으므로 거꾸로 풉니다.
            return new Vector2(rt.anchoredPosition.x + rt.sizeDelta.x, -rt.anchoredPosition.y + rt.sizeDelta.y * 0.5f);
        }

        static void Segment(RectTransform origin, Vector2 p, Vector2 q, float thickness)
        {
            var line = UIFactory.Box("Passage", origin, Passage);
            line.raycastTarget = false;
            float x = Mathf.Min(p.x, q.x) - thickness * 0.5f, y = Mathf.Min(p.y, q.y) - thickness * 0.5f;
            Place(line.rectTransform, x, y, Mathf.Abs(q.x - p.x) + thickness, Mathf.Abs(q.y - p.y) + thickness);
        }

        /// <summary>판의 왼쪽 위를 (0,0) 으로, 아래로 갈수록 y 가 커지게 놓습니다.</summary>
        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        static string FloorName(int level) => level < 0 ? $"B{-level}" : $"{level}F";

        void OnRoomClicked(string roomId, bool isHere, bool open)
        {
            if (_walking) return;
            if (isHere) { Hide(); return; }
            if (!open) { Game.UI.Toast(Loc.T("ui.room.locked")); return; }

            string here = Game.Room != null ? Game.Room.CurrentRoomId : null;
            if (here == null || !_centers.ContainsKey(here) || _token == null)
            {
                // 지도 밖(연구실 등)에서 열었으면 걸어갈 출발점이 없으니 바로 갑니다.
                Hide();
                Game.Room.Enter(roomId);
                return;
            }

            Game.UI.StartCoroutine(Walk(here, roomId));
        }
    }
}
