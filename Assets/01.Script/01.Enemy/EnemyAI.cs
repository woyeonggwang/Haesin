using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float rotSpeed = 90f;
    public float detectionRange = 30f;
    public float attackRange = 10f;

    private Transform player;
    private bool isPlayerDetected = false;
    private bool isInAttackPosition = false;

    // 평상시 회전 속도
    public float idleTurnSpeed = 20f;
    private float currentIdleRotSpeed = 0f;
    private Coroutine idleRoutine;
    public float idleMoveSpeedRatio = 0.3f;

    void Start()
    {
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            player = target.transform;
        }
        //idleRoutine = StartCoroutine(IdleRoutine());
    }

    void Update()
    {
        if (player == null)
        {
            IdleMove();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        bool wasDetected = isPlayerDetected;
        isPlayerDetected = distanceToPlayer <= detectionRange;

        // 탐지 상태가 바뀌었을 때
        if (wasDetected != isPlayerDetected)
        {
            if (isPlayerDetected)
            {
                if (idleRoutine != null)
                {
                    StopCoroutine(idleRoutine);
                    currentIdleRotSpeed = 0f; // 회전 멈춤
                }
            }
            else
            {
                idleRoutine = StartCoroutine(IdleRoutine());
            }
        }

        if (isPlayerDetected)
        {
            if (distanceToPlayer > attackRange)
            {
                isInAttackPosition = false;
                MoveForward();
                RotateTowards(player.position);
            }
            else
            {
                isInAttackPosition = true;
            }

            if (isInAttackPosition)
            {
                RotateSidewaysToPlayer();
            }
        }
        else
        {
            IdleMove();
        }
    }

    void MoveForward()
    {
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
    }

    void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, rotSpeed * Time.deltaTime);
    }

    void RotateSidewaysToPlayer()
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        targetRotation *= Quaternion.Euler(0, 90f, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotSpeed * Time.deltaTime);
    }

    void IdleMove()
    {
        transform.Rotate(0f, currentIdleRotSpeed * Time.deltaTime, 0f);
        transform.Translate(Vector3.forward * (moveSpeed * idleMoveSpeedRatio) * Time.deltaTime);
    }

    IEnumerator IdleRoutine()
    {
        while (true)
        {
            // 1. 회전 속도 결정 (-rotSpeed ~ +rotSpeed)
            currentIdleRotSpeed = 0f;
            float waitTime = Random.Range(5f, 30f);
            yield return new WaitForSeconds(waitTime);

            // 2. 회전 시간 설정
            currentIdleRotSpeed = Random.Range(-rotSpeed, rotSpeed);
            float rotateDuration = Random.Range(5f, 10f);
            yield return new WaitForSeconds(rotateDuration);

            // 3. 다시 정지
            currentIdleRotSpeed = 0f;
        }
    }
}