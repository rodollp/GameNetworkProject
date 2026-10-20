
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CoinSpawner : NetworkBehaviour
{
    [Header("Coin Settings")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int spawnCount = 5;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    // 이미 사용한 SpawnPoint의 인덱스를 저장
    private HashSet<int> usedIndices = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        SpawnCoins();
    }

    private void SpawnCoins()
    {
        if (coinPrefab == null || spawnPoints == null)
            return;

        // 사용 가능한 위치보다 많은 코인을 생성하지 않도록 제한
        int targetCount = Mathf.Min(spawnCount, spawnPoints.Length);

        usedIndices.Clear();

        int spawnedCount = 0;

        while (spawnedCount < targetCount)
        {
            // 랜덤 인덱스 선택
            int randomIndex = Random.Range(0, spawnPoints.Length);

            // 이미 사용한 위치라면 다시 선택
            if (!usedIndices.Add(randomIndex))
                continue;

            Transform spawnPoint = spawnPoints[randomIndex];

            if (spawnPoint == null)
                continue;

            // 코인 생성
            GameObject coin = Instantiate(
                coinPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            // 네트워크에 코인 등록
            NetworkObject networkObject =
                coin.GetComponent<NetworkObject>();

            networkObject.Spawn();

            spawnedCount++;

            Debug.Log(
                $"Coin Spawn! Index: {randomIndex}, " +
                $"NetworkObjectId: {networkObject.NetworkObjectId}"
            );
        }
    }
}
