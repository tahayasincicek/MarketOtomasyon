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
    const onayGirdi = document.getElementById("odeme-onay");

    let sonDurum = null;

    // ---------- Yardimcilar ----------

    function sayiOku(girdi) {
        const metin = girdi.value.trim().replace(",", ".");
        return metin === "" ? null : parseFloat(metin);
    }

    function secilenTip() {
        return document.querySelector('input[name="odeme-tip"]:checked').value;
    }

    function nakitMi() {
        return secilenTip() === "1";
    }

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

        odemeleriCiz(durum);

        if (!durum.tamamlandi) {
            tutarGirdi.value = durum.kalan > 0 ? paraBicimi.format(durum.kalan) : "";
            alinanGirdi.value = "";
            onayGirdi.value = "";
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
                "<td>" + (o.onayKodu || "") + "</td>" +
                '<td class="text-end"></td>';

            if (!durum.tamamlandi) tr.children[5].appendChild(iptalDugmesi(o.id));
            govde.appendChild(tr);
        });
    }

    function iptalDugmesi(odemeId) {
        const dugme = document.createElement("button");
        dugme.type = "button";
        dugme.className = "btn btn-sm btn-outline-danger";
        dugme.textContent = "×";
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
        const tip = secilenTip();
        const tutar = sayiOku(tutarGirdi) ?? sonDurum.kalan;
        const veri = { tip: tip, tutar: tutar };

        if (nakitMi()) {
            // Alinan bos birakilirsa musteri tam parayi vermis sayilir.
            veri.alinanTutar = sayiOku(alinanGirdi) ?? tutar;
        } else {
            const onay = onayGirdi.value.trim();
            if (onay) veri.onayKodu = onay;
        }

        const { durum, hata } = await gonder("/Odeme/Ekle", veri);
        if (hata) uyariGoster(hata); else uyariGizle();
        ciz(durum);

        if (durum.tamamlandi) window.kasa.sepetiYenile();
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

    // Nakit/kart secimine gore alanlar degisir.
    document.querySelectorAll('input[name="odeme-tip"]').forEach(function (girdi) {
        girdi.addEventListener("change", function () {
            document.getElementById("alinan-kutusu").classList.toggle("d-none", !nakitMi());
            document.getElementById("onay-kutusu").classList.toggle("d-none", nakitMi());
        });
    });

    [tutarGirdi, alinanGirdi, onayGirdi].forEach(function (girdi) {
        girdi.addEventListener("keydown", function (e) {
            if (e.key === "Enter") { e.preventDefault(); odemeEkle(); }
        });
    });

    // Pencere kapaninca odak barkod alanina doner.
    modalElemani.addEventListener("hidden.bs.modal", () => window.kasa.odakla());

    window.odeme = { ac };
})();
