using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

namespace Practice
{
    public class ChatSystem : MonoBehaviour
    {
        [Header("대화창 UI ------------------------------------------------------------")]
        [SerializeField] private GameObject Panel;
        [SerializeField] private TextMeshProUGUI Name; // 화자 이름
        [SerializeField] private TextMeshProUGUI Chat; // 본문 내용

        [Header("스토리 진행 번호 ------------------------------------------------------------")]
        [SerializeField] private int storyIndex = 0;
        [SerializeField] private string currentName = "";
        [SerializeField] private string currentChat = "";

        [Header("대화창 본문 글자 연출 ------------------------------------------------------------")]
        [SerializeField] private float speed = 0.03f;
        [SerializeField] private bool isTyping = false;
        private Coroutine ChatShowing;
        [SerializeField] GameObject ReverseTriangle;

        [Header("키보드 연결 안됨 에러 출력 여부 ------------------------------------------------------------")]
        [SerializeField] private bool isNotKeyboardConnected_OnceDebuggedIt = false;


        string[] ChatDB =
        {
            "박사:좋아 이제 테스트만 하면 된다",
            "박사:이 날을 위해 내가 몇십년을 준비했지",
            "박사:잠깐, 갑자기 새로운 에러 코드가 떳..",
            ":펑!"
        };

        void Start()
        {
            OnClick();
        }

        void Update()
        {
            if (Keyboard.current == null)
            {
                if (isNotKeyboardConnected_OnceDebuggedIt == false)
                {
                    Debug.Log("키보드가 연결되어 있지 않습니다.");
                    isNotKeyboardConnected_OnceDebuggedIt = true;
                }
            }
            else if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                OnClick();
            }
        }

        public void OnClick()
        {
            if (isTyping == true)
            {
                /* 스킵 */
                StopCoroutine(ChatShowing); // 연출 중인 코루틴을 중지

                // 대사 전부 출력
                NextPage();
                ReverseTriangle.SetActive(true);

                /* 스토리 진행도 UP */
                storyIndex++;
            }
            else if(isTyping == false)
            {
                // 다음 대사 출력
                ReverseTriangle.SetActive(false);

                ChatShowing = StartCoroutine(ChatShow());
            }
        }

        private void NextPage()
        {
            if (storyIndex >= ChatDB.Length)
            {
                Debug.Log("스토리가 끝났습니다. 대화창을 닫습니다.");
                Panel.SetActive(false);
                return;
            }

            // 화자 이름이 비어있으면 Name UI를 비활성화 (이름표 숨기기)
            NameShow(currentName);

            Chat.text = currentChat;

            isTyping = false;
            ReverseTriangle.SetActive(true);
        }

        IEnumerator ChatShow()
        {
            if (storyIndex >= ChatDB.Length)
            {
                Debug.Log("스토리가 끝났습니다. 대화창을 닫습니다.");
                Panel.SetActive(false);
                yield break;
            }

            isTyping = true;
            ReverseTriangle.SetActive(false);

            currentName = ChatDB[storyIndex].Split(':')[0];

            // 화자 이름이 비어있으면 Name UI를 비활성화 (이름표 숨기기)
            NameShow(currentName);

            Chat.text = "";

            currentChat = ChatDB[storyIndex].Split(':')[1];

            for (int i = 0; i < currentChat.Length; i++)
            {
                Chat.text += currentChat[i];
                yield return new WaitForSeconds(speed);
            }
            isTyping = false;
            ReverseTriangle.SetActive(true);
            storyIndex++;
        }

        private void NameShow(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.Log("화자가 비었습니다.");
                Name.gameObject.SetActive(false);
            }
            else
            {
                Name.gameObject.SetActive(true);
            }
            Name.text = name;
        }
    }
}