using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.Room
{
    /// <summary>
    /// 방 전환과 핫스팟 배치를 담당합니다.
    ///
    /// 씬은 늘리지 않습니다 — 방 8개를 각각 씬으로 만들면 상태 관리가 지옥이 되고,
    /// 씬 로드마다 매니저를 다시 찾아야 합니다. 배경과 핫스팟만 갈아끼웁니다.
    ///
    /// 핫스팟 좌표는 시트의 x/y/w/h(정규화 0~1)를 씁니다.
    /// 비어 있으면 화면 하단에 버튼 목록으로 자동 배치합니다 —
    /// 배경 아트가 아직 없어도 기획자가 동선을 바로 확인할 수 있게 하기 위해서입니다.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        public string CurrentRoomId { get; private set; }

        readonly HashSet<string> _consumed = new HashSet<string>();

        static readonly Color[] PlaceholderTints =
        {
            new Color(0.16f, 0.17f, 0.22f), new Color(0.19f, 0.16f, 0.20f),
            new Color(0.14f, 0.19f, 0.21f), new Color(0.21f, 0.18f, 0.15f),
            new Color(0.17f, 0.15f, 0.23f), new Color(0.15f, 0.21f, 0.18f),
            new Color(0.22f, 0.16f, 0.16f), new Color(0.12f, 0.13f, 0.20f),
        };

        public void Enter(string roomId)
        {
            if (Game.Db == null) return;

            if (!Game.Db.Rooms.TryGetValue(roomId, out var room))
            {
                Debug.LogError($"[Room] '{roomId}' 방을 찾을 수 없습니다.");
                return;
            }

            if (!Game.Flags.Check(room.RequiredFlag))
            {
                Game.UI.Toast(Loc.T("ui.room.locked"));
                return;
            }

            CurrentRoomId = roomId;

            // 같은 곡이면 AudioManager 가 알아서 넘깁니다 — 방을 옮길 때마다 끊기지 않습니다.
            Game.Audio?.PlayBgm(room.Bgm);

            ApplyBackground(room);
            Game.UI.SetRoomLabel(room.DisplayName);
            RebuildHotspots();

            Game.State = GameState.Exploring;
            Debug.Log($"[Room] 입장: {room.DisplayName} ({roomId})");
        }

        void ApplyBackground(RoomRow room)
        {
            // Resources/Art/Backgrounds/{배경키}.png 가 있으면 그걸 깔고,
            // 없으면 방마다 다른 단색 플레이스홀더를 깝니다. 아트가 늦어도 게임은 돌아갑니다.
            var sprite = Resources.Load<Sprite>($"Art/Backgrounds/{room.BackgroundKey}");
            if (sprite != null)
            {
                Game.UI.BackgroundImage.sprite = sprite;
                Game.UI.BackgroundImage.color = Color.white;
                Game.UI.BackgroundImage.type = Image.Type.Simple;
                // 화면을 꽉 채우는 게 우선이라 비율은 고정하지 않습니다.
                // (레터박스가 생기면 그 틈으로 UI 바탕이 비쳐서 더 나쁩니다)
                Game.UI.BackgroundImage.preserveAspect = false;
            }
            else
            {
                Game.UI.BackgroundImage.sprite = null;
                Game.UI.BackgroundImage.color = TintFor(room.Id);
            }
        }

        static Color TintFor(string roomId)
        {
            int hash = 0;
            foreach (char c in roomId) hash = hash * 31 + c;
            return PlaceholderTints[Mathf.Abs(hash) % PlaceholderTints.Length];
        }

        /// <summary>플래그가 바뀌면 다시 불러야 하는 함수. 새로 열린 핫스팟이 반영됩니다.</summary>
        public void RebuildHotspots()
        {
            var layer = Game.UI.HotspotLayer;
            UIFactory.Clear(layer);

            var visible = new List<HotspotRow>();
            foreach (var h in Game.Db.GetHotspots(CurrentRoomId))
            {
                if (!Game.Flags.Check(h.RequiredFlag)) continue;
                if (h.Once && _consumed.Contains(h.Id)) continue;
                visible.Add(h);
            }

            // 자동 배치가 필요한 것만 따로 셈 (좌표가 있는 건 그 자리에 그대로 둡니다)
            int autoIndex = 0, autoTotal = 0;
            foreach (var h in visible) if (!h.HasRect) autoTotal++;

            foreach (var h in visible)
            {
                var button = CreateHotspotButton(h, ref autoIndex, autoTotal);
                var captured = h;
                button.onClick.AddListener(() => OnHotspotClicked(captured));
            }
        }

        Button CreateHotspotButton(HotspotRow h, ref int autoIndex, int autoTotal)
        {
            bool placed = h.HasRect;

            // 좌표가 있으면 투명한 클릭 영역, 없으면 라벨이 보이는 버튼
            var button = UIFactory.Btn(
                $"HS_{h.Id}",
                Game.UI.HotspotLayer,
                placed ? string.Empty : Caption(h),
                24,
                placed ? UIFactory.Hotspot : new Color(0.11f, 0.12f, 0.17f, 0.95f),
                UIFactory.Ink);

            var rt = button.GetComponent<RectTransform>();

            if (placed)
            {
                // 시트 좌표는 좌상단 기준이 직관적이므로 y를 뒤집어 유니티(좌하단 기준)에 맞춥니다.
                rt.anchorMin = new Vector2(h.X, 1f - (h.Y + h.H));
                rt.anchorMax = new Vector2(h.X + h.W, 1f - h.Y);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                const float width = 300f, height = 76f, gap = 16f;
                int columns = Mathf.Max(1, Mathf.FloorToInt(1820f / (width + gap)));
                int row = autoIndex / columns;
                int col = autoIndex % columns;
                int inRow = Mathf.Min(columns, autoTotal - row * columns);

                float totalWidth = inRow * width + (inRow - 1) * gap;
                float startX = -totalWidth * 0.5f;
                float x = startX + col * (width + gap);
                float y = 372f + row * (height + gap); // 대화창(340) 위

                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(width, height);

                autoIndex++;
            }

            return button;
        }

        static string Caption(HotspotRow h)
        {
            switch (h.Type)
            {
                case "move": return $"▶  {h.DisplayName}";
                case "talk": return $"◆  {h.DisplayName}";
                case "get":  return $"●  {h.DisplayName}";
                default:     return $"·  {h.DisplayName}";
            }
        }

        void OnHotspotClicked(HotspotRow h)
        {
            if (!Game.CanInteract) return;

            if (h.Once) _consumed.Add(h.Id);

            if (h.Type == "move")
            {
                Enter(h.Target);
                return;
            }

            if (string.IsNullOrEmpty(h.Target))
            {
                Game.UI.Toast(Loc.T("ui.room.nothing"));
                RebuildHotspots();
                return;
            }

            Game.Dialogue.Play(h.Target, RebuildHotspots);
        }

        public void RestoreConsumed(IEnumerable<string> ids)
        {
            _consumed.Clear();
            if (ids == null) return;
            foreach (var id in ids) _consumed.Add(id);
        }

        public IEnumerable<string> ConsumedHotspots => _consumed;
    }
}
