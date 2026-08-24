// Kasa ekrani. Sunucuya form verisi gonderir, donen sepeti tabloya basar.
// Sayfa hicbir islemde yenilenmez; barkod alanindaki odak korunur.
(function () {
    "use strict";

    const barkodGirdi = document.getElementById("barkod");
    const sepetGovde = document.getElementById("sepet-govde");
    const bosSepet = document.getElementById("bos-sepet");
    const uyari = document.getElementById("uyari");
    const kasaEkrani = document.getElementById("kasa-ekrani");
    const vardiyaAcik = kasaEkrani && kasaEkrani.dataset.vardiyaAcik === "true";

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
        const govde = await yanit.json();

        // Acik vardiya yoksa sunucu 409 + { hata } doner, sepet govdesi gelmez.
        if (!yanit.ok) { uyariGoster(govde.hata); return; }

        ciz(govde);
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
        // Bootstrap d-flex, hidden ozniteligini ezer; gizleme d-none ile yapilir.
        document.getElementById("indirim-satiri").classList.toggle("d-none", sepet.toplamIndirim <= 0);
        document.getElementById("toplam-indirim").textContent = "-" + paraBicimi.format(sepet.toplamIndirim);
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

        // Kampanya indirimi mavi, elle indirim kirmizi rozetle gosterilir;
        // kasiyer indirimin nereden geldigini bir bakista ayirt etsin.
        const indirimRozeti = satir.indirimTutari > 0
            ? ' <span class="badge ' + (satir.kampanyaId ? "bg-primary" : "bg-danger") + '">-'
              + paraBicimi.format(satir.indirimTutari) + "</span>"
            : "";

        const kampanyaEtiketi = satir.kampanyaAdi
            ? '<span class="d-block text-primary small">' + metniKacir(satir.kampanyaAdi) + "</span>"
            : "";

        tr.innerHTML =
            '<td class="text-muted">' + satir.satirNo + "</td>" +
            '<td><span class="urun-hucre">' + resimEtiketi(satir.resimYolu, satir.ad, "kucuk") +
                '<span class="urun-hucre-metin">' + metniKacir(satir.ad) +
                '<span class="d-block text-muted font-monospace small">' + metniKacir(satir.kod) +
                (kg ? ' <span class="badge bg-warning text-dark">kg</span>' : "") + indirimRozeti + "</span>" +
                kampanyaEtiketi + "</span></span></td>" +
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
        dugme.innerHTML = '<i class="ph ph-trash"></i>';
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

    // Sunucudaki _UrunResmi partial'inin JS karsiligi. Resmi olmayan urun
    // cogunluktadir; yer tutucu ayni yeri kaplar ki satirlar hizada kalsin.
    function resimEtiketi(yol, ad, boyut) {
        const sinif = "urun-resim urun-resim-" + boyut;
        return yol
            ? '<img class="' + sinif + '" src="' + metniKacir(yol) + '" alt="' + metniKacir(ad) +
              '" loading="lazy" decoding="async" />'
            : '<span class="' + sinif + ' urun-resim-yok" role="img" aria-label="' +
              metniKacir(ad) + ' — fotoğraf yok"><i class="ph ph-package"></i></span>';
    }

    function uyariGoster(mesaj) {
        uyari.textContent = mesaj;
        uyari.classList.remove("d-none");
    }

    function uyariGizle() {
        uyari.classList.add("d-none");
    }

    // Sepetin son satiri DEGIL, bu istekte okutulan urunun satiri gosterilir.
    // Okutulan urun sepette zaten varsa mevcut satirina eklenir; son satira
    // bakilirsa kasiyere bambaska bir urunun fotografi gosterilir.
    // KG urunlerde her okutma ayri satir acar, en yenisi sondakidir.
    function sonOkutulan(sepet) {
        const satirlar = sepet.satirlar || [];
        const okutulan = sepet.sonOkutulanUrunId ?? null;

        const eslesenler = okutulan === null
            ? satirlar
            : satirlar.filter(s => s.urunId === okutulan);

        const kaynak = eslesenler.length > 0 ? eslesenler : satirlar;
        const son = kaynak[kaynak.length - 1];
        document.getElementById("son-ad").textContent = son ? son.ad : "—";
        document.getElementById("son-detay").textContent = son
            ? miktarBicimi.format(son.miktar) + " " + son.birim + " × " + paraBicimi.format(son.birimFiyat)
            : "";

        sonResimCiz(son);
    }

    // Panelin yuksekligi sabit kalsin diye <img> ile <span> arasinda gecis
    // yapmak yerine ayni yerdeki dugum degistirilir.
    function sonResimCiz(son) {
        const eski = document.getElementById("son-resim");
        if (!eski) return;

        const kap = document.createElement("div");
        kap.innerHTML = son
            ? resimEtiketi(son.resimYolu, son.ad, "buyuk")
            : '<span class="urun-resim urun-resim-yok urun-resim-buyuk" role="img" ' +
              'aria-label="Son okutulan ürün"><i class="ph ph-barcode"></i></span>';

        const yeni = kap.firstElementChild;
        yeni.id = "son-resim";
        eski.replaceWith(yeni);
    }

    /// Ortak akis: istegi calistir, sonucu ciz, odagi barkod alanina geri ver.
    async function islet(istek, sonuOkutulanGuncelle) {
        try {
            const { sepet, hata } = await istek();

            if (hata) uyariGoster(hata); else uyariGizle();
            if (sepet) {
                ciz(sepet);
                if (sonuOkutulanGuncelle && !hata) sonOkutulan(sepet);
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

    function barkoduSirayaAl(barkod) {
        if (!vardiyaAcik) return;

        barkod = (barkod || "").trim();
        if (!barkod) return;

        // Alan sunucu cevabi BEKLENMEDEN temizlenir: barkod okuyucu cok hizli
        // yazar, sonraki okutma cevap gelmeden baslarsa iki barkod birlesirdi.
        barkodGirdi.value = "";

        // Kamera, el terminali ve hizli urun tuslari ayni sunucu akisini kullanir.
        siraya(() => islet(() => gonder("/Kasa/Ekle", { barkod: barkod }), true));
    }

    barkodGirdi.addEventListener("keydown", function (e) {
        if (e.key !== "Enter") return;
        e.preventDefault();
        barkoduSirayaAl(barkodGirdi.value);
    });

    document.querySelectorAll(".hizli-urun").forEach(function (dugme) {
        dugme.addEventListener("click", function () {
            barkoduSirayaAl(dugme.dataset.barkod);
        });
    });

    document.getElementById("btn-iptal").addEventListener("click", fisiIptalEt);

    document.getElementById("btn-odeme").addEventListener("click", () => window.odeme.ac());

    // Odeme penceresi bu ucları kullanir: sepeti yeniler, uyari gosterir, odagi geri verir.
    window.kasa = { sepetiYenile: sepetiYukle, uyariGoster, odakla };

    async function fisiIptalEt() {
        if (!confirm("Fiş iptal edilecek, sepetteki tüm satırlar silinecek. Onaylıyor musunuz?")) {
            odakla();
            return;
        }
        await islet(() => gonder("/Kasa/Iptal", {}));
        document.getElementById("son-ad").textContent = "—";
        document.getElementById("son-detay").textContent = "";
        sonResimCiz(null);
    }

    // ---------- Indirim paneli ----------

    const indirimPaneli = document.getElementById("indirim-paneli");
    const indirimYuzde = document.getElementById("indirim-yuzde");
    const indirimOnaylayan = document.getElementById("indirim-onaylayan");

    // "satir" veya "fis"; panel kapaliyken null.
    let indirimKapsami = null;

    function indirimAc(kapsam) {
        if (kapsam === "satir" && seciliSatirId === null) {
            uyariGoster("Önce indirim uygulanacak satırı seçin.");
            odakla();
            return;
        }

        indirimKapsami = kapsam;
        document.getElementById("indirim-baslik").textContent =
            kapsam === "satir" ? "Seçili satıra indirim" : "Fiş geneline indirim";

        indirimPaneli.classList.remove("d-none");
        indirimYuzde.value = "";
        indirimYuzde.focus();
    }

    function indirimKapat() {
        indirimKapsami = null;
        indirimPaneli.classList.add("d-none");
        odakla();
    }

    async function indirimUygula(yuzde) {
        const onaylayan = indirimOnaylayan.value;
        const veri = onaylayan ? { yuzde: yuzde, onaylayanKullaniciId: onaylayan } : { yuzde: yuzde };

        if (indirimKapsami === "satir") veri.satirId = seciliSatirId;

        const yol = indirimKapsami === "satir" ? "/Kasa/SatirIndirimi" : "/Kasa/FisIndirimi";
        const kapsam = indirimKapsami;

        indirimKapat();
        await islet(() => gonder(yol, veri));

        // Yetki reddi gibi durumlarda panel yeniden acilir; kasiyer onay secebilsin.
        if (!uyari.classList.contains("d-none")) {
            indirimKapsami = kapsam;
            indirimPaneli.classList.remove("d-none");
        }
    }

    document.getElementById("btn-indirim-uygula").addEventListener("click", function () {
        const yuzde = parseFloat(indirimYuzde.value.replace(",", "."));
        if (isNaN(yuzde) || yuzde <= 0) { uyariGoster("Geçerli bir indirim oranı girin."); return; }
        indirimUygula(yuzde);
    });

    // Indirimi kaldirmak icin yuzde 0 gonderilir; yetki kontrolu aranmaz.
    document.getElementById("btn-indirim-kaldir").addEventListener("click", () => indirimUygula(0));
    document.getElementById("btn-indirim-vazgec").addEventListener("click", indirimKapat);
    document.getElementById("btn-fis-indirim").addEventListener("click", () => indirimAc("fis"));

    indirimYuzde.addEventListener("keydown", function (e) {
        if (e.key === "Enter") { e.preventDefault(); document.getElementById("btn-indirim-uygula").click(); }
        if (e.key === "Escape") { e.preventDefault(); indirimKapat(); }
    });

    // Kisayollar sayfanin herhangi bir yerinde calisir.
    document.addEventListener("keydown", async function (e) {
        if (!vardiyaAcik) return;

        if (e.key === "F2") {
            e.preventDefault();
            document.getElementById("btn-odeme").click();
        } else if (e.key === "F4") {
            e.preventDefault();
            if (seciliSatirId === null) { uyariGoster("Önce silinecek satırı seçin."); odakla(); return; }
            await islet(() => gonder("/Kasa/SatirSil", { satirId: seciliSatirId }));
        } else if (e.key === "F5") {
            e.preventDefault();
            indirimAc("satir");
        } else if (e.key === "F6") {
            e.preventDefault();
            indirimAc("fis");
        } else if (e.key === "Escape") {
            e.preventDefault();
            // Indirim paneli acikken Esc once paneli kapatir, fisi iptal etmez.
            if (indirimKapsami !== null) indirimKapat(); else fisiIptalEt();
        }
    });

    if (vardiyaAcik) sepetiYukle().then(odakla);
})();
