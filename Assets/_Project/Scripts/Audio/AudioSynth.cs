using UnityEngine;

namespace SevenDoctors.Audio
{
    /// <summary>
    /// 소리를 코드로 만듭니다. 아트를 gen_art.py 로 뽑는 것과 같은 자리입니다 —
    /// 사운드 담당이 실제 음원을 넣기 전까지 게임이 '들리게' 하는 게 목적입니다.
    ///
    /// 파일을 두지 않은 이유는 저장소에 바이너리를 늘리지 않으려는 것도 있지만,
    /// 루프가 정확히 맞아떨어지기 때문입니다. 주파수를 1/길이 의 배수로 반올림하면
    /// 끝과 처음이 위상까지 이어져서, 이음매 없이 도는 루프가 공짜로 나옵니다.
    /// (음원 파일로 이걸 맞추려면 편집에서 꽤 고생합니다)
    ///
    /// 실제 음원이 Resources/Audio 에 들어오면 AudioManager 가 그쪽을 먼저 씁니다.
    /// </summary>
    public static class AudioSynth
    {
        public const int SampleRate = 44100;

        // ── 뼈대 ──────────────────────────────────────────────────────────────

        /// <summary>루프 길이에 딱 떨어지는 주파수로 맞춥니다. 이음매가 사라집니다.</summary>
        static float Seamless(float hz, float seconds)
        {
            float cycles = Mathf.Max(1f, Mathf.Round(hz * seconds));
            return cycles / seconds;
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] data, float peak = 0.85f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 0.0001f) return;

            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        /// <summary>사인 부분음 하나를 통째로 더합니다. 진폭은 느린 LFO 로 숨 쉬게 합니다.</summary>
        static void AddPartial(float[] data, float hz, float amp, float seconds,
                               float lfoHz = 0f, float lfoDepth = 0f, float phase = 0f)
        {
            hz = Seamless(hz, seconds);
            if (lfoHz > 0f) lfoHz = Seamless(lfoHz, seconds);

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float a = amp;
                if (lfoHz > 0f) a *= 1f - lfoDepth + lfoDepth * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * lfoHz * t));
                data[i] += a * Mathf.Sin(2f * Mathf.PI * hz * t + phase);
            }
        }

        // ── BGM ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 방 분위기용 패드. 화음 하나를 길게 늘이고 진폭만 흔듭니다.
        /// 멜로디를 넣지 않은 건 의도입니다 — 플레이스홀더 멜로디는 몇 분만 들어도
        /// 거슬리는데, 패드는 배경으로 가라앉습니다.
        /// </summary>
        public static AudioClip Pad(string name, float rootHz, float[] intervals,
                                    float seconds = 8f, float brightness = 0.5f)
        {
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];

            for (int n = 0; n < intervals.Length; n++)
            {
                float hz = rootHz * intervals[n];
                float amp = 0.5f / (n + 1.4f);

                // 음마다 다른 속도로 흔들어야 화음이 고여 있지 않고 움직입니다.
                AddPartial(data, hz, amp, seconds, 0.07f + n * 0.031f, 0.45f, n * 1.1f);
                AddPartial(data, hz * 2f, amp * brightness * 0.35f, seconds, 0.05f + n * 0.023f, 0.6f, n * 2.3f);
            }

            Normalize(data, 0.7f);
            return Make(name, data);
        }

        // ── 효과음 ────────────────────────────────────────────────────────────

        /// <summary>짧은 잡음 버스트. 발소리·철컥 같은 '부딪히는' 소리의 뼈대입니다.</summary>
        public static AudioClip Noise(string name, float seconds, float decay,
                                      float lowPass = 1f, int seed = 7)
        {
            var rng = new System.Random(seed);
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];

            float last = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float raw = (float)(rng.NextDouble() * 2.0 - 1.0);

                // 1차 저역통과 — 값이 작을수록 둔탁해집니다.
                last += (raw - last) * lowPass;
                data[i] = last * Mathf.Exp(-decay * t);
            }

            Normalize(data);
            return Make(name, data);
        }

        /// <summary>내려가거나 올라가는 톤. 레버·잠금 해제처럼 '움직이는' 소리에 씁니다.</summary>
        public static AudioClip Sweep(string name, float fromHz, float toHz,
                                      float seconds, float decay, float square = 0f)
        {
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];

            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                float u = t / seconds;
                float hz = Mathf.Lerp(fromHz, toHz, u * u);   // 처음에 빠르게 움직입니다

                phase += 2f * Mathf.PI * hz / SampleRate;
                float s = Mathf.Sin(phase);
                if (square > 0f) s = Mathf.Lerp(s, Mathf.Sign(s), square);

                data[i] = s * Mathf.Exp(-decay * t);
            }

            Normalize(data);
            return Make(name, data);
        }

        /// <summary>두 음을 번갈아 내는 경보음.</summary>
        public static AudioClip Alarm(string name, float hzA, float hzB, float beep, int repeats)
        {
            float seconds = beep * repeats;
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;
                int step = Mathf.FloorToInt(t / beep);
                float hz = (step % 2 == 0) ? hzA : hzB;

                // 각 삑 소리마다 앞뒤를 짧게 눕혀서 딱딱 끊기는 소리를 없앱니다.
                float local = (t - step * beep) / beep;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(local));

                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.9f;
            }

            Normalize(data);
            return Make(name, data);
        }

        // ── 목소리 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 글자 하나가 찍힐 때 나는 짧은 소리. 재생할 때 pitch 만 바꿔서
        /// 글자마다·인물마다 다르게 들리게 합니다 — 동물의 숲이 쓰는 수법입니다.
        ///
        /// 사인만 쓰면 '삐' 소리라 기계 같습니다. 배음을 얹고 포먼트처럼 한쪽을
        /// 키워야 사람이 웅얼거리는 느낌이 납니다.
        /// </summary>
        public static AudioClip VoiceBlip(string name, float baseHz = 440f, float seconds = 0.085f)
        {
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / SampleRate;

                // 살짝 떨어지는 피치 — 말끝이 내려가는 느낌을 줍니다.
                float hz = baseHz * Mathf.Lerp(1.08f, 0.92f, t / seconds);

                float s  = Mathf.Sin(2f * Mathf.PI * hz * t)             * 1.00f;
                s       += Mathf.Sin(2f * Mathf.PI * hz * 2f * t)        * 0.45f;
                s       += Mathf.Sin(2f * Mathf.PI * hz * 3f * t)        * 0.22f;
                s       += Mathf.Sin(2f * Mathf.PI * hz * 4.7f * t)      * 0.12f;  // 비정수배 — 목소리처럼 탁해집니다

                // 빠르게 열고 천천히 닫습니다.
                float attack = Mathf.Clamp01(t / 0.006f);
                float decay  = Mathf.Exp(-26f * t);
                data[i] = s * attack * decay;
            }

            Normalize(data);
            return Make(name, data);
        }
    }
}
