using System.Collections;
using Unity.Netcode;
using UnityEngine;

// 총을 구현 (Netcode 네트워크 호환)
public class Gun : NetworkBehaviour {
    // 총의 상태를 표현하는 데 사용할 타입을 선언
    public enum State {
        Ready, // 발사 준비됨
        Empty, // 탄알집이 빔
        Reloading // 재장전 중
    }

    private bool isReloadingLocal = false;

    public State state {
        get {
            if (isReloadingLocal) return State.Reloading;
            if (magAmmo > 0) return State.Ready;
            return State.Empty;
        }
    }

    public Transform fireTransform; // 탄알이 발사될 위치

    public ParticleSystem muzzleFlashEffect; // 총구 화염 효과
    public ParticleSystem shellEjectEffect; // 탄피 배출 효과

    private LineRenderer bulletLineRenderer; // 탄알 궤적을 그리기 위한 렌더러
    private AudioSource gunAudioPlayer; // 총 소리 재생기

    public GunData gunData; // 총의 현재 데이터

    private float fireDistance = 50f; // 사정거리

    public NetworkVariable<int> ammoRemainNetwork = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> magAmmoNetwork = new NetworkVariable<int>(
        30, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int ammoRemain {
        get => ammoRemainNetwork.Value;
        set {
            if (IsServer) ammoRemainNetwork.Value = value;
        }
    }

    public int magAmmo {
        get => magAmmoNetwork.Value;
        set {
            if (IsServer) magAmmoNetwork.Value = value;
        }
    }

    private float lastFireTime; // 총을 마지막으로 발사한 시점

    private void Awake() {
        // 사용할 컴포넌트의 참조 가져오기
        gunAudioPlayer = GetComponent<AudioSource>();
        bulletLineRenderer = GetComponent<LineRenderer>();

        if (bulletLineRenderer != null)
        {
            bulletLineRenderer.positionCount = 2;
            bulletLineRenderer.enabled = false;
        }
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (IsServer) {
            ammoRemainNetwork.Value = gunData.startAmmoRemain;
            magAmmoNetwork.Value = gunData.magCapacity;
        }
    }

    private void OnEnable() {
        // 총 상태 초기화
        isReloadingLocal = false;
        lastFireTime = 0;
    }

    // 발사 시도
    public void Fire() {
        if (state == State.Ready && Time.time >= lastFireTime + gunData.timeBetFire)
        {
            if (magAmmo > 0)
            {
                lastFireTime = Time.time;
                FireServerRpc(fireTransform.position, fireTransform.forward);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc(Vector3 firePos, Vector3 fireDir) {
        if (magAmmoNetwork.Value <= 0 || isReloadingLocal) return;

        magAmmoNetwork.Value--;

        RaycastHit hit;
        Vector3 hitPosition = Vector3.zero;

        if (Physics.Raycast(firePos, fireDir, out hit, fireDistance))
        {
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null)
            {
                target.OnDamage(gunData.damage, hit.point, hit.normal);
            }
            hitPosition = hit.point;
        }
        else
        {
            hitPosition = firePos + fireDir * fireDistance;
        }

        ShotEffectClientRpc(hitPosition);
    }

    [ClientRpc]
    private void ShotEffectClientRpc(Vector3 hitPosition) {
        StartCoroutine(ShotEffect(hitPosition));
    }

    // 발사 이펙트와 소리를 재생하고 탄알 궤적을 그림
    private IEnumerator ShotEffect(Vector3 hitPosition) {
        if (muzzleFlashEffect != null) muzzleFlashEffect.Play();
        if (shellEjectEffect != null) shellEjectEffect.Play();

        if (gunAudioPlayer != null && gunData != null && gunData.shotClip != null)
        {
            gunAudioPlayer.PlayOneShot(gunData.shotClip);
        }

        if (bulletLineRenderer != null && fireTransform != null)
        {
            bulletLineRenderer.SetPosition(0, fireTransform.position);
            bulletLineRenderer.SetPosition(1, hitPosition);
            bulletLineRenderer.enabled = true;

            yield return new WaitForSeconds(0.03f);

            bulletLineRenderer.enabled = false;
        }
    }

    // 재장전 시도
    public bool Reload() {
        if (state == State.Reloading || ammoRemain <= 0 || magAmmo >= gunData.magCapacity)
        {
            return false;
        }

        StartCoroutine(ReloadLocalRoutine());
        ReloadServerRpc();
        return true;
    }

    private IEnumerator ReloadLocalRoutine() {
        isReloadingLocal = true;
        yield return new WaitForSeconds(gunData.reloadTime);
        isReloadingLocal = false;
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReloadServerRpc() {
        if (ammoRemainNetwork.Value <= 0 || magAmmoNetwork.Value >= gunData.magCapacity) return;
        StartCoroutine(ReloadRoutine());
    }

    // 실제 재장전 처리를 진행 (서버)
    private IEnumerator ReloadRoutine() {
        PlayReloadSoundClientRpc();

        yield return new WaitForSeconds(gunData.reloadTime);

        int ammoToFill = gunData.magCapacity - magAmmoNetwork.Value;

        if (ammoRemainNetwork.Value < ammoToFill)
        {
            ammoToFill = ammoRemainNetwork.Value;
        }

        magAmmoNetwork.Value += ammoToFill;
        ammoRemainNetwork.Value -= ammoToFill;
    }

    [ClientRpc]
    private void PlayReloadSoundClientRpc() {
        if (gunAudioPlayer != null && gunData != null && gunData.reloadClip != null)
        {
            gunAudioPlayer.PlayOneShot(gunData.reloadClip);
        }
    }
}