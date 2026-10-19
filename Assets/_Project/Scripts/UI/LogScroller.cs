using UnityEngine;
using UnityEngine.EventSystems;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 대화 기록창(LogView)의 휠·키보드 넘기기. 기록창이 열려 있을 때만 켜져 있습니다.
    ///
    ///   휠        한 칸씩 (방향만 봅니다 — 입력 모듈마다 한 칸 값이 1 이기도 120 이기도 합니다)
    ///   ↑ / ↓     누르고 있는 동안 계속
    ///   Home/End  맨 처음 / 맨 끝
    /// </summary>
    public class LogScroller : MonoBehaviour, IScrollHandler
    {
        const float KeySpeed = 1100f;   // ↑↓ 를 누르고 있을 때 px/초

        public LogView View;

        public void OnScroll(PointerEventData e) => View?.ScrollWheel(e.scrollDelta.y);

        void Update()
        {
            if (View == null) return;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            bool up = kb.upArrowKey.isPressed, down = kb.downArrowKey.isPressed;
            bool home = kb.homeKey.wasPressedThisFrame, end = kb.endKey.wasPressedThisFrame;
#else
            bool up = Input.GetKey(KeyCode.UpArrow), down = Input.GetKey(KeyCode.DownArrow);
            bool home = Input.GetKeyDown(KeyCode.Home), end = Input.GetKeyDown(KeyCode.End);
#endif

            if (home) View.ScrollToTop();
            else if (end) View.ScrollToBottom();
            else if (up != down) View.ScrollBy((up ? 1f : -1f) * KeySpeed * Time.unscaledDeltaTime);
        }
    }
}
