/* Vardiya (Z/X) raporunu ayrı sayfaya gitmeden gösterir.

   Kasadaki fiş önizlemesiyle aynı desen: rapor gövdesi AJAX ile
   çekilip pencerede gösteriliyor, yazdırma da oradan yapılıyor.
   Kasiyer listeden ayrılmadan raporu görüp basabilsin diye.

   Bağlantı yine gerçek bir <a>: JavaScript çalışmazsa ya da kullanıcı
   yeni sekmede açmak isterse rapor kendi adresinden erişilebilir
   kalıyor. Tıklama burada yakalanıp pencereye çevriliyor. */
(() => {
    const modalOge = document.getElementById("rapor-onizleme-modal");
    if (!modalOge || !window.bootstrap) return;

    const modal = new bootstrap.Modal(modalOge);
    const yukleniyor = document.getElementById("rapor-onizleme-yukleniyor");
    const hataKutusu = document.getElementById("rapor-onizleme-hata");
    const icerik = document.getElementById("rapor-onizleme-icerik");
    const yazdirDugmesi = document.getElementById("btn-rapor-yazdir");

    let istekKontrolcusu = null;

    function sifirla() {
        yukleniyor.classList.remove("d-none");
        hataKutusu.classList.add("d-none");
        hataKutusu.textContent = "";
        icerik.classList.add("d-none");
        icerik.innerHTML = "";
        yazdirDugmesi.disabled = true;
    }

    function hataGoster(mesaj) {
        yukleniyor.classList.add("d-none");
        hataKutusu.textContent = mesaj;
        hataKutusu.classList.remove("d-none");
    }

    async function raporAc(vardiyaId) {
        sifirla();
        modal.show();

        // Kullanici hizlica iki rapora tiklarsa onceki istek iptal
        // edilsin; yoksa gec gelen yanit yenisinin uzerine yazar.
        istekKontrolcusu?.abort();
        istekKontrolcusu = new AbortController();

        try {
            const yanit = await fetch(`/Vardiya/Rapor/${vardiyaId}?gomulu=true`, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: istekKontrolcusu.signal
            });

            if (!yanit.ok) {
                hataGoster("Rapor alınamadı. Sayfayı yenileyip tekrar deneyin.");
                return;
            }

            icerik.innerHTML = await yanit.text();
            yukleniyor.classList.add("d-none");
            icerik.classList.remove("d-none");
            yazdirDugmesi.disabled = false;
        } catch (hata) {
            if (hata.name === "AbortError") return;
            hataGoster("Rapor alınamadı. Bağlantınızı kontrol edin.");
        }
    }

    /* Yazdirirken sayfanin tamami degil yalnizca rapor basilir.
       Odeme ekranindaki fis yazdirmayla ayni yontem: govde
       gecici olarak kopyalanip .fis-yazdiriliyor sinifi ile
       digerleri gizleniyor. */
    function yazdir() {
        const kaynak = icerik.querySelector(".termal-fis");
        if (!kaynak) return;

        document.querySelector(".fis-baski-kopya")?.remove();

        const kopya = document.createElement("div");
        kopya.className = "fis-baski-kopya";
        kopya.appendChild(kaynak.cloneNode(true));
        document.body.appendChild(kopya);
        document.body.classList.add("fis-yazdiriliyor");

        let temizlendi = false;
        function temizle() {
            if (temizlendi) return;
            temizlendi = true;
            document.body.classList.remove("fis-yazdiriliyor");
            kopya.remove();
        }

        window.addEventListener("afterprint", temizle, { once: true });
        try {
            window.print();
        } finally {
            // Masaustu tarayicilarda print diyalog kapanana kadar bekler.
            setTimeout(temizle, 0);
        }
    }

    document.querySelectorAll("a.rapor-ac").forEach(baglanti => {
        baglanti.addEventListener("click", olay => {
            // Ctrl/Cmd/orta tik ile yeni sekmede acmak isteyen
            // kullaniciyi engelleme.
            if (olay.ctrlKey || olay.metaKey || olay.shiftKey || olay.button !== 0) return;

            olay.preventDefault();
            raporAc(baglanti.dataset.vardiyaId);
        });
    });

    yazdirDugmesi.addEventListener("click", yazdir);

    modalOge.addEventListener("hidden.bs.modal", () => {
        istekKontrolcusu?.abort();
        sifirla();
    });
})();
