using System;
using Unity.Netcode;

// Solo valores: ninguna referencia a GameObject, string ni List. Struct unmanaged.
public struct Ranura : INetworkSerializable, IEquatable<Ranura>
{
    public int tipo;
    public int cantidad;

    public Ranura(int tipo, int cantidad) { this.tipo = tipo; this.cantidad = cantidad; }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tipo);
        serializer.SerializeValue(ref cantidad);
    }

    public bool Equals(Ranura other) => tipo == other.tipo && cantidad == other.cantidad;
    public override bool Equals(object obj) => obj is Ranura other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(tipo, cantidad);
}
