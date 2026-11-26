using System.Collections.Generic;
using SevenDoctors.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 도우미 로봇이 혼자 노는 동작 — 아무도 안 부를 때 살아 있는 것처럼 보이게.
    ///
    ///   뒤뚱뒤뚱 걷기   오리처럼 좌우로 기우뚱하며 왼쪽으로 조금 갔다가 돌아옴
    ///   넘어지기        걷다가 가끔 발이 꼬여 옆으로 픽 쓰러짐 → 허둥대다 벌떡
    ///   졸기            플레이어가 한동안 아무것도 안 하면 꾸벅꾸벅 + Zzz. 가끔은 그냥 낮잠도
    ///
    /// 플레이어에게 방해가 되면 안 되므로, '바쁠 때'는 하던 걸 멈추고 제자리로 돌아옵니다:
    /// 막혀서 힌트 표시 중 · 말풍선이 떠 있음 · 로봇 창이 열림 · 대사·퍼즐 중(탐색이 아님).
    /// 자는 중에 마우스를 움직이거나 키를 누르면 깹니다. 눌러서 창을 열어도 깹니다.
    ///
    /// 숫자는 아래 상수에서 고칩니다. 로봇의 기울기는 Rig(그림 묶음)만 돌려서, 누르는 칸은 그대로입니다.
    /// </summary>
    public partial class HelperBotView
    {
        const float IdleMinGap = 7f, IdleMaxGap = 16f;   // 혼자 뭔가 하는 간격 (초)
        const float WalkRange = 360f;                    // 제자리에서 왼쪽으로 최대 이만큼 (px)
        const float WalkSpeed = 105f, HurrySpeed = 420f; // 평소 / 바빠서 돌아올 때
        const float WaddleHz = 2.4f, WaddleAngle = 9f, WaddleHop = 6f;
        const float TripChance = 0.3f;                   // 걸을 때마다 넘어질 확률
        const float NapChance = 0.15f;                   // 혼자 놀 차례에 걷는 대신 낮잠 잘 확률
        const float SleepAfterIdle = 40f;                // 플레이어가 이만큼 가만있으면 잠듦 (초)
        const float FallDrop = 28f;                      // 쓰러질 때 바닥까지 내려가는 높이 (px)

        enum Idle { Hover, Walk, Pause, Trip, Lying, GetUp, Sleep, Waking }

        Idle _idle = Idle.Hover;
        float _idleTime;          // 지금 동작을 시작한 뒤 지난 시간
        float _nextIdleAt = -1f;  // 다음에 혼자 뭔가 할 시각
        float _idleX, _idleY, _rigAngle;
        float _bobScale = 1f, _bobSpeed = 1f;
        float _walkTarget, _tripAt = -1f, _fallDir = 1f, _stateLength, _walkPhase;
        float _lastInputAt;
        bool _nap;                // 스스로 깨는 낮잠인지 (플레이어가 가만있어서 든 잠은 아님)
        float _zzzTimer;
        readonly List<Text> _zzz = new List<Text>();
        readonly List<float> _zzzAge = new List<float>();

        // ── 바깥과 이어지는 곳 ────────────────────────────────────────────────

        void ResetIdle()
        {
            _idle = Idle.Hover;
            _idleX = _idleY = _rigAngle = 0f;
            _bobScale = _bobSpeed = 1f;
            _nextIdleAt = -1f;
            foreach (var z in _zzz) if (z != null) Destroy(z.gameObject);
            _zzz.Clear();
            _zzzAge.Clear();
        }

        /// <summary>지금은 혼자 놀면 안 되는 때인지. 이때는 하던 걸 접고 제자리에서 둥실거립니다.</summary>
        bool Busy()
        {
            bool bubble = _bubble != null && _bubble.gameObject.activeSelf;
            return _stuck || bubble || IsMenuOpen || Game.State != GameState.Exploring;
        }

        void UpdateIdle(float dt)
        {
            float now = Time.unscaledTime;
            if (_nextIdleAt < 0f) { _nextIdleAt = now + Random.Range(IdleMinGap, IdleMaxGap); _lastInputAt = now; }
            if (PlayerDidSomething()) _lastInputAt = now;

            _idleTime += dt;
            bool busy = Busy();

            switch (_idle)
            {
                case Idle.Hover:
                    Settle(dt, 0f, 1f, 1f);
                    // 걷다 멈춘 자리에 있으면 바쁠 때 얼른 제자리로
                    if (busy && Mathf.Abs(_idleX) > 1f) { StartWalk(0f, false); break; }
                    if (busy) break;
                    if (now - _lastInputAt > SleepAfterIdle) { StartSleep(false); break; }
                    if (now >= _nextIdleAt) PickSomething();
                    break;

                case Idle.Walk:  UpdateWalk(dt, busy); break;

                case Idle.Pause:
                    Settle(dt, 0f, 1f, 1f);
                    if (busy || _idleTime >= _stateLength) EndAction();
                    break;

                case Idle.Trip:
                {
                    // 발이 꼬여 옆으로 픽 — 처음엔 천천히, 끝에 확 (가속)
                    float p = Mathf.Clamp01(_idleTime / 0.3f);
                    _rigAngle = _fallDir * 88f * p * p;
                    _idleY = -FallDrop * p * p;
                    _bobScale = 1f - p;
                    if (p >= 1f) SetIdle(Idle.Lying, Random.Range(1.1f, 1.7f));
                    break;
                }

                case Idle.Lying:
                {
                    // 바닥에 닿은 직후 두어 번 통통 — 그 뒤엔 허둥지둥(표정)
                    float settle = Mathf.Exp(-_idleTime * 9f) * Mathf.Sin(_idleTime * 30f) * 5f;
                    _rigAngle = _fallDir * (88f - Mathf.Abs(settle));
                    _idleY = -FallDrop;
                    _bobScale = 0f;
                    if (busy || _idleTime >= _stateLength) { SetIdle(Idle.GetUp, 0.5f); ApplyFace("curious"); }
                    break;
                }

                case Idle.GetUp:
                {
                    // 벌떡 — 살짝 반대쪽으로 넘쳤다가 바로 섬 (easeOutBack)
                    float p = Mathf.Clamp01(_idleTime / _stateLength);
                    float back = 1f + 2.2f * Mathf.Pow(p - 1f, 3f) + 1.2f * Mathf.Pow(p - 1f, 2f);
                    _rigAngle = _fallDir * 88f * (1f - back);
                    _idleY = -FallDrop * (1f - p) + Mathf.Sin(p * Mathf.PI) * 14f;
                    _bobScale = p;
                    if (p >= 1f)
                    {
                        _rigAngle = 0f; _idleY = 0f;
                        if (!busy) ApplyFace("happy");   // 머쓱하게 웃기
                        SetIdle(Idle.Pause, busy ? 0f : 1.2f);
                    }
                    break;
                }

                case Idle.Sleep:  UpdateSleep(dt, busy, now); break;

                case Idle.Waking:
                {
                    // 화들짝 — 작게 폴짝 뛰며 고개를 바로 함
                    float p = Mathf.Clamp01(_idleTime / 0.45f);
                    _idleY = Mathf.Lerp(-14f, 0f, p) + Mathf.Sin(p * Mathf.PI) * 16f;
                    _rigAngle = Mathf.Lerp(_rigAngle, 0f, dt * 12f);
                    _bobScale = Mathf.Lerp(0.35f, 1f, p);
                    _bobSpeed = Mathf.Lerp(0.5f, 1f, p);
                    if (p >= 1f) EndAction();
                    break;
                }
            }

            UpdateZzz(dt);
        }

        // ── 고르기 ────────────────────────────────────────────────────────────

        void PickSomething()
        {
            if (Random.value < NapChance) { StartSleep(true); return; }

            // 반은 제자리 쪽으로 돌아가는 걸음 — 너무 멀리 나가 있지 않게
            float target = (_idleX < -40f && Random.value < 0.5f)
                ? 0f
                : -Random.Range(80f, WalkRange);
            if (Mathf.Abs(target - _idleX) < 60f) target = _idleX < -WalkRange * 0.5f ? 0f : -WalkRange;
            StartWalk(target, Random.value < TripChance);
        }

        void SetIdle(Idle next, float length)
        {
            _idle = next;
            _idleTime = 0f;
            _stateLength = length;
        }

        /// <summary>하나를 마치고 둥실거리기로 돌아갑니다. 다음 동작까지 다시 기다립니다.</summary>
        void EndAction()
        {
            SetIdle(Idle.Hover, 0f);
            _nextIdleAt = Time.unscaledTime + Random.Range(IdleMinGap, IdleMaxGap);
            // 말풍선이 떠 있으면 웃는 얼굴(Say)을 그대로 둡니다 — 풍선이 닫힐 때 맞춰집니다.
            if (_bubble == null || !_bubble.gameObject.activeSelf) ApplyFace(_stuck ? "hint" : "normal");
        }

        /// <summary>둥실거리기 상태로 부드럽게 돌아가게 각도·높이·둥실 정도를 끌어당깁니다.</summary>
        void Settle(float dt, float angle, float bobScale, float bobSpeed)
        {
            float k = 1f - Mathf.Exp(-dt * 8f);
            _rigAngle = Mathf.Lerp(_rigAngle, angle, k);
            _idleY = Mathf.Lerp(_idleY, 0f, k);
            _bobScale = Mathf.Lerp(_bobScale, bobScale, k);
            _bobSpeed = Mathf.Lerp(_bobSpeed, bobSpeed, k);
        }

        // ── 걷기 · 넘어지기 ───────────────────────────────────────────────────

        void StartWalk(float target, bool willTrip)
        {
            _walkTarget = target;
            _tripAt = willTrip ? Random.Range(0.3f, 0.8f) : -1f;   // 가는 길의 몇 % 지점에서 넘어질지
            _fallDir = target < _idleX ? 1f : -1f;                   // 가는 쪽으로 쓰러짐 (왼쪽으로 가면 시계 반대)
            _walkPhase = 0f;
            SetIdle(Idle.Walk, Mathf.Max(1f, Mathf.Abs(target - _idleX)));   // 길이 = 걸을 거리 (몇 % 왔는지 셀 때 씀)
            if (!Busy()) ApplyFace("normal");
        }

        void UpdateWalk(float dt, bool busy)
        {
            float speed = busy ? HurrySpeed : WalkSpeed;
            if (busy) { _walkTarget = 0f; _tripAt = -1f; }   // 바쁘면 넘어지지 말고 얼른 제자리로

            _idleX = Mathf.MoveTowards(_idleX, _walkTarget, speed * dt);
            _walkPhase += dt * WaddleHz * (busy ? 2f : 1f);

            // 뒤뚱뒤뚱 — 좌우로 기우뚱 + 한 걸음마다 콩
            float swing = Mathf.Sin(_walkPhase * Mathf.PI * 2f);
            _rigAngle = swing * WaddleAngle;
            _idleY = Mathf.Abs(swing) * WaddleHop;
            _bobScale = Mathf.Lerp(_bobScale, 0.3f, dt * 6f);

            float progress = 1f - Mathf.Abs(_walkTarget - _idleX) / _stateLength;
            if (_tripAt >= 0f && progress >= _tripAt)
            {
                _tripAt = -1f;
                SetIdle(Idle.Trip, 0.3f);
                ApplyFace("panic");
                return;
            }

            if (Mathf.Approximately(_idleX, _walkTarget))
            {
                if (busy) EndAction();
                else { SetIdle(Idle.Pause, Random.Range(1.2f, 2.6f)); ApplyFace("curious"); }   // 두리번
            }
        }

        // ── 졸기 ──────────────────────────────────────────────────────────────

        void StartSleep(bool nap)
        {
            _nap = nap;
            SetIdle(Idle.Sleep, nap ? Random.Range(8f, 15f) : float.MaxValue);
            _zzzTimer = 0.6f;
            ApplyFace("sleepy");
        }

        void UpdateSleep(float dt, bool busy, float now)
        {
            // 천천히 가라앉아 둥실거림도 느려지고, 고개를 꾸벅꾸벅
            float k = 1f - Mathf.Exp(-dt * 2f);
            _idleY = Mathf.Lerp(_idleY, -14f, k);
            _bobScale = Mathf.Lerp(_bobScale, 0.35f, k);
            _bobSpeed = Mathf.Lerp(_bobSpeed, 0.5f, k);
            float nod = Mathf.Sin(_idleTime * 0.9f);
            _rigAngle = Mathf.Lerp(_rigAngle, 7f + nod * 3f + (nod > 0.95f ? 4f : 0f), k * 3f);

            _zzzTimer -= dt;
            if (_zzzTimer <= 0f) { SpawnZzz(); _zzzTimer = 1.4f; }

            bool woke = busy || now - _lastInputAt < 0.05f || (_nap && _idleTime >= _stateLength);
            if (woke)
            {
                _lastInputAt = now;
                SetIdle(Idle.Waking, 0.45f);
                if (!busy) ApplyFace("curious");
            }
        }

        void SpawnZzz()
        {
            if (_bot == null || _zzz.Count >= 3) return;
            var z = UIFactory.Label("Zzz", _bot, "z", 30, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f, 0f));
            z.fontStyle = FontStyle.Bold;
            var rt = z.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.75f, 0.85f);   // 머리 오른쪽 위
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchoredPosition = Vector2.zero;
            _zzz.Add(z);
            _zzzAge.Add(0f);
        }

        void UpdateZzz(float dt)
        {
            for (int i = _zzz.Count - 1; i >= 0; i--)
            {
                var z = _zzz[i];
                float age = _zzzAge[i] + dt;
                _zzzAge[i] = age;
                const float life = 2.4f;

                if (z == null || age >= life) { if (z != null) Destroy(z.gameObject); _zzz.RemoveAt(i); _zzzAge.RemoveAt(i); continue; }

                // 위로 떠오르며 오른쪽으로 살랑, 점점 커졌다가 사라짐. 깨면 빨리 사라짐.
                float p = age / life;
                if (_idle != Idle.Sleep) _zzzAge[i] += dt * 3f;
                z.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(p * 5f) * 8f + p * 24f, p * 70f);
                z.rectTransform.localScale = Vector3.one * (0.6f + p * 0.8f);
                var c = z.color;
                c.a = Mathf.Sin(p * Mathf.PI) * 0.9f;
                z.color = c;
            }
        }

        // ── 입력 감지 (새 Input System) ────────────────────────────────────────

        static bool PlayerDidSomething()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.01f ||
                                  mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                                  mouse.scroll.ReadValue().sqrMagnitude > 0.01f))
                return true;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.anyKey.isPressed;
#else
            return Input.anyKey || Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f;
#endif
        }
    }
}
