using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// 좀비 AI 구현 (Netcode 네트워크 호환)
public class Zombie : LivingEntity
{
    public LayerMask whatIsTarget; // 추적 대상 레이어

    private LivingEntity targetEntity; // 추적 대상
    private NavMeshAgent navMeshAgent; // 경로 계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격 시 재생할 파티클 효과
    public AudioClip deathSound; // 사망 시 재생할 소리
    public AudioClip hitSound; // 피격 시 재생할 소리

    private Animator zombieAnimator; // 애니메이터 컴포넌트
    private AudioSource zombieAudioPlayer; // 오디오 소스 컴포넌트
    private Renderer zombieRenderer; // 렌더러 컴포넌트

    public float damage = 20f; // 공격력
    public float timeBetAttack = 0.5f; // 공격 간격
    private float lastAttackTime; // 마지막 공격 시점

    public NetworkVariable<bool> hasTargetNetwork = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget {
        get
        {
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }
            return false;
        }
    }

    private void Awake() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        zombieAnimator = GetComponent<Animator>();
        zombieAudioPlayer = GetComponent<AudioSource>();
        zombieRenderer = GetComponentInChildren<Renderer>();
    }

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    public void Setup(ZombieData zombieData) {
        startingHealth = zombieData.health;
        damage = zombieData.damage;
        if (navMeshAgent != null) navMeshAgent.speed = zombieData.speed;
        
        SetupClientRpc(zombieData.skinColor, zombieData.speed);
    }

    [ClientRpc]
    private void SetupClientRpc(Color skinColor, float speed) {
        if (zombieRenderer != null) zombieRenderer.material.color = skinColor;
        if (navMeshAgent != null && IsServer) navMeshAgent.speed = speed;
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (!IsServer && navMeshAgent != null)
        {
            navMeshAgent.enabled = false; // 클라이언트에서는 NavMeshAgent를 비활성화하여 NetworkTransform과 충돌 방지
        }

        hasTargetNetwork.OnValueChanged += (prev, current) => {
            if (zombieAnimator != null) zombieAnimator.SetBool("HasTarget", current);
        };

        if (IsServer)
        {
            StartCoroutine(UpdatePath());
        }
    }

    private void Update() {
        if (zombieAnimator != null)
        {
            bool targetBool = IsSpawned ? hasTargetNetwork.Value : hasTarget;
            zombieAnimator.SetBool("HasTarget", targetBool);
        }
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로 갱신 (서버 전용)
    private IEnumerator UpdatePath()
    {
        if (!IsServer) yield break;

        while (!dead)
        {
            if (hasTarget)
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.SetDestination(targetEntity.transform.position);
                }
            }
            else
            {
                if (navMeshAgent != null && navMeshAgent.enabled)
                {
                    navMeshAgent.isStopped = true;
                }

                Collider[] colliders = Physics.OverlapSphere(transform.position, 20f, whatIsTarget);

                float closestDistance = float.MaxValue;
                LivingEntity nearestPlayer = null;

                foreach (Collider collider in colliders)
                {
                    LivingEntity livingEntity = collider.GetComponent<LivingEntity>();
                    if (livingEntity != null && !livingEntity.dead)
                    {
                        float dist = Vector3.Distance(transform.position, collider.transform.position);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            nearestPlayer = livingEntity;
                        }
                    }
                }
                targetEntity = nearestPlayer;
            }

            hasTargetNetwork.Value = hasTarget;

            yield return new WaitForSeconds(0.25f);
        }
    }

    // 데미지를 입었을 때 실행할 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal) {
        if (!IsServer) return;

        if (!dead)
        {
            PlayHitEffectClientRpc(hitPoint, hitNormal);
        }

        base.OnDamage(damage, hitPoint, hitNormal);
    }

    [ClientRpc]
    private void PlayHitEffectClientRpc(Vector3 hitPoint, Vector3 hitNormal) {
        if (hitEffect != null)
        {
            hitEffect.transform.position = hitPoint;
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal);
            hitEffect.Play();
        }

        if (zombieAudioPlayer != null && hitSound != null)
        {
            zombieAudioPlayer.PlayOneShot(hitSound);
        }
    }

    // 사망 처리
    public override void Die() {
        if (!IsServer) return;

        base.Die();
        DieClientRpc();
    }

    [ClientRpc]
    private void DieClientRpc() {
        Collider[] zombieColliders = GetComponents<Collider>();
        foreach (Collider collider in zombieColliders)
        {
            collider.enabled = false;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.enabled = false;
        }

        if (zombieAnimator != null) zombieAnimator.SetTrigger("Die");
        if (zombieAudioPlayer != null && deathSound != null) zombieAudioPlayer.PlayOneShot(deathSound);
    }

    private void OnTriggerStay(Collider other) {
        if (!IsServer) return;

        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            LivingEntity attackTarget = other.GetComponent<LivingEntity>();

            if (attackTarget != null && attackTarget == targetEntity)
            {
                lastAttackTime = Time.time;

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position - other.transform.position;

                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }
    }
}