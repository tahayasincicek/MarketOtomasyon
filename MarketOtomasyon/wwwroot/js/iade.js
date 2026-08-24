// Iade stok, para ve satis satirini tek transaction icinde degistirir.
// Kasiyerin cift tiklamasi ayni formu iki kez gondermemeli: ilk istek
// basarili olduktan sonra ikinci istek hakli olarak "kalan yok" hatasi alir
// ve kullanici tek iadenin basarisiz oldugunu zanneder.
(function () {
    "use strict";

    const form = document.getElementById("iade-form");
    const dugme = document.getElementById("btn-iade-tamamla");
    if (!form || !dugme) return;

    let gonderiliyor = false;

    form.addEventListener("submit", function (olay) {
        if (gonderiliyor) {
            olay.preventDefault();
            return;
        }

        gonderiliyor = true;
        dugme.disabled = true;
        dugme.setAttribute("aria-disabled", "true");
        dugme.innerHTML =
            '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>' +
            " İade kaydediliyor…";
    });
})();
