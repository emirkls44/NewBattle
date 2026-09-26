using UnityEngine;

/// <summary>
/// Telefonda oyunu 60 FPS'te calistirir ve oyun sirasinda ekranin kararmasini engeller.
///
/// Unity mobilde varsayilan olarak 30 FPS'e kilitler. Aga 60 tick'te giden bir
/// nisancida 30 FPS, joystick'e basmakla karakterin donmesi arasinda hissedilir
/// bir gecikme demek. Ekran zaman asimi da inis haritasini secerken ya da maci
/// izlerken telefonun kendiliginden kararmasina yol aciyordu.
///
/// Sahneye eklenmesi gerekmez: Unity bunu sahne yuklenmeden once kendisi cagirir.
/// </summary>
public static class MobileFrameRate
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
#endif
    }
}
