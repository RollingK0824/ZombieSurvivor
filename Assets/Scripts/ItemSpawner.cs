using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI; // 내비메쉬 관련 코드

// 주기적으로 아이템을 플레이어 근처에 생성하는 스크립트 (Netcode 호환)
public class ItemSpawner : NetworkBehaviour {
    public GameObject[] items; // 생성할 아이템들
    public Transform playerTransform; // 플레이어의 트랜스폼 (단일 플레이어용 백업)

    public float maxDistance = 5f; // 플레이어 위치로부터 아이템이 배치될 최대 반경

    public float timeBetSpawnMax = 7f; // 최대 시간 간격
    public float timeBetSpawnMin = 2f; // 최소 시간 간격
    private float timeBetSpawn; // 생성 간격

    private float lastSpawnTime; // 마지막 생성 시점

    private void Start() {
        // 생성 간격과 마지막 생성 시점 초기화
        timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
        lastSpawnTime = 0;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer)
        {
            ClearAllItems();
        }
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            ClearAllItems();
        }
    }

    private void ClearAllItems() {
        AmmoPack[] ammoPacks = FindObjectsOfType<AmmoPack>();
        foreach (var a in ammoPacks)
        {
            if (a != null)
            {
                NetworkObject netObj = a.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
                else if (a.gameObject != null) Destroy(a.gameObject);
            }
        }

        Coin[] coins = FindObjectsOfType<Coin>();
        foreach (var c in coins)
        {
            if (c != null)
            {
                NetworkObject netObj = c.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
                else if (c.gameObject != null) Destroy(c.gameObject);
            }
        }

        HealthPack[] healthPacks = FindObjectsOfType<HealthPack>();
        foreach (var h in healthPacks)
        {
            if (h != null)
            {
                NetworkObject netObj = h.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) netObj.Despawn();
                else if (h.gameObject != null) Destroy(h.gameObject);
            }
        }
    }

    // 주기적으로 아이템 생성 처리 실행 (서버 전용)
    private void Update() {
        if (!IsServer) return;

        // 현재 시점이 마지막 생성 시점에서 생성 주기 이상 지남
        if (Time.time >= lastSpawnTime + timeBetSpawn)
        {
            Vector3 centerPos = GetRandomPlayerPosition();

            if (centerPos != Vector3.zero)
            {
                // 마지막 생성 시간 갱신
                lastSpawnTime = Time.time;
                // 생성 주기를 랜덤으로 변경
                timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
                // 아이템 생성 실행
                Spawn(centerPos);
            }
        }
    }

    private Vector3 GetRandomPlayerPosition() {
        PlayerHealth[] players = FindObjectsOfType<PlayerHealth>();
        var livingPlayers = new System.Collections.Generic.List<Transform>();

        foreach (var p in players)
        {
            if (p != null && !p.dead)
            {
                livingPlayers.Add(p.transform);
            }
        }

        if (livingPlayers.Count > 0)
        {
            return livingPlayers[Random.Range(0, livingPlayers.Count)].position;
        }

        if (playerTransform != null) return playerTransform.position;

        return Vector3.zero;
    }

    // 실제 아이템 생성 처리 (서버 전용)
    private void Spawn(Vector3 centerPosition) {
        if (!IsServer) return;
        if (items == null || items.Length == 0) return;

        // 플레이어 근처에서 내비메시 위의 랜덤 위치 가져오기
        Vector3 spawnPosition = GetRandomPointOnNavMesh(centerPosition, maxDistance);
        // 바닥에서 0.5만큼 위로 올리기
        spawnPosition += Vector3.up * 0.5f;

        // 아이템 중 하나를 무작위로 골라 랜덤 위치에 생성
        GameObject selectedItem = items[Random.Range(0, items.Length)];
        GameObject item = Instantiate(selectedItem, spawnPosition, Quaternion.identity);

        NetworkObject netObj = item.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
        }

        // 생성된 아이템을 5초 뒤에 파괴
        StartCoroutine(DespawnItemAfterDelay(netObj, 5f));
    }

    private IEnumerator DespawnItemAfterDelay(NetworkObject netObj, float delay) {
        yield return new WaitForSeconds(delay);
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
    }

    // 내비메시 위의 랜덤한 위치를 반환하는 메서드
    private Vector3 GetRandomPointOnNavMesh(Vector3 center, float distance) {
        Vector3 randomPos = Random.insideUnitSphere * distance + center;

        NavMeshHit hit;
        NavMesh.SamplePosition(randomPos, out hit, distance, NavMesh.AllAreas);

        return hit.position;
    }
}