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
        // 발사 직후 위로 튀어오르는 보정은 플레이어 조준 감각을 위한 것이므로
        // 탄도를 정확히 계산해 쐄는 적 포탄에는 적용하지 않는다.
        if (shotMode == ShotMode.Player)
            transform.GetComponent<Rigidbody>().AddForce(new Vector3(0, 730f, 0));
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
        bool playerHitEnemy = collision.collider.gameObject.CompareTag("Enemy") && shotMode == ShotMode.Player;
        bool enemyHitPlayer = collision.collider.gameObject.CompareTag("Player") && shotMode == ShotMode.Enemy;

        if (playerHitEnemy || enemyHitPlayer)
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
