"use strict";

document.addEventListener("error", function (event) {
    const imagen = event.target;
    if (!(imagen instanceof HTMLImageElement) || !imagen.hasAttribute("data-logo-tienda")) {
        return;
    }

    const reemplazo = imagen.getAttribute("data-reemplazo");
    if (reemplazo && imagen.dataset.reemplazoAplicado !== "true") {
        imagen.dataset.reemplazoAplicado = "true";
        imagen.src = reemplazo;
    }
}, true);
