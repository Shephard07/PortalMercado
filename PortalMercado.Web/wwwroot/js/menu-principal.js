(() => {
    "use strict";
    const menu = document.getElementById("menu-lateral");
    const boton = document.querySelector('[aria-controls="menu-lateral"]');
    if (!menu || !boton) return;
    // Bootstrap gestiona apertura y aria-expanded; Escape devuelve el foco al botón.
    menu.addEventListener("keydown", event => {
        if (event.key !== "Escape" || !window.matchMedia("(max-width: 991.98px)").matches) return;
        const instancia = window.bootstrap?.Collapse.getOrCreateInstance(menu, { toggle: false });
        if (instancia) {
            instancia.hide();
            boton.focus();
        }
    });
})();
