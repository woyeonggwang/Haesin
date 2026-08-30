using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShotMode {
    Player,
    Enemy
}
public class Bullet : MonoBehaviour
{


    public ShotMode shotMode;
    public GameObject exploseEfx;

    private void OnEnable()
    {
        transform.GetComponent<Rigidbody>().AddForce(new Vector3(0,730f,0));
    }


    private void Update()
    {
        if (transform.position.y < -20f)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.gameObject.CompareTag("Enemy")&&shotMode==ShotMode.Player)
        {
            GameObject efxTemp = Instantiate(exploseEfx);
            efxTemp.transform.position = transform.position;
            efxTemp.SetActive(true);
            StartCoroutine(DestroyPlay());
        }
    }
    IEnumerator DestroyPlay()
    {
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
