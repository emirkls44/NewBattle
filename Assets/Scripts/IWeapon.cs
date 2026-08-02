using UnityEngine;
using Fusion;

public interface IWeapon
{
    void Shoot(Vector3 firePoint, Vector2 aimDirection);
    void AddAmmo(int amount);
    int GetCurrentAmmo();
    bool IsWeaponVisible();
    void SetWeaponVisibility(bool isVisible);
}