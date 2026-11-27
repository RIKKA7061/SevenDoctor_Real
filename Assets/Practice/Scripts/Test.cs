using UnityEngine;

public class Test : MonoBehaviour
{
    void Start()
    {
        string hang = "\"a\"\"b\"";
        bool isTextMode = false;
        string[] Box = new string[3];
        int boxIndex = 0;
        for (int j = 0; j < hang.Length; j++)
        {
            if (hang[j] == ',' && isTextMode == false)
            {
                boxIndex++;
            }
            else if (hang[j] == '\"' && isTextMode == false)
            {
                isTextMode = true;
            }
            else if (hang[j] == '\"' && isTextMode == true)
            {
                if(j + 1 < hang.Length) 
                {
                    if (hang[j] == '\"' && hang[j + 1] == '\"')
                    {
                        Box[boxIndex] += hang[j];
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
                Box[boxIndex] += hang[j];
            }
        }

        foreach (var item in Box)
        {
            Debug.Log(item);
        }
    }
}
