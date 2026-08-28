using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 점수와 게임 오버 여부를 관리하는 게임 매니저 (Netcode 호환)
public class GameManager : NetworkBehaviour {
    // 싱글톤 접근용 프로퍼티
    public static GameManager instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindObjectOfType<GameManager>();
            }
            return m_instance;
        }
    }

    private static GameManager m_instance; // 싱글톤 변수

    public Transform[] playerSpawnPoints; // 플레이어 스폰 위치들
    public GameObject fallbackPlayerPrefab; // 백업용 플레이어 프리팹

    public NetworkVariable<int> scoreNetwork = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isGameoverNetwork = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int score => scoreNetwork.Value; // 현재 게임 점수
    public bool isGameover => isGameoverNetwork.Value; // 게임 오버 상태

    private void Awake() {
        if (instance != this && m_instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            m_instance = this;
        }
    }

    private void Start() {
        bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (isOffline)
        {
            SpawnOfflinePlayer();
        }
    }

    private void SpawnOfflinePlayer()
    {
        PlayerHealth[] existingPlayers = FindObjectsOfType<PlayerHealth>();
        if (existingPlayers != null && existingPlayers.Length > 0) return;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (playerSpawnPoints != null && playerSpawnPoints.Length > 0 && playerSpawnPoints[0] != null)
        {
            spawnPos = playerSpawnPoints[0].position;
            spawnRot = playerSpawnPoints[0].rotation;
        }

        GameObject prefabToSpawn = fallbackPlayerPrefab;
        if (prefabToSpawn == null && NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
        {
            prefabToSpawn = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        }

        if (prefabToSpawn != null)
        {
            GameObject playerInstance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            PlayerHealth playerHealth = playerInstance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.onDeath += CheckAllPlayersDead;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        scoreNetwork.OnValueChanged += OnScoreChanged;
        isGameoverNetwork.OnValueChanged += OnGameoverChanged;

        if (IsServer)
        {
            scoreNetwork.Value = 0;
            isGameoverNetwork.Value = false;

            SpawnPlayers();
        }

        UpdateScoreUI();
        UpdateGameOverUI();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        scoreNetwork.OnValueChanged -= OnScoreChanged;
        isGameoverNetwork.OnValueChanged -= OnGameoverChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnScoreChanged(int previous, int current) {
        UpdateScoreUI();
    }

    private void OnGameoverChanged(bool previous, bool current) {
        UpdateGameOverUI();
    }

    private void UpdateScoreUI() {
        if (UIManager.instance != null)
        {
            UIManager.instance.UpdateScoreText(scoreNetwork.Value);
        }
    }

    private void UpdateGameOverUI() {
        if (UIManager.instance != null)
        {
            UIManager.instance.SetActiveGameoverUI(isGameoverNetwork.Value);
        }
    }

    // 점수를 추가 (서버 권한)
    public void AddScore(int newScore) {
        if (!IsServer) return;

        if (!isGameoverNetwork.Value)
        {
            scoreNetwork.Value += newScore;
        }
    }

    // 게임 오버 처리 (서버 권한)
    public void EndGame() {
        if (!IsServer) return;

        isGameoverNetwork.Value = true;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            CheckAllPlayersDead();
        }
    }

    private void ClearExistingPlayers() {
        if (!IsServer) return;
        PlayerHealth[] oldPlayers = FindObjectsOfType<PlayerHealth>();
        foreach (var p in oldPlayers)
        {
            if (p != null)
            {
                NetworkObject netObj = p.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
                else if (p.gameObject != null)
                {
                    Destroy(p.gameObject);
                }
            }
        }
    }

    // 서버에서 모든 클라이언트 플레이어 캐릭터 생성
    private void SpawnPlayers()
    {
        if (!IsServer) return;

        ClearExistingPlayers();

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        int spawnIndex = 0;

        foreach (var client in clients)
        {
            ulong clientId = client.ClientId;

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0)
            {
                Transform sp = playerSpawnPoints[spawnIndex % playerSpawnPoints.Length];
                spawnPos = sp.position;
                spawnRot = sp.rotation;
            }

            GameObject prefabToSpawn = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
            if (prefabToSpawn == null)
            {
                prefabToSpawn = fallbackPlayerPrefab;
            }

            if (prefabToSpawn != null)
            {
                GameObject playerInstance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

                if (netObj != null)
                {
                    netObj.SpawnWithOwnership(clientId);
                }

                PlayerHealth playerHealth = playerInstance.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.onDeath += CheckAllPlayersDead;
                }
            }

            spawnIndex++;
        }
    }

    // 모든 플레이어 사망 여부 확인
    private void CheckAllPlayersDead()
    {
        if (!IsServer) return;

        PlayerHealth[] players = FindObjectsOfType<PlayerHealth>();
        bool allDead = true;

        if (players.Length == 0) return;

        foreach (var player in players)
        {
            if (!player.dead)
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
        {
            EndGame();
        }
    }
}