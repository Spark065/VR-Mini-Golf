# Mini Golf Top Fiziği

`BallController`, topun duvar sekmesini fizik çözücüsünün değiştirdiği hızdan değil,
çarpışma öncesinde saklanan yatay hızdan hesaplar. Temas noktalarının normalleri
ortalama alınmaz; top merkezine göre dışarı yöneltilen ve geliş yönüne en güçlü
karşılık veren dik yüzey seçilir. `OnCollisionStay` da aynı çözümleyiciyi kullandığı
için collider teması kesilmese bile ikinci çarpışma işlenir. Çıkış yönü
`Vector3.Reflect` ile hesaplanır; küçük sürtünme ve hız kaybından sonra minimum
dışarı hız ile pozisyon ayırması uygulanır.

## Önerilen Inspector değerleri

| Ayar | Değer |
| --- | ---: |
| Mass | 0.04593 kg |
| Linear Damping | 0.04 |
| Angular Damping | 0.05 |
| Maximum Depenetration Velocity | 3.0 m/s |
| Collision Detection | Continuous Dynamic |
| Interpolation | Interpolate |
| Maximum Launch Speed | 3.6 m/s |
| Grass Friction | 0.42 m/s² |
| Low/High Speed Multiplier | 0.85 / 0.62 |
| Extra Gravity | 1.0 m/s² |
| Wall Reflection Speed Retention | 0.98 |
| Wall Speed Loss | 0.06 |
| High Speed Additional Wall Loss | 0.08 |
| Speed For Maximum Wall Loss | 3.6 m/s |
| Wall Surface Friction | 0.02 |
| Minimum Wall Impact Speed | 0.08 m/s |
| Minimum Wall Bounce Speed | 0.10 m/s |
| Wall Separation Speed | 0.08 m/s |
| Wall Separation Distance | 0.003 m |

`Wall Reflection Speed Retention` temel sekme gücüdür. Gerçek hız kaybı
`Wall Speed Loss` ile başlar ve top `Speed For Maximum Wall Loss` değerine
yaklaştıkça `High Speed Additional Wall Loss` kadar artar. `Wall Surface Friction`
yalnızca duvar boyunca kalan bileşeni azaltır. Top temas halinde kalırsa
`Wall Separation Speed` ve `Wall Separation Distance` topu dışarı çıkarır.

## Fizik materyalleri

- `MiniGolfBall`: dynamic/static friction `0.08/0.08`, bounce `0`, friction/bounce combine `Minimum`.
- `MiniGolfGround`: `0.16/0.18`, bounce `0`, friction/bounce combine `Minimum`.
- `MiniGolfObstacle`: `0.02/0.02`, bounce `0`, friction/bounce combine `Minimum`.

Engel materyalindeki bounciness sıfırdır; sekmenin tek sahibi `BallController`
olduğu için PhysX ve script aynı çarpışmaya iki ayrı impuls uygulamaz.

Sahnedeki topları güvenli başlangıç değerlerine taşımak için
`Tools > VR Mini Golf > Fizik > Sahnedeki Toplari Dengele` kullanılabilir. Bu
komut açık sahneyi değiştirir; sonucu kontrol ettikten sonra sahne bilinçli olarak
kaydedilmelidir.

## Test

- Duvara 45 derece gelen top, karşı tarafta yaklaşık 45 dereceyle ayrılmalıdır.
- Dik gelen top kendi geliş çizgisi üzerinden geri dönmelidir.
- Sığ açıda top duvara paralel yapışmamalı; normal bileşeni aynı açıyla tersine dönmelidir.
- Köşede temas normalleri ortalanmamalı; ilk karşılayan yüzeyin normali kullanılmalıdır.
- İki paralel duvar arasında en az dört ardışık sekmede top duvara yapışmamalıdır.
