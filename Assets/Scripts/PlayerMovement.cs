using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Networking;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private int speed;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        if (!IsOwner) return;
        Movement();
    }

    public void Movement()
    {
        float y = Input.GetAxisRaw("Vertical");
        float x = Input.GetAxisRaw("Horizontal");

        if (y == 0 && x == 0) return;

        Vector3 dir = new Vector3(x, 0, y);

        transform.position += dir * speed * Time.deltaTime;


        MoveServerRpc(transform.position);
    }

    [ServerRpc]
    public void MoveServerRpc(Vector3 pos)
    {
        MoveClientRpc(pos);
    }

    [ClientRpc]
    public void MoveClientRpc(Vector3 pos)
    {
        if (IsOwner) return; //-> todos menos el dueño porque se entiende que este ya se movio!
        transform.position = pos;
    }
}
