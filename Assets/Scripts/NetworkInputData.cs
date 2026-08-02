using Fusion;
using UnityEngine;

// CS0101 çakýþmasýný önlemek için PlayerInputButtons olarak deðiþtirildi
public enum PlayerInputButtons
{
    Attack = 0,
    Jump = 1
}

// Sýnýf (class) deðil struct kullanýldý (Zero-GC tahsisi)
public struct NetworkInputData : INetworkInput
{
    public Vector2 JoystickInput;
    public NetworkButtons Buttons;
    public Vector2 RightJoystickVector;
}