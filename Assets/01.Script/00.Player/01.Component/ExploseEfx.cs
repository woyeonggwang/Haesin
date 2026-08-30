using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExploseEfx : MonoBehaviour
{
    private void OnEnable()
    {
        StartCoroutine(DestroyPlay());
    }

    IEnumerator DestroyPlay()
    {
        yield return new WaitForSeconds(3f);
        Destroy(gameObject);

    }
}
