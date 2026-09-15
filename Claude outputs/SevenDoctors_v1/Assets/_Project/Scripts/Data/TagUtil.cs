using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SevenDoctors.Data
{
    /// <summary>
    /// 기획자가 시트 '연출태그' 칸에 쓰는 [key] / [key:value] / [key:a:b] 형식을 해석합니다.
    /// 기획자가 외워야 할 문법은 이게 전부입니다.
    /// </summary>
    public static class TagUtil
    {
        static readonly Regex TagRegex = new Regex(@"\[([^\[\]]+)\]", RegexOptions.Compiled);

        public struct Tag
        {
            public string Key;    // flag, get, goto, puzzle, wait, shake, bgm, sfx, portrait, fade_in, fade_out
            public string Value;  // 첫 번째 인자 (없으면 빈 문자열)
            public string Value2; // 두 번째 인자 (portrait 처럼 인자가 둘인 경우)
        }

        public static List<Tag> ExtractTags(string raw)
        {
            var result = new List<Tag>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (Match m in TagRegex.Matches(raw))
            {
                var body = m.Groups[1].Value.Trim();
                if (body.Length == 0) continue;

                var parts = body.Split(':');
                result.Add(new Tag
                {
                    Key    = parts[0].Trim().ToLowerInvariant(),
                    Value  = parts.Length > 1 ? parts[1].Trim() : string.Empty,
                    Value2 = parts.Length > 2 ? parts[2].Trim() : string.Empty,
                });
            }

            return result;
        }

        /// <summary>대사 본문에 태그가 섞여 들어온 경우를 대비해 태그를 걷어냅니다.</summary>
        public static string StripTags(string raw)
        {
            return string.IsNullOrEmpty(raw) ? string.Empty : TagRegex.Replace(raw, string.Empty).Trim();
        }
    }
}
