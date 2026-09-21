using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenDoctors.Core
{
    public enum Language { Korean, English }

    /// <summary>
    /// 언어 설정과 문자열 조회.
    ///
    /// 번역을 시트 안에 같이 두는 쪽을 골랐습니다 — 컬럼을 하나 늘리는 것뿐이라
    /// 기획자가 원문 옆칸에 바로 적을 수 있고, 행이 어긋나 짝이 틀어질 일도 없습니다.
    /// (Dialogues 탭의 '대사' 옆에 '대사_en' 이 붙는 식)
    ///
    /// UI 에 박혀 있던 문구는 코드에서 빼서 UIStrings 탭으로 옮겼습니다.
    /// 화면에 보이는 한국어가 소스에 남아 있으면 번역이 반드시 새어 나갑니다.
    ///
    /// 번역이 비어 있으면 조용히 한국어로 돌아갑니다. 영어가 아직 안 들어온
    /// 행이 있어도 게임은 안 멈추고, 빠진 자리만 한국어로 보입니다.
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "sd.language";

        static readonly Dictionary<string, string[]> _ui = new Dictionary<string, string[]>();

        public static Language Current { get; private set; } = Language.Korean;

        /// <summary>언어가 바뀌었을 때. 이미 그려 둔 UI 를 다시 그리라는 신호입니다.</summary>
        public static event Action Changed;

        public static bool IsEnglish => Current == Language.English;

        // ── 설정 ──────────────────────────────────────────────────────────────

        /// <summary>저장된 언어를 불러옵니다. 없으면 시스템 언어로 한 번 추측합니다.</summary>
        public static void LoadSetting()
        {
            if (PlayerPrefs.HasKey(PrefKey))
            {
                Current = PlayerPrefs.GetInt(PrefKey, 0) == 1 ? Language.English : Language.Korean;
                return;
            }

            Current = Application.systemLanguage == SystemLanguage.Korean
                ? Language.Korean
                : Language.English;
        }

        public static void Set(Language language)
        {
            if (Current == language) return;

            Current = language;
            PlayerPrefs.SetInt(PrefKey, language == Language.English ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Toggle() => Set(IsEnglish ? Language.Korean : Language.English);

        /// <summary>언어 이름은 늘 그 언어로 씁니다 — 못 읽는 언어로 적혀 있으면 되돌릴 수가 없습니다.</summary>
        public static string NameOf(Language language) => language == Language.English ? "English" : "한국어";

        // ── 조회 ──────────────────────────────────────────────────────────────

        /// <summary>시트의 원문/번역 두 칸 중 지금 언어에 맞는 쪽. 번역이 비면 원문입니다.</summary>
        public static string Pick(string korean, string english)
            => IsEnglish && !string.IsNullOrEmpty(english) ? english : korean;

        /// <summary>UIStrings 탭의 문구. 없으면 fallback, 그것도 없으면 키를 그대로 돌려줍니다.</summary>
        public static string TOr(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key) && _ui.TryGetValue(key, out var pair))
            {
                var s = Pick(pair[0], pair[1]);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return fallback;
        }

        /// <summary>
        /// UIStrings 탭의 문구. 인자를 주면 string.Format 까지 합니다.
        /// 자리표시자가 인자와 안 맞아도 게임을 죽이지 않고 원문을 그대로 보여줍니다.
        /// </summary>
        public static string T(string key, params object[] args)
        {
            var format = TOr(key, key);
            if (args == null || args.Length == 0) return format;

            try { return string.Format(format, args); }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] '{key}' 의 자리표시자가 인자와 맞지 않습니다: \"{format}\"");
                return format;
            }
        }

        /// <summary>시트의 카테고리 값(인물·물건·기록)을 화면에 보일 말로. 없으면 원래 값 그대로.</summary>
        public static string Category(string category)
            => string.IsNullOrEmpty(category) ? category : TOr($"ui.category.{category}", category);

        // ── 적재 ──────────────────────────────────────────────────────────────

        public static void RegisterUiString(string key, string korean, string english)
        {
            if (string.IsNullOrEmpty(key)) return;

            // 여러 줄짜리 문구는 시트에서 따옴표로 묶여 오는데, 체크아웃 설정에 따라
            // 줄 끝에 \r 이 섞여 들어옵니다. UI 에 네모로 찍히므로 여기서 털어냅니다.
            _ui[key] = new[] { Clean(korean), Clean(english) };

            string Clean(string s) => string.IsNullOrEmpty(s) ? s : s.Replace("\r", "");
        }

        public static void ClearUiStrings() => _ui.Clear();

        public static int UiStringCount => _ui.Count;
    }
}
