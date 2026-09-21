using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// UI를 프리팹 없이 코드로 만들기 위한 헬퍼.
    ///
    /// 왜 프리팹이 아닌가:
    ///  - 3인 팀이 같은 저장소를 쓰면 씬/프리팹 파일이 가장 자주 충돌하고, 병합이 사실상 불가능합니다.
    ///  - 코드로 만들면 충돌이 나도 텍스트 병합이 되고, 참조 끊김(Missing Reference)이 아예 없습니다.
    ///  - 아트 리소스가 들어오면 여기서 이미지 교체만 하면 됩니다.
    ///
    /// 폰트는 TextMeshPro가 아니라 레거시 Text를 씁니다. TMP는 한글 폰트 에셋을 따로 굽지 않으면
    /// 글자가 안 나오는데, 지금 단계에서 그 작업을 강제하고 싶지 않아서입니다.
    /// (아트 확정 후 TMP로 갈아타는 건 이 파일만 고치면 됩니다.)
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink        = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color InkDim     = new Color(0.68f, 0.71f, 0.78f);
        public static readonly Color Panel      = new Color(0.07f, 0.08f, 0.11f, 0.92f);
        public static readonly Color PanelSolid = new Color(0.10f, 0.11f, 0.15f, 1f);
        public static readonly Color Accent     = new Color(0.85f, 0.72f, 0.42f);
        public static readonly Color Hotspot    = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Scrim      = new Color(0f, 0f, 0f, 0.78f);

        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                // 한글이 나오는 OS 폰트를 우선 시도합니다.
                try
                {
                    _font = UnityEngine.Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans KR",
                                "Apple SD Gothic Neo", "Arial Unicode MS" }, 32);
                }
                catch { _font = null; }

                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { /* 구버전 대응 */ }
                }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch { }
                }
                return _font;
            }
        }

        // ── 기본 요소 ─────────────────────────────────────────────────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0.001f;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size,
                                 TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = text;
            t.alignment = anchor;
            t.color = color ?? Ink;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button Btn(string name, Transform parent, string caption, int size,
                                 Color? bg = null, Color? fg = null)
        {
            var img = Box(name, parent, bg ?? PanelSolid);
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            // 캡션이 비어 있어도 글자 자식은 답니다. 예전엔 건너뛰었는데, 그러면
            // 나중에 채워 넣으려고 GetComponentInChildren<Text>() 로 잡는 쪽이
            // 전부 null 을 받아 갑니다 — 버튼을 빈 채로 만들었다가 텍스트만
            // 나중에 넣는 게 흔한 패턴이라 사고가 반복됩니다.
            {
                var label = Label("Label", img.transform, caption ?? string.Empty, size,
                                  TextAnchor.MiddleCenter, fg ?? Ink);
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(16, 8);
                lrt.offsetMax = new Vector2(-16, -8);
            }

            return btn;
        }

        /// <summary>
        /// 버튼의 글자를 가져옵니다. 없으면 만들어서 답니다.
        ///
        /// 예전에 구운 UI 프리팹에는 캡션 없이 만든 버튼에 글자 자식이 없습니다.
        /// 프리팹을 다시 굽지 않아도 돌아가도록, 붙일 때 이걸로 메웁니다.
        /// </summary>
        public static Text EnsureLabel(Button btn, int size, Color? fg = null)
        {
            if (btn == null) return null;

            var existing = btn.GetComponentInChildren<Text>(true);
            if (existing != null) return existing;

            var label = Label("Label", btn.transform, string.Empty, size, TextAnchor.MiddleCenter, fg ?? Ink);
            label.rectTransform.offsetMin = new Vector2(16, 8);
            label.rectTransform.offsetMax = new Vector2(-16, -8);
            return label;
        }

        /// <summary>앵커/피벗/오프셋을 한 번에 지정합니다.</summary>
        public static RectTransform Anchor(RectTransform rt, Vector2 min, Vector2 max,
                                           Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static VerticalLayoutGroup VLayout(RectTransform rt, int spacing, RectOffset padding,
                                                  TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;   // LayoutElement.preferredHeight 를 존중하게 함
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static GridLayoutGroup Grid(RectTransform rt, Vector2 cell, Vector2 spacing,
                                           int columns, RectOffset padding = null)
        {
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset(0, 0, 0, 0);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Mathf.Max(1, columns);
            g.childAlignment = TextAnchor.UpperCenter;
            return g;
        }

        public static LayoutElement Height(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            return le;
        }


        // ── 프리팹에서 되살릴 때 쓰는 헬퍼 ─────────────────────────────────────

        /// <summary>경로로 자식을 찾아 컴포넌트를 돌려줍니다. 못 찾으면 null 과 함께 경고를 남깁니다.</summary>
        public static T Find<T>(Transform root, string path) where T : Component
        {
            if (root == null) return null;

            var child = root.Find(path);
            if (child == null)
            {
                Debug.LogWarning($"[UIFactory] '{root.name}' 아래에서 '{path}' 를 찾지 못했습니다. UI 프리팹을 다시 만들어야 할 수 있습니다.");
                return null;
            }

            var component = child.GetComponent<T>();
            if (component == null)
                Debug.LogWarning($"[UIFactory] '{path}' 에 {typeof(T).Name} 컴포넌트가 없습니다.");
            return component;
        }

        public static RectTransform FindRect(Transform root, string path)
        {
            var child = root != null ? root.Find(path) : null;
            if (child == null)
                Debug.LogWarning($"[UIFactory] '{root?.name}' 아래에서 '{path}' 를 찾지 못했습니다.");
            return child as RectTransform;
        }

        /// <summary>
        /// 프리팹에 저장된 Text 들의 폰트를 다시 붙입니다.
        ///
        /// OS 폰트(맑은 고딕)는 에셋이 아니라 런타임에 만들어지는 객체라서 프리팹에 직렬화되지 않습니다.
        /// 이걸 안 하면 프리팹에서 되살린 UI 의 글자가 통째로 안 보입니다.
        /// </summary>
        public static void ReapplyFonts(Transform root)
        {
            if (root == null) return;

            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var t in texts)
                if (t.font == null) t.font = Font;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
