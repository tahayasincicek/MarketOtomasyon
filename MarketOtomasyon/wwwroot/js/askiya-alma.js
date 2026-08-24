// Askiya alma / geri cagirma. Kasadaki sepet bir kenara alinip
// sonraki musteriye gecilebilsin diye.
(function () {
    "use strict";

    const paraBicimi = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    const modalElemani = document.getElementById("bekleyenler-modal");
    const modal = new bootstrap.Modal(modalElemani);
    const govde = document.getElementById("bekleyenler-govde");
    const bosMesaj = document.getElementById("bekleyen-yok");
    const rozet = document.getElementById("bekleyen-sayisi");

    async function gonder(yol, veri) {
        const yanit = await fetch(yol, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: new URLSearchParams(veri || {})
        });

        const govde = await yanit.json();
        return { basarili: yanit.ok, hata: govde.hata };
    }

    async function listeyiGetir() {
        const yanit = await fetch("/Satis/Bekleyenler");
        return await yanit.json();
    }

    /// Bekleyen fis sayisi butondaki rozette gosterilir; kasiyer askida
    /// unutulmus sepet oldugunu vardiya boyunca gorebilsin.
    async function rozetiGuncelle() {
        const liste = await listeyiGetir();
        rozet.textContent = liste.length;
        rozet.classList.toggle("d-none", liste.length === 0);
        return liste;
    }

    function ciz(liste) {
        govde.innerHTML = "";
        bosMesaj.classList.toggle("d-none", liste.length > 0);

        liste.forEach(function (f) {
            const tr = document.createElement("tr");
            tr.innerHTML =
                '<td class="font-monospace small">' + f.fisNo + "</td>" +
                "<td>" + new Date(f.tarih).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" }) + "</td>" +
                '<td class="text-end">' + f.satirSayisi + "</td>" +
                '<td class="text-end fw-semibold">' + paraBicimi.format(f.genelToplam) + "</td>" +
                '<td class="text-end"></td>';

            tr.children[4].appendChild(geriCagirDugmesi(f.fisId));
            govde.appendChild(tr);
        });
    }

    function geriCagirDugmesi(fisId) {
        const dugme = document.createElement("button");
        dugme.type = "button";
        dugme.className = "btn btn-sm btn-primary";
        dugme.innerHTML = '<i class="ph ph-arrow-u-down-left"></i> Geri Çağır';
        dugme.addEventListener("click", async function () {
            const { basarili, hata } = await gonder("/Satis/GeriCagir", { fisId: fisId });
            if (!basarili) { window.kasa.uyariGoster(hata); return; }

            modal.hide();
            await window.kasa.sepetiYenile();
            await rozetiGuncelle();
        });
        return dugme;
    }

    async function ac() {
        ciz(await rozetiGuncelle());
        modal.show();
    }

    async function askiyaAl() {
        const { basarili, hata } = await gonder("/Satis/AskiyaAl");
        if (!basarili) { window.kasa.uyariGoster(hata); return; }

        await window.kasa.sepetiYenile();
        await rozetiGuncelle();
        window.kasa.odakla();
    }

    // ---------- Olaylar ----------

    document.getElementById("btn-askiya-al").addEventListener("click", askiyaAl);
    document.getElementById("btn-bekleyenler").addEventListener("click", ac);

    document.addEventListener("keydown", function (e) {
        if (e.key === "F7") { e.preventDefault(); askiyaAl(); }
        if (e.key === "F8") { e.preventDefault(); ac(); }
    });

    modalElemani.addEventListener("hidden.bs.modal", () => window.kasa.odakla());

    rozetiGuncelle();

    window.bekleyenler = { rozetiGuncelle };
})();
