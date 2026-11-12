using System.Collections.Generic;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenDoctors.UI
{
    /// <summary>마우스 커서의 모양. 올려놓은 곳을 누르면 무슨 일이 일어나는지를 알려 줍니다.</summary>
    public enum CursorKind
    {
        Arrow,  // 기본 — 평소, UI 버튼, 퍼즐, 지도
        Look,   // 조사 — look 핫스팟, 증거 크게 보기
        Get,    // 줍기 — get 핫스팟
        Talk,   // 대화 — talk 핫스팟
        Move,   // 이동 — move 핫스팟
    }

    /// <summary>
    /// 마우스 커서를 상황에 맞는 그림으로 바꿉니다.
    ///
    /// 화면 속 핫스팟은 마우스를 올려도 이름표나 강조를 띄우지 않습니다 (HotspotView).
    /// 그 대신 커서 모양이 "여기는 누를 수 있다" 와 "누르면 무슨 일이 일어난다" 를 알려 줍니다.
    ///
    /// 포인터가 들어오고 나가는 이벤트(IPointerEnterHandler)를 받지 않고, 매 프레임 포인터 아래
    /// 맨 위에 있는 UI 를 직접 찾습니다. 이벤트 방식은 이 게임에서 두 군데가 샙니다:
    ///   · 핫스팟은 대사가 끝날 때마다 전부 부수고 새로 만듭니다 (RoomController.RebuildHotspots).
    ///     가만히 있는 포인터 아래에서 그렇게 바뀌면 '나갔다'·'들어왔다' 가 제때 온다는 보장이 없습니다.
    ///   · 탐색 중이 아닐 때는 핫스팟을 눌러도 아무 일이 없습니다. 상태가 바뀌는 순간에는
    ///     포인터가 움직이지 않으므로, 이벤트만 봐서는 커서를 되돌릴 계기가 없습니다.
    /// 맨 위 UI 를 보는 것은 클릭이 실제로 가는 곳과 같은 기준입니다. 패널이 핫스팟을 덮으면
    /// 커서도 따라서 화살표로 돌아갑니다.
    ///
    /// 그림은 Resources/Art/Cursors/cursor_{이름}.png (ArtSource/gen_cursors.py).
    /// 그림이 없으면 OS 기본 커서로 둡니다 — 아트가 없어도 게임은 돌아가야 합니다.
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        /// <summary>
        /// 끄면 언제나 화살표만 씁니다. 커서가 바뀌면 화면을 훑어서 누를 곳을 찾을 수 있게 되므로,
        /// 숨은 물건을 눈으로 찾게 하고 싶다면 끄세요. GameBootstrap 의 같은 이름 옵션이 여기로 옵니다.
        /// </summary>
        public bool ContextCursors = true;

        const string Folder = "Art/Cursors";

        /// <summary>아래 표의 클릭 지점을 잰 그림 크기. 그림이 이 크기가 아니면 비율로 환산합니다.</summary>
        const float DesignSize = 64f;

        // 종류별 파일 이름과 클릭 지점 — 그림의 왼쪽 위에서 잰 픽셀. CursorKind 와 같은 순서입니다.
        //
        // 그림을 바꾸면 여기도 맞춰야 합니다. 지금 값은 gen_cursors.py 의 CURSORS 표와 같고,
        // 그 스크립트의 --preview 가 이 자리에 빨간 십자를 그려 줍니다.
        // 뾰족한 끝은 먹선 끝과 크림색 끝의 중간입니다 — 어두운 방에서는 먹선이 배경에 묻혀
        // 크림색 끝이 '끝'으로 보이기 때문입니다.
        static readonly (string File, Vector2 Hotspot)[] Table =
        {
            ("cursor_arrow", new Vector2(11, 7)),    // 화살 끝
            ("cursor_look",  new Vector2(26, 26)),   // 렌즈 한가운데
            ("cursor_get",   new Vector2(31, 6)),    // 가운뎃손가락 끝
            ("cursor_talk",  new Vector2(18, 57)),   // 말풍선 꼬리 끝
            ("cursor_move",  new Vector2(57, 32)),   // 화살 끝
        };

        Texture2D[] _textures;      // 종류별 그림. 없는 종류는 null — 화살표로 대신합니다
        Vector2[] _hotspots;
        CursorKind _shown;
        bool _custom;               // 지금 우리 그림이 걸려 있는가

        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        PointerEventData _pointer;
        EventSystem _pointerOwner;

        void Awake()
        {
            _textures = new Texture2D[Table.Length];
            _hotspots = new Vector2[Table.Length];
            var problems = new List<string>();

            for (int i = 0; i < Table.Length; i++)
            {
                var texture = Resources.Load<Texture2D>($"{Folder}/{Table[i].File}");
                if (texture == null)
                {
                    problems.Add($"{Table[i].File} (파일 없음)");
                    continue;
                }

                // Cursor.SetCursor 는 CPU 에서 읽을 수 있는 텍스처만 받습니다. Sprite 로 임포트되면
                // 여기서 걸립니다 — 그대로 넘기면 유니티가 에러만 찍고 커서는 바뀌지 않습니다.
                if (!texture.isReadable)
                {
                    problems.Add($"{Table[i].File} (임포트 설정이 Cursor 타입이 아님)");
                    continue;
                }

                // 클릭 지점은 그림 안에 있어야 합니다.
                _textures[i] = texture;
                _hotspots[i] = new Vector2(
                    Mathf.Clamp(Table[i].Hotspot.x * texture.width / DesignSize, 0f, texture.width - 1),
                    Mathf.Clamp(Table[i].Hotspot.y * texture.height / DesignSize, 0f, texture.height - 1));
            }

            bool hasArrow = _textures[(int)CursorKind.Arrow] != null;

            if (problems.Count > 0)
                Debug.LogWarning(
                    $"[Cursor] 커서 그림을 쓸 수 없습니다: {string.Join(", ", problems)}. " +
                    (hasArrow ? "이 모양들은 화살표로 대신합니다. " : "OS 기본 커서를 그대로 씁니다. ") +
                    "그림은 'python ArtSource/gen_cursors.py' 로 만들고, 임포트 설정은 " +
                    "'Tools → 일곱 박사 → 아트 임포트 설정 다시 적용' 으로 맞춥니다.");

            // 화살표조차 없으면 아무것도 걸지 않습니다. 일부 상황에서만 그림이 나오면 더 어색합니다.
            if (!hasArrow) enabled = false;
        }

        void Update()
        {
            Show(ContextCursors ? KindUnderPointer() : CursorKind.Arrow);
        }

        void OnDisable()
        {
            // 에디터에서 Play 를 끝내거나 '처음으로' 로 다시 시작할 때 우리 그림이 남지 않게 합니다.
            if (_custom) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _custom = false;
        }

        void Show(CursorKind kind)
        {
            if (_custom && kind == _shown) return;

            int i = _textures[(int)kind] != null ? (int)kind : (int)CursorKind.Arrow;

            // 하드웨어 커서(Auto) — OS 가 직접 그리므로 프레임이 떨어져도 포인터가 밀리지 않습니다.
            Cursor.SetCursor(_textures[i], _hotspots[i], CursorMode.Auto);
            _shown = kind;
            _custom = true;
        }

        CursorKind KindUnderPointer()
        {
            var events = EventSystem.current;
            if (events == null || !Application.isFocused) return CursorKind.Arrow;
            if (!TryGetPointer(out var position)) return CursorKind.Arrow;

            if (_pointer == null || _pointerOwner != events)
            {
                _pointer = new PointerEventData(events);
                _pointerOwner = events;
            }
            _pointer.position = position;

            _hits.Clear();
            events.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0) return CursorKind.Arrow;

            // 맨 위 하나만 봅니다. 클릭도 맨 위 오브젝트로만 가므로 그 밑에 깔린 핫스팟은
            // 눌리지 않습니다 — 커서가 그걸 누를 수 있는 것처럼 보이면 안 됩니다.
            var hint = _hits[0].gameObject.GetComponentInParent<CursorHint>();
            if (hint == null) return CursorKind.Arrow;
            if (hint.ExploringOnly && !Game.CanInteract) return CursorKind.Arrow;
            return hint.Kind;
        }

        static bool TryGetPointer(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null)
            {
                position = default;
                return false;
            }
            position = mouse.position.ReadValue();
            return true;
#else
            position = Input.mousePosition;
            return true;
#endif
        }
    }
}
