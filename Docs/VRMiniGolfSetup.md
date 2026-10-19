# VR Mini Golf Kurulumu

## Inspector bağlantıları

### GameManager

- `Stroke Text`: Oyun boyunca görünen TextMeshProUGUI sayaç.
- `Result Panel`: Başlangıçta kapalı olması gereken, ekran merkezindeki panel kökü.
- `Result Summary Text`: Tek TMP alanında toplam vuruş ve final skorunu göstermek için önerilen alan.
- İsterseniz `Result Stroke Text` ve `Final Score Text` alanlarını iki ayrı TMP nesnesine bağlayın.
- Skor varsayılanı `100 - (vuruş * 5)` ve alt sınır `0`dır.

Mevcut sahnede aynı TMP nesnesi hem sayaç hem sonuç paneli olarak atanmıştı. Canlı sayacın kaybolmaması için sonuç paneli ayrı bir GameObject olmalıdır.

### BallController

- Mevcut sahnedeki `GolfBallController`, yeni `BallController`dan türeyen uyumluluk bileşenidir; sahne referansını değiştirmeniz gerekmez.
- Topun altına, `Play On Awake` ve `Looping` kapalı, `Simulation Space = World` olan bir Particle System ekleyip `Hit Particles` alanına bağlayın. Emission modülünün zaman bazlı rate değerlerini `0` tutun; burst sayısını kod üretir.
- `Wall Layers` alanına yalnızca kenar duvarlarının layer'ını ekleyin. Zemin bu maskede olmamalıdır.
- `Ground Layers` alanından top layer'ını çıkarın; zemin ve rampaları dahil edin.

Önerilen ilk fizik değerleri:

| Alan | Değer |
| --- | ---: |
| Stop Threshold | 0.05 m/s |
| Grass Friction | 0.8 m/s² |
| Low Speed Friction Multiplier | 1.5 |
| High Speed Friction Multiplier | 0.55 |
| Friction Transition Speed | 1.2 m/s |
| Wall Restitution | 0.62 |
| Wall Tangential Retention | 0.92 |
| Maximum Wall Rebound Speed Ratio | 0.85 |

### GolfHole

- Delik Collider'ında `Is Trigger` açık olmalı.
- Top veya child collider'ı `GolfBall` tag'ini taşımalıdır.
- İsteğe bağlı `Ball Rest Point`, deliğin içindeki nihai top konumunu belirler.

### VRGolfClub ve haptics

- XRI ile kullanımda sopada `XRGrabInteractable`, tutan kontrolcü objesinde `XRBaseInputInteractor` türevi ve çalışan bir haptic output bulunmalıdır.
- Kod, vuruş gücünü `0..1` aralığına çevirip `XRBaseInputInteractor.SendHapticImpulse` çağırır.
- Eski Meta OVR grab kurulumu kullanılıyorsa `OVRInput` fallback'i korunmuştur.

### VRGolfClub power bar

1. Sopanın yakınında bir `Canvas` oluşturup `Render Mode = World Space` yapın; altında tek bir `Image` bulundurun ve VR'da rahat okunacak ölçekte/pozda yerleştirin.
2. `VRGolfClub` üzerindeki `Power Bar Canvas` ve `Power Bar Image` alanlarına bu iki bileşeni bağlayın. İsterseniz sahnedeki `GolfBallController`ı `Target Ball` alanına verin; boşsa çalışma anında otomatik bulunur.
3. Bileşen menüsünden `Power Bar/chargebar2 Sprite'larını Otomatik Ata` komutunu çalıştırın. Bu komut boş kare için `charge bar.png`, güç kareleri için düşükten yükseğe `Folder 10013 ... Folder 10001` sırasını kullanır.

Gösterge yalnızca Meta grab sistemi sopayı tutuluyor bildirirken, top bölüm tamamlanmamışken, topun yatay hızı `Maximum Ball Speed For Stroke` sınırındayken ve vuruş cooldown'u bitmişken görünür. Sopa başı hızı `Minimum Club Head Speed` ile `Club Head Speed For Full Putt` arasında normalize edilir; başarılı vuruştan `Power Bar Reset Delay` sonra kare sıfırlanır. `VRGolfClub` artık altındaki diğer Canvas nesnelerini topluca kapatmaz.

## Physic Material önerileri

Unity 6'da asset adı `Physics Material` olarak görünür.

| Ayar | Top | Duvar | Zemin/Halı |
| --- | ---: | ---: | ---: |
| Dynamic Friction | 0.05 | 0.02 | 0.15 |
| Static Friction | 0.05 | 0.02 | 0.20 |
| Bounciness | 0.05 | 0.10 | 0.00 |
| Friction Combine | Minimum | Minimum | Minimum |
| Bounce Combine | Minimum | Minimum | Minimum |

Duvar sekmesinin ana kontrolü kodda olduğu için material bounciness düşük tutulur; böylece PhysX sekmesi ile kod sekmesi üst üste binmez. Yalnızca material tabanlı sekme isterseniz `Wall Layers` maskesini boş bırakın, duvar bounciness değerini `0.35-0.5` yapın ve Bounce Combine'ı `Maximum` seçin.

## Rigidbody önerileri

| Ayar | Öneri |
| --- | --- |
| Mass | 0.045-0.08 kg |
| Use Gravity | Açık |
| Linear Damping | 0 |
| Angular Damping | 0.05 |
| Interpolate | Interpolate |
| Collision Detection | Continuous Dynamic |
| Constraints | Parkur düzse isteğe bağlı Freeze Position Y; rampalarda kullanmayın |

Kod `Awake` sırasında damping, interpolation ve collision detection değerlerini güvenli varsayılanlara getirir. Gerçek Quest cihazında 72/80/90 Hz profillerinde tekrar test edip özellikle `Grass Friction`, `Wall Restitution` ve `Stop Threshold` değerlerini parkur ölçeğine göre ayarlayın.
