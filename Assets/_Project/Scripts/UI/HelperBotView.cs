using System.Collections;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 박사 옆에 떠 있는 도우미 로봇.
    ///
    /// 막혔다고 판단되면 안테나가 반짝일 뿐, 먼저 답을 말하지는 않습니다.
    /// 물어보는 건 플레이어 쪽이어야 합니다 — 묻지도 않았는데 알려 주면
    /// 퍼즐을 푼 게 아니라 설명을 들은 게 됩니다.
    ///
    /// 그림은 Resources/Art/Bot 을 먼저 봅니다. 없으면 도형으로 그립니다.
    /// 포트레이트가 부위 PNG 없을 때 합본으로 돌아가는 것과 같은 구조입니다.
    ///
    ///   Art/Bot/bot_body          몸통 (표정 없음)
    ///   Art/Bot/bot_face_{표정}   얼굴만. normal happy curious hint sleepy panic
    /// </summary>
    public class HelperBotView : MonoBehaviour
    {
        public const string ArtFolder = "Art/Bot";

        const float BaseY = 384f;       // 대화창(340) 위
        const float BobPeriod = 2.6f;   // 부유 유닛으로 떠 있는 느낌
        const float BobAmount = 9f;
        const float BubbleSeconds = 8f; // 힌트를 띄워 두는 시간

        RectTransform _root, _bot, _bubble;
        Image _body, _face, _antenna;
        Text _bubbleText;
        Button _button;

        string _faceKey;
        float _bobPhase;
        bool _stuck;
        Coroutine _bubbleRoutine;

        /// <summary>로봇을 눌렀을 때. GameBootstrap 이 여기에 힌트 요청을 답니다.</summary>
        public event System.Action AskRequested;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public static HelperBotView Create(RectTransform screen)
        {
            var go = new GameObject("HelperBot", typeof(RectTransform));
            go.transform.SetParent(screen, false);
            var view = go.AddComponent<HelperBotView>();
            view.BuildTree((RectTransform)go.transform);
            return view;
        }

        void BuildTree(RectTransform root)
        {
            _root = root;
            UIFactory.Anchor(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ── 로봇 본체 — 오른쪽 아래, 대화창 위
            _button = UIFactory.Btn("Bot", _root, string.Empty, 1, new Color(0f, 0f, 0f, 0f), UIFactory.Ink);
            _bot = _button.GetComponent<RectTransform>();
            _bot.anchorMin = _bot.anchorMax = new Vector2(1f, 0f);
            _bot.pivot = new Vector2(1f, 0f);
            _bot.anchoredPosition = new Vector2(-46f, BaseY);
            _bot.sizeDelta = new Vector2(168f, 200f);

            // 캡션이 비어 있어도 글자 자식은 붙습니다. 로봇에는 필요 없으니 끕니다.
            var caption = _button.GetComponentInChildren<Text>();
            if (caption != null) caption.gameObject.SetActive(false);

            BuildBody();
            _button.onClick.AddListener(OnClicked);

            // ── 말풍선 — 로봇 왼쪽 위
            var bubbleBox = UIFactory.Box("Bubble", _root, new Color(0.10f, 0.11f, 0.15f, 0.96f));
            _bubble = bubbleBox.rectTransform;
            _bubble.anchorMin = _bubble.anchorMax = new Vector2(1f, 0f);
            _bubble.pivot = new Vector2(1f, 0f);
            _bubble.anchoredPosition = new Vector2(-230f, 600f);
            _bubble.sizeDelta = new Vector2(660f, 150f);

            _bubbleText = UIFactory.Label("Text", _bubble, string.Empty, 28,
                                          TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(_bubbleText.rectTransform, Vector2.zero, Vector2.one,
                             new Vector2(26f, 22f), new Vector2(-26f, -22f));

            _bubble.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 몸통·얼굴·안테나를 겹쳐 둡니다. PNG 가 있으면 그림이, 없으면 도형이 들어갑니다.
        /// 어느 쪽이든 자리와 크기가 같으므로, 아트가 들어와도 이 코드는 안 바뀝니다.
        /// </summary>
        void BuildBody()
        {
            var bodySprite = Resources.Load<Sprite>(ArtFolder + "/bot_body");

            // 안테나 — 상태 표시등. 막혔을 때만 켜집니다.
            _antenna = UIFactory.Box("Antenna", _bot, new Color(0.45f, 0.68f, 0.95f, 0f));
            UIFactory.Anchor(_antenna.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                             new Vector2(-34f, -30f), new Vector2(34f, 6f));
            _antenna.raycastTarget = false;

            _body = UIFactory.Box("Body", _bot,
                                  bodySprite != null ? Color.white : new Color(0.86f, 0.86f, 0.84f));
            UIFactory.Anchor(_body.rectTransform, Vector2.zero, Vector2.one,
                             Vector2.zero, new Vector2(0f, -34f));
            _body.sprite = bodySprite;
            _body.preserveAspect = true;
            _body.raycastTarget = false;

            _face = UIFactory.Box("Face", _bot, new Color(0.13f, 0.14f, 0.18f));
            UIFactory.Anchor(_face.rectTransform, Vector2.zero, Vector2.one,
                             Vector2.zero, new Vector2(0f, -34f));
            _face.preserveAspect = true;
            _face.raycastTarget = false;

            ApplyFace("normal");
        }

        // ── 바깥에서 부르는 것 ────────────────────────────────────────────────

        public void Show()
        {
            if (_root != null) _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            HideBubble();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>막힌 것 같다는 표시. 안테나가 켜지고 얼굴이 '힌트' 로 바뀝니다.</summary>
        public void SetStuck(bool stuck)
        {
            _stuck = stuck;

            // 말풍선이 떠 있는 동안에는 웃는 얼굴을 유지합니다. 풍선이 닫힐 때
            // 다시 맞춰집니다.
            if (_bubbleRoutine == null) ApplyFace(stuck ? "hint" : "normal");
        }

        /// <summary>힌트를 말풍선에 띄웁니다.</summary>
        public void Say(string message)
        {
            if (_bubble == null || string.IsNullOrEmpty(message)) return;

            _bubbleText.text = message;

            // 글자 수에 맞춰 풍선 높이를 잡습니다. 두 줄짜리 힌트에 네 줄 높이가
            // 붙어 있으면 빈 공간이 그대로 보입니다.
            float textHeight = _bubbleText.preferredHeight;
            _bubble.sizeDelta = new Vector2(_bubble.sizeDelta.x,
                                            Mathf.Clamp(textHeight + 46f, 96f, 340f));

            _bubble.gameObject.SetActive(true);
            ApplyFace("happy");

            if (_bubbleRoutine != null) StopCoroutine(_bubbleRoutine);
            _bubbleRoutine = StartCoroutine(HideBubbleAfter(BubbleSeconds));
        }

        public void HideBubble()
        {
            if (_bubbleRoutine != null) { StopCoroutine(_bubbleRoutine); _bubbleRoutine = null; }
            if (_bubble != null) _bubble.gameObject.SetActive(false);
            ApplyFace(_stuck ? "hint" : "normal");
        }

        IEnumerator HideBubbleAfter(float seconds)
        {
            // 실시간으로 셉니다. 퍼즐 중에 Time.timeScale 을 건드리게 되더라도
            // 풍선이 화면에 붙박이로 남지 않습니다.
            yield return new WaitForSecondsRealtime(seconds);

            _bubble.gameObject.SetActive(false);
            _bubbleRoutine = null;
            ApplyFace(_stuck ? "hint" : "normal");
        }

        void OnClicked()
        {
            // 이미 말하고 있어도 한 번 더 누르면 다음 단계 힌트로 넘어갑니다.
            AskRequested?.Invoke();
        }

        // ── 움직임 ────────────────────────────────────────────────────────────

        void Update()
        {
            if (_bot == null) return;

            _bobPhase += Time.unscaledDeltaTime;

            // 위아래로 천천히. 떠 있다는 걸 이것만으로 보여 줍니다.
            float bob = Mathf.Sin(_bobPhase * Mathf.PI * 2f / BobPeriod) * BobAmount;
            _bot.anchoredPosition = new Vector2(_bot.anchoredPosition.x, BaseY + bob);

            if (_antenna != null)
            {
                // 막혔을 때만 반짝입니다. 평소에도 깜빡이면 눈이 계속 그쪽으로 갑니다.
                float alpha = _stuck ? 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(_bobPhase * 3.4f)) : 0f;
                var c = _antenna.color;
                _antenna.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        void ApplyFace(string key)
        {
            if (_face == null || key == _faceKey) return;
            _faceKey = key;

            var sprite = Resources.Load<Sprite>(ArtFolder + "/bot_face_" + key);
            _face.sprite = sprite;

            if (sprite != null) { _face.color = Color.white; return; }

            // PNG 가 없으면 얼굴판 색만 바꿔 기분을 표시합니다. 좋진 않지만
            // '아무것도 안 보임' 보다는 낫고, 아트가 들어오면 저절로 사라집니다.
            switch (key)
            {
                case "hint":   _face.color = new Color(0.30f, 0.46f, 0.70f); break;
                case "happy":  _face.color = new Color(0.22f, 0.40f, 0.34f); break;
                case "curious":_face.color = new Color(0.24f, 0.26f, 0.38f); break;
                case "sleepy": _face.color = new Color(0.18f, 0.18f, 0.22f); break;
                case "panic":  _face.color = new Color(0.44f, 0.30f, 0.30f); break;
                default:       _face.color = new Color(0.13f, 0.14f, 0.18f); break;
            }
        }
    }
}
