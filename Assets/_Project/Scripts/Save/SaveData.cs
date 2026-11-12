using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// 이 파일은 일부러 UnityEngine 을 쓰지 않습니다.
// 저장 → 읽기 → 같은 상태인지를 유니티 없이 돌려 볼 수 있어야, 에디터를 띄우지 않고도
// 세이브 형식을 고친 뒤 바로 확인할 수 있습니다. 로그도 여기서 찍지 않고 호출한 쪽(SaveDirector)에 맡깁니다.

namespace SevenDoctors.Save
{
    /// <summary>
    /// 세이브 파일 한 개에 들어가는 것 전부.
    ///
    /// 이어 하기에 필요한 건 이 넷뿐입니다 — 켜진 플래그, 가진 증거, 지금 방, 써 버린 1회성 핫스팟.
    /// 대사 줄 번호나 퍼즐 입력 같은 '도중' 상태는 담지 않습니다. 저장을 탐색 상태에서만 하기 때문에
    /// 그런 게 있을 수가 없고, 그래서 불러온 직후는 늘 방 안에 서 있는 상태입니다.
    ///
    /// 언어는 여기 없습니다. 플레이 진행이 아니라 기기 설정이라 PlayerPrefs(Loc)에 따로 있고,
    /// 새 게임을 시작해도 남아야 합니다. 도우미 로봇의 힌트 단계도 없습니다 — 이야기가 한 칸
    /// 나아갈 때마다 0 으로 돌아가는 값이라 다시 켰을 때 처음부터 세는 게 맞습니다.
    /// </summary>
    public class SaveData
    {
        /// <summary>
        /// 형식이 바뀌면(칸의 뜻이 달라지거나 없으면 안 되는 칸이 생기면) 1 올리고
        /// SaveJson.Migrate 에 옛 번호를 새 형식으로 옮기는 코드를 넣습니다.
        /// 칸을 '더하기만' 했으면 올릴 필요가 없습니다 — 모르는 칸은 읽을 때 건너뜁니다.
        /// </summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;

        /// <summary>저장한 시각(UTC, ISO 8601). 게임은 안 읽습니다 — 파일을 열어 본 사람을 위한 것입니다.</summary>
        public string SavedAt;

        public string RoomId;

        public List<string> Flags = new List<string>();

        /// <summary>얻은 순서 그대로. 노트가 이 순서로 보여 주므로 정렬하면 안 됩니다.</summary>
        public List<string> Evidence = new List<string>();

        public List<string> Consumed = new List<string>();

        /// <summary>
        /// 진행 상황이 같은지. 저장 시각은 보지 않습니다 —
        /// 달라진 게 없는데 디스크에 다시 쓰는 일을 막으려고 비교하는 것이라서요.
        /// 플래그와 소모 핫스팟은 집합이라 순서를 안 보고, 증거는 순서까지 봅니다.
        /// </summary>
        public bool SameProgressAs(SaveData other)
        {
            if (other == null) return false;
            if (!string.Equals(RoomId, other.RoomId, StringComparison.Ordinal)) return false;
            if (!SameSet(Flags, other.Flags) || !SameSet(Consumed, other.Consumed)) return false;

            if (Evidence.Count != other.Evidence.Count) return false;
            for (int i = 0; i < Evidence.Count; i++)
                if (!string.Equals(Evidence[i], other.Evidence[i], StringComparison.Ordinal)) return false;
            return true;
        }

        static bool SameSet(List<string> a, List<string> b)
        {
            var set = new HashSet<string>(a, StringComparer.Ordinal);
            return set.SetEquals(b);
        }
    }

    /// <summary>
    /// SaveData ↔ JSON 글자.
    ///
    /// JsonUtility 를 안 쓰고 손으로 읽고 쓰는 이유는 둘입니다. 하나는 위에 적은 대로 유니티 없이
    /// 검사하려는 것이고, 다른 하나는 깨진 파일을 다루기 위해서입니다 — JsonUtility 는 칸이 빠졌거나
    /// 형이 틀려도 조용히 기본값을 채워 넣어서, 반쯤 망가진 파일이 '빈 진행 상황' 으로 읽힙니다.
    /// 여기서는 이상하면 이상하다고 말하고, 그 파일은 쓰지 않습니다.
    /// </summary>
    public static class SaveJson
    {
        public static string Write(SaveData data)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(data.Version.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"savedAt\": "); WriteString(sb, data.SavedAt ?? ""); sb.Append(",\n");
            sb.Append("  \"room\": "); WriteString(sb, data.RoomId ?? ""); sb.Append(",\n");
            sb.Append("  \"flags\": "); WriteList(sb, data.Flags); sb.Append(",\n");
            sb.Append("  \"evidence\": "); WriteList(sb, data.Evidence); sb.Append(",\n");
            sb.Append("  \"consumed\": "); WriteList(sb, data.Consumed); sb.Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// 읽기. 쓸 수 없는 파일이면 false 와 함께 이유를 돌려줍니다. 예외는 밖으로 안 나갑니다 —
        /// 세이브 파일은 사용자가 손댈 수 있는 유일한 입력이라, 무엇이 들어 있어도 게임이 멈추면 안 됩니다.
        /// </summary>
        public static bool TryRead(string json, out SaveData data, out string error)
        {
            data = null;
            error = null;

            try
            {
                if (string.IsNullOrWhiteSpace(json)) { error = "파일이 비어 있습니다"; return false; }

                var parser = new Parser(json);
                var root = parser.ParseDocument() as Dictionary<string, object>;
                if (root == null) { error = "맨 바깥이 { } 가 아닙니다"; return false; }

                if (!root.TryGetValue("version", out var v) || !(v is double) || (double)v != Math.Floor((double)v))
                { error = "version 칸이 없거나 숫자가 아닙니다"; return false; }

                int version = (int)(double)v;
                if (version > SaveData.CurrentVersion)
                {
                    // 더 새 빌드가 쓴 파일입니다. 아는 칸만 골라 읽으면 뜻이 달라졌을 수 있으니 손대지 않습니다.
                    error = $"이 빌드보다 새 형식입니다 (파일 v{version}, 이 빌드 v{SaveData.CurrentVersion})";
                    return false;
                }
                if (version < 1) { error = $"알 수 없는 형식 번호입니다 (v{version})"; return false; }

                if (version < SaveData.CurrentVersion && !Migrate(root, version, out error)) return false;

                var result = new SaveData { Version = SaveData.CurrentVersion };

                if (!root.TryGetValue("room", out var room) || !(room is string) || ((string)room).Length == 0)
                { error = "room 칸이 없습니다"; return false; }
                result.RoomId = (string)room;

                if (root.TryGetValue("savedAt", out var at) && at is string) result.SavedAt = (string)at;

                if (!ReadList(root, "flags", result.Flags, out error)) return false;
                if (!ReadList(root, "evidence", result.Evidence, out error)) return false;
                if (!ReadList(root, "consumed", result.Consumed, out error)) return false;

                data = result;
                return true;
            }
            catch (Exception e)
            {
                // FormatException(문법) 말고도 무엇이든. 여기서 새어 나가면 시작 화면이 안 뜹니다.
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// 옛 형식을 지금 형식으로 옮깁니다. 지금은 v1 이 첫 형식이라 옮길 것이 없습니다.
        /// 형식을 바꾸게 되면 여기에 "if (from == 1) { root 를 v2 모양으로 고침; from = 2; }" 를
        /// 차례로 쌓으세요 — 그러면 아주 오래된 파일도 한 단계씩 밟아 올라옵니다.
        /// </summary>
        static bool Migrate(Dictionary<string, object> root, int from, out string error)
        {
            error = $"v{from} 에서 올리는 방법을 모릅니다";
            return false;
        }

        // ── 쓰기 ──────────────────────────────────────────────────────────────

        static void WriteList(StringBuilder sb, List<string> items)
        {
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                WriteString(sb, items[i] ?? "");
            }
            sb.Append(']');
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ── 읽기 ──────────────────────────────────────────────────────────────

        /// <summary>칸이 없으면 빈 목록(나중에 더한 칸일 수 있음). 있는데 문자열 배열이 아니면 깨진 파일입니다.</summary>
        static bool ReadList(Dictionary<string, object> root, string key, List<string> into, out string error)
        {
            error = null;
            if (!root.TryGetValue(key, out var value) || value == null) return true;

            var list = value as List<object>;
            if (list == null) { error = $"{key} 칸이 목록이 아닙니다"; return false; }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var s = item as string;
                if (s == null) { error = $"{key} 칸에 글자가 아닌 값이 있습니다"; return false; }
                if (s.Length > 0 && seen.Add(s)) into.Add(s);   // 빈 값·중복은 버립니다
            }
            return true;
        }

        /// <summary>
        /// 아주 작은 JSON 읽기. 객체는 Dictionary, 배열은 List, 숫자는 double 로 돌려줍니다.
        /// 문법이 틀리면 FormatException 을 던지고, TryRead 가 그걸 '깨진 파일' 로 바꿉니다.
        /// </summary>
        sealed class Parser
        {
            // 세이브 파일은 두 겹(객체 안의 배열)뿐입니다. 누가 "[[[[…" 를 만 겹 넣어 둔 파일로
            // 스택을 터뜨리지 못하게 얕게 막아 둡니다.
            const int MaxDepth = 16;

            readonly string _s;
            int _i;

            public Parser(string s) { _s = s; }

            public object ParseDocument()
            {
                // 메모장으로 저장하면 맨 앞에 BOM 이 붙습니다.
                if (_s.Length > 0 && _s[0] == '﻿') _i = 1;

                var value = ParseValue(0);
                SkipSpace();
                if (_i != _s.Length) throw Fail("끝난 뒤에 글자가 더 있습니다");
                return value;
            }

            object ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Fail("너무 깊이 겹쳐 있습니다");
                SkipSpace();
                if (_i >= _s.Length) throw Fail("값이 와야 할 자리에서 파일이 끝났습니다");

                char c = _s[_i];
                if (c == '{') return ParseObject(depth);
                if (c == '[') return ParseArray(depth);
                if (c == '"') return ParseString();
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                if (TryWord("true")) return true;
                if (TryWord("false")) return false;
                if (TryWord("null")) return null;
                throw Fail($"알 수 없는 글자 '{c}'");
            }

            Dictionary<string, object> ParseObject(int depth)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; // {
                SkipSpace();
                if (Peek() == '}') { _i++; return result; }

                while (true)
                {
                    SkipSpace();
                    if (Peek() != '"') throw Fail("칸 이름이 와야 합니다");
                    string key = ParseString();
                    SkipSpace();
                    if (Peek() != ':') throw Fail("':' 가 빠졌습니다");
                    _i++;
                    result[key] = ParseValue(depth + 1);
                    SkipSpace();

                    char c = Peek();
                    _i++;
                    if (c == '}') return result;
                    if (c != ',') throw Fail("',' 나 '}' 가 와야 합니다");
                }
            }

            List<object> ParseArray(int depth)
            {
                var result = new List<object>();
                _i++; // [
                SkipSpace();
                if (Peek() == ']') { _i++; return result; }

                while (true)
                {
                    result.Add(ParseValue(depth + 1));
                    SkipSpace();

                    char c = Peek();
                    _i++;
                    if (c == ']') return result;
                    if (c != ',') throw Fail("',' 나 ']' 가 와야 합니다");
                }
            }

            string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // 여는 따옴표

                while (true)
                {
                    if (_i >= _s.Length) throw Fail("따옴표가 닫히지 않았습니다");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (_i >= _s.Length) throw Fail("'\\' 뒤에서 파일이 끝났습니다");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"':  sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/'); break;
                        case 'b':  sb.Append('\b'); break;
                        case 'f':  sb.Append('\f'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length ||
                                !int.TryParse(_s.Substring(_i, 4), NumberStyles.AllowHexSpecifier,
                                              CultureInfo.InvariantCulture, out int code))
                                throw Fail("\\u 뒤에 16진수 네 자리가 와야 합니다");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Fail($"알 수 없는 이스케이프 '\\{e}'");
                    }
                }
            }

            object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;

                if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float,
                                     CultureInfo.InvariantCulture, out double n))
                    throw Fail("숫자를 읽을 수 없습니다");
                return n;
            }

            bool TryWord(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) return false;
                _i += word.Length;
                return true;
            }

            /// <summary>지금 글자. 파일이 끝났으면 '\0' — 어떤 기대값과도 안 맞아서 호출한 쪽이 오류를 냅니다.</summary>
            char Peek() => _i < _s.Length ? _s[_i] : '\0';

            void SkipSpace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            FormatException Fail(string what) => new FormatException($"{what} (위치 {_i})");
        }
    }

    public enum SaveLoadResult
    {
        /// <summary>세이브 파일이 없습니다. 처음 켰거나, 엔딩을 봤거나, 새 게임을 고른 뒤입니다.</summary>
        None,
        Ok,
        /// <summary>파일은 있는데 못 씁니다 (깨짐·더 새 형식·읽기 실패). 새 게임만 됩니다.</summary>
        Unusable,
    }

    /// <summary>
    /// 세이브 파일을 디스크에 두고 꺼내는 일. 폴더만 받으므로 유니티 밖에서도 돕니다.
    ///
    /// 슬롯은 하나입니다. 이 게임은 갈림길이 엔딩 선택 하나뿐인 외길이고 죽거나 막히는 일도 없어서,
    /// 되돌아갈 지점을 여러 개 둘 이유가 없습니다. 저장은 전부 자동이라 슬롯을 고르는 화면도 필요 없습니다.
    /// </summary>
    public class SaveStore
    {
        public const string FileName = "save.json";

        readonly string _directory;

        public SaveStore(string directory) { _directory = directory; }

        public string FilePath => Path.Combine(_directory, FileName);

        // 쓰는 도중에 꺼지면 반쯤 쓰인 파일이 남습니다. 그래서 옆에 다 쓴 뒤에 이름을 바꿉니다.
        string TempPath => FilePath + ".tmp";

        /// <summary>
        /// 못 쓰는 파일을 치워 두는 자리. 지우지 않는 건 버그 신고를 받았을 때 들여다볼 수 있게,
        /// 그리고 더 새 빌드의 세이브를 옛 빌드가 날려 버리지 않게 하려는 것입니다. 한 개만 남깁니다.
        /// </summary>
        public string BadFilePath => Path.Combine(_directory, "save.bad.json");

        public SaveLoadResult Load(out SaveData data, out string message)
        {
            data = null;
            message = null;

            string json;
            try
            {
                // 이름을 바꾸기 직전에 꺼졌다면 다 쓴 임시 파일만 남아 있습니다. 그게 최신입니다.
                if (!File.Exists(FilePath) && File.Exists(TempPath)) File.Move(TempPath, FilePath);

                if (!File.Exists(FilePath)) return SaveLoadResult.None;
                json = File.ReadAllText(FilePath, Encoding.UTF8);
            }
            catch (Exception e)
            {
                // 권한·잠금 같은 문제. 파일 내용은 멀쩡할 수 있으니 치우지 않고 그대로 둡니다.
                message = "세이브 파일을 열 수 없습니다: " + e.Message;
                return SaveLoadResult.Unusable;
            }

            if (SaveJson.TryRead(json, out data, out string error)) return SaveLoadResult.Ok;

            message = "세이브 파일을 읽을 수 없습니다: " + error;

            // 치워 두지 않으면 켤 때마다 같은 경고가 뜨고, 곧 자동 저장이 그 위를 덮어써서 흔적도 사라집니다.
            try
            {
                if (File.Exists(BadFilePath)) File.Delete(BadFilePath);
                File.Move(FilePath, BadFilePath);
                message += $" — {Path.GetFileName(BadFilePath)} 로 옮겨 두었습니다";
            }
            catch (Exception e)
            {
                message += " (옮겨 두지도 못했습니다: " + e.Message + ")";
            }

            return SaveLoadResult.Unusable;
        }

        public bool Save(SaveData data, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(TempPath, SaveJson.Write(data), new UTF8Encoding(false));

                // File.Replace 는 플랫폼마다 동작이 달라서 지우고 옮깁니다. 그 사이에 꺼지면
                // 임시 파일만 남는데, Load 가 그걸 주워 씁니다.
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(TempPath, FilePath);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>세이브를 지웁니다. 치워 둔 못 쓰는 파일(save.bad.json)은 건드리지 않습니다.</summary>
        public bool Delete(out string error)
        {
            error = null;
            try
            {
                if (File.Exists(TempPath)) File.Delete(TempPath);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
