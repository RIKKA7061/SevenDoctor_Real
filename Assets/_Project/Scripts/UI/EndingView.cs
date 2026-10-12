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
    /// </summary>
    public static class EndingView
    {
        const float FadeSeconds = 1.6f;

        public static IEnumerator Play(string endingId)
        {
            var ui = Game.UI;
            var ending = Game.Db.FindEnding(endingId);
            string title = ending != null ? ending.DisplayName : endingId;

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

            bool restart = false;
            var btn = UIFactory.Btn("Restart", root, Loc.T("ui.ending.restart"), 28,
                                    new Color(0.16f, 0.15f, 0.11f), UIFactory.Accent);
            UIFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-160f, -220f), new Vector2(160f, -150f));
            btn.onClick.AddListener(() => restart = true);

            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                group.alpha = Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }
            group.alpha = 1f;

            while (!restart) yield return null;

            GameBootstrap.Restart();
            while (true) yield return null;
        }
    }
}
