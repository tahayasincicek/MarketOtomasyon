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
