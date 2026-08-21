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
