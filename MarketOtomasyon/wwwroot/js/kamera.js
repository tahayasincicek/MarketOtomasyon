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

            // ONCE izin, SONRA cihaz listesi. Tarayicilar gizlilik geregi izin
            // verilmeden once cihaz adlarini ve kimliklerini bos dondurur;
            // bu sirayla listelenirse asagidaki "arka kamera" secimi hic tutmaz
            // ve telefon on kamerayla acilir.
            // facingMode "ideal": arka kamera yoksa da hata vermez, olani acar.
            const gecici = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } }
            });
            gecici.getTracks().forEach(t => t.stop());

            const cihazlar = await okuyucu.listVideoInputDevices();
            if (cihazlar.length === 0) {
                durumYaz("Bu cihazda kamera yok. Bilgisayarda web kamerası " +
                         "gerekir; telefondan bağlanırsanız telefonun kamerası kullanılır.", true);
                return;
            }

            cihazlariListele(cihazlar);
            await baslat(secim.value || undefined);

            acik = true;
            dugme.classList.replace("btn-outline-secondary", "btn-danger");
            dugme.innerHTML = '<i class="ph ph-video-camera-slash"></i> Kamerayı Kapat';
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

        // Her ekran okunan barkodu kendi is akisi ile ele alabilsin.
        // Olay preventDefault() ile iptal edilirse Kasa'ya ozel Enter akisi calismaz.
        const olay = new CustomEvent("barkod-kamera-okundu", {
            detail: { barkod: deger },
            bubbles: false,
            cancelable: true
        });

        const varsayilanAkisDevamEtsin = document.dispatchEvent(olay);
        if (!varsayilanAkisDevamEtsin) return;

        // Olay iptal edilmediyse Kasa'nin mevcut akisini kullan.
        barkodGirdi.value = deger;
        barkodGirdi.dispatchEvent(new KeyboardEvent("keydown", {
            key: "Enter",
            bubbles: true,
            cancelable: true
        }));
    }

    function kapat() {
        if (!acik) return;
        temizle();
        panel.classList.add("d-none");
        durumYaz("Kamera kapalı.");
        dugme.classList.replace("btn-danger", "btn-outline-secondary");
        dugme.innerHTML = '<i class="ph ph-video-camera"></i> Kamera';
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
            case "DevicesNotFoundError":
            case "OverconstrainedError":
                // En sik sebep: bilgisayarda web kamerasi yok. Kullaniciyi
                // izin ayarlarinda bosuna dolastirmamak icin cozumu de soyle.
                return "Bu cihazda kamera bulunamadı. Bilgisayarda web kamerası " +
                       "gerekir; telefondan https ile bağlanırsanız telefonun kamerası kullanılır.";
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