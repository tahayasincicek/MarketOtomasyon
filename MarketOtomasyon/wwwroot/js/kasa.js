// Kasa ekrani. Sunucuya form verisi gonderir, donen sepeti tabloya basar.
// Sayfa hicbir islemde yenilenmez; barkod alanindaki odak korunur.
(function () {
    "use strict";

    const barkodGirdi = document.getElementById("barkod");
    const sepetGovde = document.getElementById("sepet-govde");
    const bosSepet = document.getElementById("bos-sepet");
    const uyari = document.getElementById("uyari");

    const paraBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const miktarBicimi = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 3 });

    // F4 ile silinecek satir. Satira tiklayinca degisir.
    let seciliSatirId = null;

    // ---------- Sunucu cagrilari ----------

    async function gonder(yol, veri) {
        const yanit = await fetch(yol, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams(veri)
        });

        const govde = await yanit.json();

        // Hata durumunda sunucu { sepet, hata } sarmalayicisi doner.
        return yanit.ok
            ? { sepet: govde, hata: null }
            : { sepet: govde.sepet, hata: govde.hata };
    }

    async function sepetiYukle() {
        const yanit = await fetch("/Kasa/Sepet");
        ciz(await yanit.json());
    }

    // ---------- Ekrana basma ----------

    function ciz(sepet) {
        if (!sepet) return;

        sepetGovde.innerHTML = "";
        bosSepet.classList.toggle("d-none", !sepet.bos);

        sepet.satirlar.forEach(function (satir) {
            sepetGovde.appendChild(satirOlustur(satir));
        });

        // Silinen satir seciliyse secim dusurulur.
        if (!sepet.satirlar.some(s => s.satirId === seciliSatirId)) seciliSatirId = null;
        secimiVurgula();

        document.getElementById("fis-no").textContent = sepet.fisNo || "—";
        document.getElementById("ara-toplam").textContent = paraBicimi.format(sepet.araToplam);
        document.getElementById("toplam-kdv").textContent = paraBicimi.format(sepet.toplamKdv);
        document.getElementById("satir-sayisi").textContent = sepet.satirSayisi;
        document.getElementById("genel-toplam").textContent = paraBicimi.format(sepet.genelToplam) + " ₺";
        document.getElementById("btn-odeme").disabled = sepet.bos;

        kdvKirilimiCiz(sepet.kdvKirilimi);
    }

    function satirOlustur(satir) {
        const tr = document.createElement("tr");
        tr.dataset.satirId = satir.satirId;
        tr.style.cursor = "pointer";
        tr.addEventListener("click", function () {
            seciliSatirId = satir.satirId;
            secimiVurgula();
        });

        const kg = satir.birim === "KG";

        tr.innerHTML =
            '<td class="text-muted">' + satir.satirNo + "</td>" +
            "<td>" + metniKacir(satir.ad) +
                '<span class="d-block text-muted font-monospace small">' + metniKacir(satir.kod) +
                (kg ? ' <span class="badge bg-warning text-dark">kg</span>' : "") + "</span></td>" +
            '<td class="text-end"></td>' +
            '<td class="text-end">' + paraBicimi.format(satir.birimFiyat) + "</td>" +
            '<td class="text-end fw-semibold">' + paraBicimi.format(satir.satirToplam) + "</td>" +
            '<td class="text-end"></td>';

        tr.children[2].appendChild(miktarKutusu(satir));
        tr.children[5].appendChild(silDugmesi(satir.satirId));
        return tr;
    }

    function miktarKutusu(satir) {
        const girdi = document.createElement("input");
        girdi.type = "text";
        girdi.className = "form-control form-control-sm text-end";
        girdi.value = miktarBicimi.format(satir.miktar);
        girdi.addEventListener("click", e => e.stopPropagation());

        girdi.addEventListener("keydown", async function (e) {
            if (e.key !== "Enter") return;
            e.preventDefault();

            // Kullanici virgulle de yazabilir: 1,5 -> 1.5
            const miktar = parseFloat(girdi.value.replace(",", "."));
            if (isNaN(miktar)) { uyariGoster("Geçersiz miktar."); return; }

            await islet(() => gonder("/Kasa/MiktarGuncelle", { satirId: satir.satirId, miktar: miktar }));
        });

        return girdi;
    }

    function silDugmesi(satirId) {
        const dugme = document.createElement("button");
        dugme.type = "button";
        dugme.className = "btn btn-sm btn-outline-danger";
        dugme.textContent = "×";
        dugme.title = "Satırı sil";
        dugme.addEventListener("click", async function (e) {
            e.stopPropagation();
            await islet(() => gonder("/Kasa/SatirSil", { satirId: satirId }));
        });
        return dugme;
    }

    function kdvKirilimiCiz(kirilim) {
        const kap = document.getElementById("kdv-kirilimi");
        kap.innerHTML = "";

        (kirilim || []).forEach(function (k) {
            const satir = document.createElement("div");
            satir.className = "d-flex justify-content-between small text-muted";
            satir.innerHTML =
                "<span>KDV %" + k.oran + " matrah " + paraBicimi.format(k.matrah) + "</span>" +
                "<span>" + paraBicimi.format(k.kdvTutari) + "</span>";
            kap.appendChild(satir);
        });
    }

    function secimiVurgula() {
        Array.from(sepetGovde.children).forEach(function (tr) {
            tr.classList.toggle("table-active", Number(tr.dataset.satirId) === seciliSatirId);
        });
    }

    // ---------- Yardimcilar ----------

    function metniKacir(metin) {
        const d = document.createElement("div");
        d.textContent = metin ?? "";
        return d.innerHTML;
    }

    function uyariGoster(mesaj) {
        uyari.textContent = mesaj;
        uyari.classList.remove("d-none");
    }

    function uyariGizle() {
        uyari.classList.add("d-none");
    }

    function sonOkutulan(satirlar) {
        const son = satirlar[satirlar.length - 1];
        document.getElementById("son-ad").textContent = son ? son.ad : "—";
        document.getElementById("son-detay").textContent = son
            ? miktarBicimi.format(son.miktar) + " " + son.birim + " × " + paraBicimi.format(son.birimFiyat)
            : "";
    }

    /// Ortak akis: istegi calistir, sonucu ciz, odagi barkod alanina geri ver.
    async function islet(istek, sonuOkutulanGuncelle) {
        try {
            const { sepet, hata } = await istek();

            if (hata) uyariGoster(hata); else uyariGizle();
            if (sepet) {
                ciz(sepet);
                if (sonuOkutulanGuncelle && !hata) sonOkutulan(sepet.satirlar);
            }
        } catch (e) {
            uyariGoster("Sunucuya ulaşılamadı.");
        } finally {
            odakla();
        }
    }

    /// Odak her islemden sonra barkod alanina doner. Icerik secilmez:
    /// istek surerken kasiyer yeni barkod yazmis olabilir, uzerine yazilmasin.
    function odakla() {
        barkodGirdi.focus();
    }

    /// Istekleri sirayla calistiran basit kuyruk.
    let kuyruk = Promise.resolve();

    function siraya(is) {
        kuyruk = kuyruk.then(is, is);
        return kuyruk;
    }

    // ---------- Olaylar ----------

    barkodGirdi.addEventListener("keydown", function (e) {
        if (e.key !== "Enter") return;
        e.preventDefault();

        const barkod = barkodGirdi.value.trim();
        if (!barkod) return;

        // Alan sunucu cevabi BEKLENMEDEN temizlenir: barkod okuyucu cok hizli
        // yazar, sonraki okutma cevap gelmeden baslarsa iki barkod birlesirdi.
        barkodGirdi.value = "";

        // Istekler sirayla islenir; ust uste okutmada sepet yanlis sirada cizilmesin.
        siraya(() => islet(() => gonder("/Kasa/Ekle", { barkod: barkod }), true));
    });

    document.getElementById("btn-iptal").addEventListener("click", fisiIptalEt);

    document.getElementById("btn-odeme").addEventListener("click", function () {
        uyariGoster("Ödeme ekranı henüz yazılmadı (Gün 8).");
        odakla();
    });

    async function fisiIptalEt() {
        if (!confirm("Fiş iptal edilecek, sepetteki tüm satırlar silinecek. Onaylıyor musunuz?")) {
            odakla();
            return;
        }
        await islet(() => gonder("/Kasa/Iptal", {}));
        document.getElementById("son-ad").textContent = "—";
        document.getElementById("son-detay").textContent = "";
    }

    // Kisayollar sayfanin herhangi bir yerinde calisir.
    document.addEventListener("keydown", async function (e) {
        if (e.key === "F2") {
            e.preventDefault();
            document.getElementById("btn-odeme").click();
        } else if (e.key === "F4") {
            e.preventDefault();
            if (seciliSatirId === null) { uyariGoster("Önce silinecek satırı seçin."); odakla(); return; }
            await islet(() => gonder("/Kasa/SatirSil", { satirId: seciliSatirId }));
        } else if (e.key === "Escape") {
            e.preventDefault();
            fisiIptalEt();
        }
    });

    sepetiYukle().then(odakla);
})();
