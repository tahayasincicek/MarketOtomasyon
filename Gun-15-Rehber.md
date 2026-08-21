# Gün 15 — Kamera ile Barkod (uygulama rehberi)

Sunucu tarafında **hiçbir değişiklik yok**. Kamera yalnızca barkod metnini üretir;
o metin, bugüne kadar elle yazdığın barkodla aynı yoldan `/Kasa/Ekle` ucuna gider.
Terazi barkodu çözümü, koli çarpanı, kampanya — hepsi zaten çalışıyor.

Sıra: 1 → 7.

---

## Önce bilmen gereken bir şey

Görevde "ZXing'i CDN'den ekle" yazıyor ve rehberi öyle yazdım. Ama projede daha önce
bilinçli olarak **ters** bir karar verilmişti — `site.css` içindeki yorumda duruyor:

> *"CDN'e degil projeye gomulu: kasa terminali internetsiz de dogru fontla acilsin."*

Aynı gerekçe ZXing için çok daha kritik: internet gidince yazı tipi bozulur ama
**kamera hiç açılmaz**. Gerçek bir markette bu, kasanın durması demektir.

Bu yüzden 2. adımı iki seçenekli yazdım: **2a** görevde istendiği gibi CDN,
**2b** dosyayı `wwwroot/lib/` altına indirip yerelden servis etmek. İkisi de
tek satır fark. Staj defterine "CDN kullandım ama şu sebeple yerel daha doğru"
diye yazabilmen için ikisini de bıraktım.

---

## 1) Barkod test kartı — hazır

Kamerayla okutacak bir şeye ihtiyacın var. Veritabanındaki **44 gerçek barkodu**
üretip tek sayfaya bastım:

**`Gun-15-Barkod-Test-Karti.html`** — çift tıkla, tarayıcıda açılır.

İçinde üç grup var:

| Grup | Adet | Ne olmalı |
|---|---|---|
| Tekli barkodlar | 24 | Sepete 1 adet düşer |
| Koli barkodları | 14 | Sepete çarpan kadar düşer (ör. 12'li koli → 12 adet) |
| Terazi barkodları | 6 | Gramaj barkodun içinden gelir (ör. 1.250 kg) |

Barkodların tamamı geçerli EAN-13 kontrol hanesi taşıyor ve **gerçek ZXing ile
okutularak doğrulandı** — 44/44 `EAN_13` olarak çözüldü. Yani kameran okuyamazsa
sorun barkodda değil, kodda veya ışıkta.

Terazi barkodları veritabanında 7 haneli anahtar olarak duruyor (`2800001`);
karttaki 13 haneli hâlleri gramaj + kontrol hanesi eklenerek üretildi:

```
2800001 + 01250 + kontrol hanesi = 2800001012501   (Domates, 1.250 kg)
```

Sayfayı **bilgisayar ekranında açıp telefonla** okutabilirsin, ya da yazdırıp
kâğıttan. Ekrandan okuturken parlama olursa ekran parlaklığını biraz düşür.

---

## 2) ZXing kütüphanesini ekle

### 2a) CDN ile (görevde istenen)

`MarketOtomasyon/Views/Kasa/Index.cshtml` dosyasının **en altındaki**
`@section Scripts` bloğunu bul ve `kasa.js`'ten **önce** ZXing satırını ekle:

```cshtml
@section Scripts {
    <script src="https://unpkg.com/@@zxing/library@@0.21.3/umd/index.min.js"></script>
    <script src="~/js/kasa.js" asp-append-version="true"></script>
    <script src="~/js/odeme.js" asp-append-version="true"></script>
    <script src="~/js/askiya-alma.js" asp-append-version="true"></script>
    <script src="~/js/kamera.js" asp-append-version="true"></script>
}
```

> **Dikkat — Razor'da `@` işareti:** `@zxing` yazarsan Razor onu C# kodu sanır ve
> derleme hatası verir. `@@zxing` ve `@@0.21.3` şeklinde **çift** yazman şart.
> Bu, bu adımda en sık takılınan yer.

Sürümü `0.21.3` olarak sabitledim, `@@latest` değil: kütüphane bir gün kırıcı
değişiklik yaparsa kasan sessizce çalışmayı bırakmasın.

### 2b) Yerel dosya ile (önerdiğim)

Terminalde proje kökünde:

```bash
curl -o MarketOtomasyon/wwwroot/lib/zxing/index.min.js --create-dirs https://unpkg.com/@zxing/library@0.21.3/umd/index.min.js
```

Sonra yukarıdaki blokta ilk satırı bununla değiştir:

```cshtml
    <script src="~/lib/zxing/index.min.js" asp-append-version="true"></script>
```

Burada `@@` derdi yok, çünkü URL Razor'a hiç girmiyor.

---

## 3) Kasa ekranına kamera paneli

`Views/Kasa/Index.cshtml` — **`<div id="uyari" ...>` satırının hemen altına** ekle:

```cshtml
    @* Kamera paneli: kapaliyken hic yer kaplamaz, video akisi da baslamaz.
       Kamera acikken barkod alanindan odak kacmaz -- el terminali de
       kullanilabilsin diye ikisi ayni anda calisir. *@
    <div id="kamera-paneli" class="arac-cubugu d-none" style="background:#f8fafc">
        <video id="kamera-video" playsinline muted
               style="width:22rem;max-width:100%;height:12rem;object-fit:cover;background:#111827;border-radius:.25rem"></video>

        <div class="d-flex flex-column gap-1" style="min-width:0">
            <span class="arac-etiket">Kamera</span>
            <select id="kamera-secim" class="form-select" style="width:15rem"
                    aria-label="Kamera seç"></select>

            <div id="kamera-durum" class="ipucu">Kamera kapalı.</div>
            <div id="kamera-son" class="ipucu"></div>
        </div>

        <span class="ipucu arac-sag" style="max-width:18rem">
            Barkodu kameranın ortasına, ekrana paralel tutun. Aynı barkod
            2 saniye içinde tekrar okunmaz.
        </span>
    </div>
```

Sonra **araç çubuğundaki `btn-iptal` düğmesinin hemen altına** aç/kapa düğmesini ekle:

```cshtml
        <button type="button" id="btn-kamera" class="btn btn-outline-secondary">
            <i class="bi bi-camera-video"></i> Kamera
        </button>
```

---

## 4) YENİ DOSYA: `MarketOtomasyon/wwwroot/js/kamera.js`

```javascript
// Kamera ile barkod okuma. Okunan metni kasa.js'in barkod alanina yazip
// normal ekleme akisini tetikler; ayri bir sunucu ucu yoktur.
//
// Kamera erisimi yalnizca HTTPS veya localhost'ta calisir (tarayici kurali).
(function () {
    "use strict";

    const dugme = document.getElementById("btn-kamera");
    if (!dugme || typeof ZXing === "undefined") return;

    const panel = document.getElementById("kamera-paneli");
    const video = document.getElementById("kamera-video");
    const secim = document.getElementById("kamera-secim");
    const durum = document.getElementById("kamera-durum");
    const sonOkunan = document.getElementById("kamera-son");
    const barkodGirdi = document.getElementById("barkod");

    // Kasada yalnizca bu dort format kullanilir. Listeyi dar tutmak
    // hem yanlis okumayi hem de CPU yukunu azaltir.
    const FORMATLAR = [
        ZXing.BarcodeFormat.EAN_13,
        ZXing.BarcodeFormat.EAN_8,
        ZXing.BarcodeFormat.UPC_A,
        ZXing.BarcodeFormat.UPC_E,
        ZXing.BarcodeFormat.CODE_128
    ];

    // Ayni barkod kamera onunde dururken saniyede onlarca kez okunur.
    // Bu sure boyunca ayni deger yok sayilir.
    const TEKRAR_ENGEL_MS = 2000;

    let okuyucu = null;
    let acik = false;
    let sonDeger = null;
    let sonZaman = 0;

    dugme.addEventListener("click", () => (acik ? kapat() : ac()));

    // Sayfa kapanirken/gizlenirken kamera isigini sondur.
    window.addEventListener("pagehide", kapat);
    document.addEventListener("visibilitychange", function () {
        if (document.hidden) kapat();
    });

    async function ac() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            durumYaz("Bu tarayıcı kamera erişimini desteklemiyor.", true);
            panel.classList.remove("d-none");
            return;
        }

        // getUserMedia yalnizca guvenli baglamda tanimlidir; kullaniciya
        // "izin vermediniz" demek yerine gercek sebebi soyleyelim.
        if (!window.isSecureContext) {
            panel.classList.remove("d-none");
            durumYaz("Kamera yalnızca HTTPS veya localhost üzerinde çalışır. " +
                     "Şu anki adres güvenli değil.", true);
            return;
        }

        panel.classList.remove("d-none");
        durumYaz("Kamera açılıyor…");

        try {
            const ipuclari = new Map();
            ipuclari.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, FORMATLAR);
            // Kasa barkodlari nettir; "TRY_HARDER" acilirsa kare hizi duser.
            okuyucu = new ZXing.BrowserMultiFormatReader(ipuclari);

            const cihazlar = await okuyucu.listVideoInputDevices();
            if (cihazlar.length === 0) {
                durumYaz("Kamera bulunamadı.", true);
                return;
            }

            cihazlariListele(cihazlar);
            await baslat(secim.value || undefined);

            acik = true;
            dugme.classList.replace("btn-outline-secondary", "btn-danger");
            dugme.innerHTML = '<i class="bi bi-camera-video-off"></i> Kamerayı Kapat';
        } catch (e) {
            durumYaz(hataMesaji(e), true);
            temizle();
        }
    }

    function cihazlariListele(cihazlar) {
        secim.innerHTML = "";
        cihazlar.forEach(function (c, i) {
            const o = document.createElement("option");
            o.value = c.deviceId;
            o.textContent = c.label || ("Kamera " + (i + 1));
            secim.appendChild(o);
        });

        // Telefonda arka kamera tercih edilir: barkod ona dogru tutulur.
        const arka = cihazlar.find(c => /arka|back|rear|environment/i.test(c.label));
        if (arka) secim.value = arka.deviceId;

        secim.onchange = async function () {
            if (!acik) return;
            okuyucu.reset();
            await baslat(secim.value);
        };
    }

    async function baslat(cihazId) {
        durumYaz("Barkod bekleniyor…");
        await okuyucu.decodeFromVideoDevice(cihazId ?? null, video, function (sonuc, hata) {
            if (sonuc) okundu(sonuc);
            // NotFoundException her karede atilir (o karede barkod yok demektir),
            // sessizce yutulur; digerleri kullaniciya bildirilir.
            else if (hata && !(hata instanceof ZXing.NotFoundException)) {
                durumYaz("Okuma hatası: " + hata.message, true);
            }
        });
    }

    function okundu(sonuc) {
        const deger = sonuc.getText();
        const simdi = Date.now();

        if (deger === sonDeger && simdi - sonZaman < TEKRAR_ENGEL_MS) return;

        sonDeger = deger;
        sonZaman = simdi;

        bipCal();
        sonOkunan.textContent = "Son okunan: " + deger;
        durumYaz("Barkod bekleniyor…");

        // kasa.js'in kendi akisini kullan: alana yaz, Enter'i taklit et.
        // Boylece kampanya, koli carpani ve terazi cozumu ayni yoldan gecer.
        barkodGirdi.value = deger;
        barkodGirdi.dispatchEvent(new KeyboardEvent("keydown", {
            key: "Enter", bubbles: true, cancelable: true
        }));
    }

    function kapat() {
        if (!acik) return;
        temizle();
        panel.classList.add("d-none");
        durumYaz("Kamera kapalı.");
        dugme.classList.replace("btn-danger", "btn-outline-secondary");
        dugme.innerHTML = '<i class="bi bi-camera-video"></i> Kamera';
        if (barkodGirdi) barkodGirdi.focus();
    }

    function temizle() {
        acik = false;
        sonDeger = null;
        if (okuyucu) { okuyucu.reset(); okuyucu = null; }
        // reset() bazi tarayicilarda akisi birakmaz; kamera isigi yanik kalmasin.
        if (video.srcObject) {
            video.srcObject.getTracks().forEach(t => t.stop());
            video.srcObject = null;
        }
    }

    function durumYaz(mesaj, hataMi) {
        durum.textContent = mesaj;
        durum.className = hataMi ? "hata" : "ipucu";
    }

    function hataMesaji(e) {
        switch (e && e.name) {
            case "NotAllowedError":
                return "Kamera izni reddedildi. Tarayıcının adres çubuğundaki " +
                       "kamera simgesinden izin verip tekrar deneyin.";
            case "NotFoundError":
            case "OverconstrainedError":
                return "Kullanılabilir kamera bulunamadı.";
            case "NotReadableError":
                return "Kamera başka bir uygulama tarafından kullanılıyor.";
            default:
                return "Kamera açılamadı: " + (e && e.message ? e.message : "bilinmeyen hata");
        }
    }

    // Kisa bip. Ses dosyasi eklemiyoruz: tek bir ton icin ag istegi ve
    // ekstra dosya gereksiz; WebAudio ile uretmek yeterli.
    function bipCal() {
        try {
            const Ses = window.AudioContext || window.webkitAudioContext;
            if (!Ses) return;

            const ctx = new Ses();
            const osc = ctx.createOscillator();
            const ses = ctx.createGain();

            osc.type = "square";
            osc.frequency.value = 1800;
            ses.gain.value = 0.06;          // kasada kulak tirmalamasin

            osc.connect(ses).connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.09);
            osc.onended = () => ctx.close();
        } catch { /* ses cikmamasi okumayi engellemez */ }
    }
})();
```

---

## 5) `wwwroot/js/kasa.js` — Enter olayını dışarıdan tetiklenebilir yap

`kamera.js` barkod alanına yazıp Enter'ı taklit ediyor. Bunun çalışması için
`kasa.js`'teki barkod dinleyicisinin `keydown` olayını dinliyor olması gerekiyor.
Dosyada şu satırı bul:

```javascript
    barkodGirdi.addEventListener("keydown", async function (e) {
```

Zaten `keydown` dinliyorsa **hiçbir şey değiştirme**, 6. adıma geç.
Eğer `keypress` veya `keyup` yazıyorsa `keydown` yap.

---

## 6) HTTPS ile çalıştır (telefondan test için)

Kamera izni tarayıcı kuralı gereği **yalnızca HTTPS veya localhost**'ta verilir.
Bilgisayarda `localhost:5275` ile test edebilirsin ama telefondan bağlanmak için
HTTPS şart.

Bir kereye mahsus sertifikaya güven:

```bash
dotnet dev-certs https --trust
```

Sonra uygulamayı dış ağa aç:

```bash
dotnet run --project MarketOtomasyon --urls https://0.0.0.0:5001
```

Bilgisayarın yerel IP'sini öğren:

```bash
ipconfig
```

Telefondan `https://<IP>:5001/Kasa` adresine gir. Sertifika uyarısı çıkacak —
geliştirme sertifikası kendi imzalı olduğu için normal, "Gelişmiş → Devam et" de.

> Windows Güvenlik Duvarı ilk seferde 5001 portunu sorabilir; **özel ağ** için izin ver.
> Telefon ve bilgisayar aynı Wi-Fi ağında olmalı.

---

## 7) Kabul testi

1. Bilgisayarda `Gun-15-Barkod-Test-Karti.html`'i aç.
2. Telefondan `https://<IP>:5001/Kasa` adresine gir.
3. Vardiya kapalıysa önce Vardiya ekranından aç.
4. **Kamera** düğmesine bas, izin iste diyince **izin ver**.
5. Telefonu "Süt 1 L" barkoduna tut.

Olması gereken: bip sesi duyulur, "Son okunan: 8690000000012" yazar ve
**Süt 1 L sepete 1 adet düşer**.

Sonra şunları da dene:

| Test | Beklenen |
|---|---|
| Aynı barkodu kameranın önünde tutmaya devam et | Sepete tek seferde 1 adet düşer, üst üste eklenmez |
| 2 saniye bekleyip aynı barkodu tekrar okut | İkinci kez eklenir (miktar 2 olur) |
| "Süt 1 L (12'li koli)" barkodunu okut | Sepete **12 adet** düşer |
| "Domates 1.250 kg" barkodunu okut | Sepete **1,250 kg** olarak düşer |
| Kamerayı kapat | Telefonun kamera ışığı söner |
| İzni reddet, tekrar dene | "Kamera izni reddedildi…" mesajı çıkar |
| `http://` ile (HTTPS'siz) gir, kamerayı aç | "Yalnızca HTTPS veya localhost" mesajı çıkar |

---

## Takılırsan

| Belirti | Sebebi |
|---|---|
| Razor derleme hatası, `@zxing` satırını gösteriyor | 2a'da `@@` yerine `@` yazılmış |
| `ZXing is not defined` | ZXing script'i `kamera.js`'ten **sonra** eklenmiş; sırayı düzelt |
| Kamera düğmesi hiç tepki vermiyor | `kamera.js` `@section Scripts`'e eklenmemiş |
| Video akıyor ama hiç okumuyor | Barkod çok küçük/uzak; test kartını büyüt veya telefonu yaklaştır |
| Telefonda "güvenli değil" uyarısı | `http` ile girilmiş; `https` yaz |
| Barkod okunuyor ama sepete düşmüyor | Açık vardiya yok — Vardiya ekranından aç |
