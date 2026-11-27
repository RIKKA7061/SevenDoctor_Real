using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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


        [Header("대화 DB ------------------------------------------------------------")]
        [SerializeField] private TextAsset DialoguesCSV;
        [SerializeField] private bool isChatBoxInitialized = false;
        [SerializeField] private List<string> nameBox = new List<string>();
        [SerializeField] private List<string> chatBox = new List<string>();
        [SerializeField] private string[] dialoguesHangs;


        [Header("캐릭터 DB ------------------------------------------------------------")]
        [SerializeField] private TextAsset CharactersCSV;
        [SerializeField] private Dictionary<string, string> charactersDB = new Dictionary<string, string>();
        [SerializeField] private string[] charactersHangs;



        void Start()
        {
            InitializeCSV();
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
            if(isChatBoxInitialized == false)
            {
                Debug.Log("대화 DB가 초기화되지 않았습니다.");
                return;
            }
            if (isTyping == true)
            {
                /* 스킵 */
                StopCoroutine(ChatShowing); // 연출 중인 코루틴을 중지

                // 대사 전부 출력
                SkipTypeEffect_ChatShow();
                ReverseTriangle.SetActive(true);

                /* 스토리 진행도 UP */
                storyIndex++;
            }
            else if(isTyping == false)
            {
                // 다음 대사 출력
                ReverseTriangle.SetActive(false);

                ChatShowing = StartCoroutine(TypeEffect_ChatShow());
            }
        }

        private void SkipTypeEffect_ChatShow()
        {
            if (storyIndex >= chatBox.Count)
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

        IEnumerator TypeEffect_ChatShow()
        {
            if (storyIndex >= chatBox.Count)
            {
                Debug.Log("스토리가 끝났습니다. 대화창을 닫습니다.");
                Panel.SetActive(false);
                yield break;
            }

            isTyping = true;
            ReverseTriangle.SetActive(false);

            currentName = nameBox[storyIndex];

            // 화자 이름이 비어있으면 Name UI를 비활성화 (이름표 숨기기)
            NameShow(currentName);

            Chat.text = "";

            currentChat = chatBox[storyIndex];

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


        private void InitializeCSV()
        {
            dialoguesHangs = DialoguesCSV.text.Split('\n');
            charactersHangs = CharactersCSV.text.Split('\n');

            // CharactersCSV
            for (int i = 1; i < charactersHangs.Length; i++)
            {
                if (string.IsNullOrEmpty(charactersHangs[i]))
                {
                    continue;
                }
                List<string> Box = SplitCells(charactersHangs[i]);
                charactersDB.Add(Box[0], Box[1]);
            }

            // DialoguesCSV
            for (int i = 1; i < dialoguesHangs.Length; i++)
            {
                if (string.IsNullOrEmpty(dialoguesHangs[i]))
                {
                    continue;
                }
                List<string> Box = SplitCells(dialoguesHangs[i]);
                if (charactersDB.ContainsKey(Box[2]))
                {
                    nameBox.Add(charactersDB[Box[2]]);
                }
                else
                {
                    nameBox.Add(Box[2]); // 빈 화자
                }
                chatBox.Add(Box[4]);
            }

            isChatBoxInitialized = true;
            OnClick();
        }

        private List<string> SplitCells(string line)
        {
            bool isTextMode = false;
            List<string> Box = new List<string>();
            string memoji = "";

            for (int j = 0; j < line.Length; j++)
            {
                if (line[j] == ',' && isTextMode == false)
                {
                    Box.Add(memoji);
                    memoji = "";
                }
                else if (line[j] == '\"' && isTextMode == false)
                {
                    isTextMode = true;
                }
                else if (line[j] == '\"' && isTextMode == true)
                {
                    if (j + 1 < line.Length)
                    {
                        if (line[j + 1] == '\"')
                        {
                            memoji += line[j];
                            j++;
                        }
                        else
                        {
                            isTextMode = false;
                        }
                    }
                    else
                    {
                        isTextMode = false;
                    }
                }
                else
                {
                    memoji += line[j];
                }
            }
            Box.Add(memoji);
            return Box;
        }
    }
}