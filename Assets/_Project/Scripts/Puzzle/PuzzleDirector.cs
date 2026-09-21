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
    /// 퍼즐 실행기.
    ///
    /// 설계 핵심: 퍼즐은 13개지만 '엔진'은 5개고, 지금 구현한 건 1순위 두 개입니다.
    ///   CodeLock  — 성격 조합 자물쇠 / 추억의 날짜 금고 / 애너그램(변형)
    ///   Deduction — 모순 찾기 / 거짓말쟁이 대화 / 어긋난 증언(2장 메인)
    /// 나머지(Assemble / FindOdd / MiniGame)는 여기에 case 를 추가하면 같은 방식으로 붙습니다.
    ///
    /// 실패 페널티는 두지 않습니다. 원스토리 추리게임에서 페널티는 이탈률만 올립니다.
    /// 대신 오답마다 다른 반응 대사를 틀어서, 틀리는 것 자체가 콘텐츠가 되게 합니다.
    /// </summary>
    public class PuzzleDirector : MonoBehaviour
    {
        public bool LastResultSuccess { get; private set; }

        public IEnumerator RunRoutine(string puzzleId)
        {
            LastResultSuccess = false;

            if (Game.Db == null || !Game.Db.Puzzles.TryGetValue(puzzleId, out var puzzle))
            {
                Debug.LogError($"[Puzzle] '{puzzleId}' 퍼즐을 찾을 수 없습니다.");
                yield break;
            }

            var previousState = Game.State;
            Game.State = GameState.InPuzzle;

            Game.UI.PuzzleLayer.gameObject.SetActive(true);
            UIFactory.Clear(Game.UI.PuzzleLayer);

            switch (puzzle.Type.Trim().ToLowerInvariant())
            {
                case "code":
                case "codelock":
                    yield return RunCodeLock(puzzle);
                    break;

                case "deduction":
                    yield return RunDeduction(puzzle);
                    break;

                default:
                    Debug.LogWarning($"[Puzzle] '{puzzle.Type}' 타입은 아직 구현 전입니다. 통과 처리합니다.");
                    LastResultSuccess = true;
                    break;
            }

            UIFactory.Clear(Game.UI.PuzzleLayer);
            Game.UI.PuzzleLayer.gameObject.SetActive(false);
            Game.State = previousState;

            if (LastResultSuccess)
            {
                Game.Flags.Set(puzzle.SuccessFlag, true);
                if (!string.IsNullOrEmpty(puzzle.SuccessDialogue))
                {
                    yield return Game.Dialogue.PlayCore(puzzle.SuccessDialogue);
                    Game.UI.Dialogue.Hide();
                }
            }
        }

        // ── 공통 뼈대 ─────────────────────────────────────────────────────────

        /// <summary>제목 / 힌트 / 본문 영역을 갖춘 퍼즐 창을 만들고 본문 RectTransform 을 돌려줍니다.</summary>
        RectTransform BuildFrame(PuzzleRow puzzle, out Text feedback)
        {
            var scrim = UIFactory.Box("Scrim", Game.UI.PuzzleLayer, UIFactory.Scrim);
            scrim.raycastTarget = true;

            var panel = UIFactory.Box("Panel", Game.UI.PuzzleLayer, UIFactory.PanelSolid);
            UIFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-780, -460), new Vector2(780, 460));

            var title = UIFactory.Label("Title", panel.transform, puzzle.Title, 38,
                                        TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(48, -96), new Vector2(-48, -32));

            if (!string.IsNullOrEmpty(puzzle.Hint))
            {
                var hint = UIFactory.Label("Hint", panel.transform, Loc.T("ui.puzzle.hint", puzzle.Hint), 24,
                                           TextAnchor.UpperLeft, UIFactory.InkDim);
                UIFactory.Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                                 new Vector2(48, -140), new Vector2(-48, -96));
            }

            feedback = UIFactory.Label("Feedback", panel.transform, "", 26,
                                       TextAnchor.MiddleCenter, new Color(0.92f, 0.55f, 0.48f));
            UIFactory.Anchor(feedback.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(48, 24), new Vector2(-48, 76));

            var body = UIFactory.Rect("Body", panel.transform);
            UIFactory.Anchor(body, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(48, 88), new Vector2(-48, -152));
            return body;
        }

        // ── CodeLock ─────────────────────────────────────────────────────────

        IEnumerator RunCodeLock(PuzzleRow puzzle)
        {
            var body = BuildFrame(puzzle, out var feedback);

            string answer = (puzzle.Answer ?? string.Empty).Trim();
            if (answer.Length == 0)
            {
                Debug.LogError($"[Puzzle] CodeLock '{puzzle.Id}' 에 '정답' 칸이 비어 있습니다.");
                LastResultSuccess = true;
                yield break;
            }

            string input = "";
            bool solved = false, aborted = false;

            // 입력 표시창
            var display = UIFactory.Box("Display", body, new Color(0.04f, 0.05f, 0.07f));
            UIFactory.Anchor(display.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(200, -132), new Vector2(-200, -20));
            display.raycastTarget = false;

            var displayLabel = UIFactory.Label("Value", display.transform, "", 64,
                                               TextAnchor.MiddleCenter, UIFactory.Ink);

            void Redraw()
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < answer.Length; i++)
                {
                    sb.Append(i < input.Length ? input[i].ToString() : "_");
                    if (i < answer.Length - 1) sb.Append("  ");
                }
                displayLabel.text = sb.ToString();
            }
            Redraw();

            // 키패드 (0~9, 지우기, 확인)
            var pad = UIFactory.Rect("Keypad", body);
            UIFactory.Anchor(pad, new Vector2(0.5f, 0), new Vector2(0.5f, 1),
                             new Vector2(-330, 0), new Vector2(330, -152));
            UIFactory.Grid(pad, new Vector2(200, 92), new Vector2(16, 14), 3);

            // 키패드는 0~9 와 지우기/확인. 어떤 키인지를 화면 글자로 구분하면
            // 번역하는 순간 분기가 깨지므로, 종류를 따로 들고 다닙니다.
            const int KeyDigit = 0, KeyClear = 1, KeyEnter = 2;
            var keys = new (int Kind, string Digit)[]
            {
                (KeyDigit, "1"), (KeyDigit, "2"), (KeyDigit, "3"),
                (KeyDigit, "4"), (KeyDigit, "5"), (KeyDigit, "6"),
                (KeyDigit, "7"), (KeyDigit, "8"), (KeyDigit, "9"),
                (KeyClear, null), (KeyDigit, "0"), (KeyEnter, null),
            };

            foreach (var key in keys)
            {
                int kind = key.Kind;
                string digit = key.Digit;
                bool isAction = kind != KeyDigit;

                string caption = kind == KeyClear ? Loc.T("ui.puzzle.clear")
                               : kind == KeyEnter ? Loc.T("ui.puzzle.confirm")
                               : digit;
                string name = kind == KeyClear ? "Key_Clear"
                            : kind == KeyEnter ? "Key_Enter"
                            : $"Key_{digit}";

                var btn = UIFactory.Btn(name, pad, caption, isAction ? 26 : 38,
                                        kind == KeyEnter ? new Color(0.20f, 0.17f, 0.10f)
                                                         : new Color(0.13f, 0.14f, 0.19f),
                                        kind == KeyEnter ? UIFactory.Accent : UIFactory.Ink);

                btn.onClick.AddListener(() =>
                {
                    feedback.text = "";

                    if (kind == KeyClear)
                    {
                        if (input.Length > 0) input = input.Substring(0, input.Length - 1);
                    }
                    else if (kind == KeyEnter)
                    {
                        if (input == answer) { solved = true; return; }

                        feedback.text = Loc.T("ui.puzzle.wrong_code");
                        input = "";
                    }
                    else if (input.Length < answer.Length)
                    {
                        input += digit;
                    }

                    Redraw();
                });
            }

            var back = UIFactory.Btn("Back", body, Loc.T("ui.puzzle.back_later"), 24,
                                     new Color(0.13f, 0.14f, 0.19f), UIFactory.InkDim);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0),
                             new Vector2(-260, 0), new Vector2(0, 68));
            back.onClick.AddListener(() => aborted = true);

            while (!solved && !aborted) yield return null;

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

        // ── Deduction ────────────────────────────────────────────────────────

        IEnumerator RunDeduction(PuzzleRow puzzle)
        {
            var body = BuildFrame(puzzle, out var feedback);

            var slots = Game.Db.GetDeductionSlots(puzzle.Id);
            if (slots.Count == 0)
            {
                Debug.LogError($"[Puzzle] Deduction '{puzzle.Id}' 에 슬롯이 없습니다. DeductionSlots 탭을 확인하세요.");
                LastResultSuccess = true;
                yield break;
            }

            var filled = new string[slots.Count];
            int activeSlot = 0;
            bool solved = false, aborted = false;
            string wrongDialogue = null;

            // 문장 영역
            var sentence = UIFactory.Rect("Sentence", body);
            UIFactory.Anchor(sentence, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -260), new Vector2(0, 0));
            UIFactory.VLayout(sentence, 12, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var slotButtons = new List<Button>();
            var slotLabels = new List<Text>();

            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int index = i;

                var row = UIFactory.Rect($"Slot{i}", sentence);
                UIFactory.Height(row.gameObject, 76);

                var before = UIFactory.Label("Before", row, s.TextBefore, 30, TextAnchor.MiddleRight, UIFactory.Ink);
                UIFactory.Anchor(before.rectTransform, new Vector2(0, 0), new Vector2(0.38f, 1),
                                 new Vector2(0, 0), new Vector2(-12, 0));

                var slotBtn = UIFactory.Btn($"SlotBtn{i}", row, "____________", 28,
                                            new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
                UIFactory.Anchor(slotBtn.GetComponent<RectTransform>(), new Vector2(0.38f, 0), new Vector2(0.74f, 1),
                                 new Vector2(0, 8), new Vector2(0, -8));
                slotButtons.Add(slotBtn);
                slotLabels.Add(slotBtn.GetComponentInChildren<Text>());

                var after = UIFactory.Label("After", row, s.TextAfter, 30, TextAnchor.MiddleLeft, UIFactory.Ink);
                UIFactory.Anchor(after.rectTransform, new Vector2(0.74f, 0), new Vector2(1, 1),
                                 new Vector2(12, 0), new Vector2(0, 0));

                slotBtn.onClick.AddListener(() => { activeSlot = index; RefreshSlots(); });
            }

            void RefreshSlots()
            {
                for (int i = 0; i < slotButtons.Count; i++)
                {
                    string label = "____________";
                    if (!string.IsNullOrEmpty(filled[i]) &&
                        Game.Db.Evidences.TryGetValue(filled[i], out var ev))
                        label = ev.DisplayName;

                    slotLabels[i].text = label;
                    slotButtons[i].targetGraphic.color = (i == activeSlot)
                        ? new Color(0.26f, 0.22f, 0.12f)
                        : new Color(0.16f, 0.15f, 0.11f);
                }
            }
            RefreshSlots();

            // 후보 카드 영역
            var cards = UIFactory.Rect("Cards", body);
            UIFactory.Anchor(cards, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(0, 76), new Vector2(0, -268));
            UIFactory.Grid(cards, new Vector2(240, 84), new Vector2(14, 14), 5);

            void RebuildCards()
            {
                UIFactory.Clear(cards);

                var category = slots[activeSlot].Category;
                var candidates = Game.Evidence.OwnedByCategory(category);

                if (candidates.Count == 0)
                {
                    var none = UIFactory.Label("None", cards,
                        Loc.T("ui.puzzle.no_evidence", Loc.Category(category)), 24,
                        TextAnchor.MiddleCenter, UIFactory.InkDim);
                    UIFactory.Height(none.gameObject, 84);
                    return;
                }

                foreach (var ev in candidates)
                {
                    var captured = ev.Id;
                    var btn = UIFactory.Btn($"Card_{ev.Id}", cards, ev.DisplayName, 23,
                                            new Color(0.13f, 0.14f, 0.19f), UIFactory.Ink);
                    btn.onClick.AddListener(() =>
                    {
                        filled[activeSlot] = captured;
                        feedback.text = "";

                        // 다음 빈 슬롯으로 자동 이동 — 클릭 수를 줄여줍니다.
                        for (int i = 0; i < filled.Length; i++)
                        {
                            int idx = (activeSlot + 1 + i) % filled.Length;
                            if (string.IsNullOrEmpty(filled[idx])) { activeSlot = idx; break; }
                        }

                        RefreshSlots();
                        RebuildCards();
                    });
                }
            }
            RebuildCards();

            // 슬롯 버튼을 누르면 후보 목록도 갱신되어야 합니다.
            for (int i = 0; i < slotButtons.Count; i++)
                slotButtons[i].onClick.AddListener(RebuildCards);

            // 확인 / 나가기
            var submit = UIFactory.Btn("Submit", body, Loc.T("ui.puzzle.submit"), 27,
                                       new Color(0.20f, 0.17f, 0.10f), UIFactory.Accent);
            UIFactory.Anchor(submit.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(-300, 0), new Vector2(60, 64));

            submit.onClick.AddListener(() =>
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    if (string.IsNullOrEmpty(filled[i]))
                    {
                        feedback.text = Loc.T("ui.puzzle.empty_slot");
                        return;
                    }
                }

                for (int i = 0; i < slots.Count; i++)
                {
                    if (filled[i] != slots[i].AnswerEvidence)
                    {
                        // 오답 페널티 없음. 대신 그 슬롯 전용 반응 대사를 틉니다.
                        wrongDialogue = slots[i].WrongDialogue;
                        feedback.text = Loc.T("ui.puzzle.wrong_deduction");
                        filled[i] = null;
                        activeSlot = i;
                        RefreshSlots();
                        RebuildCards();
                        return;
                    }
                }

                solved = true;
            });

            var back = UIFactory.Btn("Back", body, Loc.T("ui.puzzle.think_more"), 24,
                                     new Color(0.13f, 0.14f, 0.19f), UIFactory.InkDim);
            UIFactory.Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                             new Vector2(80, 0), new Vector2(300, 64));
            back.onClick.AddListener(() => aborted = true);

            while (!solved && !aborted)
            {
                if (!string.IsNullOrEmpty(wrongDialogue))
                {
                    var target = wrongDialogue;
                    wrongDialogue = null;
                    yield return Game.Dialogue.PlayCore(target);
                    Game.UI.Dialogue.Hide();   // 퍼즐 위에 대화창이 남지 않게
                }
                yield return null;
            }

            LastResultSuccess = solved;
        }
    }
}
