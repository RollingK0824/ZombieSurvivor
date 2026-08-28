using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // UI 관련 코드

// 플레이어 캐릭터의 생명체로서의 동작을 담당
public class PlayerHealth : LivingEntity {
    public Slider healthSlider; // 체력을 표시할 UI 슬라이더

    public AudioClip deathClip; // 사망 소리
    public AudioClip hitClip; // 피격 소리
    public AudioClip itemPickupClip; // 아이템 습득 소리

    private AudioSource playerAudioPlayer; // 플레이어 소리 재생기
    private Animator playerAnimator; // 플레이어의 애니메이터

    private PlayerMovement playerMovement; // 플레이어 움직임 컴포넌트
    private PlayerShooter playerShooter; // 플레이어 슈터 컴포넌트

    private void Awake() {
        // 사용할 컴포넌트를 가져오기
        playerAnimator = GetComponent<Animator>();
        playerAudioPlayer = GetComponent<AudioSource>();

        playerMovement = GetComponent<PlayerMovement>();
        playerShooter = GetComponent<PlayerShooter>();
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        healthNetwork.OnValueChanged += OnHealthChanged;
        deadNetwork.OnValueChanged += OnDeadChanged;

        if (healthSlider != null) {
            healthSlider.gameObject.SetActive(true);
            healthSlider.maxValue = startingHealth;
            healthSlider.value = health;
        }

        if (IsOwner) {
            SetupCameraFollow();
        }
    }

    private void Update() {
        if (IsOwner) {
            SetupCameraFollow();
        }
    }

    private void SetupCameraFollow() {
        if (!IsOwner) return;

        // 1. Cinemachine v3 (Unity.Cinemachine.CinemachineCamera) 직접 연동
        var cinemachineCam = FindObjectOfType<Unity.Cinemachine.CinemachineCamera>();
        if (cinemachineCam != null) {
            if (cinemachineCam.Target.TrackingTarget != transform)
            {
                cinemachineCam.Target.TrackingTarget = transform;
                cinemachineCam.Target.LookAtTarget = transform;
            }
            return;
        }

        // 2. 리플렉션을 활용한 Cinemachine v2 및 기타 카메라 추적 스크립트 범용 지원
        Component[] components = FindObjectsOfType<Component>();
        foreach (Component comp in components) {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName == "CinemachineVirtualCamera" || typeName == "CinemachineCamera") {
                var targetProp = comp.GetType().GetProperty("Target");
                if (targetProp != null) {
                    var targetVal = targetProp.GetValue(comp);
                    if (targetVal != null) {
                        var trackingProp = targetVal.GetType().GetProperty("TrackingTarget");
                        if (trackingProp != null) {
                            var curVal = trackingProp.GetValue(targetVal) as Transform;
                            if (curVal != transform) trackingProp.SetValue(targetVal, transform);
                        }
                        var lookAtTargetProp = targetVal.GetType().GetProperty("LookAtTarget");
                        if (lookAtTargetProp != null) {
                            var curLook = lookAtTargetProp.GetValue(targetVal) as Transform;
                            if (curLook != transform) lookAtTargetProp.SetValue(targetVal, transform);
                        }
                    }
                }
                var followProp = comp.GetType().GetProperty("Follow");
                if (followProp != null) {
                    var curFollow = followProp.GetValue(comp) as Transform;
                    if (curFollow != transform) followProp.SetValue(comp, transform);
                }

                var lookAtProp = comp.GetType().GetProperty("LookAt");
                if (lookAtProp != null) {
                    var curLookAt = lookAtProp.GetValue(comp) as Transform;
                    if (curLookAt != transform) lookAtProp.SetValue(comp, transform);
                }
            }
        }
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        healthNetwork.OnValueChanged -= OnHealthChanged;
        deadNetwork.OnValueChanged -= OnDeadChanged;
    }

    private void OnHealthChanged(float previous, float current) {
        if (healthSlider != null) {
            healthSlider.value = current;
        }
    }

    private void OnDeadChanged(bool previous, bool current) {
        if (current && !previous) {
            HandleDeathEffects();
        }
    }

    protected override void OnEnable() {
        base.OnEnable();
        if (healthSlider != null) {
            healthSlider.gameObject.SetActive(true);
            healthSlider.maxValue = startingHealth;
            healthSlider.value = health;
        }
    }

    // 데미지 처리
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitDirection) {
        if (!IsServer) return;

        if (!dead)
        {
            PlayHitSoundClientRpc();
        }

        base.OnDamage(damage, hitPoint, hitDirection);
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc() {
        if (playerAudioPlayer != null && hitClip != null) {
            playerAudioPlayer.PlayOneShot(hitClip);
        }
    }

    // 사망 처리
    public override void Die() {
        if (!IsServer) return;
        base.Die();
    }

    private void HandleDeathEffects() {
        if (healthSlider != null) {
            healthSlider.gameObject.SetActive(false);
        }

        if (playerAudioPlayer != null && deathClip != null) {
            playerAudioPlayer.PlayOneShot(deathClip);
        }

        if (playerAnimator != null) {
            playerAnimator.SetTrigger("Die");
        }

        if (playerMovement != null) playerMovement.enabled = false;
        if (playerShooter != null) playerShooter.enabled = false;
    }

    private void OnTriggerEnter(Collider other) {
        // 아이템과 충돌한 경우 서버에서만 처리
        if (!IsServer) return;

        if (!dead)
        {
            IItem item = other.GetComponent<IItem>();

            if (item != null)
            {
                item.Use(gameObject);
                PlayItemPickupSoundClientRpc();
            }
        }
    }

    [ClientRpc]
    private void PlayItemPickupSoundClientRpc() {
        if (playerAudioPlayer != null && itemPickupClip != null) {
            playerAudioPlayer.PlayOneShot(itemPickupClip);
        }
    }
}