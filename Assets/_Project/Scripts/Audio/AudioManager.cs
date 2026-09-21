using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SevenDoctors.Audio
{
    /// <summary>
    /// 소리 전부를 한 군데서 냅니다. BGM / 효과음 / 목소리 세 갈래입니다.
    ///
    /// 음원은 Resources/Audio 를 먼저 보고, 없으면 코드로 만들어 씁니다.
    /// 포트레이트가 부위 PNG 없으면 합본으로 돌아가는 것과 같은 구조입니다 —
    /// 사운드 담당이 파일을 넣기 시작하면 넣은 것부터 차례로 진짜 소리로 바뀌고,
    /// 그때 이 파일은 손대지 않아도 됩니다.
    ///
    ///   Resources/Audio/Bgm/{Rooms 탭 BGM 칸}
    ///   Resources/Audio/Sfx/{[sfx:키]}
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Range(0f, 1f)] public float BgmVolume   = 0.28f;
        [Range(0f, 1f)] public float SfxVolume   = 0.55f;
        [Range(0f, 1f)] public float VoiceVolume = 0.40f;

        public float CrossfadeSeconds = 1.1f;

        AudioSource _bgm, _sfx, _voice;
        AudioClip _blip;
        Coroutine _fade;

        string _bgmKey;
        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        void Awake()
        {
            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.volume = BgmVolume;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;

            _blip = AudioSynth.VoiceBlip("voice_blip");
        }

        // ── BGM ───────────────────────────────────────────────────────────────

        /// <summary>같은 곡이면 아무것도 하지 않습니다 — 방을 옮길 때마다 끊기면 안 됩니다.</summary>
        public void PlayBgm(string key)
        {
            if (string.IsNullOrEmpty(key)) { StopBgm(); return; }
            if (key == _bgmKey && _bgm.isPlaying) return;

            var clip = Resolve("Bgm", key);
            if (clip == null) return;

            _bgmKey = key;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossfadeTo(clip));
        }

        public void StopBgm()
        {
            _bgmKey = null;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossfadeTo(null));
        }

        IEnumerator CrossfadeTo(AudioClip next)
        {
            float from = _bgm.volume;

            if (_bgm.isPlaying)
            {
                for (float t = 0f; t < CrossfadeSeconds * 0.5f; t += Time.unscaledDeltaTime)
                {
                    _bgm.volume = Mathf.Lerp(from, 0f, t / (CrossfadeSeconds * 0.5f));
                    yield return null;
                }
                _bgm.Stop();
            }

            _bgm.volume = 0f;
            if (next == null) { _fade = null; yield break; }

            _bgm.clip = next;
            _bgm.Play();

            for (float t = 0f; t < CrossfadeSeconds * 0.5f; t += Time.unscaledDeltaTime)
            {
                _bgm.volume = Mathf.Lerp(0f, BgmVolume, t / (CrossfadeSeconds * 0.5f));
                yield return null;
            }
            _bgm.volume = BgmVolume;
            _fade = null;
        }

        // ── 효과음 ────────────────────────────────────────────────────────────

        public void PlaySfx(string key)
        {
            var clip = Resolve("Sfx", key);
            if (clip == null) return;

            _sfx.pitch = Random.Range(0.96f, 1.04f);   // 같은 소리가 반복돼도 덜 기계적입니다
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        // ── 목소리 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 글자 하나가 찍힐 때 나는 소리. 인물마다 기본 높이가 다르고,
        /// 글자마다 조금씩 흔들립니다.
        ///
        /// 인물 높이를 ID 해시로 뽑는 건, 시트에 인물이 늘어도 아무 설정 없이
        /// 저마다 다른 목소리를 갖게 하려는 것입니다.
        /// </summary>
        public void PlayVoice(char c, string speakerId)
        {
            if (_blip == null) return;

            float basePitch = SpeakerPitch(speakerId);
            float wobble = 1f + ((c % 7) - 3) * 0.045f;   // 글자에 따라 ±13% 남짓

            _voice.pitch = Mathf.Clamp(basePitch * wobble, 0.5f, 2.6f);
            _voice.PlayOneShot(_blip, VoiceVolume);
        }

        static float SpeakerPitch(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return 1f;

            int h = 0;
            foreach (char ch in speakerId) h = h * 31 + ch;
            h = Mathf.Abs(h);

            return 0.82f + (h % 100) / 100f * 0.75f;   // 0.82 ~ 1.57
        }

        /// <summary>말소리가 이어지는 도중에 화면이 닫히면 남은 소리를 끊습니다.</summary>
        public void StopVoice()
        {
            if (_voice != null) _voice.Stop();
        }

        // ── 음원 찾기 ─────────────────────────────────────────────────────────

        AudioClip Resolve(string folder, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            string cacheKey = folder + "/" + key;
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

            // 1) 진짜 음원이 들어와 있으면 그걸 씁니다.
            var clip = Resources.Load<AudioClip>($"Audio/{cacheKey}");

            // 2) 없으면 만들어 씁니다. 만들 줄 모르는 키는 조용히 넘어갑니다.
            if (clip == null) clip = Synthesize(folder, key);

            _cache[cacheKey] = clip;
            if (clip == null) Debug.Log($"[Audio] '{cacheKey}' 는 음원도 없고 만들 줄도 모릅니다. 건너뜁니다.");
            return clip;
        }

        static AudioClip Synthesize(string folder, string key)
        {
            if (folder == "Bgm")
            {
                switch (key)
                {
                    // 연구실 — 좁은 화음에 높은 배음. 깨끗하지만 불안합니다.
                    case "bgm_lab":
                        return AudioSynth.Pad(key, 98f, new[] { 1f, 1.5f, 2.25f, 3f }, 8f, 0.8f);

                    // 저택 — 단3도를 넣어 가라앉힙니다.
                    case "bgm_mansion":
                        return AudioSynth.Pad(key, 73.4f, new[] { 1f, 1.2f, 1.5f, 2f }, 8f, 0.35f);

                    // 진실 — 더 낮고, 반음이 부딪히게 둡니다.
                    case "bgm_truth":
                        return AudioSynth.Pad(key, 55f, new[] { 1f, 1.06f, 1.5f, 2.4f }, 8f, 0.25f);
                }
                return null;
            }

            if (folder == "Sfx")
            {
                switch (key)
                {
                    case "lever":  return AudioSynth.Sweep(key, 420f, 70f, 0.32f, 11f, 0.35f);
                    case "alarm":  return AudioSynth.Alarm(key, 880f, 660f, 0.17f, 6);
                    case "step":   return AudioSynth.Noise(key, 0.16f, 26f, 0.06f, 11);
                    case "unlock": return AudioSynth.Sweep(key, 180f, 1400f, 0.18f, 18f, 0.15f);
                }
                return null;
            }

            return null;
        }
    }
}
