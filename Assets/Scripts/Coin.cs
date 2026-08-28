using Unity.Netcode;
using UnityEngine;

// 게임 점수를 증가시키는 아이템 (Netcode 호환)
public class Coin : NetworkBehaviour, IItem {
    public int score = 200; // 증가할 점수

    public void Use(GameObject target) {
        // 게임 매니저로 접근해 점수 추가
        if (GameManager.instance != null)
        {
            GameManager.instance.AddScore(score);
        }

        // 서버 권한으로 자신을 Despawn
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}