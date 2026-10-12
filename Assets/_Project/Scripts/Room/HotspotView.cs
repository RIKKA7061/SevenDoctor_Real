using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.Room
{
    /// <summary>
    /// 화면 속 물건을 직접 누르는 핫스팟. 시트에 좌표(x,y,w,h)가 있는 핫스팟에 붙습니다.
    ///
    /// 누르면 곧바로 이어집니다. 마우스를 올려도 이름표나 강조 상자는 띄우지 않습니다 —
    /// 화면 속 그림이 곧 버튼이고, 그 위에 설명을 덧붙이지 않습니다.
    ///   · 그림이 있으면(시트 '이미지' 칸) 그 자리에 세워 둡니다 — 물건·인물이 눈에 보입니다
    ///   · 그림이 없으면 배경의 그 영역이 클릭 영역이고, 가운데에 작은 점이 숨 쉬듯 깜빡입니다.
    ///     배경만으로는 누를 수 있는 곳인지 알 길이 없어서 이것 하나는 남겨 둡니다.
    /// 대사가 흐르는 동안에는 점을 숨깁니다. 누를 수 없는 걸 누르라고 하면 안 됩니다.
    /// </summary>
    public class HotspotView : MonoBehaviour
    {
        const string PropFolder = "Art/Props";
        const string MarkerKey = "prop_marker";

        static readonly Color ShadowTint = new Color(0.04f, 0.05f, 0.08f, 0.94f);
        static readonly HashSet<string> WarnedImages = new HashSet<string>();

        Image _marker;          // 그림이 없을 때의 점
        float _phase;

        public static void Attach(Button button, HotspotRow h)
        {
            var view = button.gameObject.AddComponent<HotspotView>();
            view.Build(button, h);
        }

        void Build(Button button, HotspotRow h)
        {
            // 버튼 자신은 투명한 클릭 영역입니다. 눌림·올림 색 변화도 끕니다.
            if (button.targetGraphic is Image area) area.color = new Color(1f, 1f, 1f, 0f);
            button.transition = Selectable.Transition.None;

            var sprite = LoadPicture(h.Image, out var tint);
            if (sprite != null)
            {
                var picture = UIFactory.Box("Picture", transform, tint);
                UIFactory.Anchor(picture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                picture.sprite = sprite;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
            }
            else
            {
                var dot = Resources.Load<Sprite>($"{PropFolder}/{MarkerKey}");
                if (dot != null)
                {
                    _marker = UIFactory.Box("Marker", transform, Color.white);
                    var mrt = _marker.rectTransform;
                    mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.5f);
                    mrt.sizeDelta = new Vector2(44f, 44f);
                    mrt.anchoredPosition = Vector2.zero;
                    _marker.sprite = dot;
                    _marker.raycastTarget = false;
                }
            }

            // 방마다 점이 같은 박자로 깜빡이면 기계 같아 보입니다.
            _phase = (h.Id.GetHashCode() & 0xFF) / 255f * 6.28f;
        }

        /// <summary>
        /// 시트 '이미지' 칸 해석. 인물은 대화창 초상화와 같은 규칙으로 찾아야
        /// 방에 서 있는 사람과 말하는 사람이 다르게 보이지 않습니다.
        /// </summary>
        static Sprite LoadPicture(string image, out Color tint)
        {
            tint = Color.white;
            if (string.IsNullOrWhiteSpace(image)) return null;
            image = image.Trim();

            string characterId = null;
            if (image.StartsWith("chr:")) characterId = image.Substring(4).Trim();
            else if (image.StartsWith("shadow:")) { characterId = image.Substring(7).Trim(); tint = ShadowTint; }

            Sprite sprite = null;
            if (characterId != null)
            {
                if (Game.Db != null && Game.Db.Characters.TryGetValue(characterId, out var c))
                    sprite = PortraitView.ResolveBody(c.PortraitKey, out _, out _);
            }
            else
            {
                sprite = Resources.Load<Sprite>($"{PropFolder}/{image}");
            }

            if (sprite == null && WarnedImages.Add(image))
                Debug.LogWarning($"[Hotspot] 그림 '{image}' 을 찾지 못해 클릭 영역만 둡니다.");
            return sprite;
        }

        void Update()
        {
            if (_marker == null) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f + _phase);
            _marker.color = new Color(1f, 1f, 1f, Game.CanInteract ? 0.30f + 0.40f * pulse : 0f);
        }
    }
}
