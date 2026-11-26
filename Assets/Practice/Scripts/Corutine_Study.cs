using System.Collections;
using UnityEngine;

public class Corutine_Study : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(TestCoroutine());
    }

    IEnumerator TestCoroutine()
    {
        Debug.Log("코루틴 시작");
        yield return new WaitForSeconds(1f);
        Debug.Log("1초 후 실행");
        yield return new WaitForSeconds(1f);
        Debug.Log("1초 후 실행");
    }
}
