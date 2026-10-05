using Unity.Netcode;
using UnityEngine;

public class Clase6Chest : NetworkBehaviour
{
    public NetworkObject LootPrefab;
    [Min(1)] public int LootCount = 3;
    private bool opened;

    public bool TryOpenServer(Clase6Player player)
    {
        if (!IsServer || !IsSpawned || opened || player == null || !player.IsSpawned ||
            (player.transform.position - transform.position).sqrMagnitude > player.InteractRange * player.InteractRange) return false;
        opened = true;
        PoolDeRed pool = FindFirstObjectByType<PoolDeRed>();
        for (int i = 0; i < LootCount; i++)
        {
            Vector3 offset = Quaternion.Euler(0f, i * 360f / LootCount, 0f) * Vector3.forward * 1.2f;
            Vector3 point = transform.position + offset + Vector3.up;
            if (pool != null && pool.Prefab == LootPrefab)
                pool.SpawnServidor(point, Quaternion.identity, player.OwnerClientId);
            else
            {
                NetworkObject cube = Instantiate(LootPrefab, point, Quaternion.identity);
                cube.DontDestroyWithOwner = true;
                cube.SpawnWithOwnership(player.OwnerClientId);
            }
        }
        NetworkObject.Despawn(true); // Cofre dinamico, creado con InstantiateAndSpawn.
        return true;
    }
}
