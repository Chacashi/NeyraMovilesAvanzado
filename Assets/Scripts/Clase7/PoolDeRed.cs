using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// El mismo componente se registra en TODAS las maquinas antes de conectar.
public class PoolDeRed : MonoBehaviour, INetworkPrefabInstanceHandler
{
    public NetworkObject Prefab;
    [Min(0)] public int Precalentar = 12;
    [Min(1)] public int MaxInactivos = 64;
    private readonly Stack<NetworkObject> libres = new();
    private readonly HashSet<NetworkObject> enPool = new();
    private readonly HashSet<NetworkObject> creados = new();
    private NetworkManager manager;
    public int ObjetosCreados => creados.Count;
    public int ObjetosDisponibles => enPool.Count;

    private void Awake()
    {
        manager = GetComponent<NetworkManager>();
        if (!manager.PrefabHandler.AddHandler(Prefab, this))
            Debug.LogError("El prefab ya tiene otro handler de pool registrado.");
        for (int i = 0; i < Mathf.Min(Precalentar, MaxInactivos); i++) Destroy(Crear());
    }

    private NetworkObject Crear()
    {
        NetworkObject instance = UnityEngine.Object.Instantiate(Prefab);
        // Evita perder los inactivos cuando NGO sincroniza/carga la escena del cliente.
        DontDestroyOnLoad(instance.gameObject);
        creados.Add(instance);
        return instance;
    }

    // NGO llama esto en clientes. El servidor toma una instancia explicitamente en SpawnServidor.
    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        NetworkObject instance = null;
        while (libres.Count > 0 && instance == null)
        {
            instance = libres.Pop();
            enPool.Remove(instance);
        }
        if (instance == null) instance = Crear();
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Prefab.transform.localScale;
        instance.gameObject.SetActive(true);
        return instance;
    }

    public NetworkObject SpawnServidor(Vector3 position, Quaternion rotation, ulong owner, int tipo = 1, bool reclamado = true)
    {
        if (!manager.IsServer || !manager.IsListening || manager.ShutdownInProgress) return null;
        NetworkObject instance = Instantiate(owner, position, rotation);
        instance.GetComponent<CuboReciclado>().Preparar(tipo, reclamado);
        instance.DontDestroyWithOwner = true;
        instance.SpawnWithOwnership(owner);
        return instance;
    }

    // Despawn(true) llega aqui en vez de destruir el GameObject: se conserva la instancia.
    public void Destroy(NetworkObject instance)
    {
        if (instance == null || !enPool.Add(instance)) return; // Proteccion frente a devoluciones duplicadas.
        instance.gameObject.SetActive(false);
        if (enPool.Count > MaxInactivos)
        {
            enPool.Remove(instance);
            creados.Remove(instance);
            UnityEngine.Object.Destroy(instance.gameObject);
            return;
        }
        libres.Push(instance);
    }

    public void DejarTrasDesconexion(List<Ranura> datos, Vector3 position)
    {
        StartCoroutine(DejarDespuesDelDespawn(datos, position));
    }

    private IEnumerator DejarDespuesDelDespawn(List<Ranura> datos, Vector3 position)
    {
        // No modificar la coleccion de spawns mientras NGO despawnea al jugador.
        yield return null;
        int index = 0;
        foreach (Ranura ranura in datos)
        {
            for (int i = 0; i < ranura.cantidad; i++)
            {
                if (!manager.IsServer || manager.ShutdownInProgress) yield break;
                Vector3 offset = Quaternion.Euler(0f, index++ * 137.5f, 0f) * Vector3.forward * (0.5f + index * 0.025f);
                Vector3 drop = position + offset;
                drop.x = Mathf.Clamp(drop.x, -9f, 9f);
                drop.z = Mathf.Clamp(drop.z, -9f, 9f);
                drop.y = 0.8f;
                SpawnServidor(drop, Quaternion.identity, NetworkManager.ServerClientId, ranura.tipo, false);
            }
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        if (manager != null && Prefab != null) manager.PrefabHandler.RemoveHandler(Prefab);
        foreach (NetworkObject instance in creados)
            if (instance != null && !instance.IsSpawned) UnityEngine.Object.Destroy(instance.gameObject);
        libres.Clear(); enPool.Clear(); creados.Clear();
    }
}
