using Unity.Netcode;
using UnityEngine;

// 플레이어의 위치 및 회전을 클라이언트(Owner) 권한으로 동기화하는 컴포넌트
public class PositionSync : NetworkBehaviour
{
    private Vector3 _lastPosition; // 마지막으로 동기화된 위치
    private Quaternion _lastRotation; // 마지막으로 동기화된 회전

    // 위치/회전 동기화를 위한 네트워크 변수 (Owner 쓰기 권한)
    public NetworkVariable<Vector3> networkPosition 
        = new NetworkVariable<Vector3>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Owner);

    public NetworkVariable<Quaternion> networkRotation 
        = new NetworkVariable<Quaternion>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            networkPosition.Value = transform.position;
            networkRotation.Value = transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            if (Vector3.Distance(_lastPosition, transform.position) > 0.001f)
            {
                _lastPosition = transform.position;
                networkPosition.Value = _lastPosition;
            }

            if (Quaternion.Angle(_lastRotation, transform.rotation) > 0.01f)
            {
                _lastRotation = transform.rotation;
                networkRotation.Value = _lastRotation;
            }
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 20f);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation.Value, Time.deltaTime * 20f);
        }
    }
}