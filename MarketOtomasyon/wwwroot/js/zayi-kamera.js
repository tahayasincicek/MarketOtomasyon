// Zayi/fire ekraninda elle girilen, USB okuyucudan gelen veya kameradan
// okunan barkodu urune baglar. Kaydi kendiliginden gondermez; miktar ve
// sebep kullanici tarafindan girildikten sonra normal form akisi kullanilir.
(function () {
    "use strict";

    const form = document.getElementById("zayi-form");
    if (!form) return;

    const barkodGirdi = document.getElementById("barkod");
    const urunSecim = document.getElementById("UrunId");
    const miktarGirdi = document.getElementById("Miktar");
    const bilgi = document.getElementById("zayi-urun-bilgi");
    const kameraDugmesi = document.getElementById("btn-kamera");
    const cozumAdresi = form.dataset.barkodCozUrl;

    let aktifIstek = null;
    let barkodlaSeciliyor = false;

    document.addEventListener("barkod-kamera-okundu", function (olay) {
        // kamera.js'in Kasa icin kullandigi yapay Enter davranisini durdur.
        olay.preventDefault();

        const barkod = olay.detail && olay.detail.barkod;
        if (!barkod) return;

        barkodGirdi.value = barkod;
        barkoduCoz(barkod, true);
    });

    // Elle giriste ve USB barkod okuyucuda Enter formu gondermek yerine
    // once barkodun hangi urune ait oldugunu dogrular.
    barkodGirdi.addEventListener("keydown", function (olay) {
        if (olay.key !== "Enter") return;

        olay.preventDefault();
        barkoduCoz(barkodGirdi.value, false);
    });

    // Barkod degistirildiginde daha once dogrulanmis urunu kullanma.
    barkodGirdi.addEventListener("input", function () {
        urunSecim.value = "0";
        bilgiGizle();
    });

    // Kullanici urunu listeden kendisi secerse eski barkodun POST sirasinda
    // secimi geri ezmesini engelle. Manuel urun secimi her zaman gecerlidir.
    urunSecim.addEventListener("change", function () {
        if (barkodlaSeciliyor) return;

        barkodGirdi.value = "";

        const secenek = urunSecim.selectedOptions[0];
        if (!secenek || urunSecim.value === "0") {
            bilgiGizle();
            return;
        }

        basariGoster({
            kod: secenek.dataset.kod || "",
            ad: secenek.dataset.ad || secenek.textContent.trim(),
            birim: secenek.dataset.birim || ""
        });
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

            const secenek = Array.from(urunSecim.options)
                .find(o => o.value === String(sonuc.urunId));

            if (!secenek) {
                throw new Error("Barkoda ait ürün zayi ürünleri listesinde bulunamadı.");
            }

            barkodlaSeciliyor = true;
            urunSecim.value = String(sonuc.urunId);
            urunSecim.dispatchEvent(new Event("change", { bubbles: true }));
            barkodlaSeciliyor = false;

            basariGoster(sonuc);

            // Zayi miktari barkodun kodladigi miktardan alinmaz. Gercek fire
            // miktarini kullanici girmeli; bu nedenle yalnizca odagi tasiyoruz.
            if (miktarGirdi) {
                miktarGirdi.focus();
                miktarGirdi.select();
            }

            if (kameradanGeldi &&
                kameraDugmesi &&
                kameraDugmesi.classList.contains("btn-danger")) {
                kameraDugmesi.click();
            }
        } catch (hata) {
            if (hata.name === "AbortError") return;

            barkodlaSeciliyor = false;
            urunSecim.value = "0";
            hataGoster(hata.message || "Barkod sorgulanırken hata oluştu.");
            barkodGirdi.focus();
            barkodGirdi.select();
        } finally {
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
        bilgi.replaceChildren();

        const ikon = document.createElement("i");
        ikon.className = "ph ph-check-circle";

        const metin = document.createElement("span");
        const birim = sonuc.birim ? " (" + sonuc.birim + ")" : "";
        metin.textContent =
            " Ürün seçildi: " + sonuc.kod + " — " + sonuc.ad + birim;

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
