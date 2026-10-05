using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Base del profesor: la aprobacion sigue siendo responsabilidad de este MonoBehaviour.
// Las peticiones RPC viven en Clase6Player, que si es un NetworkBehaviour.
public class PlayerAssigner : MonoBehaviour
{
    public NetworkObject GuardianPrefab;
    public NetworkObject ExplorerPrefab;
    public NetworkObject ChestPrefab;
    [Min(1)] public int MaxPlayers = 3;
    public string ServerAddress = "127.0.0.1";
    public ushort Port = 7786;
    public NetworkManager Manager => GetComponent<NetworkManager>();
    public bool HasGuardian => roles.ContainsValue(PlayerRole.Guardian);
    private readonly Dictionary<ulong, PlayerRole> roles = new();
    private readonly Dictionary<ulong, int> spawnSlots = new();
    private readonly Dictionary<ulong, double> nextChestTimes = new();

    private void Awake()
    {
        Manager.NetworkConfig.ConnectionApproval = true;
        Manager.ConnectionApprovalCallback = Aprrove;
        Manager.OnClientDisconnectCallback += OnDisconnected;
    }

    public void PrepareConnection(PlayerRole role, bool host)
    {
        if (Manager.IsListening) return;
        if (host) { roles.Clear(); spawnSlots.Clear(); nextChestTimes.Clear(); }
        Manager.NetworkConfig.ConnectionData = new[] { (byte)role };
        GetComponent<UnityTransport>().SetConnectionData(ServerAddress, Port, "0.0.0.0");
    }

    private void Aprrove(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Pending = false;
        response.Approved = false;
        response.CreatePlayerObject = false;
        bool valid = request.Payload != null && request.Payload.Length == 1 && request.Payload[0] <= 1;
        // NGO no permite rechazar al host. Si arranca desde el Inspector, usar Guardian por defecto.
        if (!valid && request.ClientNetworkId != NetworkManager.ServerClientId)
        {
            response.Reason = "Seleccion de personaje invalida.";
            return;
        }
        PlayerRole role = valid ? (PlayerRole)request.Payload[0] : PlayerRole.Guardian;
        if (roles.Count >= MaxPlayers)
        {
            response.Reason = "La partida ya tiene tres participantes.";
            return;
        }
        if (role == PlayerRole.Guardian && HasGuardian)
        {
            response.Reason = "Ya hay un Guardian. Elige Explorador.";
            return;
        }
        NetworkObject prefab = role == PlayerRole.Guardian ? GuardianPrefab : ExplorerPrefab;
        if (prefab == null)
        {
            response.Reason = "El prefab del rol no esta configurado.";
            return;
        }
        int slot = 0;
        while (spawnSlots.ContainsValue(slot)) slot++;
        spawnSlots[request.ClientNetworkId] = slot;
        roles[request.ClientNetworkId] = role;
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = prefab.PrefabIdHash;
        response.Position = new Vector3((slot - 1) * 3f, 1f, 0f);
        response.Rotation = Quaternion.identity;
        Debug.Log($"Approval: cliente {request.ClientNetworkId} -> {role}");
    }

    public bool IsCurrentPlayer(Clase6Player player)
    {
        return Manager.IsServer && player != null && player.IsSpawned &&
            Manager.ConnectedClients.TryGetValue(player.OwnerClientId, out NetworkClient client) &&
            client.PlayerObject == player.NetworkObject;
    }

    public bool IsGuardian(Clase6Player player)
    {
        return IsCurrentPlayer(player) && roles.TryGetValue(player.OwnerClientId, out PlayerRole role) &&
            role == PlayerRole.Guardian;
    }

    public void CreateChestServer(Clase6Player player)
    {
        if (!IsCurrentPlayer(player)) return;
        if (!IsGuardian(player))
        {
            player.MessageRpc("Solo el Guardian puede crear cofres.");
            return;
        }
        if (nextChestTimes.TryGetValue(player.OwnerClientId, out double nextTime) && Time.timeAsDouble < nextTime)
        {
            player.MessageRpc("Espera 2 segundos entre solicitudes.");
            return;
        }
        nextChestTimes[player.OwnerClientId] = Time.timeAsDouble + 2;
        Vector3 position = player.transform.position + player.transform.forward * 2.5f;
        position.y = 0.6f;
        if (Mathf.Abs(position.x) > 8f || Mathf.Abs(position.z) > 8f)
        {
            player.MessageRpc("Gira hacia el centro: el cofre debe quedar dentro del mapa.");
            return;
        }
        Physics.SyncTransforms();
        if (Physics.CheckBox(position, Vector3.one * 0.45f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
        {
            player.MessageRpc("Hay un objeto delante: busca espacio libre.");
            return;
        }
        NetworkObject chest = Manager.SpawnManager.InstantiateAndSpawn(ChestPrefab,
            position: position, rotation: player.transform.rotation);
        player.MessageRpc(chest != null ? "Cofre creado. Acercate y pulsa E." : "No se pudo crear el cofre.");
    }

    // Ejemplo de reemplazo del profesor: llamado por SwitchCharacterRpc del jugador.
    public void SwitchCharacterServer(Clase6Player oldPlayer)
    {
        if (!IsCurrentPlayer(oldPlayer)) return;
        PlayerRole nextRole = oldPlayer.Role == PlayerRole.Guardian ? PlayerRole.Explorer : PlayerRole.Guardian;
        if (nextRole == PlayerRole.Guardian && HasGuardian)
        {
            oldPlayer.MessageRpc("El puesto de Guardian esta ocupado.");
            return;
        }
        ulong id = oldPlayer.OwnerClientId;
        NetworkObject prefab = nextRole == PlayerRole.Guardian ? GuardianPrefab : ExplorerPrefab;
        NetworkObject replacement = Instantiate(prefab, oldPlayer.transform.position, oldPlayer.transform.rotation);
        InventarioDeRed oldInventory = oldPlayer.GetComponent<InventarioDeRed>();
        InventarioDeRed newInventory = replacement.GetComponent<InventarioDeRed>();
        if (oldInventory != null && newInventory != null)
            newInventory.RestaurarAlAparecer(oldInventory.ExtraerParaCambioServidor());
        roles[id] = nextRole;
        oldPlayer.NetworkObject.Despawn(true);
        replacement.SpawnAsPlayerObject(id);
        replacement.GetComponent<Clase6Player>().MessageRpc($"Ahora eres {nextRole}.");
    }

    private void OnDisconnected(ulong clientId)
    {
        if (!Manager.IsServer || Manager.ShutdownInProgress) return;
        // La cache del cubo conserva el dueño previo aunque NGO ya lo haya retirado.
        foreach (Clase6LootCube cube in FindObjectsByType<Clase6LootCube>(FindObjectsSortMode.None))
            if (cube.IsSpawned) cube.ReleaseDisconnectedServer(clientId);
        roles.Remove(clientId);
        spawnSlots.Remove(clientId);
        nextChestTimes.Remove(clientId);
    }

    private void OnDestroy()
    {
        if (Manager == null) return;
        Manager.OnClientDisconnectCallback -= OnDisconnected;
        Manager.ConnectionApprovalCallback -= Aprrove;
    }
}
