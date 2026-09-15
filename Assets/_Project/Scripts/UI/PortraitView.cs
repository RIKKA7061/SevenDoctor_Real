using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 화자 포트레이트. 몸 스프라이트 위에 표정 스프라이트를 겹칩니다.
    ///
    /// 몸과 표정을 나눈 이유는 리소스 절약이기도 하지만 설정 그 자체이기도 합니다 —
    /// 일곱 박사는 전부 같은 얼굴이라, 표정 한 세트를 전원이 공유하는 게 맞습니다.
    /// (교만만 얼굴이 어둠에 묻힌 몸 스프라이트를 씁니다)
    ///
    /// 리소스 경로:
    ///   Resources/Art/Characters/{Characters 탭 '포트레이트키'}.png
    ///   Resources/Art/Faces/{Dialogues 탭 '표정'}.png
    /// 파일이 없으면 조용히 숨깁니다. 아트가 아직 안 들어온 인물이 있어도 게임은 안 멈춥니다.
    /// </summary>
    public class PortraitView
    {
        public RectTransform Root { get; private set; }

        Image _body;
        Image _face;
        string _characterKey;
        string _faceKey;

        // 1920x1080 기준. 세로 720이면 화면의 약 2/3 — 비주얼 노벨의 통상 비율입니다.
        // 상단바(72)에 머리가 닿지 않고, 아래는 대화창(340)에 살짝 가려집니다.
        const float Width = 420f;
        const float Height = 720f;       // 700:1200 비율 유지
        const float BottomOffset = 258f;
        const float AnchorX = 0.78f;

        PortraitView() { }

        /// <summary>프리팹이든 코드 생성이든 상관없이 붙습니다. 없으면 만들고, 있으면 그걸 씁니다.</summary>
        public static PortraitView CreateOrBind(RectTransform portraitLayer)
        {
            if (portraitLayer == null) return null;

            var v = new PortraitView { Root = portraitLayer };

            v._body = portraitLayer.Find("Body")?.GetComponent<Image>();
            v._face = portraitLayer.Find("Face")?.GetComponent<Image>();

            if (v._body == null) v._body = MakeSlot("Body", portraitLayer);
            if (v._face == null) v._face = MakeSlot("Face", portraitLayer);

            v.Hide();
            return v;
        }

        static Image MakeSlot(string name, RectTransform parent)
        {
            var img = UIFactory.Box(name, parent, new Color(1, 1, 1, 1));
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.sprite = null;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(AnchorX, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = new Vector2(0f, BottomOffset);
            return img;
        }

        // ── 동작 ──────────────────────────────────────────────────────────────

        public void Show(string portraitKey, string faceKey)
        {
            if (string.IsNullOrEmpty(portraitKey)) { Hide(); return; }

            if (portraitKey != _characterKey)
            {
                var sprite = Resources.Load<Sprite>($"Art/Characters/{portraitKey}");
                if (sprite == null) { Hide(); return; }

                _body.sprite = sprite;
                _characterKey = portraitKey;
            }

            _body.color = Color.white;
            _body.gameObject.SetActive(true);

            SetFace(faceKey);
        }

        public void SetFace(string faceKey)
        {
            if (_body == null || !_body.gameObject.activeSelf) return;

            if (string.IsNullOrEmpty(faceKey)) faceKey = "normal";
            if (faceKey == _faceKey && _face.sprite != null)
            {
                _face.gameObject.SetActive(true);
                return;
            }

            var sprite = Resources.Load<Sprite>($"Art/Faces/{faceKey}");
            if (sprite == null && faceKey != "normal")
                sprite = Resources.Load<Sprite>("Art/Faces/normal");

            _face.sprite = sprite;
            _faceKey = faceKey;
            _face.color = Color.white;
            _face.gameObject.SetActive(sprite != null);
        }

        public void Hide()
        {
            if (_body != null) _body.gameObject.SetActive(false);
            if (_face != null) _face.gameObject.SetActive(false);
        }
    }
}
