using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MoveControl : MonoBehaviour
{

    public float moveSpeed;
    public float maxSpeed;
    public float minSpeed;
    public float rotY;
    public float[] targetRotSpeed;
    public float rotSpeed;
    private float directionSpeed;
    private float directionRotation;
    // ② 자동 생성된 Input Action 클래스
    private InputAction _inputActions;
    public Image boostGauge;
    public float deceleration;
    public bool boost = false;
    public float buffSpeed = 0f;     // 부스트 속도
    public float maxBuff = 10f;      // 최대 부스트
    public float buffAccel = 5f;     // 가속도
    public float buffDecay = 5f;     // 감속도
    private void Awake()
    {
        _inputActions = new InputAction();  // Input 액션 인스턴스 생성
    }

    private void OnEnable()
    {
        
        // 액션 맵 활성화
        _inputActions.Enable();
    }
    void Update()
    {
        // 부스트 가속/감속 처리
        if (boost && directionSpeed > 0)
        {
            buffSpeed = Mathf.MoveTowards(buffSpeed, maxBuff, buffAccel * Time.deltaTime);
        }
        else
        {
            buffSpeed = Mathf.MoveTowards(buffSpeed, 0f, buffDecay * Time.deltaTime);
        }
        rotSpeed = boost ? targetRotSpeed[1] : targetRotSpeed[0];

        float targetY = transform.eulerAngles.y+(directionRotation*rotSpeed)*Time.deltaTime;
        Vector3 currentRotation = transform.eulerAngles;
        transform.eulerAngles = new Vector3(currentRotation.x, targetY, currentRotation.z);
        if (directionSpeed != 0)
        {

            if (directionSpeed > 0)
            {

                if (moveSpeed < maxSpeed + buffSpeed && moveSpeed > minSpeed)
                {
                    moveSpeed += (Time.deltaTime * 10f) + buffSpeed;
                }
            }
            else
            {
                if (moveSpeed < maxSpeed + buffSpeed && moveSpeed > minSpeed)
                {
                    moveSpeed -= (Time.deltaTime * 10f) + buffSpeed;
                }
            }
        }
        else
        {
            if (moveSpeed > 0)
            {
                moveSpeed -= deceleration * Time.deltaTime*buffDecay;
                if (moveSpeed < 0) moveSpeed = 0;
            }
            else if (moveSpeed < 0)
            {
                moveSpeed += deceleration * Time.deltaTime;
                if (moveSpeed > 0) moveSpeed = 0;
            }
        }
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
    }
    // 부스트 입력 처리 (스페이스바)
    private void OnBoost(InputValue value)
    {
        boost = value.isPressed;
    }
    private void OnMove(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        directionSpeed = input.y;
        directionRotation = input.x;
        Debug.Log($"SEND_MESSAGE : {input}");
    }
}
