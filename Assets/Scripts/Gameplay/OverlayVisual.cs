using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// "Bu renderer karakterin govdesi degil, bir gosterge katmani" isareti.
    ///
    /// PlayerVisibility gizlenme sirasinda govde renderer'larini kapatir; cember ve
    /// ayak izi gibi gostergeler kendi kurallarina gore yonetildigi icin bu isareti
    /// tasiyanlar o taramanin disinda birakilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class OverlayVisual : MonoBehaviour
    {
    }
}
