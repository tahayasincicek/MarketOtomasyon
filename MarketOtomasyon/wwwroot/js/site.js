// Ust seritteki tarih/saat. Kasa terminalinde duvarda saat olmaz;
// fis saatiyle ekrandaki saat ayni kaynaktan okunsun diye hep gorunur.
(function () {
    "use strict";

    const kutu = document.getElementById("serit-saat");
    if (!kutu) return;

    const bicim = new Intl.DateTimeFormat("tr-TR", {
        day: "2-digit", month: "2-digit", year: "numeric",
        hour: "2-digit", minute: "2-digit"
    });

    yaz();
    setInterval(yaz, 15000);

    function yaz() { kutu.textContent = bicim.format(new Date()); }
})();

// Dar ekranda sol menuyu ac/kapat.
(function () {
    "use strict";

    const menu = document.getElementById("yan-menu");
    const dugme = document.getElementById("menu-dugmesi");
    if (!menu || !dugme) return;

    dugme.addEventListener("click", function (e) {
        e.stopPropagation();
        menu.classList.toggle("acik");
    });

    // Menu acikken disariya tiklanirsa kapanir.
    document.addEventListener("click", function (e) {
        if (menu.classList.contains("acik") && !menu.contains(e.target)) {
            menu.classList.remove("acik");
        }
    });
})();

// Menu gruplarinin acik/kapali durumunu sayfalar arasinda hatirla.
//
// Sunucu her sayfada yalnizca icinde bulunulan grubu acik gonderir.
// Bu tek basina kullanildiginda, kullanicinin elle actigi baska bir
// grup ilk tiklamada kapaniyordu: menude gezinen biri her adimda
// actigi yeri kaybediyordu.
//
// Durum localStorage'da tutulur; sunucunun varsayilani yalnizca hic
// kayit yokken (ilk giris) gecerlidir.
(function () {
    "use strict";

    const gruplar = document.querySelectorAll("[data-menu-grup]");
    if (!gruplar.length) return;

    const ANAHTAR = "menu-acik-gruplar";

    let kayit = null;
    try {
        kayit = JSON.parse(localStorage.getItem(ANAHTAR) || "null");
    } catch (e) {
        kayit = null;   // bozuk kayit veya erisim yok: varsayilanla devam
    }

    if (kayit && typeof kayit === "object") {
        gruplar.forEach(function (grup) {
            const ad = grup.dataset.menuGrup;
            if (Object.prototype.hasOwnProperty.call(kayit, ad)) {
                grup.open = kayit[ad] === true;
            }
        });
    }

    // Bulunulan sayfanin grubu her zaman acik kalir; kullanici onu daha
    // once kapatmis olsa bile nerede oldugunu gormeli.
    gruplar.forEach(function (grup) {
        if (grup.querySelector(".menu-oge.aktif")) grup.open = true;
    });

    // Dinleyici, yukaridaki duzeltmelerden SONRA baglanir: aksi halde
    // sayfa yuklenirken yaptigimiz duzeltmeler de kullanici tercihi
    // sanilip kaydedilirdi.
    function kaydet() {
        const durum = {};
        gruplar.forEach(function (grup) { durum[grup.dataset.menuGrup] = grup.open; });

        try {
            localStorage.setItem(ANAHTAR, JSON.stringify(durum));
        } catch (e) {
            // Gizli sekmede veya kota dolduysa sessizce vazgec: menunun
            // calismasi bu kaydin basarisina bagli olmamali.
        }
    }

    gruplar.forEach(function (grup) { grup.addEventListener("toggle", kaydet); });
})();
