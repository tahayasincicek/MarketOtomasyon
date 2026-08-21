// Vardiya ekrani: para sayim foyu, canli fark onizlemesi ve vardiya suresi.
// Hepsi gorsel yardim; kapanisi sunucu yeniden hesaplar (VardiyaService).
(function () {
    "use strict";

    const paraBicimi = new Intl.NumberFormat("tr-TR", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    vardiyaSuresi();
    sayimFoyu();
    farkOnizleme();

    // ---------- Vardiya suresi ----------

    function vardiyaSuresi() {
        const kutu = document.getElementById("vardiya-sure");
        if (!kutu) return;

        const baslangic = new Date(kutu.dataset.baslangic);
        if (isNaN(baslangic)) return;

        yaz();
        // Dakikada bir yeterli: saniye gostermiyoruz.
        setInterval(yaz, 60000);

        function yaz() {
            const dakika = Math.max(0, Math.floor((Date.now() - baslangic) / 60000));
            kutu.textContent = Math.floor(dakika / 60) + " sa " + (dakika % 60) + " dk";
        }
    }

    // ---------- Para sayim foyu ----------

    function sayimFoyu() {
        const adetler = document.querySelectorAll(".sayim-adet");
        if (adetler.length === 0) return;

        const sayilan = document.getElementById("sayilanTutar");
        const temizle = document.getElementById("sayim-temizle");

        adetler.forEach(function (girdi) {
            girdi.addEventListener("input", topla);

            // Enter formu gondermesin: kasiyer kupurler arasinda gezinir.
            girdi.addEventListener("keydown", function (e) {
                if (e.key !== "Enter") return;
                e.preventDefault();

                const sirada = [...adetler][[...adetler].indexOf(girdi) + 1];
                if (sirada) sirada.focus(); else sayilan.focus();
            });
        });

        if (temizle) {
            temizle.addEventListener("click", function () {
                adetler.forEach(g => { g.value = ""; });
                topla();
            });
        }

        function topla() {
            let toplam = 0;
            let girilenVar = false;

            adetler.forEach(function (girdi) {
                const adet = parseInt(girdi.value, 10);
                const deger = parseFloat(girdi.dataset.deger);
                const tutar = isNaN(adet) || adet < 0 ? 0 : adet * deger;

                if (!isNaN(adet) && adet > 0) girilenVar = true;

                // Tutar sutunu satirdaki son hucre.
                girdi.closest("tr").querySelector(".sayim-tutar").textContent = paraBicimi.format(tutar);
                toplam += tutar;
            });

            // Foy bosaltildiysa elle yazilan tutari silmeyelim: sadece
            // sayim yapildiginda sayilan tutari devralir.
            if (girilenVar) {
                sayilan.value = toplam.toFixed(2);
                sayilan.dispatchEvent(new Event("input"));
            }
        }
    }

    // ---------- Canli fark onizlemesi ----------

    function farkOnizleme() {
        const sayilan = document.getElementById("sayilanTutar");
        const beklenenKutu = document.getElementById("beklenen-tutar");
        const kutu = document.getElementById("fark-kutu");
        if (!sayilan || !beklenenKutu || !kutu) return;

        const beklenen = parseFloat(beklenenKutu.dataset.beklenen) || 0;
        const etiket = document.getElementById("fark-etiket");
        const tutar = document.getElementById("fark-tutar");

        sayilan.addEventListener("input", ciz);
        ciz();

        function ciz() {
            const girilen = parseFloat(sayilan.value);
            const fark = (isNaN(girilen) ? 0 : girilen) - beklenen;

            // Kurus artiklarini fark saymayalim.
            const yuvarlanmis = Math.round(fark * 100) / 100;

            kutu.classList.remove("denk", "acik", "fazla");

            if (yuvarlanmis === 0) {
                kutu.classList.add("denk");
                etiket.textContent = "Kasa denk";
            } else if (yuvarlanmis < 0) {
                kutu.classList.add("acik");
                etiket.textContent = "Kasa açığı";
            } else {
                kutu.classList.add("fazla");
                etiket.textContent = "Kasa fazlası";
            }

            tutar.textContent = paraBicimi.format(yuvarlanmis) + " ₺";
        }
    }
})();
