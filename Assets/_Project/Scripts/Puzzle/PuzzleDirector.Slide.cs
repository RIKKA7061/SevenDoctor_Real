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
    /// 슬라이딩 퍼즐. 그림 한 장을 N×N 으로 자르고 오른쪽 아래 한 칸을 비운 뒤 섞습니다.
    /// 빈칸 옆 조각을 누르면 밀려 들어가고, 전부 제자리에 오면 풀립니다.
    ///
    /// 시트(Puzzles 탭)에서는 이렇게 씁니다:
    ///   타입 = Slide
    ///   정답 = 그림키        (3×3)      예) pz_invitation
    ///        = 그림키:칸수   (N×N)      예) pz_invitation:4
    /// 그림은 Resources/Art/Puzzles → Evidence → Props 순서로 찾습니다. 정사각형이어야 하고,
    /// 조각끼리 똑같아 보이는 칸이 없어야 합니다 (ArtSource/gen_puzzles.py 머리말 참고).
    ///
    /// 섞을 때는 완성 상태에서 '실제로 가능한 이동'만 거꾸로 밟습니다. 아무렇게나 섞으면
    /// 절반은 영영 못 푸는 배치가 나옵니다.
    ///
    /// 실패 페널티가 없는 게임이라 막혀도 빠져나갈 길을 둡니다:
    ///   번호 보기 · 다시 섞기 · (많이 움직이면) 도우미에게 맡기기 · 나중에 다시
    /// '맡기기' 는 지금까지의 이동을 거꾸로 되감는 것이라 따로 풀이를 계산하지 않습니다.
    /// </summary>
    public partial class PuzzleDirector
    {
        const float SlideBoard = 600f;      // 판 한 변 (1920x1080 기준)
        const float SlideGap = 4f;          // 조각 사이 틈
        const float SlideSeconds = 0.10f;   // 조각 하나가 미끄러지는 시간
        const int SlideAutoSolveAfter = 40; // 이만큼 움직이면 '맡기기' 가 나타납니다

        IEnumerator RunSlide(PuzzleRow puzzle)
        {
            var body = BuildFrame(puzzle, out var feedback);

            ParseSlideAnswer(puzzle.Answer, out string imageKey, out int n);
            var sprite = LoadSlideImage(imageKey);
            if (sprite == null)
            {
                Debug.LogError($"[Puzzle] Slide '{puzzle.Id}' 의 그림 '{imageKey}' 을 찾을 수 없습니다. " +
                               "Puzzles 탭 '정답' 칸과 Resources/Art/Puzzles 를 확인하세요.");
                LastResultSuccess = true;
                yield break;
            }

            int count = n * n;
            int last = count - 1;                 // 빈칸이 될 조각 (오른쪽 아래)
            float cell = SlideBoard / n;

            // ── 판 ────────────────────────────────────────────────────────────
            var frame = UIFactory.Box("Board", body, new Color(0.04f, 0.05f, 0.07f));
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(SlideBoard + 20f, SlideBoard + 20f);
            frt.anchoredPosition = new Vector2(-290f, 0f);

            var grid = UIFactory.Rect("Grid", frt);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(SlideBoard, SlideBoard);
            grid.anchoredPosition = Vector2.zero;

            // 조각 t 의 제자리는 칸 t. cellOf[t] 는 지금 들어 있는 칸.
            var cellOf = new int[last];
            var tiles = new RectTransform[last];
            var tileButtons = new Button[last];
            var numbers = new GameObject[last];
            int blank = last;
            var trail = new Stack<int>();         // 빈칸이 지나온 칸들 — 되감으면 완성 상태
            int moves = 0;
            bool solved = false, aborted = false, autoSolving = false, showNumbers = false;

            Vector2 CellPosition(int index) =>
                new Vector2((index % n) * cell + SlideGap * 0.5f, -(index / n) * cell - SlideGap * 0.5f);

            Rect PieceUv(int index) =>
                new Rect((index % n) / (float)n, 1f - (index / n + 1f) / n, 1f / n, 1f / n);

            RawImage MakePiece(string name, Transform parent, int index)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(parent, false);
                UIFactory.Anchor((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = go.GetComponent<RawImage>();
                raw.texture = sprite.texture;
                raw.uvRect = PieceUv(index);
                raw.raycastTarget = false;
                return raw;
            }

            for (int t = 0; t < last; t++)
            {
                cellOf[t] = t;

                var btn = UIFactory.Btn($"Tile{t}", grid, string.Empty, 1, new Color(0.10f, 0.11f, 0.15f), UIFactory.Ink);
                tileButtons[t] = btn;
                var blankCaption = btn.GetComponentInChildren<Text>();
                if (blankCaption != null) blankCaption.gameObject.SetActive(false);

                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cell - SlideGap, cell - SlideGap);
                rt.anchoredPosition = CellPosition(t);
                tiles[t] = rt;

                MakePiece("Piece", rt, t);

                // 번호 — 그림만으로 어려울 때 켜는 보조 바퀴
                var badge = UIFactory.Box("Number", rt, new Color(0.05f, 0.06f, 0.09f, 0.82f));
                var brt = badge.rectTransform;
                brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 1f);
                brt.sizeDelta = new Vector2(46f, 46f);
                brt.anchoredPosition = new Vector2(6f, -6f);
                badge.raycastTarget = false;
                var num = UIFactory.Label("Text", brt, (t + 1).ToString(), 26, TextAnchor.MiddleCenter, UIFactory.Accent);
                UIFactory.Anchor(num.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                num.raycastTarget = false;
                badge.gameObject.SetActive(false);
                numbers[t] = badge.gameObject;
            }

            // 빈칸에 들어갈 마지막 조각. 다 맞추면 나타납니다.
            var lastHolder = UIFactory.Rect("LastPiece", grid);
            lastHolder.anchorMin = lastHolder.anchorMax = lastHolder.pivot = new Vector2(0f, 1f);
            lastHolder.sizeDelta = new Vector2(cell - SlideGap, cell - SlideGap);
            lastHolder.anchoredPosition = CellPosition(last);
            var lastPiece = MakePiece("Piece", lastHolder, last);
            lastPiece.color = new Color(1f, 1f, 1f, 0f);

            // 칸 fromCell 의 조각을 빈칸으로 밉니다. 빈칸은 fromCell 로 옮겨 갑니다.
            void Slide(int fromCell)
            {
                for (int t = 0; t < last; t++)
                {
                    if (cellOf[t] != fromCell) continue;
                    cellOf[t] = blank;
                    break;
                }

                // 방금 온 칸으로 되돌아가면 이동 하나가 지워진 것입니다. 쌓지 않고 뺍니다.
                if (trail.Count > 0 && trail.Peek() == fromCell) trail.Pop();
                else trail.Push(blank);
                blank = fromCell;
            }

            bool IsSolved()
            {
                for (int t = 0; t < last; t++) if (cellOf[t] != t) return false;
                return true;
            }

            void Shuffle()
            {
                // 완성 상태로 되감은 뒤 다시 섞습니다. 지금 배치에서 더 섞으면 되감기 줄이 한없이 길어집니다.
                while (trail.Count > 0) Slide(trail.Peek());

                int previous = -1;
                int steps = n * n * 4;
                var options = new List<int>(4);
                for (int i = 0; i < steps || IsSolved(); i++)
                {
                    options.Clear();
                    foreach (int c in SlideNeighbors(blank, n))
                        if (c != previous) options.Add(c);   // 바로 되돌아가면 섞이지 않습니다
                    int pick = options[Random.Range(0, options.Count)];
                    previous = blank;
                    Slide(pick);
                }

                for (int t = 0; t < last; t++) tiles[t].anchoredPosition = CellPosition(cellOf[t]);
                moves = 0;
            }

            // ── 오른쪽 안내 ───────────────────────────────────────────────────
            var side = UIFactory.Rect("Side", body);
            UIFactory.Anchor(side, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                             new Vector2(100f, 0f), new Vector2(620f, 0f));
            UIFactory.VLayout(side, 10, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var goalLabel = UIFactory.Label("GoalLabel", side, Loc.T("ui.puzzle.slide.goal"), 24,
                                            TextAnchor.MiddleLeft, UIFactory.InkDim);
            UIFactory.Height(goalLabel.gameObject, 34);

            var goal = UIFactory.Box("Goal", side, Color.white);
            goal.sprite = sprite;
            goal.preserveAspect = true;
            goal.raycastTarget = false;
            UIFactory.Height(goal.gameObject, 210);

            var how = UIFactory.Label("How", side, Loc.T("ui.puzzle.slide.how"), 23, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Height(how.gameObject, 62);

            var movesLabel = UIFactory.Label("Moves", side, "", 23, TextAnchor.MiddleLeft, UIFactory.InkDim);
            UIFactory.Height(movesLabel.gameObject, 32);

            var dim = new Color(0.13f, 0.14f, 0.19f);
            var numbersBtn = UIFactory.Btn("Numbers", side, "", 24, dim, UIFactory.Ink);
            var numbersLabel = UIFactory.EnsureLabel(numbersBtn, 24, UIFactory.Ink);
            UIFactory.Height(numbersBtn.gameObject, 56);

            var shuffleBtn = UIFactory.Btn("Shuffle", side, Loc.T("ui.puzzle.slide.shuffle"), 24, dim, UIFactory.Ink);
            UIFactory.Height(shuffleBtn.gameObject, 56);

            var autoBtn = UIFactory.Btn("AutoSolve", side, Loc.T("ui.puzzle.slide.autosolve"), 24,
                                        new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Height(autoBtn.gameObject, 56);
            autoBtn.gameObject.SetActive(false);

            var back = UIFactory.Btn("Back", side, Loc.T("ui.puzzle.back_later"), 24, dim, UIFactory.InkDim);
            UIFactory.Height(back.gameObject, 56);

            void Refresh()
            {
                movesLabel.text = Loc.T("ui.puzzle.slide.moves", moves);
                numbersLabel.text = Loc.T(showNumbers ? "ui.puzzle.slide.numbers_off" : "ui.puzzle.slide.numbers_on");
                foreach (var badge in numbers) badge.SetActive(showNumbers && !solved);
                if (moves >= SlideAutoSolveAfter && !solved) autoBtn.gameObject.SetActive(true);
                if (!solved && IsSolved()) solved = true;
            }

            numbersBtn.onClick.AddListener(() => { showNumbers = !showNumbers; Refresh(); });
            shuffleBtn.onClick.AddListener(() => { if (!solved && !autoSolving) { Shuffle(); Refresh(); } });
            autoBtn.onClick.AddListener(() => { if (!solved) autoSolving = true; });
            back.onClick.AddListener(() => aborted = true);

            for (int t = 0; t < last; t++)
            {
                int tile = t;
                tileButtons[t].onClick.AddListener(() =>
                {
                    if (solved || autoSolving) return;
                    if (!SlideAdjacent(cellOf[tile], blank, n)) return;   // 빈칸 옆 조각만 움직입니다
                    Slide(cellOf[tile]);
                    moves++;
                    Refresh();
                });
            }

            Shuffle();
            Refresh();

            // ── 진행 ──────────────────────────────────────────────────────────
            float speed = cell / SlideSeconds;
            float autoTimer = 0f;

            while (!aborted)
            {
                bool settled = true;
                for (int t = 0; t < last; t++)
                {
                    var target = CellPosition(cellOf[t]);
                    if (tiles[t].anchoredPosition == target) continue;
                    tiles[t].anchoredPosition = Vector2.MoveTowards(
                        tiles[t].anchoredPosition, target, speed * Time.unscaledDeltaTime);
                    settled = false;
                }

                if (solved && settled) break;

                if (autoSolving && !solved)
                {
                    autoTimer += Time.unscaledDeltaTime;
                    if (autoTimer >= SlideSeconds * 0.8f && trail.Count > 0)
                    {
                        autoTimer = 0f;
                        Slide(trail.Peek());
                        Refresh();
                    }
                }

                yield return null;
            }

            if (solved)
            {
                // 마지막 조각이 채워지는 걸 보여 주고 넘어갑니다. 여기가 이 퍼즐의 보상입니다.
                foreach (var badge in numbers) badge.SetActive(false);
                feedback.color = UIFactory.Accent;
                feedback.text = Loc.T("ui.puzzle.slide.done");
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    lastPiece.color = new Color(1f, 1f, 1f, t / 0.5f);
                    yield return null;
                }
                lastPiece.color = Color.white;
                yield return new WaitForSecondsRealtime(1.1f);
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

        /// <summary>'정답' 칸 해석. "그림키" 또는 "그림키:칸수". 칸수는 2~6 으로 잡아 둡니다.</summary>
        static void ParseSlideAnswer(string answer, out string imageKey, out int size)
        {
            imageKey = (answer ?? string.Empty).Trim();
            size = 3;

            int colon = imageKey.LastIndexOf(':');
            if (colon <= 0) return;

            if (int.TryParse(imageKey.Substring(colon + 1).Trim(), out int parsed))
                size = Mathf.Clamp(parsed, 2, 6);
            imageKey = imageKey.Substring(0, colon).Trim();
        }

        static Sprite LoadSlideImage(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var folder in new[] { "Art/Puzzles", "Art/Evidence", "Art/Props" })
            {
                var sprite = Resources.Load<Sprite>($"{folder}/{key}");
                if (sprite != null) return sprite;
            }
            return null;
        }

        static bool SlideAdjacent(int a, int b, int n)
        {
            int dx = Mathf.Abs(a % n - b % n), dy = Mathf.Abs(a / n - b / n);
            return dx + dy == 1;
        }

        static IEnumerable<int> SlideNeighbors(int index, int n)
        {
            int x = index % n, y = index / n;
            if (x > 0) yield return index - 1;
            if (x < n - 1) yield return index + 1;
            if (y > 0) yield return index - n;
            if (y < n - 1) yield return index + n;
        }
    }
}
