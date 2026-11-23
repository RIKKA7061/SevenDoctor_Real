using System.Collections;
using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.Puzzle
{
    /// <summary>
    /// 타일 금고 퍼즐. 타일을 누르면 그 타일과 상하좌우가 함께 뒤집힙니다.
    /// 모든 타일을 황동색으로 밝히면 풀립니다.
    ///
    /// 시트(Puzzles 탭)에서는 이렇게 씁니다:
    ///   타입 = Tiles
    ///   정답 = 눌러야 할 칸을 줄마다 0/1 로, 줄은 '/' 로    예) 0100/0001/1000/0010
    ///          → 다 밝은 판에서 그 칸들을 누른 상태로 시작합니다. 그러니 그 칸들을 누르면 풀립니다.
    ///          (판 크기는 줄 수로 정합니다. 3~6)
    ///        = 0100/0001/1000/0010 ev_flowers
    ///          → 띄우고 증거 ID 를 붙이면, 그 단서를 갖고 있을 때 퍼즐 옆에 그림을 띄웁니다
    ///            (퍼즐 중엔 노트를 못 여니까요). 눌러서 크게 볼 수도 있습니다.
    ///        = 비워 두면 4×4 에서 아무 칸 6개를 누른 상태로 섞습니다.
    ///
    /// 누르는 순서는 상관없고, 같은 칸을 두 번 누르면 원래대로 돌아옵니다.
    /// 정답 칸 말고 다른 방법으로 다 밝혀도 통과입니다 — 판이 다 밝으면 열린 겁니다.
    /// </summary>
    public partial class PuzzleDirector
    {
        const float TilesBoard = 600f;
        const float TilesGap = 12f;
        const float TilesFlipSeconds = 0.18f;

        static readonly Color TileOff = new Color(0.42f, 0.13f, 0.17f);
        static readonly Color TileOn = new Color(0.86f, 0.68f, 0.33f);

        IEnumerator RunTiles(PuzzleRow puzzle)
        {
            var body = BuildFrame(puzzle, out var feedback);

            var parts = (puzzle.Answer ?? string.Empty).Trim().Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            var start = ParseTilesAnswer(parts.Length > 0 ? parts[0] : string.Empty, out int n);
            string clueId = parts.Length > 1 ? parts[1] : null;
            int count = n * n;
            var state = (bool[])start.Clone();
            int moves = 0;
            bool solved = false, aborted = false;

            // ── 판 ────────────────────────────────────────────────────────────
            var frame = UIFactory.Box("Vault", body, new Color(0.18f, 0.15f, 0.11f));
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(TilesBoard + 40f, TilesBoard + 40f);
            frt.anchoredPosition = new Vector2(-290f, 0f);
            var frameEdge = frame.gameObject.AddComponent<Outline>();
            frameEdge.effectColor = new Color(0.05f, 0.04f, 0.03f);
            frameEdge.effectDistance = new Vector2(4f, -4f);

            var grid = UIFactory.Rect("Grid", frt);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(TilesBoard, TilesBoard);
            grid.anchoredPosition = Vector2.zero;

            float cell = TilesBoard / n;
            var tiles = new Image[count];
            var flipAt = new float[count];      // 뒤집기 애니메이션이 시작된 시각 (안 뒤집는 중이면 -1)
            for (int i = 0; i < count; i++) flipAt[i] = -1f;

            // ── 오른쪽 안내 ───────────────────────────────────────────────────
            var side = UIFactory.Rect("Side", body);
            UIFactory.Anchor(side, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                             new Vector2(100f, 0f), new Vector2(620f, 0f));
            UIFactory.VLayout(side, 10, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var how = UIFactory.Label("How", side,
                Loc.TOr("ui.puzzle.tiles.how", "타일을 누르면 그 타일과 위·아래·양옆이 함께 뒤집힙니다. 전부 황동색으로 밝히면 열립니다."),
                23, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Height(how.gameObject, 92);

            // 단서 그림 — 정답 칸 뒤에 증거 ID 가 붙어 있고 그걸 갖고 있을 때만.
            if (!string.IsNullOrEmpty(clueId) && Game.Evidence != null && Game.Evidence.Has(clueId) &&
                Game.Db.Evidences.TryGetValue(clueId, out var clue))
            {
                var picture = EvidenceZoomView.LoadImage(clue.IconKey);
                if (picture != null)
                {
                    var clueLabel = UIFactory.Label("ClueLabel", side, clue.DisplayName, 22,
                                                    TextAnchor.MiddleLeft, UIFactory.InkDim);
                    UIFactory.Height(clueLabel.gameObject, 30);

                    var clueBtn = UIFactory.Btn("Clue", side, string.Empty, 1, new Color(0f, 0f, 0f, 0f), UIFactory.Ink);
                    var clueCaption = clueBtn.GetComponentInChildren<Text>();
                    if (clueCaption != null) clueCaption.gameObject.SetActive(false);
                    clueBtn.targetGraphic.color = Color.white;
                    ((Image)clueBtn.targetGraphic).sprite = picture;
                    ((Image)clueBtn.targetGraphic).preserveAspect = true;
                    UIFactory.Height(clueBtn.gameObject, 220);
                    string id = clueId;
                    clueBtn.onClick.AddListener(() => EvidenceZoomView.Open(id));   // 눌러서 크게
                }
            }

            var movesLabel = UIFactory.Label("Moves", side, "", 23, TextAnchor.MiddleLeft, UIFactory.InkDim);
            UIFactory.Height(movesLabel.gameObject, 32);

            var dim = new Color(0.13f, 0.14f, 0.19f);
            var resetBtn = UIFactory.Btn("Reset", side, Loc.TOr("ui.puzzle.tiles.reset", "처음 상태로"), 24, dim, UIFactory.Ink);
            UIFactory.Height(resetBtn.gameObject, 56);

            var back = UIFactory.Btn("Back", side, Loc.T("ui.puzzle.back_later"), 24, dim, UIFactory.InkDim);
            UIFactory.Height(back.gameObject, 56);

            void Refresh()
            {
                movesLabel.text = string.Format(Loc.TOr("ui.puzzle.tiles.moves", "누른 횟수 {0}"), moves);
            }

            for (int i = 0; i < count; i++)
            {
                int index = i;
                var btn = UIFactory.Btn($"Tile{i}", grid, string.Empty, 1, Color.white, UIFactory.Ink);
                var caption = btn.GetComponentInChildren<Text>();
                if (caption != null) caption.gameObject.SetActive(false);

                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cell - TilesGap, cell - TilesGap);
                rt.anchoredPosition = new Vector2((i % n + 0.5f) * cell, -(i / n + 0.5f) * cell);

                var edge = btn.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.08f, 0.06f, 0.04f);
                edge.effectDistance = new Vector2(3f, -3f);

                tiles[i] = (Image)btn.targetGraphic;
                tiles[i].color = state[i] ? TileOn : TileOff;

                btn.onClick.AddListener(() =>
                {
                    if (solved) return;
                    foreach (int t in TilesAround(index, n))
                    {
                        state[t] = !state[t];
                        flipAt[t] = Time.unscaledTime;
                    }
                    moves++;
                    Refresh();

                    bool all = true;
                    foreach (var on in state) if (!on) { all = false; break; }
                    solved = all;
                });
            }

            resetBtn.onClick.AddListener(() =>
            {
                if (solved) return;
                for (int i = 0; i < count; i++)
                {
                    if (state[i] != start[i]) flipAt[i] = Time.unscaledTime;
                    state[i] = start[i];
                }
                moves = 0;
                Refresh();
            });
            back.onClick.AddListener(() => aborted = true);
            Refresh();

            // ── 진행 — 뒤집히는 모습: 가로로 납작해졌다가 색이 바뀌고 다시 펴집니다 ──
            float solvedAt = -1f;
            while (!aborted)
            {
                for (int i = 0; i < count; i++)
                {
                    var rt = tiles[i].rectTransform;
                    if (flipAt[i] < 0f)
                    {
                        tiles[i].color = state[i] ? TileOn : TileOff;
                        continue;
                    }

                    float p = (Time.unscaledTime - flipAt[i]) / TilesFlipSeconds;
                    if (p >= 1f)
                    {
                        flipAt[i] = -1f;
                        rt.localScale = Vector3.one;
                        tiles[i].color = state[i] ? TileOn : TileOff;
                        continue;
                    }
                    rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p * Mathf.PI)), 1f, 1f);
                    // 반쯤 돌았을 때(옆에서 보일 때) 색을 바꿉니다
                    tiles[i].color = (p < 0.5f ? !state[i] : state[i]) ? TileOn : TileOff;
                }

                if (solved)
                {
                    if (solvedAt < 0f)
                    {
                        solvedAt = Time.unscaledTime;
                        feedback.color = UIFactory.Accent;
                        feedback.text = Loc.TOr("ui.puzzle.tiles.done", "…철컥. 금고가 열렸다.");
                    }
                    else if (Time.unscaledTime - solvedAt > 1.4f) break;
                }

                yield return null;
            }

            LastResultSuccess = solved;

            if (aborted && !string.IsNullOrEmpty(puzzle.FailDialogue))
            {
                UIFactory.Clear(Game.UI.PuzzleLayer);
                Game.UI.PuzzleLayer.gameObject.SetActive(false);
                yield return Game.Dialogue.PlayCore(puzzle.FailDialogue);
                Game.UI.Dialogue.Hide();
                Game.UI.PuzzleLayer.gameObject.SetActive(true);
            }
        }

        static IEnumerable<int> TilesAround(int index, int n)
        {
            yield return index;
            foreach (int c in SlideNeighbors(index, n)) yield return c;
        }

        /// <summary>
        /// '정답' 칸을 읽어 시작 상태를 만듭니다. 다 밝은 판에서 정답 칸들을 누른 상태입니다.
        /// 비어 있거나 잘못 적혔으면 4×4 에서 아무 칸 6개를 누른 상태로 섞습니다.
        /// </summary>
        static bool[] ParseTilesAnswer(string answer, out int n)
        {
            var rows = (answer ?? string.Empty).Trim().Split('/');
            n = rows.Length;
            bool valid = n >= 3 && n <= 6;
            var presses = new List<int>();
            for (int r = 0; valid && r < n; r++)
            {
                var row = rows[r].Trim();
                if (row.Length != n) { valid = false; break; }
                for (int col = 0; col < n; col++)
                {
                    if (row[col] == '1') presses.Add(r * n + col);
                    else if (row[col] != '0') { valid = false; break; }
                }
            }

            if (!valid || presses.Count == 0)
            {
                if (!string.IsNullOrEmpty(answer) && answer.Trim().Length > 0)
                    Debug.LogWarning($"[Puzzle] Tiles 정답 '{answer}' 를 읽지 못했습니다. 예) 0100/0001/1000/0010 — 아무렇게나 섞습니다.");
                n = 4;
                presses.Clear();
                var pool = new List<int>();
                for (int i = 0; i < 16; i++) pool.Add(i);
                for (int k = 0; k < 6; k++)
                {
                    int pick = Random.Range(0, pool.Count);
                    presses.Add(pool[pick]);
                    pool.RemoveAt(pick);
                }
            }

            var state = new bool[n * n];
            for (int i = 0; i < state.Length; i++) state[i] = true;
            foreach (int p in presses)
                foreach (int t in TilesAround(p, n)) state[t] = !state[t];

            // 누른 칸이 서로 상쇄돼서 처음부터 다 밝으면 퍼즐이 아닙니다. 한 칸 눌러 둡니다.
            bool all = true;
            foreach (var on in state) if (!on) { all = false; break; }
            if (all) foreach (int t in TilesAround(0, n)) state[t] = !state[t];
            return state;
        }
    }
}
