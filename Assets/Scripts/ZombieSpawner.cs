using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 좀비 게임 오브젝트를 주기적으로 생성 (Netcode 네트워크 및 오프라인 플레이 호환)
public class ZombieSpawner : NetworkBehaviour {
    public Zombie zombiePrefab; // 생성할 좀비 원본 프리팹

    public ZombieData[] zombieDatas; // 사용할 좀비 셋업 데이터들
    public Transform[] spawnPoints; // 좀비 AI를 소환할 위치들

    private List<Zombie> zombies = new List<Zombie>(); // 생성된 좀비들을 담는 리스트
    private int localWave = 0;

    public NetworkVariable<int> waveNetwork = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> zombieCountNetwork = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool IsServerOrOffline {
        get {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return true; // 오프라인/직접 플레이 모드
            }
            return NetworkManager.Singleton.IsServer || IsServer;
        }
    }

    private void Start() {
        EnsureSpawnPoints();
        if (IsServerOrOffline)
        {
            ClearAllZombies();
        }
    }

    private void EnsureSpawnPoints() {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            GameObject pointsGroup = GameObject.Find("Spawn Points");
            if (pointsGroup != null)
            {
                List<Transform> list = new List<Transform>();
                foreach (Transform child in pointsGroup.transform)
                {
                    list.Add(child);
                }
                if (list.Count > 0)
                {
                    spawnPoints = list.ToArray();
                }
            }
        }
    }

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        waveNetwork.OnValueChanged += (prev, current) => UpdateUI();
        zombieCountNetwork.OnValueChanged += (prev, current) => UpdateUI();

        if (IsServer)
        {
            localWave = 0;
            waveNetwork.Value = 0;
            zombieCountNetwork.Value = 0;
            ClearAllZombies();
        }

        UpdateUI();
    }

    private void ClearAllZombies() {
        if (!IsServerOrOffline) return;

        Zombie[] existingZombies = FindObjectsOfType<Zombie>();
        foreach (var z in existingZombies)
        {
            if (z != null)
            {
                NetworkObject netObj = z.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                else if (z.gameObject != null)
                {
                    Destroy(z.gameObject);
                }
            }
        }
        zombies.Clear();
    }

    private void Update() {
        if (!IsServerOrOffline) return;

        // 게임 오버 상태일때는 생성하지 않음
        if (GameManager.instance != null && GameManager.instance.isGameover)
        {
            return;
        }

        // 좀비를 모두 물리친 경우 다음 스폰 실행
        if (zombies.Count <= 0)
        {
            SpawnWave();
        }
    }

    // 웨이브 정보를 UI로 표시
    private void UpdateUI() {
        if (UIManager.instance != null)
        {
            int currentWave = (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? waveNetwork.Value : localWave;
            int count = (IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) ? zombieCountNetwork.Value : zombies.Count;
            UIManager.instance.UpdateWaveText(currentWave, count);
        }
    }

    // 현재 웨이브에 맞춰 좀비들을 생성
    private void SpawnWave() {
        if (!IsServerOrOffline) return;

        localWave++;
        if (IsSpawned)
        {
            waveNetwork.Value = localWave;
        }

        int spawnCount = Mathf.RoundToInt(localWave * 1.5f);

        for (int i = 0; i < spawnCount; i++)
        {
            CreateZombie();
        }

        UpdateUI();
    }

    // 좀비를 생성하고 생성한 좀비에게 추적할 대상을 할당
    private void CreateZombie() {
        if (!IsServerOrOffline) return;

        EnsureSpawnPoints();

        if (zombiePrefab == null)
        {
            Debug.LogError("[ZombieSpawner] zombiePrefab이 비어있습니다. Inspector에서 프리팹을 할당하세요.");
            return;
        }

        if (zombieDatas == null || zombieDatas.Length == 0)
        {
            Debug.LogError("[ZombieSpawner] zombieDatas가 비어있습니다. Inspector에서 데이터를 할당하세요.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[ZombieSpawner] spawnPoints가 비어있습니다. 씬의 Spawn Points를 할당하세요.");
            return;
        }

        ZombieData zombieData = zombieDatas[Random.Range(0, zombieDatas.Length)];
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        Zombie zombie = Instantiate(zombiePrefab, spawnPoint.position, spawnPoint.rotation);

        NetworkObject netObj = zombie.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            netObj.Spawn();
        }

        zombie.Setup(zombieData);
        zombies.Add(zombie);

        if (IsSpawned)
        {
            zombieCountNetwork.Value = zombies.Count;
        }

        zombie.onDeath += () => {
            if (IsServerOrOffline)
            {
                zombies.Remove(zombie);
                if (IsSpawned)
                {
                    zombieCountNetwork.Value = zombies.Count;
                }
                if (GameManager.instance != null)
                {
                    GameManager.instance.AddScore(100);
                }
                UpdateUI();
                StartCoroutine(DespawnZombieAfterDelay(netObj, 10f));
            }
        };
    }

    private IEnumerator DespawnZombieAfterDelay(NetworkObject netObj, float delay) {
        yield return new WaitForSeconds(delay);
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else if (netObj != null && netObj.gameObject != null)
        {
            Destroy(netObj.gameObject);
        }
    }
}