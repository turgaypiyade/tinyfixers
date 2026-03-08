# Special swap öncelik notu (PB / Line + normal taş + yeni special üretimi)

Bu not, şu edge-case için önerilen akışı netleştirir:
- Özel taş (`PatchBot`, `LineH` vb.) + normal taş swap ediliyor.
- Swap aynı anda bir match üretiyor ve yeni bir special oluşma hakkı doğuyor (örn. `LineV`).

## Önerilen çözüm: **deterministic, staged ve senkron**
Asenkron yerine tek bir deterministik sıra önerilir. Böylece hem oyuncu geri bildirimi daha okunur olur, hem de zincir davranışları test edilebilir kalır.

1. **Swap tamamlanır** (görsel hareket biter).
2. **Yeni special üretim hakkı capture edilir** (pending creation olarak saklanır).
3. **Mevcut özel taşın etkisi çalışır** (PB dash/teleport-hit veya Line patlatması).
4. **Pending special board'a yerleştirilir** (LineV vb. üretilir).
5. **Collapse + spawn** çalışır (yeni oluşan special doğal fizik akışına girer / düşer).
6. **Sonraki resolve pass** içinde yeni oluşan special normal kurallarla değerlendirilir.

## Neden bu sıra?
- `PB + normal` davranışını bozmaz.
- Match'ten gelen yeni special kaybolmaz; deterministic şekilde yaratılır.
- Aynı frame'de iki farklı öncelik savaşı ("önce kim patlasın?") yaşamazsın.
- Görsel okunabilirlik artar: oyuncu önce swap sonucunu, sonra özel etkiyi, sonra yeni special'ın settle olmasını görür.

## Pratik kural
- "Swap'te var olan special" **öncelikli** çalışır.
- "Swap sonucu yeni üretilen special" **bir sonraki resolve döngüsünün adayıdır**.

Bu model, PB yerine `LineH` olduğunda da aynı şekilde uygulanmalıdır.
