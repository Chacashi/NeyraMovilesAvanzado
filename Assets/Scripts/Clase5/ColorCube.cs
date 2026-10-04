using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class ColorCube : NetworkBehaviour
{
    private static readonly Color[] Colors = { Color.red, Color.green, Color.blue, Color.yellow };

    public Vector3 HandSlot = new(0.8f, 0.2f, 0.8f);
    public float FallLimit = -10f;
    public float MaxDistanceFromSpawn = 100f;
    public NetworkVariable<ulong> HeldBy = new(ulong.MaxValue,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool IsHeld => HeldBy.Value != ulong.MaxValue;

    private Vector3 spawnPoint;
    private Quaternion spawnRotation;
    private NetworkTransform networkTransform;
    private NetworkRigidbody networkBody;
    private Collider cubeCollider;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        networkBody = GetComponent<NetworkRigidbody>();
        cubeCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        GetComponent<Renderer>().material.color = Colors[NetworkObjectId % (ulong)Colors.Length];
        spawnPoint = transform.position;
        spawnRotation = transform.rotation;
        HeldBy.OnValueChanged += OnHeldChanged;
        ApplyHeldVisual();
    }

    public override void OnNetworkDespawn()
    {
        HeldBy.OnValueChanged -= OnHeldChanged;
    }

    private void OnHeldChanged(ulong previous, ulong current) => ApplyHeldVisual();

    private void ApplyHeldVisual()
    {
        // El cubo sostenido no empuja al avatar ni participa en otras recogidas.
        cubeCollider.enabled = !IsHeld;
    }

    public bool TryGrabServer(NetworkObject user)
    {
        if (!IsServer || !IsSpawned || IsHeld || user == null || !user.IsSpawned) return false;
        networkBody.SetLinearVelocity(Vector3.zero);
        networkBody.SetAngularVelocity(Vector3.zero);
        networkBody.SetIsKinematic(true);
        if (!NetworkObject.TrySetParent(user, false))
        {
            networkBody.SetIsKinematic(false);
            return false;
        }

        HeldBy.Value = user.OwnerClientId;
        // Switch Transform Space When Parented convierte a coordenadas locales.
        networkTransform.Teleport(HandSlot, Quaternion.identity, transform.localScale);
        return true;
    }

    public bool ReleaseServer(ulong clientId, Vector3 position, Vector3 velocity)
    {
        if (!IsServer || !IsSpawned || HeldBy.Value != clientId) return false;
        if (!NetworkObject.TryRemoveParent(true)) return false;

        // Desemparentar conservando el mundo; Teleport descarta la interpolacion anterior.
        networkTransform.Teleport(position, transform.rotation, transform.localScale);
        HeldBy.Value = ulong.MaxValue;
        networkBody.SetIsKinematic(false);
        networkBody.SetAngularVelocity(Vector3.zero);
        networkBody.SetLinearVelocity(velocity);
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        if (IsHeld)
        {
            if (!NetworkManager.ConnectedClients.ContainsKey(HeldBy.Value)) ReturnToSpawnServer();
            return;
        }

        if (transform.position.y < FallLimit ||
            (transform.position - spawnPoint).sqrMagnitude > MaxDistanceFromSpawn * MaxDistanceFromSpawn)
            ReturnToSpawnServer();
    }

    public void ReturnToSpawnServer()
    {
        if (!IsServer || !IsSpawned) return;
        if (IsHeld && !NetworkObject.TryRemoveParent(true)) return;
        HeldBy.Value = ulong.MaxValue;
        networkBody.SetIsKinematic(false);
        networkBody.SetLinearVelocity(Vector3.zero);
        networkBody.SetAngularVelocity(Vector3.zero);
        networkTransform.Teleport(spawnPoint, spawnRotation, transform.localScale);
    }
}
