// Odeme penceresi. Kasa ekranindaki F2 / "Ödeme Al" bunu acar.
// Sepetten ayri calisir: fis kapanana kadar sepete geri donulebilir.
(function () {
    "use strict";

    const paraBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const modalElemani = document.getElementById("odeme-modal");
    const modal = new bootstrap.Modal(modalElemani);

    const uyari = document.getElementById("odeme-uyari");
    const tutarGirdi = document.getElementById("odeme-tutar");
    const alinanGirdi = document.getElementById("odeme-alinan");

    let sonDurum = null;

    // ---------- Yardimcilar ----------

    function sayiOku(girdi) {
        const metin = girdi.value.trim().replace(",", ".");
        return metin === "" ? null : parseFloat(metin);
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
        if (durum.tamamlandi) yazdir.href = "/Satis/Fis/" + durum.fisId;

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
        dugme.innerHTML = '<i class="bi bi-x-lg"></i>';
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
        const tutar = sayiOku(tutarGirdi) ?? sonDurum.kalan;

        // Alinan tutar zorunlu: kasiyer musterinin ne kadar verdigini
        // acikca girmeden odeme tamamlanamaz. Bos birakip Enter'a basmak
        // "musteri tam parayi verdi" saymamali - bu kasiyerin karar
        // verecegi bir sey, varsayilan degil.
        const alinan = sayiOku(alinanGirdi);
        if (alinan === null) {
            uyariGoster("Müşteriden alınan tutarı girin.");
            alinanGirdi.focus();
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

    // ---------- Olaylar ----------

    document.getElementById("btn-odeme-ekle").addEventListener("click", odemeEkle);
    document.getElementById("btn-odeme-vazgec").addEventListener("click", vazgec);

    document.getElementById("btn-odeme-kapat").addEventListener("click", function () {
        modal.hide();
        window.kasa.sepetiYenile();
    });

    [tutarGirdi, alinanGirdi].forEach(function (girdi) {
        girdi.addEventListener("keydown", function (e) {
            if (e.key === "Enter") { e.preventDefault(); odemeEkle(); }
        });
    });

    // Pencere kapaninca odak barkod alanina doner.
    modalElemani.addEventListener("hidden.bs.modal", () => window.kasa.odakla());

    window.odeme = { ac };
})();
