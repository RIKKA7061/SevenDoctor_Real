using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SevenDoctors.UI
{
    /// <summary>
    /// 포트레이트를 살아 있게 만드는 부분. Live2D 를 쓰지 않고, 이미 레이어로
    /// 나뉘어 있는 스프라이트를 각자 흔들어서 같은 인상을 냅니다.
    ///
    /// 겹치는 네 가지가 동시에 돕니다:
    ///   · 호흡   — 몸이 느리게 오르내리고 아주 조금 늘어납니다
    ///   · 흔들림 — 주기가 다른 사인 두 개를 겹쳐 규칙성을 숨깁니다
    ///   · 시차   — 얼굴이 몸보다 조금 더 움직여 얕은 입체감을 만듭니다
    ///   · 반응   — 표정이 바뀌는 순간 감쇠 진동을 한 번 실어 보냅니다
    ///
    /// 여기에 눈 깜빡임·시선·입이 부위 레이어(FaceRig)가 있을 때만 더해집니다.
    /// 부위 PNG 가 아직 없으면 위 네 가지만 돌고, 표정은 합본 한 장으로 바뀝니다.
    /// 모든 값은 눈에 띄면 과한 크기입니다 — 없으면 죽어 보이는 정도로 잡았습니다.
    /// </summary>
    public class PortraitAnimator : MonoBehaviour
    {
        // ── 부위 묶음 ─────────────────────────────────────────────────────────

        /// <summary>PortraitView 가 배치까지 끝내서 넘겨주는 얼굴 부위들.</summary>
        public class FaceRig
        {
            public RectTransform EyeGroup;   // 세로로 눌러서 깜빡입니다
            public RectTransform Pupils;     // 눈 안에서 미세하게 흔들립니다
            public Image Mouth;
            public Sprite MouthClosed, MouthOpen;
            public Vector2 MouthClosedPos, MouthClosedSize;
            public Vector2 MouthOpenPos, MouthOpenSize;
            public bool CanBlink = true;
        }

        // ── 느낌 상수 ─────────────────────────────────────────────────────────
        // 호흡은 사람보다 살짝 느리게 잡아야 연기처럼 보입니다.
        const float BreathPeriod  = 3.4f;
        const float BreathRise    = 5f;      // px
        const float BreathStretch = 0.008f;  // 세로 배율 증가분

        const float SwayPeriodA = 6.7f;      // 서로 나누어떨어지지 않는 주기 두 개
        const float SwayPeriodB = 11.3f;
        const float SwayX       = 3.2f;      // px
        const float SwayTilt    = 0.5f;      // 도

        const float FaceParallax = 1.35f;    // 얼굴이 몸보다 이만큼 더 움직입니다

        const float BlinkClose  = 0.075f;
        const float BlinkHold   = 0.045f;
        const float BlinkOpen   = 0.105f;
        const float BlinkMinGap = 2.4f;
        const float BlinkMaxGap = 6.5f;
        const float DoubleBlinkChance = 0.22f;

        const float GazeMinGap = 1.6f;
        const float GazeMaxGap = 5.0f;
        const float GazeRangeX = 7f;         // 원본 캔버스 px
        const float GazeRangeY = 3.5f;
        const float GazeSpeed  = 7f;

        const float MouthMinGap = 0.055f;    // 대사 출력 중 입이 바뀌는 간격
        const float MouthMaxGap = 0.105f;

        // ── 상태 ──────────────────────────────────────────────────────────────

        RectTransform _body, _face;
        Vector2 _bodyBase, _faceBase;
        FaceRig _rig;
        Vector2 _pupilBase;
        float _scale = 1f;   // 원본 캔버스 px 를 포트레이트 px 로 바꾸는 배율

        bool _active;
        float _t;

        float _blinkGap, _blinkTimer, _blinkSquash = 1f;
        bool _blinking;
        Coroutine _blinkRoutine;

        Vector2 _gazeTarget, _gazeCur;
        float _gazeTimer, _gazeGap;

        bool _speaking, _mouthOpenNow;
        float _mouthTimer, _mouthGap;

        // 반응 모션 — 표정이 바뀔 때 한 번 실리는 감쇠 진동
        float _reactT, _reactDur;
        float _reactY, _reactScale, _reactRot, _reactFreq, _reactDamp;

        // ── 연결 ──────────────────────────────────────────────────────────────

        public void Bind(RectTransform body, RectTransform face, float canvasScale)
        {
            _body  = body;
            _face  = face;
            _scale = canvasScale > 0f ? canvasScale : 1f;
            if (_body != null) _bodyBase = _body.anchoredPosition;
            if (_face != null) _faceBase = _face.anchoredPosition;
            ResetTimers();
        }

        public void SetRig(FaceRig rig)
        {
            _rig = rig;
            _pupilBase = rig != null && rig.Pupils != null ? rig.Pupils.anchoredPosition : Vector2.zero;
            _gazeCur = _gazeTarget = Vector2.zero;
            _blinkSquash = 1f;
            ApplyMouth(false);
        }

        public void ClearRig()
        {
            _rig = null;
            _blinkSquash = 1f;
        }

        /// <summary>포트레이트가 숨겨질 때. 다음에 나타나면 처음부터 돕니다.</summary>
        public void SetActive(bool on)
        {
            _active = on;
            if (on) { ResetTimers(); return; }

            if (_blinkRoutine != null) { StopCoroutine(_blinkRoutine); _blinkRoutine = null; }
            _blinking = false;
            _blinkSquash = 1f;
            _speaking = false;
            ApplyMouth(false);
        }

        void ResetTimers()
        {
            _t = 0f;
            _blinkTimer = 0f;
            _blinkGap = UnityEngine.Random.Range(BlinkMinGap, BlinkMaxGap);
            _gazeTimer = 0f;
            _gazeGap = UnityEngine.Random.Range(GazeMinGap, GazeMaxGap);
            _reactT = _reactDur = 0f;
        }

        // ── 바깥에서 부르는 것들 ──────────────────────────────────────────────

        /// <summary>대사가 찍히는 동안 true. 입이 움직입니다.</summary>
        public void SetSpeaking(bool speaking)
        {
            if (_speaking == speaking) return;
            _speaking = speaking;
            _mouthTimer = 0f;
            _mouthGap = UnityEngine.Random.Range(MouthMinGap, MouthMaxGap);
            if (!speaking) ApplyMouth(false);
        }

        /// <summary>
        /// 표정을 바꿉니다. 눈을 감은 순간에 스프라이트를 갈아 끼워서 바뀌는
        /// 장면 자체를 보여주지 않습니다 — 선화라서 크로스페이드로 겹치면
        /// 탁해지기만 합니다. 눈을 감았다 뜨면 다른 표정인 편이 훨씬 낫습니다.
        /// </summary>
        public void SwapFace(Action swap, string faceKey)
        {
            if (swap == null) return;

            if (!_active || _rig == null || !isActiveAndEnabled)
            {
                swap();
                PlayReaction(faceKey);
                return;
            }

            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(SwapRoutine(swap, faceKey));
        }

        IEnumerator SwapRoutine(Action swap, string faceKey)
        {
            _blinking = true;

            yield return Squash(1f, 0f, BlinkClose);
            swap();
            PlayReaction(faceKey);
            yield return new WaitForSeconds(BlinkHold);
            yield return Squash(0f, 1f, BlinkOpen);

            _blinkSquash = 1f;
            _blinking = false;
            _blinkTimer = 0f;
            _blinkGap = UnityEngine.Random.Range(BlinkMinGap, BlinkMaxGap);
            _blinkRoutine = null;
        }

        /// <summary>표정에 맞는 반응 모션. 감정마다 몸이 다르게 반응합니다.</summary>
        public void PlayReaction(string faceKey)
        {
            switch (faceKey)
            {
                // 놀람은 위로 튀어오릅니다 — 빠르고 크게.
                case "surprised": SetReaction(0.55f,  20f,  0.028f, 0.0f, 2.3f, 5.0f); break;
                // 분노는 한 번 들이받듯 짧고 날카롭게 떨립니다.
                case "angry":     SetReaction(0.42f,  -7f,  0.010f, 1.9f, 4.6f, 7.5f); break;
                // 슬픔은 가라앉습니다 — 느리고, 제대로 돌아오지 않는 느낌.
                case "sad":       SetReaction(0.95f, -15f, -0.018f, 0.7f, 0.8f, 3.4f); break;
                // 졸림은 더 느리게 처집니다.
                case "sleepy":    SetReaction(1.10f, -11f, -0.012f, 0.9f, 0.6f, 2.9f); break;
                default:          SetReaction(0.45f,   6f,  0.008f, 0.0f, 1.7f, 6.0f); break;
            }
        }

        void SetReaction(float dur, float y, float scale, float rot, float freq, float damp)
        {
            _reactDur = dur; _reactT = 0f;
            _reactY = y; _reactScale = scale; _reactRot = rot;
            _reactFreq = freq; _reactDamp = damp;
        }

        // ── 매 프레임 ─────────────────────────────────────────────────────────

        void Update()
        {
            if (!_active || _body == null) return;

            float dt = Time.deltaTime;
            _t += dt;

            TickBlink(dt);
            TickGaze(dt);
            TickMouth(dt);

            Apply();
        }

        void TickBlink(float dt)
        {
            if (_rig == null || !_rig.CanBlink || _blinking) return;

            _blinkTimer += dt;
            if (_blinkTimer < _blinkGap) return;

            _blinkTimer = 0f;
            _blinkGap = UnityEngine.Random.Range(BlinkMinGap, BlinkMaxGap);
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        IEnumerator BlinkRoutine()
        {
            _blinking = true;

            // 가끔 두 번 연속으로 깜빡입니다. 이게 없으면 시계처럼 규칙적입니다.
            int times = UnityEngine.Random.value < DoubleBlinkChance ? 2 : 1;
            for (int i = 0; i < times; i++)
            {
                yield return Squash(1f, 0f, BlinkClose);
                yield return new WaitForSeconds(BlinkHold);
                yield return Squash(0f, 1f, BlinkOpen);
                if (i + 1 < times) yield return new WaitForSeconds(0.07f);
            }

            _blinkSquash = 1f;
            _blinking = false;
            _blinkRoutine = null;
        }

        IEnumerator Squash(float from, float to, float duration)
        {
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                float u = Mathf.Clamp01(e / duration);
                _blinkSquash = Mathf.Lerp(from, to, u * u * (3f - 2f * u)); // smoothstep
                yield return null;
            }
            _blinkSquash = to;
        }

        void TickGaze(float dt)
        {
            if (_rig == null || _rig.Pupils == null) return;

            _gazeTimer += dt;
            if (_gazeTimer >= _gazeGap)
            {
                _gazeTimer = 0f;
                _gazeGap = UnityEngine.Random.Range(GazeMinGap, GazeMaxGap);
                var r = UnityEngine.Random.insideUnitCircle;
                _gazeTarget = new Vector2(r.x * GazeRangeX, r.y * GazeRangeY) * _scale;
            }

            _gazeCur = Vector2.Lerp(_gazeCur, _gazeTarget, 1f - Mathf.Exp(-GazeSpeed * dt));
            _rig.Pupils.anchoredPosition = _pupilBase + _gazeCur;
        }

        void TickMouth(float dt)
        {
            if (!_speaking || _rig == null || _rig.Mouth == null) return;

            _mouthTimer += dt;
            if (_mouthTimer < _mouthGap) return;

            _mouthTimer = 0f;
            _mouthGap = UnityEngine.Random.Range(MouthMinGap, MouthMaxGap);
            ApplyMouth(!_mouthOpenNow);
        }

        void ApplyMouth(bool open)
        {
            _mouthOpenNow = open;
            if (_rig == null || _rig.Mouth == null) return;

            // 벌린 입과 다문 입은 크기가 달라서 위치까지 같이 옮겨야 합니다.
            var sprite = open ? _rig.MouthOpen : _rig.MouthClosed;
            if (sprite == null) { sprite = _rig.MouthClosed; open = false; _mouthOpenNow = false; }
            if (sprite == null) return;

            _rig.Mouth.sprite = sprite;
            var rt = _rig.Mouth.rectTransform;
            rt.anchoredPosition = open ? _rig.MouthOpenPos  : _rig.MouthClosedPos;
            rt.sizeDelta        = open ? _rig.MouthOpenSize : _rig.MouthClosedSize;
        }

        void Apply()
        {
            float breath = Mathf.Sin(_t * Mathf.PI * 2f / BreathPeriod);
            float swayA  = Mathf.Sin(_t * Mathf.PI * 2f / SwayPeriodA);
            float swayB  = Mathf.Sin(_t * Mathf.PI * 2f / SwayPeriodB + 1.7f);

            float x   = (swayA * 0.65f + swayB * 0.35f) * SwayX;
            float y   = breath * BreathRise;
            float sy  = 1f + breath * BreathStretch;
            float rot = swayB * SwayTilt;

            // 반응 모션 — 감쇠 사인. 한 번 튀고 제자리로 돌아옵니다.
            if (_reactT < _reactDur)
            {
                _reactT += Time.deltaTime;
                float u = _reactT / _reactDur;
                float env = Mathf.Exp(-_reactDamp * u) * Mathf.Sin(Mathf.PI * 2f * _reactFreq * u);
                y   += _reactY * env;
                sy  += _reactScale * env;
                rot += _reactRot * env;
            }

            var offset = new Vector2(x, y);
            var tilt = Quaternion.Euler(0f, 0f, rot);

            _body.anchoredPosition = _bodyBase + offset;
            _body.localScale = new Vector3(1f, sy, 1f);
            _body.localRotation = tilt;

            if (_face != null)
            {
                // 얼굴만 조금 더 밀어 얕은 깊이를 만듭니다. 회전까지 다르게 주면
                // 얼굴이 몸에서 떨어져 보여서, 기울기는 같이 갑니다.
                _face.anchoredPosition = _faceBase + offset * FaceParallax;
                _face.localScale = new Vector3(1f, sy, 1f);
                _face.localRotation = tilt;
            }

            if (_rig != null && _rig.EyeGroup != null)
                _rig.EyeGroup.localScale = new Vector3(1f, Mathf.Max(0.02f, _blinkSquash), 1f);
        }
    }
}
