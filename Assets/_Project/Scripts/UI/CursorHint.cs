using UnityEngine;

namespace SevenDoctors.UI
{
    /// <summary>
    /// "이 위에서는 커서가 이 모양" 이라는 표시. 누를 수 있는 UI 오브젝트에 붙여 두면,
    /// 포인터가 그 오브젝트(와 자식) 위에 있을 때 GameCursor 가 읽어 갑니다.
    ///
    /// 스스로는 아무 일도 하지 않습니다. 포인터가 들어오고 나가는 이벤트를 받아 커서를
    /// 바꾸는 방식이 아닌 이유는 GameCursor 주석에 있습니다.
    /// </summary>
    public class CursorHint : MonoBehaviour
    {
        public CursorKind Kind = CursorKind.Look;

        /// <summary>
        /// 탐색 중(Game.CanInteract)일 때만 모양을 바꿉니다. 핫스팟이 그렇습니다 —
        /// 대사가 흐르는 동안에는 눌러도 아무 일이 없으니 커서도 화살표로 남아야 합니다.
        /// </summary>
        public bool ExploringOnly;

        public static CursorHint Attach(GameObject target, CursorKind kind, bool exploringOnly = false)
        {
            var hint = target.GetComponent<CursorHint>();
            if (hint == null) hint = target.AddComponent<CursorHint>();

            hint.Kind = kind;
            hint.ExploringOnly = exploringOnly;
            return hint;
        }
    }
}
