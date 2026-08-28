using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리자 관련 코드
using UnityEngine.UI; // UI 관련 코드

// 필요한 UI에 즉시 접근하고 변경할 수 있도록 허용하는 UI 매니저
public class UIManager : MonoBehaviour {
    // 싱글톤 접근용 프로퍼티
    public static UIManager instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = FindObjectOfType<UIManager>();
            }

            return m_instance;
        }
    }

    private static UIManager m_instance; // 싱글톤이 할당될 변수

    public Text ammoText; // 탄약 표시용 텍스트
    public Text scoreText; // 점수 표시용 텍스트
    public Text waveText; // 적 웨이브 표시용 텍스트
    public GameObject gameoverUI; // 게임 오버시 활성화할 UI (Host / Single전용)
    public GameObject clientGameoverUI; // 클라이언트용 게임오버 UI (Waiting for Host...)

    // 탄약 텍스트 갱신
    public void UpdateAmmoText(int magAmmo, int remainAmmo) {
        if (ammoText != null) ammoText.text = magAmmo + "/" + remainAmmo;
    }

    // 점수 텍스트 갱신
    public void UpdateScoreText(int newScore) {
        if (scoreText != null) scoreText.text = "Score : " + newScore;
    }

    // 적 웨이브 텍스트 갱신
    public void UpdateWaveText(int waves, int count) {
        if (waveText != null) waveText.text = "Wave : " + waves + "\nEnemy Left : " + count;
    }

    // 게임 오버 UI 활성화 (Host와 Client 분기)
    public void SetActiveGameoverUI(bool active) {
        bool isHostOrOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        if (active)
        {
            if (isHostOrOffline)
            {
                if (gameoverUI != null) gameoverUI.SetActive(true);
                if (clientGameoverUI != null) clientGameoverUI.SetActive(false);
            }
            else
            {
                if (clientGameoverUI != null)
                {
                    clientGameoverUI.SetActive(true);
                    if (gameoverUI != null) gameoverUI.SetActive(false);
                }
                else if (gameoverUI != null)
                {
                    gameoverUI.SetActive(true);
                }
            }
        }
        else
        {
            if (gameoverUI != null) gameoverUI.SetActive(false);
            if (clientGameoverUI != null) clientGameoverUI.SetActive(false);
        }
    }

    // 게임 재시작 (멀티플레이어 호환)
    public void GameRestart() {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("main", LoadSceneMode.Single);
        }
        else if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}