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

    [Tooltip("이 포탄을 쏜 진영. 같은 진영의 배에는 맞아도 터지지 않는다.")]
    public Faction ownerFaction = Faction.Pirate;
    [Tooltip("진영으로 피아를 판단할지. 끄거나 상대가 ShipFaction 을 가지지 않으면 예전의 Player/Enemy 태그 방식으로 돌아간다.")]
    public bool useFaction = true;

    [Tooltip("명중했을 때 깎는 체력.")]
    public float damage = 1f;

    public GameObject exploseEfx;

    private void OnEnable()
    {
        // 발사 직후 위로 튀어오르는 보정은 플레이어 조준 감각을 위한 것이므로
        // 탄도를 정확히 계산해 쏘는 적 포탄에는 적용하지 않는다.
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
        ShipFaction hit;
        if (!ShouldExplode(collision, out hit)) return;

        // 맞은 배의 체력을 깎는다. 체력 컴포넌트가 없는 상대는 폭발만 한다.
        if (hit != null)
        {
            ShipHealth health = hit.GetComponent<ShipHealth>();
            if (health != null) health.TakeDamage(damage, transform.position);
        }

        GameObject efxTemp = Instantiate(exploseEfx);
        efxTemp.transform.position = transform.position;
        efxTemp.SetActive(true);
        StartCoroutine(DestroyPlay());
    }

    /// <summary>
    /// 이 충돌이 피해로 이어지는지 판단하고, 맞은 배의 진영표를 함께 돌려준다.
    /// 진영이 셋(해적/관군/왜군)이 되면서 Player/Enemy 태그 둘로는
    /// 누가 누구의 적인지 표현할 수 없게 되었다. 그래서 맞은 배에
    /// ShipFaction 이 있으면 진영으로 판단하고, 없을 때만 예전 방식을 쓴다.
    /// </summary>
    bool ShouldExplode(Collision collision, out ShipFaction hit)
    {
        hit = null;
        if (collision == null || collision.collider == null) return false;
        GameObject go = collision.collider.gameObject;

        if (useFaction)
        {
            hit = ShipFaction.Of(go);
            if (hit != null)
            {
                // 아군 오사는 걸러진다
                if (hit.faction == ownerFaction) { hit = null; return false; }

                // 이미 가라앉는 중인 배는 더 때려도 의미가 없다
                ShipHealth h = hit.GetComponent<ShipHealth>();
                if (h != null && (h.isDead || h.isSinking)) { hit = null; return false; }

                return true;
            }
        }

        // ShipFaction 이 없는 상대 - 예전의 2진영 판단
        bool playerHitEnemy = go.CompareTag("Enemy") && shotMode == ShotMode.Player;
        bool enemyHitPlayer = go.CompareTag("Player") && shotMode == ShotMode.Enemy;
        return playerHitEnemy || enemyHitPlayer;
    }

    IEnumerator DestroyPlay()
    {
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }
}
