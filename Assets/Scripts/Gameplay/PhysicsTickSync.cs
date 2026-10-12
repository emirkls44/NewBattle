using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// CharacterController.Move'dan once fizik dunyasini transform'larla esitler.
    ///
    /// Odaya katilan oyuncu (client) kendi hareketini onceden tahmin eder; host'tan
    /// duzeltme gelince Fusion transform'u geri alip son tick'leri yeniden oynatir.
    /// Projede autoSyncTransforms kapali oldugu icin CharacterController bu geri
    /// alinmayi gormuyor, eski konumundan devam ediyordu: her yeniden oynatmada
    /// hareket ust uste biniyor, katilan oyuncu cok hizli ve titrek gidiyordu.
    /// Host'ta yeniden oynatma olmadigi icin orada sorun gorunmuyordu.
    ///
    /// Tick basina bir kez calisir (kac oyuncu olursa olsun).
    /// </summary>
    public static class PhysicsTickSync
    {
        private static NetworkRunner _runner;
        private static int _tick = -1;
        private static bool _resimulation;

        public static void Ensure(NetworkRunner runner)
        {
            if (runner == null)
                return;

            int tick = runner.Tick;
            bool resimulation = runner.IsResimulation;
            if (runner == _runner && tick == _tick && resimulation == _resimulation)
                return;

            _runner = runner;
            _tick = tick;
            _resimulation = resimulation;
            Physics.SyncTransforms();
        }
    }
}
