using UnityEngine;
using UnityEngine.UI;

public class PracticeRoom : MonoBehaviour
{
    [SerializeField] private Image background;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Sprite sprite = Resources.Load<Sprite>("Art/Backgrounds/bg_lab");

        if (sprite == null) Debug.LogWarning("경로 오류");
        else Debug.Log("배경 이미지 로드 성공");

        background.sprite = sprite;
    }
}
