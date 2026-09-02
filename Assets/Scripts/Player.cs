using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    private static readonly string[] RandomNames = { "Alfredo", "Ermenegildo", "Chacashi", "Pepito", "Margarita", "Tobias" };

    private static readonly Color[] Palette =
    {
        new(1f, 0.38f, 0.38f),      // rojo coral
        new(0.37f, 0.66f, 1f),      // azul
        new(0.48f, 0.88f, 0.36f),   // verde
        new(1f, 0.84f, 0.25f),      // amarillo
        new(0.83f, 0.52f, 1f),      // morado
        new(1f, 0.55f, 0.16f),      // naranja
    };

    // Se sincronizan a TODOS los clientes: nombre, color y logs del jugador.
    public NetworkVariable<FixedString64Bytes> DisplayName = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> PlayerColor = new(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> LogsCollected = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Mapas visibles en cada cliente: ClientId -> nombre / color del jugador.
    // Asi cualquier script (tablero, troncos) sabe quien es cada jugador.
    public static readonly Dictionary<ulong, string> NameByClientId = new();
    public static readonly Dictionary<ulong, Color> ColorByClientId = new();

    // Se dispara cuando cambia la identidad (o aparece/desaparece) un jugador.
    public static event Action PlayerIdentityChanged;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            DisplayName.Value = RandomNames[UnityEngine.Random.Range(0, RandomNames.Length)];
            PlayerColor.Value = Palette[UnityEngine.Random.Range(0, Palette.Length)];
        }

        PlayerColor.OnValueChanged += OnColorChanged;

        RegisterLocally();
        PaintCube();
        PlayerIdentityChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        PlayerColor.OnValueChanged -= OnColorChanged;
        UnregisterLocally();
        PlayerIdentityChanged?.Invoke();
    }

    private void OnColorChanged(Color previous, Color current)
    {
        RegisterLocally();
        PaintCube();
        PlayerIdentityChanged?.Invoke();
    }

    private void RegisterLocally()
    {
        if (!IsSpawned || NetworkObject == null) return;

        NameByClientId[OwnerClientId] = DisplayName.Value.ToString();
        ColorByClientId[OwnerClientId] = PlayerColor.Value;
    }

    private void UnregisterLocally()
    {
        NameByClientId.Remove(OwnerClientId);
        ColorByClientId.Remove(OwnerClientId);
    }

    private void PaintCube()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = PlayerColor.Value;
    }

    // Solo el servidor puede sumar logs al marcador del jugador.
    public void AddCollectedLogServer(int amount)
    {
        if (!IsServer) return;
        LogsCollected.Value += amount;
    }
}