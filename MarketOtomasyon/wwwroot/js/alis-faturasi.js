// Alis faturasi ekrani.
//
// Satirlar istemci tarafinda eklenir; sayfa yalnizca "Faturayi Kaydet"
// basildiginda sunucuya gider. Transfer ekraniyla ayni kamera deseni
// kullanilir (bkz. transfer.js basindaki aciklama).
//
// Transfer'den FARKI: ayni urun ikinci kez okutulunca miktar BIRLESMEZ,
// yeni bir satir acilir. Cunku ayni faturada ayni urun farkli lot/SKT ile
// birden fazla kez gelebilir (UQ_AlisFatSatir kisiti UNIQUE(FaturaId,
// SatirNo) - UNIQUE(FaturaId, UrunId) DEGIL). Transferde ise parti
// ayrimi onemsizdi, tek depo bakiyesi tasiniyordu.
(function () {
    "use strict";

    const form = document.getElementById("fatura-form");
    if (!form) return;

    const barkodGirdi = document.getElementById("barkod");
    const govde = document.getElementById("fatura-satirlar");
    const bosMesaji = document.getElementById("bos-mesaji");
    const tamamlaSerit = document.getElementById("tamamla-serit");
    const ozetMetni = document.getElementById("ozet-metni");
    const uyari = document.getElementById("fatura-uyari");
    const cozumAdresi = form.dataset.barkodCozUrl;

    let aktifIstek = null;
    let siraNo = 0;   // her satir icin benzersiz anahtar; UrunId tekrar edebilir

    const sayiBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    /* ---------- Yardimcilar ---------- */

    function uyariGoster(mesaj) {
        uyari.textContent = mesaj;
        uyari.classList.remove("d-none");
    }

    function uyariGizle() {
        uyari.classList.add("d-none");
    }

    function odakla() {
        barkodGirdi.focus();
        barkodGirdi.select();
    }

    /// Hidden/gorunur alan adlari sunucudaki model baglama sirasina gore
    /// yeniden numaralanir: Satirlar[0], Satirlar[1]... Bir satir
    /// silindiginde aradaki bosluk kalirsa MVC listeyi orada keser.
    function indeksleriYenile() {
        const satirlar = govde.querySelectorAll("tr");

        satirlar.forEach(function (tr, i) {
            tr.querySelectorAll("[name*='Satirlar[']").forEach(function (alan) {
                alan.name = alan.name.replace(/Satirlar\[\d+\]/, "Satirlar[" + i + "]");
            });
        });

        const varMi = satirlar.length > 0;
        bosMesaji.classList.toggle("d-none", varMi);
        tamamlaSerit.classList.toggle("d-none", !varMi);

        let toplamMatrah = 0;
        satirlar.forEach(function (tr) {
            toplamMatrah += matrahHesapla(tr);
        });

        ozetMetni.innerHTML = varMi
            ? "<strong>" + satirlar.length + "</strong> kalem · matrah toplamı " + sayiBicimi.format(toplamMatrah)
            : "";
    }

    function matrahHesapla(tr) {
        const miktar = Number(tr.querySelector(".girdi-miktar").value) || 0;
        const fiyat = Number(tr.querySelector(".girdi-fiyat").value) || 0;
        return miktar * fiyat;
    }

    /// Matrah goruntusu bilgi amaclidir; kesin tutar sunucuda
    /// FaturaHesaplayici ile yeniden hesaplanir.
    function satiriYenidenHesapla(tr) {
        tr.querySelector(".goster-matrah").textContent = sayiBicimi.format(matrahHesapla(tr));
        indeksleriYenile();
    }

    function satirEkle(urun) {
        const bu = ++siraNo;
        const tr = document.createElement("tr");
        tr.dataset.satirNo = bu;

        tr.innerHTML =
            '<td class="kod"></td>' +
            '<td><span class="urun-hucre-metin"></span>' +
            '<input type="hidden" name="Satirlar[0].UrunId" />' +
            '<input type="hidden" name="Satirlar[0].UrunKod" />' +
            '<input type="hidden" name="Satirlar[0].UrunAd" /></td>' +
            '<td class="sayi"><input type="number" step="0.001" min="0.001" ' +
            'class="form-control form-control-sm text-end girdi-miktar" name="Satirlar[0].Miktar" />' +
            '<input type="hidden" name="Satirlar[0].Birim" /></td>' +
            '<td class="sayi"><input type="number" step="0.01" min="0" ' +
            'class="form-control form-control-sm text-end girdi-fiyat" name="Satirlar[0].BirimFiyat" /></td>' +
            '<td class="sayi"><input type="number" step="0.01" min="0" max="100" ' +
            'class="form-control form-control-sm text-end girdi-kdv" name="Satirlar[0].KdvOrani" /></td>' +
            '<td class="sayi goster-matrah">0,00</td>' +
            '<td><input type="date" class="form-control form-control-sm" ' +
            'name="Satirlar[0].SonKullanmaTarihi" /></td>' +
            '<td><input type="text" class="form-control form-control-sm" ' +
            'name="Satirlar[0].LotNo" placeholder="Lot" /></td>' +
            '<td class="islem"><button type="button" class="btn btn-danger btn-satir-sil" ' +
            'title="Satırı çıkar"><i class="ph ph-trash"></i></button></td>';

        // Metin icerigi textContent ile yaziliyor: urun adi veritabanindan
        // geliyor ve innerHTML ile basilsa HTML olarak yorumlanabilirdi.
        tr.querySelector("td.kod").textContent = urun.kod;
        tr.querySelector(".urun-hucre-metin").textContent = urun.ad;

        tr.querySelector("input[name$='.UrunId']").value = urun.urunId;
        tr.querySelector("input[name$='.UrunKod']").value = urun.kod;
        tr.querySelector("input[name$='.UrunAd']").value = urun.ad;
        tr.querySelector("input[name$='.Birim']").value = urun.birim;
        tr.querySelector(".girdi-miktar").value = urun.miktar;
        tr.querySelector(".girdi-kdv").value = urun.kdvOrani;

        govde.appendChild(tr);
        indeksleriYenile();

        // Birim fiyat bos gelir: barkod coz uc'u alis fiyatini bilmez,
        // yalnizca satis fiyatini doner. Kullanici KDV haric alis
        // fiyatini kendisi girer.
        tr.querySelector(".girdi-fiyat").focus();
    }

    /* ---------- Barkod cozme ---------- */

    async function barkoduCoz(barkod) {
        const temiz = (barkod || "").trim();
        if (!temiz) return;

        // Kamera hizli okuyabilir; onceki istek gec donup yeni sonucu
        // ezmesin.
        if (aktifIstek) aktifIstek.abort();
        const buIstek = new AbortController();
        aktifIstek = buIstek;

        try {
            const ayirac = cozumAdresi.includes("?") ? "&" : "?";
            const cevap = await fetch(
                cozumAdresi + ayirac + "barkod=" + encodeURIComponent(temiz),
                { headers: { "Accept": "application/json" }, signal: buIstek.signal });

            const sonuc = await cevap.json();

            if (!cevap.ok || !sonuc || !sonuc.basarili) {
                uyariGoster(sonuc && sonuc.hata ? sonuc.hata : "Barkod çözümlenemedi.");
                return;
            }

            uyariGizle();
            satirEkle({
                urunId: sonuc.urunId,
                kod: sonuc.kod,
                ad: sonuc.ad,
                birim: sonuc.birim,
                kdvOrani: sonuc.kdvOrani,
                miktar: sonuc.miktar
            });

            barkodGirdi.value = "";
        } catch (e) {
            if (e.name !== "AbortError") uyariGoster("Sunucuya ulaşılamadı.");
        } finally {
            barkodGirdi.focus();
        }
    }

    /* ---------- Olaylar ---------- */

    // kamera.js barkod okudugunda bu olayi yayar. preventDefault(),
    // kameranin Kasa'ya ozel yapay Enter davranisini durdurur.
    document.addEventListener("barkod-kamera-okundu", function (olay) {
        olay.preventDefault();

        const barkod = olay.detail && olay.detail.barkod;
        if (barkod) barkoduCoz(barkod);
    });

    document.getElementById("btn-satir-ekle")
        .addEventListener("click", () => barkoduCoz(barkodGirdi.value));

    barkodGirdi.addEventListener("keydown", function (olay) {
        if (olay.key !== "Enter") return;

        // Enter formu gondermesin; once urun eklensin.
        olay.preventDefault();
        barkoduCoz(barkodGirdi.value);
    });

    govde.addEventListener("click", function (olay) {
        const dugme = olay.target.closest(".btn-satir-sil");
        if (!dugme) return;

        dugme.closest("tr").remove();
        indeksleriYenile();
        odakla();
    });

    // Miktar veya fiyat degisince matrah gorutusu canli guncellensin.
    govde.addEventListener("input", function (olay) {
        if (olay.target.classList.contains("girdi-miktar") || olay.target.classList.contains("girdi-fiyat")) {
            satiriYenidenHesapla(olay.target.closest("tr"));
        }
    });

    indeksleriYenile();
})();
