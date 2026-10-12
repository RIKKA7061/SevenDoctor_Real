using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 증거 그림 크게 보기. 노트나 추리 퍼즐에서 그림을 누르면 화면 가운데에 크게 뜹니다.
    ///
    /// 날짜·시각 같은 작은 단서는 설명에 적지 않고 그림 안에만 넣어 두기 때문에,
    /// 여기서 들여다보는 것 자체가 추리입니다. 그래서 한 번 더 확대할 수 있게 합니다:
    ///   · 휠           — 커서 쪽으로 확대/축소
    ///   · 그림 클릭     — 그 자리를 2.5배로, 한 번 더 누르면 원래대로
    ///   · 드래그        — 확대한 그림 옮기기
    ///   · 바깥 클릭 / Esc / 닫기 — 닫기
    /// 누가 열었든(노트·퍼즐) 그 위에 겹쳐 뜨고, 닫으면 원래 화면으로 돌아갑니다.
    /// </summary>
    public class EvidenceZoomView : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        const float ViewSize = 900f;     // 1920x1080 기준. 1024px 그림이 거의 1:1 로 보입니다
        const float MaxZoom = 4f;
        const float ClickZoom = 2.5f;
        const float WheelStep = 1.18f;

        static EvidenceZoomView _open;

        RectTransform _root, _viewport, _content;
        float _zoom = 1f;
        bool _dragging;

        public static bool IsOpen => _open != null;

        /// <summary>증거 하나를 크게 띄웁니다. 그림이 없으면 아무것도 안 합니다.</summary>
        public static void Open(string evidenceId)
        {
            if (Game.UI == null || Game.Db == null ||
                !Game.Db.Evidences.TryGetValue(evidenceId, out var row)) return;

            var sprite = LoadImage(row.IconKey);
            if (sprite == null) return;

            Close();

            var root = UIFactory.Rect("EvidenceZoom", Game.UI.Screen);
            UIFactory.Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();   // 노트·퍼즐보다 위

            // 바깥을 누르면 닫힙니다.
            var scrim = UIFactory.Btn("Scrim", root, string.Empty, 1, new Color(0f, 0f, 0f, 0.86f), UIFactory.Ink);
            UIFactory.Anchor(scrim.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var scrimCaption = scrim.GetComponentInChildren<Text>();
            if (scrimCaption != null) scrimCaption.gameObject.SetActive(false);
            scrim.onClick.AddListener(Close);

            // 그림 — 가운데에서 왼쪽으로 조금 비켜, 오른쪽에 설명을 둡니다.
            var frame = UIFactory.Box("Frame", root, new Color(0.08f, 0.09f, 0.12f, 1f));
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(ViewSize, ViewSize);
            frt.anchoredPosition = new Vector2(-250f, 0f);
            frame.raycastTarget = true;
            frame.gameObject.AddComponent<RectMask2D>();

            var content = UIFactory.Box("Image", frt, Color.white);
            UIFactory.Anchor(content.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            content.sprite = sprite;
            content.preserveAspect = true;
            content.raycastTarget = false;

            // 설명
            var side = UIFactory.Rect("Side", root);
            side.anchorMin = side.anchorMax = new Vector2(0.5f, 0.5f);
            side.pivot = new Vector2(0f, 0.5f);
            side.sizeDelta = new Vector2(480f, ViewSize);
            side.anchoredPosition = new Vector2(ViewSize * 0.5f - 250f + 40f, 0f);

            var name = UIFactory.Label("Name", side, row.DisplayName, 38, TextAnchor.UpperLeft, UIFactory.Accent);
            UIFactory.Anchor(name.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                             new Vector2(0, -120), new Vector2(0, 0));

            string body = string.IsNullOrEmpty(row.Category)
                ? row.Description
                : $"<color=#8C93A5>[{Loc.Category(row.Category)}]</color>\n\n{row.Description}";
            var desc = UIFactory.Label("Body", side, body, 26, TextAnchor.UpperLeft, UIFactory.Ink);
            UIFactory.Anchor(desc.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                             new Vector2(0, 200), new Vector2(0, -130));
            desc.lineSpacing = 1.3f;

            var help = UIFactory.Label("Help", side, Loc.T("ui.zoom.help"), 22, TextAnchor.LowerLeft, UIFactory.InkDim);
            UIFactory.Anchor(help.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
                             new Vector2(0, 96), new Vector2(0, 190));

            var close = UIFactory.Btn("Close", side, Loc.T("ui.notebook.close"), 26,
                                      new Color(0.16f, 0.17f, 0.23f), UIFactory.Ink);
            UIFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0),
                             new Vector2(0, 0), new Vector2(200, 72));
            close.onClick.AddListener(Close);

            var view = frame.gameObject.AddComponent<EvidenceZoomView>();
            view._root = root;
            view._viewport = frt;
            view._content = content.rectTransform;
            _open = view;
        }

        public static void Close()
        {
            if (_open == null) return;
            var root = _open._root;
            _open = null;
            if (root != null) Destroy(root.gameObject);
        }

        /// <summary>노트·퍼즐 카드가 쓰는 것과 같은 규칙. Resources/Art/Evidence/{아이콘키}.</summary>
        public static Sprite LoadImage(string iconKey)
        {
            return string.IsNullOrEmpty(iconKey) ? null : Resources.Load<Sprite>($"Art/Evidence/{iconKey}");
        }

        // ── 입력 ──────────────────────────────────────────────────────────────

        void Update()
        {
            if (WasCloseKeyPressed()) Close();
        }

        public void OnScroll(PointerEventData e)
        {
            // 입력 모듈마다 한 칸의 값이 1 이기도 120 이기도 해서 방향만 봅니다.
            float dir = Mathf.Sign(e.scrollDelta.y);
            if (dir == 0f) return;
            ZoomAt(e, _zoom * (dir > 0 ? WheelStep : 1f / WheelStep));
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_dragging) return;
            ZoomAt(e, _zoom < 1.5f ? ClickZoom : 1f);
        }

        public void OnBeginDrag(PointerEventData e) => _dragging = true;

        public void OnDrag(PointerEventData e)
        {
            if (_zoom <= 1f) return;
            _content.anchoredPosition += e.delta / CanvasScale();
            Clamp();
        }

        public void OnEndDrag(PointerEventData e) => _dragging = false;

        /// <summary>커서 아래의 점이 확대 뒤에도 커서 아래에 남도록 옮깁니다.</summary>
        void ZoomAt(PointerEventData e, float target)
        {
            target = Mathf.Clamp(target, 1f, MaxZoom);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _viewport, e.position, e.pressEventCamera, out var cursor))
                cursor = Vector2.zero;

            var pos = _content.anchoredPosition;
            var under = (cursor - pos) / _zoom;
            _zoom = target;
            _content.localScale = new Vector3(_zoom, _zoom, 1f);
            _content.anchoredPosition = cursor - under * _zoom;
            Clamp();
        }

        /// <summary>확대한 그림이 테두리 밖으로 밀려나 빈 곳이 보이지 않게 합니다.</summary>
        void Clamp()
        {
            var limit = _viewport.rect.size * (_zoom - 1f) * 0.5f;
            var p = _content.anchoredPosition;
            _content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -limit.x, limit.x),
                                                    Mathf.Clamp(p.y, -limit.y, limit.y));
        }

        float CanvasScale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        static bool WasCloseKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
