using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Clase6LootCube : NetworkBehaviour
{
    // Host reclamante y servidor sin propietario humano comparten ID 0: distinguirlos.
    public NetworkVariable<bool> Claimed = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private ulong lastAssignedOwner;
    private Material bodyMaterial;
    private Vector3 spawnPoint;
    private NetworkRigidbody networkBody;
    private NetworkTransform networkTransform;

    private void Awake()
    {
        bodyMaterial = GetComponent<Renderer>().material;
        networkBody = GetComponent<NetworkRigidbody>();
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CuboReciclado state = GetComponent<CuboReciclado>();
            Claimed.Value = state == null || state.ReclamadoAlAparecer;
        }
        lastAssignedOwner = OwnerClientId;
        spawnPoint = transform.position;
        Claimed.OnValueChanged += OnClaimChanged;
        Repaint();
        Debug.Log($"Loot OnNetworkSpawn: #{NetworkObjectId}, dueño {OwnerClientId}");
    }

    public override void OnNetworkDespawn()
    {
        Claimed.OnValueChanged -= OnClaimChanged;
        lastAssignedOwner = 0;
        spawnPoint = Vector3.zero;
        bodyMaterial.color = Color.gray;
    }
    private void OnClaimChanged(bool previous, bool current) => Repaint();
    private void Repaint() { if (IsSpawned) bodyMaterial.color = Claimed.Value ? Clase6Player.ColorForOwner(OwnerClientId) : Color.gray; }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        Repaint();
        Debug.Log($"Loot OnOwnershipChanged: #{NetworkObjectId}, {previous} -> {current}");
    }
    public override void OnGainedOwnership() { Repaint(); Debug.Log($"OnGainedOwnership: cubo #{NetworkObjectId}"); }
    public override void OnLostOwnership() { Repaint(); Debug.Log($"OnLostOwnership: ya no controlo el cubo #{NetworkObjectId}"); }

    private bool InRange(Clase6Player player) => player != null && player.IsSpawned &&
        (player.transform.position - transform.position).sqrMagnitude <= player.InteractRange * player.InteractRange;

    public bool ClaimServer(Clase6Player player)
    {
        if (!IsServer || !IsSpawned || Claimed.Value || !InRange(player)) return false;
        lastAssignedOwner = player.OwnerClientId;
        if (OwnerClientId != lastAssignedOwner) NetworkObject.ChangeOwnership(lastAssignedOwner);
        Claimed.Value = true;
        return true;
    }

    public bool GiftServer(Clase6Player sender, Clase6Player receiver)
    {
        if (!IsServer || !IsSpawned || !Claimed.Value || !InRange(sender) || OwnerClientId != sender.OwnerClientId ||
            receiver == null || !receiver.IsSpawned || receiver.OwnerClientId == sender.OwnerClientId ||
            !NetworkManager.ConnectedClients.ContainsKey(receiver.OwnerClientId) ||
            (receiver.transform.position - sender.transform.position).sqrMagnitude > sender.GiftRange * sender.GiftRange) return false;
        lastAssignedOwner = receiver.OwnerClientId;
        NetworkObject.ChangeOwnership(receiver.OwnerClientId);
        receiver.MessageRpc($"El jugador {sender.OwnerClientId} te regalo un cubo.");
        return true;
    }

    public bool ConsumeServer(Clase6Player player)
    {
        if (!IsServer || !IsSpawned || !Claimed.Value || !InRange(player) || OwnerClientId != player.OwnerClientId) return false;
        NetworkObject.Despawn(true);
        return true;
    }

    public void ReleaseDisconnectedServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned || !Claimed.Value || lastAssignedOwner != clientId) return;
        Claimed.Value = false;
        if (!NetworkObject.IsOwnedByServer) NetworkObject.RemoveOwnership();
        lastAssignedOwner = NetworkManager.ServerClientId;
        Repaint();
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || transform.position.y >= -10f) return;
        networkBody.SetLinearVelocity(Vector3.zero);
        networkBody.SetAngularVelocity(Vector3.zero);
        networkTransform.Teleport(spawnPoint, Quaternion.identity, transform.localScale);
    }

    public override void OnDestroy()
    {
        if (bodyMaterial != null) Destroy(bodyMaterial);
        base.OnDestroy();
    }
}
