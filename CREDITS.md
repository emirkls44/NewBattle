# Kaynaklar ve Lisanslar

Oyundaki her dış kaynaklı içerik ve lisansı. Yeni bir model, ses, font ya da görsel
eklerken buraya satır ekle; lisansı belirsiz hiçbir şeyi oyuna koyma.

## Atıf (credit) gerektirenler — mağaza açıklamasında ve oyun içinde yazılmalı

| İçerik | Nerede | Lisans | Yazılması gereken |
|---|---|---|---|
| "Low Poly Forest" — ağaç, çalı, kaya, çiçek modelleri | `Assets/GameArt/Imported/low_poly_forest`, `Prefabs/Nature`, `Prefabs/Props` | CC BY 4.0 | "Low Poly Forest" by NorTEEN_X (sketchfab.com/norten77077), CC BY 4.0 |
| Titan One fontu (NB Display adıyla da) | `Assets/Fonts/TitanOne-Regular.ttf`, `NBDisplay-Regular.ttf` | SIL Open Font License 1.1 | Titan One © Rodrigo Fuenzalida, SIL OFL 1.1 (atıf şart değil, önerilir) |
| Liberation Sans fontu (TextMesh Pro ile gelir) | `Assets/TextMesh Pro/Fonts` | SIL Open Font License 1.1 | — |

Örnek metin (mağaza açıklaması / Ayarlar > Hakkında):

> Ağaç ve doğa modelleri: "Low Poly Forest" by NorTEEN_X (sketchfab.com/norten77077), CC BY 4.0.
> Font: Titan One by Rodrigo Fuenzalida, SIL Open Font License.

## Atıf gerektirmeyenler

| İçerik | Nerede | Durum |
|---|---|---|
| Meshy AI modelleri (Ahmet, Ayşe, Melek, Tie Guy, sandık, ilk yardım, roketatar, tank, koltuk, çalı...) | `Assets/Arts/Characters`, `Assets/GameArt/Imported/Props` | Ücretli Meshy planıyla özel (private) lisans — ticari kullanım serbest |
| Mixamo animasyonları (`mixamo.com` klipleri) | Karakter animasyonları | Adobe Mixamo: oyunlarda ticari kullanım serbest, atıf gerekmez (ham animasyon dosyası olarak ayrıca dağıtılamaz) |
| Tüm ses efektleri ve menü müziği | `Assets/GameArt/Audio` | Bizim: `Tools/Audio/generate_sounds.py` ile sentezlendi |
| Menü görselleri, ikonlar, arka planlar | `Assets/GameArt/MenuUI` | Bizim: `Tools/MenuArt/generate_menu_art.py` |
| Silah ikonları | `Assets/GameArt/HudIcons/Weapons` | Bizim: `Tools/Icons/render_weapon_icons.py` (oyunun kendi modellerinden) |
| HUD rozetleri (can, kalkan, kuru kafa, oyuncu) | `Assets/GameArt/HudIcons` | Bizim: `Tools/Icons/draw_hud_icons.py` (internetten alınan eskilerin yerine) |
| Binalar, loot, çimen, ada, harita | `Assets/GameArt/Meshes`, `GeneratedIslandAssets` | Bizim: editör scriptleriyle üretildi |
| Photon Fusion 2 | `Assets/Photon` | Photon SDK lisansı (Photon bulutuyla kullanılır); CCU sınırı plana bağlı |
| Unity 6 | — | Unity Personal: yıllık gelir/fon 200.000 $ altında ücretsiz |

## Kaldırılanlar

- `SegoeUIEmoji.ttf` + `Emoji SDF.asset`: Windows'un emoji fontunun kopyasıydı. Microsoft
  fontları oyunla dağıtılamaz. Oyunda kullanılmıyordu, silindi.
- HUD rozetlerinin eski hâlleri (internetten, lisansı belirsiz) kendi çizimlerimizle değiştirildi.

## Dikkat

- Battlelands Royale (Futureplay) oyununun adı, logosu, karakterleri, ikonları, sesleri ve
  ekran görüntüleri kullanılmamalı. Oyun türü ve mekanikler serbest, görsel kimlik değil.
- Mağaza metninde ve görsellerinde başka bir oyunun adı geçmemeli ("Battlelands gibi" vb.).
- Sketchfab, Pinterest, Google Görseller gibi yerlerden lisansına bakmadan görsel/model alma.
  Güvenli kaynaklar: CC0 (Kenney, Poly Haven, Quaternius), kendi ürettiğimiz, ücretli lisanslı.
