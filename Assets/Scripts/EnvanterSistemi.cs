using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnvanterSistemi : MonoBehaviour
{
    public static EnvanterSistemi instance;

    [Header("Kare Slotlar")]
    public Image yumrukKaresi;
    public Image silahKaresi;

    [Header("Mermi UI")]
    public TextMeshProUGUI mermiYazisi;

    [Header("Renk Ayarlarý")]
    public Color aktifParlakRenk = new Color(1f, 1f, 1f, 1f);
    public Color pasifSonukRenk = new Color(1f, 1f, 1f, 0.3f);
    public Color gizliRenk = new Color(1f, 1f, 1f, 0f);

    private bool silahSahipligi = false;

    // MÝMARÝ DÜZELTME: Sahneyi taramak (FindObjects) yerine yerel oyuncuyu önbellekte tutuyoruz.
    private PlayerShooting localPlayerShooting;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        yumrukKaresi.color = aktifParlakRenk;
        silahKaresi.color = gizliRenk;

        if (mermiYazisi != null) mermiYazisi.gameObject.SetActive(false);
    }

    // YENÝ: Oyuncu objesi aðda yetkili (HasStateAuthority) olarak doðduðunda, 
    // PlayerShooting içindeki Spawned() metodundan bu fonksiyonu çaðýrýp kendini kaydetmeli:
    // EnvanterSistemi.instance.RegisterLocalPlayer(this);
    public void RegisterLocalPlayer(PlayerShooting player)
    {
        localPlayerShooting = player;
    }

    public void ElineSilahAldi(int mevcutMermi)
    {
        silahSahipligi = true;
        yumrukKaresi.color = pasifSonukRenk;
        silahKaresi.color = aktifParlakRenk;

        if (mermiYazisi != null)
        {
            mermiYazisi.gameObject.SetActive(true);
            MermiGuncelle(mevcutMermi);
        }
    }

    public void MermiGuncelle(int mevcutMermi)
    {
        if (mermiYazisi != null)
        {
            // PERFORMANS: .ToString() yerine GC-Free çalýþan SetText kullanýlarak bellek sýzýntýsý önlendi.
            mermiYazisi.SetText("{0}", mevcutMermi);
        }
    }

    public void YumrugaGecisYapildi()
    {
        yumrukKaresi.color = aktifParlakRenk;

        if (silahSahipligi)
        {
            silahKaresi.color = pasifSonukRenk;
        }
        else
        {
            silahKaresi.color = gizliRenk;
        }

        if (mermiYazisi != null) mermiYazisi.gameObject.SetActive(false);

        // MÝMARÝ DÜZELTME: Sahnede döngüye girmek yerine önbellekteki oyuncuya direkt komut gönder.
        if (localPlayerShooting != null)
        {
            localPlayerShooting.SilahiBelindeSakla();
        }
    }

    public void SilahaGecisYapildi()
    {
        // Eðer yerel oyuncu kaydolmadýysa iþlemi durdur.
        if (!silahSahipligi || localPlayerShooting == null) return;

        yumrukKaresi.color = pasifSonukRenk;
        silahKaresi.color = aktifParlakRenk;

        if (mermiYazisi != null) mermiYazisi.gameObject.SetActive(true);

        // MÝMARÝ DÜZELTME: Sahnede döngüye girmek yerine önbellekteki oyuncuya direkt komut gönder.
        localPlayerShooting.SilahiElineAl();

        // currentAmmo deðiþkeninin PlayerShooting içinde public olarak eriþilebilir olduðu varsayýlmýþtýr.
        MermiGuncelle(localPlayerShooting.currentAmmo);
    }
}