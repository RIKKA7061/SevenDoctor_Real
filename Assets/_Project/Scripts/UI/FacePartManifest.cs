using System;
using UnityEngine;

namespace SevenDoctors.UI
{
    /// <summary>
    /// gen_art.py 가 뽑아 놓은 표정 부위 배치표(Art/Faces/faceparts.json).
    ///
    /// 부위 PNG 는 내용 경계로 잘려 있어서 그대로 겹치면 위치가 맞지 않습니다.
    /// 원래 700x1200 캔버스의 어디에 있었는지를 여기서 읽어 제자리에 놓습니다.
    /// 파일이 없으면 부위 애니메이션 없이 합본 한 장으로 돌아갑니다 — 아트가
    /// 아직 안 들어왔다고 게임이 멈추지는 않습니다.
    /// </summary>
    [Serializable]
    public class FacePartManifest
    {
        public const string ResourcePath = "Art/Faces/faceparts";

        public int canvasW = 700;
        public int canvasH = 1200;
        public FaceEntry[] faces;

        public static FacePartManifest Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return null;

            try
            {
                var m = JsonUtility.FromJson<FacePartManifest>(asset.text);
                if (m == null || m.faces == null || m.faces.Length == 0) return null;
                if (m.canvasW <= 0 || m.canvasH <= 0) return null;
                return m;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Portrait] faceparts.json 을 읽지 못했습니다 — 합본 표정으로 갑니다. {e.Message}");
                return null;
            }
        }

        public FaceEntry Find(string key)
        {
            if (string.IsNullOrEmpty(key) || faces == null) return null;
            foreach (var f in faces)
                if (f != null && f.key == key) return f;
            return null;
        }
    }

    [Serializable]
    public class FaceEntry
    {
        public string key;
        public bool canBlink = true;
        public FacePartRect[] parts;

        public FacePartRect Find(string partName)
        {
            if (parts == null) return null;
            foreach (var p in parts)
                if (p != null && p.name == partName) return p;
            return null;
        }
    }

    /// <summary>부위가 원본 캔버스에서 차지하던 사각형. y 는 위에서부터입니다.</summary>
    [Serializable]
    public class FacePartRect
    {
        public string name;
        public int x, y, w, h;
    }
}
