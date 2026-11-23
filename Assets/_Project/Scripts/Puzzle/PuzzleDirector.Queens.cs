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
    /// 퀸 배치 퍼즐. 체스 퀸 N 개를 서로 공격하지 못하게 판에 놓습니다.
    /// 가로줄·세로줄마다 정확히 하나, 대각선에도 둘이 겹치면 안 됩니다.
    /// 금색 테두리 퀸은 처음부터 자리가 정해져 있어서 움직일 수 없습니다.
    ///
    /// 시트(Puzzles 탭)에서는 이렇게 씁니다:
    ///   타입 = Queens
    ///   정답 = 판 크기          예) 6       → 열 때마다 정답이 하나뿐인 판을 새로 만듭니다
    ///        = 판 크기:고정 수   예) 8:3     → 고정 퀸 개수의 최소값 (기본 2)
    ///
    /// 정답이 하나뿐이 되도록 고정 퀸을 고르므로 '맞는데 왜 안 돼' 가 없습니다 —
    /// 서로 공격하지 않게 N 개를 다 놓으면 무조건 통과입니다.
    /// 틀린 것(서로 공격하는 퀸이 생긴 것)은 도우미 로봇이 막혔는지 판단할 때 셉니다.
    /// </summary>
    public partial class PuzzleDirector
    {
        const float QueensBoard = 620f;

        static readonly Color QueenSquareLight = new Color(0.93f, 0.95f, 0.92f);
        static readonly Color QueenSquareDark = new Color(0.62f, 0.70f, 0.60f);
        static readonly Color QueenAttacked = new Color(0.48f, 0.14f, 0.20f, 0.30f);
        static readonly Color QueenGiven = new Color(0.72f, 0.54f, 0.16f);
        static readonly Color QueenBad = new Color(0.62f, 0.14f, 0.20f);
        static readonly Color QueenInk = new Color(0.11f, 0.17f, 0.16f);

        IEnumerator RunQueens(PuzzleRow puzzle)
        {
            var body = BuildFrame(puzzle, out var feedback);

            ParseQueensAnswer(puzzle.Answer, out int n, out int minGivens);
            var givenCols = GenerateQueens(n, minGivens, out var solution);   // 줄마다 고정 퀸의 칸, 없으면 -1
            var queenSprite = Resources.Load<Sprite>("Art/Puzzles/pz_queen");

            var placed = new HashSet<int>();
            var givens = new HashSet<int>();
            for (int r = 0; r < n; r++)
                if (givenCols[r] >= 0) { placed.Add(r * n + givenCols[r]); givens.Add(r * n + givenCols[r]); }

            bool solved = false, aborted = false, showAttacks = false;
            int lastConflicts = 0;

            // ── 판 ────────────────────────────────────────────────────────────
            var frame = UIFactory.Box("Board", body, new Color(0.11f, 0.17f, 0.16f));
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(QueensBoard + 20f, QueensBoard + 20f);
            frt.anchoredPosition = new Vector2(-290f, 0f);

            var grid = UIFactory.Rect("Grid", frt);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(QueensBoard, QueensBoard);
            grid.anchoredPosition = Vector2.zero;

            float cell = QueensBoard / n;
            int count = n * n;
            var squares = new Image[count];
            var shades = new Image[count];
            var pieces = new Image[count];
            var marks = new Image[count];
            var glyphs = new Text[count];

            // ── 오른쪽 안내 ───────────────────────────────────────────────────
            var side = UIFactory.Rect("Side", body);
            UIFactory.Anchor(side, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                             new Vector2(100f, 0f), new Vector2(620f, 0f));
            UIFactory.VLayout(side, 10, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var how = UIFactory.Label("How", side,
                Loc.TOr("ui.puzzle.queens.how",
                        "칸을 누르면 퀸을 놓고, 다시 누르면 치웁니다. 가로줄·세로줄·대각선마다 퀸은 하나만. 금색 테두리 퀸은 움직일 수 없습니다."),
                23, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Height(how.gameObject, 124);

            var countLabel = UIFactory.Label("Count", side, "", 26, TextAnchor.MiddleLeft, UIFactory.Accent);
            UIFactory.Height(countLabel.gameObject, 40);

            var dim = new Color(0.13f, 0.14f, 0.19f);
            var attackBtn = UIFactory.Btn("Attacks", side, "", 24, dim, UIFactory.Ink);
            var attackLabel = UIFactory.EnsureLabel(attackBtn, 24, UIFactory.Ink);
            UIFactory.Height(attackBtn.gameObject, 56);

            var resetBtn = UIFactory.Btn("Reset", side, Loc.TOr("ui.puzzle.queens.reset", "처음부터"), 24, dim, UIFactory.Ink);
            UIFactory.Height(resetBtn.gameObject, 56);

            var back = UIFactory.Btn("Back", side, Loc.T("ui.puzzle.back_later"), 24, dim, UIFactory.InkDim);
            UIFactory.Height(back.gameObject, 56);

            bool Attacks(int a, int b)
            {
                int r1 = a / n, c1 = a % n, r2 = b / n, c2 = b % n;
                return r1 == r2 || c1 == c2 || Mathf.Abs(r1 - r2) == Mathf.Abs(c1 - c2);
            }

            HashSet<int> Conflicts()
            {
                var list = new List<int>(placed);
                var bad = new HashSet<int>();
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        if (Attacks(list[i], list[j])) { bad.Add(list[i]); bad.Add(list[j]); }
                return bad;
            }

            void Redraw()
            {
                var bad = Conflicts();
                for (int i = 0; i < count; i++)
                {
                    bool q = placed.Contains(i);
                    bool attacked = false;
                    if (showAttacks && !q)
                        foreach (int p in placed) if (Attacks(p, i)) { attacked = true; break; }

                    shades[i].color = attacked ? QueenAttacked : Color.clear;

                    var ink = bad.Contains(i) ? QueenBad : QueenInk;
                    pieces[i].enabled = q && queenSprite != null;
                    pieces[i].color = ink;
                    glyphs[i].gameObject.SetActive(q && queenSprite == null);
                    glyphs[i].color = ink;

                    marks[i].gameObject.SetActive(givens.Contains(i));
                    marks[i].color = bad.Contains(i) ? QueenBad : QueenGiven;
                }

                countLabel.text = string.Format(Loc.TOr("ui.puzzle.queens.count", "놓은 퀸  {0} / {1}"), placed.Count, n);
                attackLabel.text = showAttacks ? Loc.TOr("ui.puzzle.queens.attacks_off", "공격 범위 숨기기")
                                               : Loc.TOr("ui.puzzle.queens.attacks_on", "공격 범위 보기");

                if (bad.Count > 0)
                {
                    feedback.color = new Color(0.92f, 0.55f, 0.48f);
                    feedback.text = Loc.TOr("ui.puzzle.queens.conflict", "서로 공격하는 퀸이 있습니다. 붉은 퀸을 확인하세요.");
                }
                else feedback.text = "";

                // 충돌이 새로 생길 때만 '틀림' 으로 셉니다. 충돌을 그대로 두고 다른 칸을 눌러도 다시 세지 않습니다.
                if (bad.Count > lastConflicts) WrongAttempts++;
                lastConflicts = bad.Count;

                if (placed.Count == n && bad.Count == 0) solved = true;
            }

            for (int i = 0; i < count; i++)
            {
                int index = i;
                int r = i / n, c = i % n;
                var btn = UIFactory.Btn($"Square{i}", grid, string.Empty, 1,
                                        (r + c) % 2 == 0 ? QueenSquareLight : QueenSquareDark, UIFactory.Ink);
                btn.transition = Selectable.Transition.None;
                var caption = btn.GetComponentInChildren<Text>();
                if (caption != null) caption.gameObject.SetActive(false);

                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cell, cell);
                rt.anchoredPosition = new Vector2(c * cell, -r * cell);
                squares[i] = (Image)btn.targetGraphic;

                // 고정 퀸 테두리 — 금색 칸 위에 원래 칸 색을 조금 작게 덮어 안쪽 테두리처럼 보이게 합니다.
                marks[i] = UIFactory.Box("GivenFrame", rt, QueenGiven);
                marks[i].raycastTarget = false;
                var inner = UIFactory.Box("Inner", marks[i].rectTransform, squares[i].color);
                inner.raycastTarget = false;
                UIFactory.Anchor(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                marks[i].gameObject.SetActive(false);

                shades[i] = UIFactory.Box("Attacked", rt, Color.clear);
                shades[i].raycastTarget = false;

                pieces[i] = UIFactory.Box("Queen", rt, QueenInk);
                UIFactory.Anchor(pieces[i].rectTransform, Vector2.zero, Vector2.one,
                                 new Vector2(cell * 0.14f, cell * 0.12f), new Vector2(-cell * 0.14f, -cell * 0.12f));
                pieces[i].sprite = queenSprite;
                pieces[i].preserveAspect = true;
                pieces[i].raycastTarget = false;

                // 그림이 없을 때만 쓰는 글자 말. 폰트에 ♛ 가 없을 수도 있어서 'Q' 로 둡니다.
                glyphs[i] = UIFactory.Label("Glyph", rt, "Q", Mathf.RoundToInt(cell * 0.55f), TextAnchor.MiddleCenter, QueenInk);
                glyphs[i].fontStyle = FontStyle.Bold;

                btn.onClick.AddListener(() =>
                {
                    if (solved || givens.Contains(index)) return;
                    if (!placed.Remove(index)) placed.Add(index);
                    Redraw();
                });
            }

            attackBtn.onClick.AddListener(() => { showAttacks = !showAttacks; Redraw(); });
            resetBtn.onClick.AddListener(() =>
            {
                if (solved) return;
                placed.Clear();
                foreach (int g in givens) placed.Add(g);
                lastConflicts = 0;
                Redraw();
            });
            back.onClick.AddListener(() => aborted = true);

            Redraw();

            while (!solved && !aborted) yield return null;

            if (solved)
            {
                feedback.color = UIFactory.Accent;
                feedback.text = Loc.TOr("ui.puzzle.queens.done", "…모든 퀸이 제자리를 찾았다.");
                yield return new WaitForSecondsRealtime(1.4f);
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

        /// <summary>'정답' 칸 해석. "크기" 또는 "크기:고정 수". 크기는 4~10, 기본 6.</summary>
        static void ParseQueensAnswer(string answer, out int size, out int minGivens)
        {
            size = 6;
            minGivens = 2;
            var text = (answer ?? string.Empty).Trim();
            if (text.Length == 0) return;

            var parts = text.Split(':');
            if (int.TryParse(parts[0].Trim(), out int s)) size = Mathf.Clamp(s, 4, 10);
            if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int k)) minGivens = Mathf.Clamp(k, 0, size);
        }

        /// <summary>
        /// 무작위 정답 하나를 만든 뒤, 정답이 그것 하나만 남을 때까지 고정 퀸을 늘려 가며 고릅니다.
        /// 돌려주는 건 줄마다 고정 퀸의 칸(없으면 -1). 정답은 solution 으로 나옵니다.
        /// </summary>
        static int[] GenerateQueens(int n, int minGivens, out int[] solution)
        {
            solution = RandomQueens(n);
            var order = new List<int>();
            for (int k = minGivens; k <= n; k++)
            {
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    order.Clear();
                    for (int r = 0; r < n; r++) order.Add(r);
                    for (int i = order.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (order[i], order[j]) = (order[j], order[i]);
                    }

                    var g = new int[n];
                    for (int r = 0; r < n; r++) g[r] = -1;
                    for (int i = 0; i < k; i++) g[order[i]] = solution[order[i]];

                    if (CountQueens(n, g, 2) == 1) return g;
                }
            }
            return (int[])solution.Clone();
        }

        static int[] RandomQueens(int n)
        {
            var sol = new int[n];
            var cols = new bool[n];
            var d1 = new bool[2 * n];
            var d2 = new bool[2 * n];

            bool Go(int r)
            {
                if (r == n) return true;
                var order = new List<int>();
                for (int c = 0; c < n; c++) order.Add(c);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
                foreach (int c in order)
                {
                    if (cols[c] || d1[r + c] || d2[r - c + n]) continue;
                    cols[c] = d1[r + c] = d2[r - c + n] = true;
                    sol[r] = c;
                    if (Go(r + 1)) return true;
                    cols[c] = d1[r + c] = d2[r - c + n] = false;
                }
                return false;
            }

            Go(0);
            return sol;
        }

        /// <summary>고정 퀸 g 를 지키는 배치가 몇 개인지 셉니다 (limit 에서 멈춤).</summary>
        static int CountQueens(int n, int[] g, int limit)
        {
            var cols = new bool[n];
            var d1 = new bool[2 * n];
            var d2 = new bool[2 * n];
            int found = 0;

            void Go(int r)
            {
                if (found >= limit) return;
                if (r == n) { found++; return; }
                for (int c = 0; c < n; c++)
                {
                    if (g[r] >= 0 && g[r] != c) continue;
                    if (cols[c] || d1[r + c] || d2[r - c + n]) continue;
                    cols[c] = d1[r + c] = d2[r - c + n] = true;
                    Go(r + 1);
                    cols[c] = d1[r + c] = d2[r - c + n] = false;
                }
            }

            Go(0);
            return found;
        }
    }
}
