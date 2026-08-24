// Odeme penceresi. Kasa ekranindaki F2 / "Ödeme Al" bunu acar.
// Sepetten ayri calisir: fis kapanana kadar sepete geri donulebilir.
(function () {
    "use strict";

    const paraBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const modalElemani = document.getElementById("odeme-modal");
    const modal = new bootstrap.Modal(modalElemani);

    const fisOnizlemeModalElemani = document.getElementById("fis-onizleme-modal");
    const fisOnizlemeModal = new bootstrap.Modal(fisOnizlemeModalElemani);
    const fisOnizlemeYukleniyor = document.getElementById("fis-onizleme-yukleniyor");
    const fisOnizlemeHata = document.getElementById("fis-onizleme-hata");
    const fisOnizlemeIcerik = document.getElementById("fis-onizleme-icerik");
    const fisOnizlemeYazdir = document.getElementById("btn-fis-onizleme-yazdir");

    const uyari = document.getElementById("odeme-uyari");
    const tutarGirdi = document.getElementById("odeme-tutar");
    const alinanGirdi = document.getElementById("odeme-alinan");

    let sonDurum = null;
    let fisOnizlemeAciliyor = false;
    let fisIstekKontrolcusu = null;

    // ---------- Yardimcilar ----------

    function sayiOku(girdi) {
        let metin = girdi.value.trim().replace(/\s/g, "");
        if (metin === "") return null;

        // Odeme alanlari tr-TR biciminde gosterilir (ornegin 1.250,75).
        // parseFloat bu degeri 1.25 olarak okudugu icin bin TL'yi asan
        // odemelerde mahsup ve para ustu yanlis hesaplanirdi.
        //
        // Turkce girislerin yaninda barkod klavyesi/harici numpad ile
        // yazilabilecek 1250.75 ve 1,250.75 bicimlerini de kabul ediyoruz.
        if (!/^[+-]?[0-9.,]+$/.test(metin)) return null;

        const isaret = /^[+-]/.test(metin) ? metin[0] : "";
        if (isaret) metin = metin.slice(1);

        const sonVirgul = metin.lastIndexOf(",");
        const sonNokta = metin.lastIndexOf(".");
        let normal;

        if (sonVirgul >= 0 && sonNokta >= 0) {
            // Iki ayirac da varsa en sondaki ondalik, digeri binliktir:
            // 1.250,75 ve 1,250.75 ayni sonuca donusur.
            const ondalikKonumu = Math.max(sonVirgul, sonNokta);
            normal = metin.slice(0, ondalikKonumu).replace(/[.,]/g, "") +
                "." + metin.slice(ondalikKonumu + 1).replace(/[.,]/g, "");
        } else if (sonVirgul >= 0) {
            // Uygulamanin ana giris bicimi: 1250,75.
            normal = metin.slice(0, sonVirgul).replace(/,/g, "") +
                "." + metin.slice(sonVirgul + 1).replace(/,/g, "");
        } else if (sonNokta >= 0 && /^\d{1,3}(?:\.\d{3})+$/.test(metin)) {
            // Yalnizca nokta ve uclu gruplar varsa Turkce binlik bicimidir:
            // 1.250 veya 1.250.000.
            normal = metin.replace(/\./g, "");
        } else {
            // 1250.75 gibi nokta ile ondalik girisi.
            normal = metin;
        }

        const sayi = Number(isaret + normal);
        return Number.isFinite(sayi) ? sayi : null;
    }

    function paraGirdisiniBicimlendir(girdi) {
        const sayi = sayiOku(girdi);
        if (sayi !== null) girdi.value = paraBicimi.format(sayi);
    }

    // Su an yalnizca nakit acik; kart POS entegrasyonuyla eklenecek.
    const TipNakit = 1;

    function uyariGoster(mesaj) {
        uyari.textContent = mesaj;
        uyari.classList.remove("d-none");
    }

    function uyariGizle() {
        uyari.classList.add("d-none");
    }

    async function gonder(yol, veri) {
        const yanit = await fetch(yol, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams(veri)
        });

        const govde = await yanit.json();
        return yanit.ok ? { durum: govde, hata: null } : { durum: govde.durum, hata: govde.hata };
    }

    // ---------- Ekrana basma ----------

    function ciz(durum) {
        sonDurum = durum;

        document.getElementById("odeme-fis-no").textContent = durum.fisNo || "";
        document.getElementById("odeme-genel-toplam").textContent = paraBicimi.format(durum.genelToplam);
        document.getElementById("odeme-odenen").textContent = paraBicimi.format(durum.odenen);
        document.getElementById("odeme-kalan").textContent = paraBicimi.format(durum.kalan);

        const paraUstuKutusu = document.getElementById("para-ustu-kutusu");
        paraUstuKutusu.classList.toggle("d-none", durum.toplamParaUstu <= 0);
        document.getElementById("para-ustu").textContent = paraBicimi.format(durum.toplamParaUstu) + " ₺";

        // Fis kapandiginda yeni odeme alinamaz; pencere "Yeni Fiş" ile kapanir.
        document.getElementById("odeme-tamam").classList.toggle("d-none", !durum.tamamlandi);
        document.getElementById("btn-odeme-ekle").disabled = durum.tamamlandi;
        document.getElementById("btn-odeme-kapat").classList.toggle("d-none", !durum.tamamlandi);
        document.getElementById("btn-odeme-vazgec").classList.toggle("d-none", durum.tamamlandi);

        const yazdir = document.getElementById("btn-fis-yazdir");
        yazdir.classList.toggle("d-none", !durum.tamamlandi);
        yazdir.disabled = !durum.tamamlandi;
        if (durum.tamamlandi) {
            yazdir.dataset.fisId = durum.fisId;
        } else {
            delete yazdir.dataset.fisId;
        }

        // Stok bakiyesini asan satirlar varsa satis gecti ama kasiyer bilsin.
        const stokUyari = document.getElementById("stok-uyari");
        const uyarilar = durum.uyarilar || [];
        stokUyari.classList.toggle("d-none", uyarilar.length === 0);
        stokUyari.textContent = uyarilar.length
            ? "Stok uyarısı — " + uyarilar.join(" · ")
            : "";

        odemeleriCiz(durum);

        if (!durum.tamamlandi) {
            tutarGirdi.value = durum.kalan > 0 ? paraBicimi.format(durum.kalan) : "";
            alinanGirdi.value = "";
            tutarGirdi.focus();
            tutarGirdi.select();
        }
    }

    function odemeleriCiz(durum) {
        const govde = document.getElementById("odeme-listesi");
        govde.innerHTML = "";

        durum.odemeler.forEach(function (o) {
            const tr = document.createElement("tr");
            tr.innerHTML =
                "<td>" + o.tipAdi + "</td>" +
                '<td class="text-end">' + paraBicimi.format(o.tutar) + "</td>" +
                '<td class="text-end">' + (o.alinanTutar != null ? paraBicimi.format(o.alinanTutar) : "—") + "</td>" +
                '<td class="text-end">' + (o.paraUstu != null ? paraBicimi.format(o.paraUstu) : "—") + "</td>" +
                '<td class="text-end"></td>';

            if (!durum.tamamlandi) tr.children[4].appendChild(iptalDugmesi(o.id));
            govde.appendChild(tr);
        });
    }

    function iptalDugmesi(odemeId) {
        const dugme = document.createElement("button");
        dugme.type = "button";
        dugme.className = "btn btn-sm btn-outline-danger";
        dugme.innerHTML = '<i class="ph ph-x"></i>';
        dugme.title = "Bu ödemeyi iptal et";
        dugme.addEventListener("click", async function () {
            const { durum, hata } = await gonder("/Odeme/Iptal", { fisId: sonDurum.fisId, odemeId: odemeId });
            if (hata) uyariGoster(hata); else uyariGizle();
            ciz(durum);
        });
        return dugme;
    }

    // ---------- Islemler ----------

    async function ac() {
        const yanit = await fetch("/Odeme/Durum");
        const durum = await yanit.json();

        if (!durum.fisId || durum.genelToplam <= 0) {
            window.kasa.uyariGoster("Sepet boş, ödeme alınamaz.");
            return;
        }

        uyariGizle();
        ciz(durum);
        modal.show();
    }

    async function odemeEkle() {
        const girilenTutar = sayiOku(tutarGirdi);
        if (tutarGirdi.value.trim() !== "" && girilenTutar === null) {
            uyariGoster("Geçerli bir mahsup tutarı girin.");
            tutarGirdi.focus();
            tutarGirdi.select();
            return;
        }

        const tutar = girilenTutar ?? sonDurum.kalan;

        // Alinan tutar zorunlu: kasiyer musterinin ne kadar verdigini
        // acikca girmeden odeme tamamlanamaz. Bos birakip Enter'a basmak
        // "musteri tam parayi verdi" saymamali - bu kasiyerin karar
        // verecegi bir sey, varsayilan degil.
        const alinan = sayiOku(alinanGirdi);
        if (alinan === null) {
            uyariGoster(alinanGirdi.value.trim() === ""
                ? "Müşteriden alınan tutarı girin."
                : "Müşteriden alınan tutar geçerli bir sayı olmalıdır.");
            alinanGirdi.focus();
            alinanGirdi.select();
            return;
        }

        const veri = { tip: TipNakit, tutar: tutar, alinanTutar: alinan };

        const { durum, hata } = await gonder("/Odeme/Ekle", veri);
        if (hata) uyariGoster(hata); else uyariGizle();
        ciz(durum);

        if (durum.tamamlandi) {
            window.kasa.sepetiYenile();
            window.bekleyenler.rozetiGuncelle();
        }
    }

    async function vazgec() {
        await gonder("/Odeme/Vazgec", { fisId: sonDurum.fisId });
        modal.hide();
        window.kasa.sepetiYenile();
    }

    function odemePenceresiniKapat() {
        return new Promise(function (tamamla) {
            if (!modalElemani.classList.contains("show")) {
                tamamla();
                return;
            }

            modalElemani.addEventListener("hidden.bs.modal", tamamla, { once: true });
            modal.hide();
        });
    }

    function fisOnizlemeyiSifirla() {
        fisOnizlemeYukleniyor.classList.remove("d-none");
        fisOnizlemeHata.classList.add("d-none");
        fisOnizlemeHata.textContent = "";
        fisOnizlemeIcerik.classList.add("d-none");
        fisOnizlemeIcerik.replaceChildren();
        fisOnizlemeYazdir.disabled = true;
    }

    async function fisOnizlemeAc() {
        const dugme = document.getElementById("btn-fis-yazdir");
        const fisId = Number(dugme.dataset.fisId);
        if (!fisId) {
            uyariGoster("Yazdırılacak fiş bulunamadı.");
            return;
        }

        dugme.disabled = true;
        fisOnizlemeyiSifirla();
        fisIstekKontrolcusu?.abort();
        fisIstekKontrolcusu = new AbortController();

        fisOnizlemeAciliyor = true;
        await odemePenceresiniKapat();
        fisOnizlemeAciliyor = false;
        fisOnizlemeModal.show();

        try {
            const yanit = await fetch("/Satis/Fis/" + fisId + "?gomulu=true", {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: fisIstekKontrolcusu.signal
            });

            if (!yanit.ok) throw new Error("Fiş alınamadı (HTTP " + yanit.status + ").");

            const html = await yanit.text();
            fisOnizlemeIcerik.innerHTML = html;
            fisOnizlemeYukleniyor.classList.add("d-none");
            fisOnizlemeIcerik.classList.remove("d-none");
            fisOnizlemeYazdir.disabled = false;
            fisOnizlemeYazdir.focus();
        } catch (hata) {
            if (hata.name === "AbortError") return;
            fisOnizlemeYukleniyor.classList.add("d-none");
            fisOnizlemeHata.textContent = hata.message || "Fiş önizlemesi hazırlanamadı.";
            fisOnizlemeHata.classList.remove("d-none");
        } finally {
            dugme.disabled = false;
        }
    }

    function fisYazdir() {
        const kaynak = fisOnizlemeIcerik.querySelector(".termal-fis");
        if (!kaynak) return;

        document.querySelector(".fis-baski-kopya")?.remove();

        const baskiKopyasi = document.createElement("div");
        baskiKopyasi.className = "fis-baski-kopya";
        baskiKopyasi.appendChild(kaynak.cloneNode(true));
        document.body.appendChild(baskiKopyasi);
        document.body.classList.add("fis-yazdiriliyor");

        let temizlendi = false;
        function temizle() {
            if (temizlendi) return;
            temizlendi = true;
            document.body.classList.remove("fis-yazdiriliyor");
            baskiKopyasi.remove();
        }

        window.addEventListener("afterprint", temizle, { once: true });
        try {
            window.print();
        } finally {
            // window.print masaustu tarayicilarda diyalog kapanana kadar bekler.
            setTimeout(temizle, 0);
        }
    }

    // ---------- Olaylar ----------

    document.getElementById("btn-odeme-ekle").addEventListener("click", odemeEkle);
    document.getElementById("btn-odeme-vazgec").addEventListener("click", vazgec);
    document.getElementById("btn-fis-yazdir").addEventListener("click", fisOnizlemeAc);
    fisOnizlemeYazdir.addEventListener("click", fisYazdir);

    document.getElementById("btn-odeme-kapat").addEventListener("click", function () {
        modal.hide();
        window.kasa.sepetiYenile();
    });

    [tutarGirdi, alinanGirdi].forEach(function (girdi) {
        girdi.addEventListener("keydown", function (e) {
            if (e.key === "Enter") { e.preventDefault(); odemeEkle(); }
        });

        // Kasiyer alandan ayrildiginda okunan degeri gorerek kontrol edebilsin.
        // 1500, 1.500 ve 1.500,00 ayni sekilde 1.500,00 olarak gosterilir.
        girdi.addEventListener("blur", function () {
            paraGirdisiniBicimlendir(girdi);
        });
    });

    // Pencere kapaninca odak barkod alanina doner. Fis onizlemesine
    // geciliyorsa aradaki modal gecisinde odagi arkaya kacirma.
    modalElemani.addEventListener("hidden.bs.modal", function () {
        if (!fisOnizlemeAciliyor) window.kasa.odakla();
    });

    fisOnizlemeModalElemani.addEventListener("hidden.bs.modal", function () {
        fisIstekKontrolcusu?.abort();
        fisIstekKontrolcusu = null;
        fisOnizlemeyiSifirla();
        window.kasa.odakla();
    });

    window.odeme = { ac };
})();
