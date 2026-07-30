using Fusion;
using UnityEngine;

// Performans: Struct kullanýmý heap üzerinde bellek tahsisi (GC Alloc) yapmaz. 
public struct NetworkInputData : INetworkInput
{
    // DEÐÝÞÝKLÝK: Ýsimlendirmeler PlayerController.cs içindeki kullanýmýnla birebir eþleþtirildi.
    public Vector2 MoveDirection;
    public Vector2 RightJoystickVector;

    // Ateþ etme gibi aksiyonlar için GC dostu buton yapýsý.
    public NetworkButtons buttons;
}