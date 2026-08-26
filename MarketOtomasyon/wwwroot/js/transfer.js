// Depo transferi ekrani.
//
// Satirlar istemci tarafinda eklenir; sayfa yalnizca "Transferi Tamamla"
// basildiginda sunucuya gider. Onceki surumde her barkod okumasi formu
// gonderiyordu ve sayfa yenilendigi icin kamera kapanip yeniden
// aciliyordu. Zayi ve mal kabul ekranlarinda bu sorun yoktu cunku orada
// form gonderilmiyor; transfer de artik ayni sekilde davraniyor.
(function () {
    "use strict";

    const form = document.getElementById("transfer-form");
    if (!form) return;

    const barkodGirdi = document.getElementById("barkod");
    const govde = document.getElementById("transfer-satirlar");
    const bosMesaji = document.getElementById("bos-mesaji");
    const tamamlaSerit = document.getElementById("tamamla-serit");
    const ozetMetni = document.getElementById("ozet-metni");
    const uyari = document.getElementById("transfer-uyari");
    const kaynakDepo = document.getElementById("kaynak-depo");
    const cozumAdresi = form.dataset.barkodCozUrl;

    let aktifIstek = null;

    const sayiBicimi = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 3 });

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

    /// Hidden alan adlari sunucudaki model baglama sirasina gore yeniden
    /// numaralanir: Satirlar[0], Satirlar[1]... Bir satir silindiginde
    /// aradaki indeks bosluk kalirsa MVC listeyi ilk boslukta keser.
    function indeksleriYenile() {
        const satirlar = govde.querySelectorAll("tr");

        satirlar.forEach(function (tr, i) {
            tr.querySelectorAll("input[type=hidden]").forEach(function (girdi) {
                girdi.name = girdi.name.replace(/Satirlar\[\d+\]/, "Satirlar[" + i + "]");
            });
        });

        const varMi = satirlar.length > 0;
        bosMesaji.classList.toggle("d-none", varMi);
        tamamlaSerit.classList.toggle("d-none", !varMi);

        let toplam = 0;
        satirlar.forEach(function (tr) {
            toplam += Number(tr.querySelector("input[name$='.Miktar']").value) || 0;
        });

        ozetMetni.innerHTML = varMi
            ? "<strong>" + satirlar.length + "</strong> ürün · toplam " + sayiBicimi.format(toplam)
            : "";
    }

    function gizliAlan(ad, deger) {
        const girdi = document.createElement("input");
        girdi.type = "hidden";
        girdi.name = "Satirlar[0]." + ad;   // indeksleriYenile duzeltecek
        girdi.value = deger;
        return girdi;
    }

    function satirEkle(urun) {
        // Ayni urun ikinci kez okutulursa yeni satir acilmaz, miktar artar:
        // UQ_TransferSatir ayni urunu iki satirda kabul etmiyor ve kasa
        // ekrani da barkod tekrarinda ayni sekilde davraniyor.
        const mevcut = govde.querySelector('tr[data-urun-id="' + urun.urunId + '"]');

        if (mevcut) {
            const miktarGirdi = mevcut.querySelector("input[name$='.Miktar']");
            const yeni = (Number(miktarGirdi.value) || 0) + urun.miktar;

            miktarGirdi.value = yeni;
            mevcut.querySelector("td.miktar").textContent = sayiBicimi.format(yeni);
            indeksleriYenile();
            return;
        }

        const tr = document.createElement("tr");
        tr.dataset.urunId = urun.urunId;

        tr.innerHTML =
            '<td class="kod"></td>' +
            '<td><span class="urun-hucre-metin"></span></td>' +
            "<td></td>" +
            '<td class="sayi bakiye">—</td>' +
            '<td class="sayi miktar"></td>' +
            '<td class="islem">' +
            '<button type="button" class="btn btn-danger btn-satir-sil" title="Satırı çıkar">' +
            '<i class="ph ph-trash"></i></button></td>';

        // Metin icerigi textContent ile yaziliyor: urun adi veritabanindan
        // geliyor ve innerHTML ile basilsa HTML olarak yorumlanabilirdi.
        tr.querySelector("td.kod").textContent = urun.kod;
        tr.querySelector(".urun-hucre-metin").textContent = urun.ad;
        tr.querySelectorAll("td")[2].textContent = urun.birim;
        tr.querySelector("td.miktar").textContent = sayiBicimi.format(urun.miktar);

        tr.appendChild(gizliAlan("UrunId", urun.urunId));
        tr.appendChild(gizliAlan("UrunKod", urun.kod));
        tr.appendChild(gizliAlan("UrunAd", urun.ad));
        tr.appendChild(gizliAlan("Birim", urun.birim));
        tr.appendChild(gizliAlan("Miktar", urun.miktar));

        govde.appendChild(tr);
        indeksleriYenile();
    }

    /* ---------- Barkod cozme ---------- */

    async function barkoduCoz(barkod) {
        const temiz = (barkod || "").trim();
        if (!temiz) return;

        if (!kaynakDepo || Number(kaynakDepo.value) <= 0) {
            uyariGoster("Önce kaynak depoyu seçin.");
            odakla();
            return;
        }

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
                // Koli barkodunda carpan, terazi barkodunda gramaj.
                miktar: sonuc.miktar
            });

            barkodGirdi.value = "";
        } catch (e) {
            if (e.name !== "AbortError") uyariGoster("Sunucuya ulaşılamadı.");
        } finally {
            odakla();
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

    // Kaynak depo degisince eski bakiyeler yaniltici olur.
    if (kaynakDepo) {
        kaynakDepo.addEventListener("change", function () {
            govde.querySelectorAll("td.bakiye").forEach(function (hucre) {
                hucre.textContent = "—";
            });
        });
    }

    indeksleriYenile();
})();
