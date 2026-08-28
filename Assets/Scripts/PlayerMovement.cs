using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// 플레이어 캐릭터를 사용자 입력에 따라 움직이는 스크립트
public class PlayerMovement : NetworkBehaviour {
    public float moveSpeed = 5f; // 앞뒤 움직임의 속도
    public float rotateSpeed = 180f; // 좌우 회전 속도

    private PlayerInput playerInput; // 플레이어 입력을 알려주는 컴포넌트
    private Rigidbody playerRigidbody; // 플레이어 캐릭터의 리지드바디
    private Animator playerAnimator; // 플레이어 캐릭터의 애니메이터

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        // 기본 Server-Authoritative NetworkTransform이 위치를 서버 좌표(0,0,0)로 강제 리셋시키는 현상을 방지
        var defaultNetTransform = GetComponent<NetworkTransform>();
        if (defaultNetTransform != null)
        {
            defaultNetTransform.enabled = false;
        }

        // 로컬 조종자(Owner)가 아닌 원격 클라이언트/서버에서는 물리 엔진이 위치 동기화(PositionSync)를 방해하지 않도록 isKinematic 설정
        playerRigidbody = GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = !IsOwner;
        }
    }

    private void Start() {
        // 사용할 컴포넌트들의 참조를 가져오기
        playerInput = GetComponent<PlayerInput>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerAnimator = GetComponent<Animator>();
    }

    // FixedUpdate는 물리 갱신 주기에 맞춰 실행됨
    private void FixedUpdate() {
        if (!IsOwner) return;

        // 물리 갱신 주기마다 움직임, 회전, 애니메이션 처리 실행
        Rotate();
        Move();

        if (playerAnimator != null && playerInput != null)
        {
            playerAnimator.SetFloat("Move", playerInput.move);
        }
    }

    // 입력값에 따라 캐릭터를 앞뒤로 움직임
    private void Move() {
        if (playerInput == null || playerRigidbody == null) return;

        Vector3 moveDistance = 
            playerInput.move * transform.forward * moveSpeed * Time.fixedDeltaTime;
        playerRigidbody.MovePosition(playerRigidbody.position + moveDistance);
    }

    // 입력값에 따라 캐릭터를 좌우로 회전
    private void Rotate() {
        if (playerInput == null || playerRigidbody == null) return;

        float turn = playerInput.rotate * rotateSpeed * Time.fixedDeltaTime;
        playerRigidbody.rotation = 
            playerRigidbody.rotation * Quaternion.Euler(0, turn, 0f);
    }

    [ClientRpc]
    public void SpawnToPositionClientRpc(Vector3 position)
    {
        transform.position = position;
    }
}