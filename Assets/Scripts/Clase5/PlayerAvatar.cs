using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAvatar : NetworkBehaviour
{
    public InputSystem_Actions inputs;
    public float Speed = 5f;
    public Chest chest;

    [Header("Interaccion con el cubo")]
    public float interactRadius = 2.5f;
    public Vector3 throwForce = new(0f, 3f, 8f); // Velocidad inicial en unidades/segundo.
    public float FallLimit = -10f;

    private Vector2 moveInput;
    private ColorCube heldCube; // Solo el servidor decide que cubo sostiene este avatar.
    private Chest openingChest;
    private NetworkTransform networkTransform;
    private Vector3 spawnPoint;
    private float fallSpeed;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        inputs = new InputSystem_Actions();
        inputs.Player.Move.performed += OnMove;
        inputs.Player.Move.canceled += OnMove;
        // Interact tiene Hold: started llega al pulsar; canceled al soltar E.
        inputs.Player.Interact.started += OnInteractPressed;
        inputs.Player.Interact.canceled += OnInteractReleased;
        inputs.Player.Attack.started += OnThrowPressed;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        spawnPoint = new Vector3(((int)(OwnerClientId % 4) - 1.5f) * 2f, 0f, 0f);
        transform.position = spawnPoint;
        inputs.Player.Enable();
    }

    private void OnEnable()
    {
        if (IsSpawned && IsOwner) inputs.Player.Enable();
    }

    private void OnDisable()
    {
        if (IsSpawned && IsOwner && NetworkManager.IsListening && !NetworkManager.ShutdownInProgress)
            SetInteractionRpc(false);
        inputs?.Disable();
        moveInput = Vector2.zero;
    }

    public override void OnNetworkDespawn()
    {
        inputs.Disable();
        moveInput = Vector2.zero;
        if (IsServer)
        {
            if (openingChest != null) openingChest.CancelOpeningServer(OwnerClientId);
            if (heldCube != null && heldCube.IsSpawned) heldCube.ReturnToSpawnServer();
        }
    }

    public override void OnDestroy()
    {
        inputs?.Dispose();
        base.OnDestroy();
    }

    private void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (IsSpawned && IsOwner) SetInteractionRpc(true);
    }

    private void OnInteractReleased(InputAction.CallbackContext context)
    {
        if (IsSpawned && IsOwner && NetworkManager.IsListening && !NetworkManager.ShutdownInProgress)
            SetInteractionRpc(false);
    }

    private void OnThrowPressed(InputAction.CallbackContext context)
    {
        if (IsSpawned && IsOwner) ThrowCubeRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetInteractionRpc(bool pressed)
    {
        if (!pressed)
        {
            if (openingChest != null) openingChest.CancelOpeningServer(OwnerClientId);
            openingChest = null;
            return;
        }

        if (heldCube != null && heldCube.IsSpawned && heldCube.HeldBy.Value == OwnerClientId)
        {
            ReleaseCubeServer(Vector3.zero); // E mientras llevas un cubo: soltar.
            return;
        }
        heldCube = null;

        // Elegir el cubo libre mas cercano, no el primero del array ni el de otro jugador.
        ColorCube nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider col in Physics.OverlapSphere(transform.position, interactRadius))
        {
            ColorCube cube = col.GetComponentInParent<ColorCube>();
            if (cube == null || !cube.IsSpawned || cube.IsHeld) continue;
            float distance = (cube.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearest = cube;
            nearestDistance = distance;
        }
        if (nearest != null && nearest.TryGrabServer(NetworkObject))
        {
            heldCube = nearest;
            return;
        }

        // Cada cofre tiene su bloqueo y contador. Elegir el mas cercano dentro de alcance.
        chest = null;
        float nearestChestDistance = float.PositiveInfinity;
        foreach (Chest candidate in FindObjectsByType<Chest>(FindObjectsSortMode.None))
        {
            if (!candidate.IsSpawned || !candidate.IsInRange(transform.position)) continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance >= nearestChestDistance) continue;
            chest = candidate;
            nearestChestDistance = distance;
        }
        if (chest != null && chest.TryStartOpeningServer(this)) openingChest = chest;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ThrowCubeRpc()
    {
        ReleaseCubeServer(transform.TransformDirection(throwForce));
    }

    private void ReleaseCubeServer(Vector3 velocity)
    {
        if (heldCube == null || !heldCube.IsSpawned) return;
        Vector3 releasePoint = transform.position + transform.forward * 1.6f + Vector3.up * 0.3f;
        if (heldCube.ReleaseServer(OwnerClientId, releasePoint, velocity)) heldCube = null;
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;
        Vector3 dir = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0f, moveInput.y), 1f);
        transform.position += dir * Speed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);

        // El avatar solo sincroniza X/Z: la caida local termina con Teleport del propietario.
        bool grounded = false;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * 0.5f,
                     Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<PlayerAvatar>() != null ||
                hit.collider.GetComponentInParent<ColorCube>() != null ||
                hit.collider.GetComponentInParent<Chest>() != null) continue;
            grounded = true;
            break;
        }
        if (!grounded)
        {
            fallSpeed += 9.81f * Time.deltaTime;
            transform.position += Vector3.down * fallSpeed * Time.deltaTime;
        }
        else fallSpeed = 0f;

        if (transform.position.y < FallLimit)
        {
            SetInteractionRpc(false);
            ReturnHeldCubeRpc();
            fallSpeed = 0f;
            networkTransform.Teleport(spawnPoint, Quaternion.identity, transform.localScale);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReturnHeldCubeRpc()
    {
        if (heldCube != null && heldCube.IsSpawned && heldCube.HeldBy.Value == OwnerClientId)
            heldCube.ReturnToSpawnServer();
        heldCube = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
