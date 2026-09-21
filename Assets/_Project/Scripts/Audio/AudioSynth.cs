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
        /// 모음 하나. 앞의 두 수(F1, F2)가 어느 모음으로 들리는지를 거의 다 정합니다.
        /// 남성 성인 기준값입니다 — 재생할 때 pitch 를 올리면 여성·아이 쪽으로 갑니다.
        /// </summary>
        public struct Vowel
        {
            public string Name;
            public float F1, F2, F3;
            public Vowel(string name, float f1, float f2, float f3) { Name = name; F1 = f1; F2 = f2; F3 = f3; }
        }

        public static readonly Vowel[] Vowels =
        {
            new Vowel("a", 730f, 1090f, 2440f),
            new Vowel("e", 530f, 1840f, 2480f),
            new Vowel("i", 270f, 2290f, 3010f),
            new Vowel("o", 570f,  840f, 2410f),
            new Vowel("u", 300f,  870f, 2240f),
        };

        /// <summary>
        /// 글자 하나가 찍힐 때 나는 짧은 소리.
        ///
        /// 사람 목소리는 배음을 쌓는다고 나오지 않습니다. 성대가 내는 톱니 같은
        /// 파형을 목과 입이 공명으로 걸러 내는 구조라, 그 공명점(포먼트)을 흉내내야
        /// 비로소 '아/에/오' 로 들립니다. 사인만 더하면 아무리 쌓아도 삐 소리입니다.
        ///
        /// 그래서 톱니파를 만든 뒤 2극 공명기 세 개에 통과시킵니다. F1·F2 가 모음을
        /// 정하고 F3 는 사람 목소리다운 윤기만 얹습니다.
        ///
        /// 글자마다 다른 모음을 고르고 인물마다 pitch 를 달리하면, 짧은 소리 몇 개로
        /// 웅얼거리는 말처럼 들립니다 — 동물의 숲이 쓰는 수법입니다.
        /// </summary>
        public static AudioClip VoiceBlip(string name, Vowel vowel, float f0 = 138f, float seconds = 0.10f)
        {
            int n = Mathf.RoundToInt(SampleRate * seconds);
            var source = new float[n];

            // 성대 파형 — 톱니. 말끝이 살짝 내려가게 기본 주파수를 떨어뜨립니다.
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float hz = f0 * Mathf.Lerp(1.06f, 0.94f, u);

                phase += hz / SampleRate;
                if (phase >= 1f) phase -= 1f;

                // 톱니를 그대로 쓰면 지나치게 쨍합니다. 살짝 둥글려서 성대에 가깝게.
                float saw = 2f * phase - 1f;
                source[i] = saw - 0.35f * saw * saw * saw;
            }

            var data = new float[n];
            AddFormant(data, source, vowel.F1,  80f, 1.00f);
            AddFormant(data, source, vowel.F2, 110f, 0.50f);
            AddFormant(data, source, vowel.F3, 160f, 0.20f);

            // 빠르게 열고 천천히 닫습니다. 닫는 쪽이 급하면 딱딱 끊겨 들립니다.
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.Clamp01(t / 0.008f);
                float decay  = Mathf.Exp(-22f * t);
                data[i] *= attack * decay;
            }

            Normalize(data);
            return Make(name, data);
        }

        /// <summary>
        /// 2극 공명기. 입과 목이 특정 높이만 키워 주는 걸 흉내냅니다.
        /// bandwidth 가 좁을수록 그 높이가 뚜렷해지고, 목소리는 더 또렷해집니다.
        /// </summary>
        static void AddFormant(float[] dst, float[] source, float freq, float bandwidth, float gain)
        {
            float r = Mathf.Exp(-Mathf.PI * bandwidth / SampleRate);
            float theta = 2f * Mathf.PI * freq / SampleRate;
            float c = 2f * r * Mathf.Cos(theta);
            float rr = r * r;

            // 공명기는 그냥 두면 이득이 폭주합니다. 입력 쪽에서 미리 줄여 둡니다.
            float norm = (1f - r) * Mathf.Sqrt(1f - 2f * r * Mathf.Cos(2f * theta) + rr);

            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < source.Length; i++)
            {
                float y = source[i] * norm + c * y1 - rr * y2;
                y2 = y1;
                y1 = y;
                dst[i] += y * gain;
            }
        }
    }
}
