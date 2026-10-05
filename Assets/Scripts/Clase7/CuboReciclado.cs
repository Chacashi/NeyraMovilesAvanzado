using Unity.Netcode;
using UnityEngine;

public class CuboReciclado : NetworkBehaviour
{
    public NetworkVariable<int> Tipo = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool ReclamadoAlAparecer { get; private set; } = true;
    public bool Guardandose { get; private set; }
    private int siguienteTipo = 1;
    private Rigidbody body;

    private void Awake() => body = GetComponent<Rigidbody>();

    // Datos privados pendientes, sin escribir NetworkVariables antes de su inicializacion.
    public void Preparar(int tipo, bool reclamado)
    {
        siguienteTipo = tipo;
        ReclamadoAlAparecer = reclamado;
        Guardandose = false;
    }

    public override void OnNetworkSpawn()
    {
        Guardandose = false;
        if (IsServer)
        {
            Tipo.Value = siguienteTipo;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else body.isKinematic = true;
        body.WakeUp();
        Debug.Log($"[Pool] nueva vida de {GetInstanceID()}, NetworkObjectId={NetworkObjectId}, tipo={Tipo.Value}");
    }

    public bool MarcarGuardadoServidor()
    {
        if (!IsServer || !IsSpawned || Guardandose) return false;
        Guardandose = true;
        return true;
    }

    public override void OnNetworkDespawn()
    {
        Guardandose = false;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        body.Sleep();
        siguienteTipo = 1;
        ReclamadoAlAparecer = true;
    }
}
