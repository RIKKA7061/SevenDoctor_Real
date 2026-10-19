using System.Collections;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 엔딩 카드. 대사 연출태그 [ending:엔딩ID] 가 그 줄을 넘기는 순간 부릅니다.
    ///
    /// 화면을 검게 덮고 Endings 시트의 엔딩명을 띄운 뒤, '처음으로' 를 누르면 게임을
    /// 처음부터 다시 시작합니다. 여기가 끝이라 돌아오지 않습니다 — 코루틴은 씬이
    /// 다시 불릴 때까지 기다리기만 합니다. 그래서 대화 뒤쪽 줄은 실행되지 않습니다.
    ///
    /// 본 엔딩은 여기서 기록합니다(SaveSystem.MarkEndingSeen). 카드 아래에 엔딩 목록을 —
    /// 본 것은 이름, 아직 못 본 것은 ??? 로 — 늘어놓고, 엔딩 분기점 세이브가 있으면
    /// '분기점으로' 버튼이 나옵니다. 누르면 엔딩 선택지 바로 앞에서 다시 시작합니다.
    /// </summary>
    public static class EndingView
    {
        const float FadeSeconds = 1.6f;

        public static IEnumerator Play(string endingId)
        {
            var ui = Game.UI;
            var ending = Game.Db.FindEnding(endingId);
            string title = ending != null ? ending.DisplayName : endingId;
            SaveSystem.MarkEndingSeen(endingId);
            // 끝까지 왔으니 이어하기 칸도 갱신합니다 (마지막 탐색 순간 — 보통 지목 직전).
            // 분기점에서 불러와 본 엔딩이면 탐색 순간이 없으니 이어하기 칸은 그대로 남습니다.
            SaveSystem.AutoSave();

            ui.Dialogue.Hide();
            ui.Choices.Hide();
            ui.Portrait?.Hide();
            ui.Bot?.Hide();
            Game.State = GameState.Title;   // 핫스팟·노트·F1 을 막습니다

            var root = UIFactory.Rect("EndingLayer", ui.Screen);
            UIFactory.Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();         // 암전 레이어보다도 위
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var bg = UIFactory.Box("Bg", root, Color.black);
            UIFactory.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.raycastTarget = true;         // 뒤쪽 클릭이 새지 않게

            var end = UIFactory.Label("End", root, Loc.T("ui.ending.the_end"), 26,
                                      TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Anchor(end.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(0f, 90f), new Vector2(0f, 140f));

            var name = UIFactory.Label("Title", root, title, 56, TextAnchor.MiddleCenter, UIFactory.Accent);
            UIFactory.Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(0f, -40f), new Vector2(0f, 80f));

            var seen = UIFactory.Label("Collection", root, Collection(endingId), 24,
                                       TextAnchor.MiddleCenter, UIFactory.InkDim);
            UIFactory.Anchor(seen.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                             new Vector2(0f, -150f), new Vector2(0f, -70f));
            seen.lineSpacing = 1.4f;

            // 분기점 세이브가 있으면 버튼 둘을 나란히, 없으면 '처음으로' 하나만 가운데.
            StartMode? next = null;
            bool hasBranch = SaveSystem.HasBranch;
            var restartBtn = UIFactory.Btn("Restart", root, Loc.T("ui.ending.restart"), 28,
                                           new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
            UIFactory.Anchor(restartBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             hasBranch ? new Vector2(20f, -280f) : new Vector2(-160f, -280f),
                             hasBranch ? new Vector2(340f, -210f) : new Vector2(160f, -210f));
            restartBtn.onClick.AddListener(() => next = StartMode.NewGame);

            if (hasBranch)
            {
                var branchBtn = UIFactory.Btn("Branch", root, Loc.T("ui.ending.to_branch"), 28,
                                              new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
                UIFactory.Anchor(branchBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(-340f, -280f), new Vector2(-20f, -210f));
                branchBtn.onClick.AddListener(() => next = StartMode.Branch);
            }

            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                group.alpha = Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }
            group.alpha = 1f;

            while (next == null) yield return null;

            // '처음으로' 는 시작 화면으로, '분기점으로' 는 시작 화면을 건너뛰고 바로 분기점에서.
            GameBootstrap.Restart(next == StartMode.Branch ? StartMode.Branch : (StartMode?)null);
            while (true) yield return null;
        }

        /// <summary>"본 엔딩 1 / 3" 과 엔딩 이름 줄. 방금 본 엔딩은 금색, 못 본 엔딩은 ???.</summary>
        static string Collection(string justSeen)
        {
            var endings = Game.Db.Endings;
            var names = new System.Text.StringBuilder();
            foreach (var e in endings)
            {
                if (names.Length > 0) names.Append("     ");
                if (e.Id == justSeen) names.Append($"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Accent)}>{e.DisplayName}</color>");
                else names.Append(SaveSystem.IsEndingSeen(e.Id) ? e.DisplayName : "???");
            }
            return $"{Loc.T("ui.ending.collection", SaveSystem.EndingsSeenCount, endings.Count)}\n{names}";
        }
    }
}
