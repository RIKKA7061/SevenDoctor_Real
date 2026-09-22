using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SevenDoctors.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SevenDoctors.EditorTools
{
    /// <summary>
    /// 구글 스프레드시트 → Assets/_Project/Resources/Data/*.csv 다운로더.
    ///
    /// 런타임에 구글 시트를 직접 때리지 않는 이유:
    ///   · 빌드본이 네트워크에 의존하게 되고, 오프라인이면 게임이 안 켜집니다.
    ///   · 구글이 응답을 늦게 주면 로딩이 통째로 멈춥니다.
    ///   · 발표/시연 중 네트워크 사고는 반드시 일어납니다.
    /// 대신 기획자가 시트를 고치고 이 창에서 버튼 한 번 누르면 즉시 반영됩니다.
    /// 실질적으로는 '실시간 연동'과 같고, 리스크만 없습니다.
    ///
    /// 시트 공유 설정: '링크가 있는 모든 사용자 - 뷰어' 로 해두어야 다운로드됩니다.
    /// </summary>
    public class SheetImporterWindow : EditorWindow
    {
        const string PrefKeySheetId = "SevenDoctors.SheetId";
        const string DataFolder = "Assets/_Project/Resources/Data";

        static readonly string[] Tabs =
        {
            "Characters", "Rooms", "Hotspots", "Dialogues", "Choices",
            "Evidence", "AskTopics", "Puzzles", "DeductionSlots", "Flags", "Endings",
            "Hints",       // 도우미 로봇이 읽어 주는 힌트
            "UIStrings",   // 화면에 박혀 있던 UI 문구. string_id / ko / en
        };

        string _sheetId = "";
        Vector2 _scroll;
        string _log = "";

        [MenuItem("Tools/일곱 박사/시트 가져오기 %#i", false, 0)]
        public static void Open()
        {
            var window = GetWindow<SheetImporterWindow>("시트 가져오기");
            window.minSize = new Vector2(460, 420);
            window.Show();
        }

        void OnEnable() => _sheetId = EditorPrefs.GetString(PrefKeySheetId, "");

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("구글 시트 연동", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "시트 주소가\n" +
                "https://docs.google.com/spreadsheets/d/AAAA_BBBB_CCCC/edit\n" +
                "라면 AAAA_BBBB_CCCC 부분만 넣으세요.\n\n" +
                "시트 공유는 '링크가 있는 모든 사용자 - 뷰어'여야 합니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _sheetId = EditorGUILayout.TextField("스프레드시트 ID", _sheetId);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefKeySheetId, _sheetId.Trim());

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_sheetId)))
            {
                if (GUILayout.Button("시트 전부 가져오기", GUILayout.Height(38)))
                    ImportAll(_sheetId.Trim());
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("데이터 검사만 실행", GUILayout.Height(28)))
                ValidateOnly();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ── 가져오기 ──────────────────────────────────────────────────────────

        void ImportAll(string sheetId)
        {
            Directory.CreateDirectory(DataFolder);

            var log = new StringBuilder();
            int ok = 0, fail = 0;

            try
            {
                for (int i = 0; i < Tabs.Length; i++)
                {
                    string tab = Tabs[i];
                    EditorUtility.DisplayProgressBar("시트 가져오기", $"{tab} …", (float)i / Tabs.Length);

                    string url = $"https://docs.google.com/spreadsheets/d/{sheetId}/gviz/tq?tqx=out:csv&sheet={UnityWebRequest.EscapeURL(tab)}";

                    if (TryDownload(url, out string csv, out string error))
                    {
                        // gviz 는 탭이 없어도 200 으로 HTML 오류 페이지를 주는 경우가 있습니다.
                        if (csv.TrimStart().StartsWith("<"))
                        {
                            log.AppendLine($"✗ {tab} — 탭을 찾지 못했습니다. 시트 탭 이름을 확인하세요.");
                            fail++;
                            continue;
                        }

                        File.WriteAllText(Path.Combine(DataFolder, tab + ".csv"), csv, new UTF8Encoding(true));
                        int rows = Mathf.Max(0, CsvParser.Parse(csv).Count);
                        log.AppendLine($"✓ {tab} — {rows}행");
                        ok++;
                    }
                    else
                    {
                        log.AppendLine($"✗ {tab} — {error}");
                        fail++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            log.AppendLine();
            log.AppendLine($"완료: 성공 {ok} / 실패 {fail}");
            log.AppendLine();
            log.Append(RunValidation());

            _log = log.ToString();
            Debug.Log("[시트 가져오기]\n" + _log);
        }

        static bool TryDownload(string url, out string text, out string error)
        {
            text = null; error = null;

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 20;
                var operation = request.SendWebRequest();

                // 에디터 메뉴에서 도는 동기 작업이라 스핀 대기로 충분합니다.
                while (!operation.isDone)
                    System.Threading.Thread.Sleep(16);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    error = request.error;
                    return false;
                }

                text = request.downloadHandler.text;
                return true;
            }
        }

        // ── 검사 ─────────────────────────────────────────────────────────────

        void ValidateOnly()
        {
            _log = RunValidation();
            Debug.Log("[데이터 검사]\n" + _log);
        }

        static string RunValidation()
        {
            var db = GameDatabase.Load();
            var errors = db.Validate();

            var sb = new StringBuilder();
            sb.AppendLine("── 데이터 검사 ──");
            sb.AppendLine(db.Summary());
            sb.AppendLine();

            if (errors.Count == 0)
            {
                sb.AppendLine("깨진 ID 참조 없음. 바로 플레이해도 됩니다.");
            }
            else
            {
                sb.AppendLine($"문제 {errors.Count}건:");
                foreach (var e in errors) sb.AppendLine("  · " + e);
            }

            return sb.ToString();
        }

        [MenuItem("Tools/일곱 박사/데이터 검사", false, 1)]
        static void ValidateMenu()
        {
            var result = RunValidation();
            Debug.Log("[데이터 검사]\n" + result);
        }
    }
}
