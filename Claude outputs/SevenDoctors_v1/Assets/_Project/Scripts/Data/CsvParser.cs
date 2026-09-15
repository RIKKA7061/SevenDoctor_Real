using System.Collections.Generic;
using System.Text;

namespace SevenDoctors.Data
{
    /// <summary>
    /// RFC4180 기반 CSV 파서. 따옴표 안의 쉼표·줄바꿈·이스케이프("")를 모두 처리합니다.
    /// 구글 시트의 gviz CSV 출력이 이 형식이므로 그대로 먹습니다.
    /// </summary>
    public static class CsvParser
    {
        /// <summary>CSV 텍스트를 행 단위 문자열 배열로 분해합니다.</summary>
        public static List<List<string>> ParseRaw(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            // BOM 제거
            if (text[0] == '﻿') text = text.Substring(1);

            var row = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(sb.ToString()); sb.Clear();
                        break;
                    case '\r':
                        break; // \r\n 의 \r 는 버림
                    case '\n':
                        row.Add(sb.ToString()); sb.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            // 마지막 행 마무리
            if (sb.Length > 0 || row.Count > 0)
            {
                row.Add(sb.ToString());
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// 1행을 헤더로 삼아 [헤더 → 값] 딕셔너리 목록을 만듭니다.
        /// 완전히 빈 행과, 첫 칸이 '#'로 시작하는 주석 행은 건너뜁니다.
        /// </summary>
        public static List<Dictionary<string, string>> Parse(string text)
        {
            var result = new List<Dictionary<string, string>>();
            var raw = ParseRaw(text);
            if (raw.Count < 1) return result;

            var headers = new List<string>();
            foreach (var h in raw[0]) headers.Add((h ?? string.Empty).Trim());

            for (int r = 1; r < raw.Count; r++)
            {
                var cells = raw[r];

                bool allEmpty = true;
                foreach (var c in cells)
                {
                    if (!string.IsNullOrWhiteSpace(c)) { allEmpty = false; break; }
                }
                if (allEmpty) continue;

                if (cells.Count > 0 && cells[0] != null && cells[0].TrimStart().StartsWith("#")) continue;

                var dict = new Dictionary<string, string>();
                for (int c = 0; c < headers.Count; c++)
                {
                    if (string.IsNullOrEmpty(headers[c])) continue;
                    dict[headers[c]] = c < cells.Count ? (cells[c] ?? string.Empty).Trim() : string.Empty;
                }
                result.Add(dict);
            }

            return result;
        }

        // ── 안전한 값 꺼내기 헬퍼 ──────────────────────────────────────────────
        public static string Str(Dictionary<string, string> row, string key, string fallback = "")
        {
            return row != null && row.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        public static int Int(Dictionary<string, string> row, string key, int fallback = 0)
        {
            var s = Str(row, key);
            return int.TryParse(s, out var v) ? v : fallback;
        }

        public static float Float(Dictionary<string, string> row, string key, float fallback = 0f)
        {
            var s = Str(row, key);
            return float.TryParse(s, out var v) ? v : fallback;
        }

        public static bool Bool(Dictionary<string, string> row, string key, bool fallback = false)
        {
            var s = Str(row, key).ToUpperInvariant();
            if (s == "TRUE" || s == "O" || s == "Y" || s == "YES" || s == "1") return true;
            if (s == "FALSE" || s == "X" || s == "N" || s == "NO" || s == "0") return false;
            return fallback;
        }
    }
}
