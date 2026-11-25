using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Practice
{
    public class ChatSystem : MonoBehaviour
    {
        [SerializeField] private GameObject Panel;
        [SerializeField] private TextMeshProUGUI Name;
        [SerializeField] private TextMeshProUGUI Chat;

        [SerializeField] private int storyIndex = 0;

        string[] ChatDB =
        {
            "박사:좋아 이제 테스트만 하면 된다",
            "박사:이 날을 위해 내가 몇십년을 준비했지",
            "박사:잠깐, 갑자기 새로운 에러 코드가 떳..",
            ":펑!"
        };

        void Start()
        {
            NextPage();
        }

        public void NextPage()
        {
            if (storyIndex >= ChatDB.Length)
            {
                Debug.Log("스토리가 끝났습니다. 대화창을 닫습니다.");
                Panel.gameObject.SetActive(false);
                return;
            }

            if (string.IsNullOrEmpty(ChatDB[storyIndex].Split(':')[0]))
            {
                Debug.Log("화자가 비었습니다.");
                Name.gameObject.SetActive(false);
            }
            else
            {
                Name.gameObject.SetActive(true);
            }

            Name.text = ChatDB[storyIndex].Split(':')[0];
            Chat.text = ChatDB[storyIndex].Split(':')[1];
            storyIndex++;
        }
    }
}