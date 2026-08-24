// Mal kabul ekraninda elle girilen veya kameradan okunan barkodu
// aninda dogrular. Formu kendiliginden kaydetmez.
(function () {
    "use strict";

    const form = document.getElementById("mal-kabul-form");
    if (!form) return;

    const barkodGirdi = document.getElementById("barkod");
    const urunIdGirdi = document.getElementById("UrunId");
    const miktarGirdi = document.getElementById("Miktar");
    const bilgi = document.getElementById("mal-kabul-urun-bilgi");
    const kameraDugmesi = document.getElementById("btn-kamera");
    const cozumAdresi = form.dataset.barkodCozUrl;

    let aktifIstek = null;

    // kamera.js bu olayi barkod okundugunda yayar. preventDefault(),
    // kameranin Kasa'ya ozel yapay Enter davranisini durdurur.
    document.addEventListener("barkod-kamera-okundu", function (olay) {
        olay.preventDefault();

        const barkod = olay.detail && olay.detail.barkod;
        if (!barkod) return;

        barkodGirdi.value = barkod;
        barkoduCoz(barkod, true);
    });

    // Elle barkod yazan veya USB barkod okuyucu kullanan kisi Enter'a
    // bastiginda form kaydolmasin; once urun dogrulansin.
    barkodGirdi.addEventListener("keydown", function (olay) {
        if (olay.key !== "Enter") return;

        olay.preventDefault();
        barkoduCoz(barkodGirdi.value, false);
    });

    // Kullanici dogrulanmis barkodu degistirirse eski UrunId gecersizdir.
    barkodGirdi.addEventListener("input", function () {
        urunIdGirdi.value = "0";
        bilgiGizle();
    });

    async function barkoduCoz(barkod, kameradanGeldi) {
        barkod = (barkod || "").trim();

        if (!barkod) {
            hataGoster("Barkod boş olamaz.");
            barkodGirdi.focus();
            return;
        }

        if (!cozumAdresi) {
            hataGoster("Barkod çözümleme adresi sayfada bulunamadı.");
            return;
        }

        // Kullanici arka arkaya farkli barkodlar girerse eski istegin
        // gec donup yeni sonucu ezmesini engelle.
        if (aktifIstek) aktifIstek.abort();
        const buIstek = new AbortController();
        aktifIstek = buIstek;

        bilgiGoster("Barkod sorgulanıyor…", false);
        bilgi.setAttribute("aria-busy", "true");

        try {
            const ayirac = cozumAdresi.includes("?") ? "&" : "?";
            const adres = cozumAdresi + ayirac +
                "barkod=" + encodeURIComponent(barkod);

            const cevap = await fetch(adres, {
                method: "GET",
                headers: { "Accept": "application/json" },
                signal: buIstek.signal
            });

            const sonuc = await jsonOku(cevap);

            if (!cevap.ok || !sonuc || !sonuc.basarili) {
                throw new Error(
                    sonuc && sonuc.hata
                        ? sonuc.hata
                        : "Barkod çözümlenemedi."
                );
            }

            urunIdGirdi.value = String(sonuc.urunId);
            basariGoster(sonuc);

            // Mal kabulde BarkodService'in miktar sonucunu forma yazmiyoruz.
            // Koli veya terazi barkodu olsa bile teslim alinan gercek miktari
            // kullanici Miktar alanina kendisi girmelidir.
            if (miktarGirdi) {
                miktarGirdi.focus();
                miktarGirdi.select();
            }

            // Kamera ile tek urun secimi yeterlidir. Basarili okumadan sonra
            // kamerayi kapatmak hem pili korur hem ayni urunun yeniden
            // okunmasini engeller.
            if (kameradanGeldi &&
                kameraDugmesi &&
                kameraDugmesi.classList.contains("btn-danger")) {
                kameraDugmesi.click();
            }
        } catch (hata) {
            if (hata.name === "AbortError") return;

            urunIdGirdi.value = "0";
            hataGoster(hata.message || "Barkod sorgulanırken hata oluştu.");
            barkodGirdi.focus();
            barkodGirdi.select();
        } finally {
            // Eski ve iptal edilmis istek, daha yeni istegin yukleniyor
            // durumunu yanlislikla kaldirmasin.
            if (aktifIstek === buIstek) {
                aktifIstek = null;
                bilgi.removeAttribute("aria-busy");
            }
        }
    }

    async function jsonOku(cevap) {
        try {
            return await cevap.json();
        } catch {
            return null;
        }
    }

    function basariGoster(sonuc) {
        // Sunucudan gelen metni innerHTML ile basmiyoruz. textContent,
        // urun adindaki ozel karakterlerin HTML olarak calismasini engeller.
        bilgi.replaceChildren();

        const ikon = document.createElement("i");
        ikon.className = "ph ph-check-circle";

        const metin = document.createElement("span");
        metin.textContent =
            " Ürün bulundu: " + sonuc.kod + " — " + sonuc.ad +
            " (" + sonuc.birim + ")";

        bilgi.append(ikon, metin);
        bilgi.className = "bildirim bildirim-olumlu mt-2";
    }

    function hataGoster(mesaj) {
        bilgiGoster(mesaj, true);
    }

    function bilgiGoster(mesaj, hataMi) {
        bilgi.textContent = mesaj;
        bilgi.className = hataMi
            ? "bildirim bildirim-olumsuz mt-2"
            : "bildirim bildirim-bilgi mt-2";
    }

    function bilgiGizle() {
        bilgi.textContent = "";
        bilgi.className = "bildirim bildirim-bilgi d-none mt-2";
    }
})();
