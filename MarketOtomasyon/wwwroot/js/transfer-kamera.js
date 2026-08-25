// Depo transferi ekraninda kameradan okunan barkodu dogrudan listeye
// ekler.
//
// Zayi ve mal kabul ekranlarindan farki: orada okunan barkod alana
// yazilir ve kullanici formu kendisi tamamlar, cunku tek urun icin miktar
// ve sebep girilmesi gerekir. Transferde ise ayni anda onlarca urun
// okutulur; her okumada durup dugmeye basmak isi kullanilamaz hale
// getirirdi. Bu yuzden okunan barkod hemen gonderilir.
(function () {
    "use strict";

    const form = document.querySelector('form[action$="/Transfer/SatirEkle"]');
    if (!form) return;

    const barkodGirdi = document.getElementById("barkod");
    const kameraDugmesi = document.getElementById("btn-kamera");
    if (!barkodGirdi || !kameraDugmesi) return;

    // Form gonderimi sayfayi yeniler ve kamera kapanir. Kullanici arka
    // arkaya okutabilsin diye kameranin acik oldugu bilgisi tasinir ve
    // sayfa yeniden yuklendiginde kamera kendiliginden acilir.
    const ANAHTAR = "transfer-kamera-acik";

    document.addEventListener("barkod-kamera-okundu", function (olay) {
        // kamera.js'in Kasa'ya ozel yapay Enter davranisini durdur.
        olay.preventDefault();

        const barkod = olay.detail && olay.detail.barkod;
        if (!barkod) return;

        barkodGirdi.value = barkod;

        try {
            sessionStorage.setItem(ANAHTAR, "1");
        } catch (e) {
            // Gizli sekmede erisim yoksa kamera acik kalmaz; okuma yine de
            // calisir, kullanici dugmeye tekrar basar.
        }

        form.submit();
    });

    // Kullanici kamerayi elle kapatirsa bir sonraki yuklemede acilmasin.
    kameraDugmesi.addEventListener("click", function () {
        const panel = document.getElementById("kamera-paneli");
        if (!panel) return;

        // Tiklama kamera.js tarafindan islenmeden once okunuyor: panel su
        // an aciksa kullanici KAPATIYOR demektir.
        const kapaniyor = !panel.classList.contains("d-none");

        try {
            if (kapaniyor) sessionStorage.removeItem(ANAHTAR);
            else sessionStorage.setItem(ANAHTAR, "1");
        } catch (e) { /* erisim yoksa sessizce gec */ }
    });

    // kamera.js bu dosyadan SONRA yukleniyor; dugmenin dinleyicisi ancak
    // o zaman bagli oluyor. load olayi ikisinin de hazir oldugunu garanti
    // eder.
    window.addEventListener("load", function () {
        let acilsin = false;
        try {
            acilsin = sessionStorage.getItem(ANAHTAR) === "1";
        } catch (e) {
            acilsin = false;
        }

        if (acilsin) kameraDugmesi.click();
    });
})();
