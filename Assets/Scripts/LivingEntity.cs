using System;
using Unity.Netcode;
using UnityEngine;

// 생명체로서 동작할 게임 오브젝트들을 위한 뼈대를 제공
// 체력, 데미지 받아들이기, 사망 기능, 사망 이벤트를 제공
public class LivingEntity : NetworkBehaviour, IDamageable {
    public float startingHealth = 100f; // 시작 체력

    public NetworkVariable<float> healthNetwork = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> deadNetwork = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float health => healthNetwork.Value; // 현재 체력
    public bool dead => deadNetwork.Value; // 사망 상태

    public event Action onDeath; // 사망시 발동할 이벤트

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (IsServer) {
            deadNetwork.Value = false;
            healthNetwork.Value = startingHealth;
        }
    }

    protected virtual void OnEnable() {
        // 네트워크 상에서 초기화되므로 서버 권한 일치 시 로컬 초기화
        if (IsServer) {
            deadNetwork.Value = false;
            healthNetwork.Value = startingHealth;
        }
    }

    // 데미지를 입는 기능 (서버 권한 처리)
    public virtual void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) {
        if (IsSpawned && !IsServer) return;

        // 데미지만큼 체력 감소
        healthNetwork.Value -= damage;

        // 체력이 0 이하 && 아직 죽지 않았다면 사망 처리 실행
        if (healthNetwork.Value <= 0 && !deadNetwork.Value)
        {
            Die();
        }
    }

    // 체력을 회복하는 기능 (서버 권한 처리)
    public virtual void RestoreHealth(float newHealth) {
        if (IsSpawned && !IsServer) return;

        if (deadNetwork.Value)
        {
            // 이미 사망한 경우 체력을 회복할 수 없음
            return;
        }

        // 체력 추가
        healthNetwork.Value += newHealth;
    }

    // 사망 처리
    public virtual void Die() {
        if (IsSpawned && !IsServer) return;

        // 사망 상태를 참으로 변경
        deadNetwork.Value = true;

        // onDeath 이벤트에 등록된 메서드가 있다면 실행
        DieClientRpc();
    }

    [ClientRpc]
    protected virtual void DieClientRpc() {
        if (onDeath != null)
        {
            onDeath();
        }
    }
}