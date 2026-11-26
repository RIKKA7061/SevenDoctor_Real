using System.Collections.Generic;
using SevenDoctors.Core;
using SevenDoctors.Data;
using SevenDoctors.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.Room
{
    /// <summary>
    /// 방 배경 위에 얹는 빛 — 전구가 숨 쉬듯 밝아졌다 어두워지고, 그 빛이 벽·바닥에 번지고,
    /// 타임머신 테두리 조명이 돌아가고, 크리스탈 둘레로 마법 가루가 떠오릅니다.
    ///
    /// 배경 그림은 그대로 두고 그 위에 '더하는' 빛(SevenDoctors/UI Additive 셰이더)만 겹칩니다.
    /// 그래서 빛이 닿는 자리만 밝아지고 그림이 가려지지 않습니다.
    ///
    /// 어디에 어떤 빛을 둘지는 Resources/Data/RoomLights.csv 에 적습니다 (코드 안 고치고 자리·색·세기 조정).
    ///   light_id, room_id, 종류, x, y, w, h, 색, 세기, 속도, 연결, 개수, 비고
    ///   x·y·w·h 는 핫스팟과 같은 약속 — 화면 비율 0~1, 왼쪽 위 기준. x·y 는 빛의 가운데.
    ///
    ///   glow    전구 후광. 천천히 숨 쉬듯 밝기가 바뀜 (속도 = 1초에 몇 번)
    ///   flicker glow 와 같은데 오래된 전구처럼 가끔 깜빡임
    ///   core    전구 알 자체의 진한 빛. 보통 '연결' 로 후광을 따라감
    ///   spill   벽·바닥에 번진 빛(빛 반사). '연결' 한 빛이 깜빡이면 같이 깜빡임
    ///   beam    갓 아래로 떨어지는 빛줄기. x·y = 광원, w = 바닥 폭, h = 길이
    ///   ring    테두리 조명. 타원 앞쪽 반 바퀴에 점 '개수' 만큼, 밝은 부분이 돌아감 (w·h = 반지름, 속도 음수면 반대로)
    ///   sparkle 떠오르는 반짝임 '개수' 만큼 (w·h = 떠오르는 영역)
    ///
    ///   색 = #RRGGBB, 또는 #RRGGBB>#RRGGBB 로 두 색을 천천히 오감 (크리스탈처럼)
    ///   연결 = 다른 light_id. 그 빛의 밝기 변화를 그대로 따라감
    ///
    /// Game 씬에서만 돕니다 — RoomController 가 방에 들어갈 때 Show(방 ID) 를 부릅니다.
    /// </summary>
    public class RoomLights : MonoBehaviour
    {
        const string TablePath = "Data/RoomLights";
        const string ShaderPath = "Shaders/UIAdditive";
        const float ColorCycleHz = 0.12f;   // 두 색을 오가는 빠르기 — 방 안의 모든 빛이 같이 돌아서 서로 맞습니다
        const float RingDotSize = 16f;

        class LightRow
        {
            public string Id, Kind, Link;
            public Vector2 Center, Size;
            public Color ColorA, ColorB;
            public bool Cycles;
            public float Intensity, Speed;
            public int Count;
        }

        /// <summary>화면에 세운 빛 하나. 점이 여러 개인 종류(ring·sparkle)는 Parts 에 담습니다.</summary>
        class LiveLight
        {
            public LightRow Row;
            public Image Image;
            public readonly List<Image> Parts = new List<Image>();
            public readonly List<Vector4> PartState = new List<Vector4>();   // sparkle: x, 시작 시각, 수명, 크기
            public float Phase, Level = 1f;
            public float DropStart = -1f, DropEnd = -1f, NextDrop;
            public LiveLight LinkTo;
        }

        static Dictionary<string, List<LightRow>> _table;
        static Material _material;
        static bool _warnedShader;

        RectTransform _layer;
        readonly List<LiveLight> _lights = new List<LiveLight>();
        readonly Dictionary<string, LiveLight> _byId = new Dictionary<string, LiveLight>();
        Sprite _glow, _core, _beam, _spark;

        /// <summary>그 방의 빛을 세웁니다. 빛이 없는 방이면 전부 치웁니다.</summary>
        public void Show(string roomId)
        {
            EnsureTable();
            Clear();

            if (string.IsNullOrEmpty(roomId) || !_table.TryGetValue(roomId, out var rows) || rows.Count == 0)
            {
                if (_layer != null) _layer.gameObject.SetActive(false);
                return;
            }

            EnsureLayer();
            if (_layer == null) return;
            _layer.gameObject.SetActive(true);

            foreach (var row in rows) Build(row);
            foreach (var live in _lights)
                if (!string.IsNullOrEmpty(live.Row.Link) && !_byId.TryGetValue(live.Row.Link, out live.LinkTo))
                    Debug.LogWarning($"[Lights] '{live.Row.Id}' 의 연결 '{live.Row.Link}' 을 같은 방에서 찾지 못했습니다.");

            Update();   // 첫 프레임부터 제 밝기로
        }

        void Clear()
        {
            _lights.Clear();
            _byId.Clear();
            if (_layer != null) UIFactory.Clear(_layer);
        }

        // ── 준비 ──────────────────────────────────────────────────────────────

        static void EnsureTable()
        {
            if (_table != null) return;
            _table = new Dictionary<string, List<LightRow>>();

            var asset = Resources.Load<TextAsset>(TablePath);
            if (asset == null) return;   // 표가 없으면 빛 없이 돕니다

            foreach (var r in CsvParser.Parse(asset.text))
            {
                var row = new LightRow
                {
                    Id        = CsvParser.Str(r, "light_id"),
                    Kind      = CsvParser.Str(r, "종류", "glow").Trim().ToLowerInvariant(),
                    Center    = new Vector2(CsvParser.Float(r, "x"), CsvParser.Float(r, "y")),
                    Size      = new Vector2(CsvParser.Float(r, "w"), CsvParser.Float(r, "h")),
                    Intensity = CsvParser.Float(r, "세기", 0.7f),
                    Speed     = CsvParser.Float(r, "속도", 0.3f),
                    Link      = CsvParser.Str(r, "연결").Trim(),
                    Count     = Mathf.Max(1, CsvParser.Int(r, "개수", 8)),
                };
                ParseColors(CsvParser.Str(r, "색", "#FFFFFF"), row);

                string room = CsvParser.Str(r, "room_id").Trim();
                if (string.IsNullOrEmpty(row.Id) || string.IsNullOrEmpty(room)) continue;
                if (!_table.TryGetValue(room, out var list)) _table[room] = list = new List<LightRow>();
                list.Add(row);
            }
        }

        static void ParseColors(string text, LightRow row)
        {
            var parts = text.Split('>');
            if (!ColorUtility.TryParseHtmlString(parts[0].Trim(), out row.ColorA))
            {
                Debug.LogWarning($"[Lights] '{row.Id}' 의 색 '{text}' 을 읽지 못했습니다. 예) #FFB860 또는 #6ED2FF>#B48CFF");
                row.ColorA = Color.white;
            }
            row.ColorB = row.ColorA;
            row.Cycles = parts.Length > 1 && ColorUtility.TryParseHtmlString(parts[1].Trim(), out row.ColorB);
        }

        void EnsureLayer()
        {
            if (_layer != null) return;
            var ui = Game.UI;
            if (ui == null || ui.BackgroundLayer == null) return;

            // 배경 바로 위, 인물·핫스팟 아래. 화면 흔들림([shake])도 배경과 같이 받습니다.
            _layer = UIFactory.Rect("LightLayer", ui.BackgroundLayer);
            int above = ui.BackgroundImage != null ? ui.BackgroundImage.transform.GetSiblingIndex() + 1 : 1;
            _layer.SetSiblingIndex(above);

            _glow  = Resources.Load<Sprite>("Art/FX/fx_glow");
            _core  = Resources.Load<Sprite>("Art/FX/fx_core");
            _beam  = Resources.Load<Sprite>("Art/FX/fx_beam");
            _spark = Resources.Load<Sprite>("Art/FX/fx_spark");
            if (_glow == null)
                Debug.LogWarning("[Lights] Art/FX 그림이 없습니다. python ArtSource/gen_fx.py 를 돌려 주세요.");

            if (_material == null)
            {
                var shader = Resources.Load<Shader>(ShaderPath);
                if (shader != null) _material = new Material(shader) { name = "UI Additive (runtime)" };
                else if (!_warnedShader)
                {
                    _warnedShader = true;
                    Debug.LogWarning($"[Lights] Resources/{ShaderPath}.shader 가 없어 보통 UI 로 그립니다 — 빛이 덜 밝게 보입니다.");
                }
            }
        }

        Image NewImage(string name, Sprite sprite)
        {
            var img = UIFactory.Box(name, _layer, Color.clear);
            img.sprite = sprite;
            img.raycastTarget = false;   // 빛은 클릭을 가로채면 안 됩니다 — 핫스팟이 아래에 있어도 눌려야 합니다
            if (_material != null) img.material = _material;
            return img;
        }

        /// <summary>정규화 좌표(왼쪽 위 기준) 네모에 맞춰 앵커를 겁니다. 배경이 늘어나면 빛도 같이 늘어납니다.</summary>
        static void Place(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = new Vector2(left, 1f - bottom);
            rt.anchorMax = new Vector2(right, 1f - top);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>점 하나 크기는 픽셀로 고정하고 자리만 정규화 좌표로 겁니다.</summary>
        static void PlacePoint(RectTransform rt, Vector2 point, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(point.x, 1f - point.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        void Build(LightRow row)
        {
            var live = new LiveLight { Row = row, Phase = Random.value * Mathf.PI * 2f, NextDrop = Time.unscaledTime + Random.Range(2f, 6f) };
            Vector2 c = row.Center, half = row.Size * 0.5f;

            switch (row.Kind)
            {
                case "beam":
                    live.Image = NewImage(row.Id, _beam);
                    Place(live.Image.rectTransform, c.x - half.x, c.y, c.x + half.x, c.y + row.Size.y);
                    break;

                case "core":
                    live.Image = NewImage(row.Id, _core);
                    Place(live.Image.rectTransform, c.x - half.x, c.y - half.y, c.x + half.x, c.y + half.y);
                    break;

                case "ring":
                    for (int i = 0; i < row.Count; i++)
                    {
                        // 앞쪽 반 바퀴만 — 뒤쪽은 기계 몸통에 가려 안 보이는 자리입니다.
                        float a = row.Count == 1 ? Mathf.PI * 0.5f : Mathf.PI * i / (row.Count - 1);
                        // 퍼지는 후광 대신 또렷한 알 — 작은 전구가 줄지어 박힌 것처럼 보이게
                        var dot = NewImage($"{row.Id}_{i}", _core != null ? _core : _glow);
                        PlacePoint(dot.rectTransform, c + new Vector2(Mathf.Cos(a) * row.Size.x, Mathf.Sin(a) * row.Size.y), RingDotSize);
                        live.Parts.Add(dot);
                    }
                    break;

                case "sparkle":
                    for (int i = 0; i < row.Count; i++)
                    {
                        var mote = NewImage($"{row.Id}_{i}", _spark);
                        live.Parts.Add(mote);
                        live.PartState.Add(NewMote(row, Time.unscaledTime - Random.value * 2.5f));
                    }
                    break;

                default:   // glow · flicker · spill
                    live.Image = NewImage(row.Id, _glow);
                    Place(live.Image.rectTransform, c.x - half.x, c.y - half.y, c.x + half.x, c.y + half.y);
                    break;
            }

            _lights.Add(live);
            _byId[row.Id] = live;
        }

        static Vector4 NewMote(LightRow row, float startTime)
        {
            float life = Random.Range(1.6f, 2.8f) / Mathf.Max(0.1f, row.Speed);
            return new Vector4(Random.Range(-0.5f, 0.5f), startTime, life, Random.Range(14f, 26f));
        }

        // ── 매 프레임 ─────────────────────────────────────────────────────────

        void Update()
        {
            if (_lights.Count == 0) return;
            float t = Time.unscaledTime;
            float cycle = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f * ColorCycleHz);

            // 1) 스스로 밝기를 정하는 빛부터 — 연결해서 따라가는 빛이 같은 프레임 값을 받게
            foreach (var live in _lights)
                if (live.LinkTo == null) live.Level = OwnLevel(live, t);
            foreach (var live in _lights)
                if (live.LinkTo != null) live.Level = live.LinkTo.Level;

            // 2) 그리기
            foreach (var live in _lights)
            {
                var row = live.Row;
                var color = row.Cycles ? Color.Lerp(row.ColorA, row.ColorB, cycle) : row.ColorA;

                switch (row.Kind)
                {
                    case "ring":    DrawRing(live, color, t); break;
                    case "sparkle": DrawSparkle(live, color, t); break;
                    case "core":
                        // 알은 후광보다 덜 흔들립니다 — 필라멘트가 꺼질 때만 확 어두워집니다.
                        Tint(live.Image, color, row.Intensity * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(live.Level)));
                        break;
                    default:
                        Tint(live.Image, color, row.Intensity * live.Level);
                        break;
                }
            }
        }

        float OwnLevel(LiveLight live, float t)
        {
            var row = live.Row;
            float breathe = Mathf.Sin(t * Mathf.PI * 2f * row.Speed + live.Phase);

            if (row.Kind != "flicker") return 1f + 0.15f * breathe;

            // 오래된 전구: 평소엔 살짝 지글거리다가, 가끔 짧게 두세 번 꺼질 듯 깜빡입니다.
            float jitter = (Mathf.PerlinNoise(t * 7f * Mathf.Max(0.2f, row.Speed), live.Phase) - 0.5f) * 0.18f;
            if (t >= live.NextDrop)
            {
                live.DropStart = t;
                live.DropEnd = t + Random.Range(0.12f, 0.45f);
                live.NextDrop = live.DropEnd + Random.Range(2.5f, 8f);
            }
            if (t < live.DropEnd)
            {
                float k = (t - live.DropStart) * 38f;   // 꺼짐 구간 안에서 빠르게 껌뻑
                return Mathf.Sin(k) > 0.2f ? 0.18f : 0.75f;
            }
            return 1f + 0.06f * breathe + jitter;
        }

        static void Tint(Image img, Color color, float alpha)
        {
            if (img == null) return;
            color.a = Mathf.Clamp01(alpha);
            img.color = color;
        }

        static void DrawRing(LiveLight live, Color color, float t)
        {
            var row = live.Row;
            int n = live.Parts.Count;
            // 밝은 부분 두 개가 반 바퀴 간격으로 돌아갑니다. 속도가 음수면 반대 방향.
            float head = Mathf.Repeat(t * row.Speed, 1f);
            for (int i = 0; i < n; i++)
            {
                float f = n == 1 ? 0f : i / (float)(n - 1);
                float bump = 0f;
                for (int k = 0; k < 2; k++)
                {
                    float d = Mathf.Abs(Mathf.Repeat(f - head - k * 0.5f + 0.5f, 1f) - 0.5f);
                    bump = Mathf.Max(bump, Mathf.Exp(-(d * d) / (0.11f * 0.11f)));
                }
                // 꺼진 알은 아주 희미하게만 — 켜진 알과 대비가 커야 또렷해 보입니다.
                Tint(live.Parts[i], color, row.Intensity * (0.18f + 0.82f * bump));
            }
            live.Level = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 2f * Mathf.Abs(row.Speed));
        }

        static void DrawSparkle(LiveLight live, Color color, float t)
        {
            var row = live.Row;
            for (int i = 0; i < live.Parts.Count; i++)
            {
                var s = live.PartState[i];
                float p = (t - s.y) / s.z;
                if (p >= 1f)
                {
                    s = NewMote(row, t);
                    live.PartState[i] = s;
                    p = 0f;
                }

                // 아래에서 위로 떠오르며, 좌우로 살랑이고, 가운데쯤 가장 밝습니다.
                float x = row.Center.x + s.x * row.Size.x + Mathf.Sin(p * 6f + i) * row.Size.x * 0.08f;
                float y = row.Center.y + row.Size.y * (0.5f - p);
                var rt = live.Parts[i].rectTransform;
                PlacePoint(rt, new Vector2(x, y), s.w * (0.6f + 0.4f * Mathf.Sin(p * Mathf.PI)));
                rt.localRotation = Quaternion.Euler(0f, 0f, p * 140f + i * 37f);
                Tint(live.Parts[i], color, row.Intensity * Mathf.Sin(p * Mathf.PI));
            }
        }
    }
}
