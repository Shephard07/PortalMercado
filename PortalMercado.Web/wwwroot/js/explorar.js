(() => {
    const limpiar = () => document.querySelectorAll('[data-explorar-form]').forEach(form => {
        form.removeAttribute('aria-busy');
        form.querySelectorAll('button[type="submit"]').forEach(button => button.disabled = false);
        const aviso = form.querySelector('.ep-cargando');
        if (aviso) aviso.hidden = true;
    });
    document.querySelectorAll('[data-explorar-form]').forEach(form => {
        form.addEventListener('submit', () => {
            form.setAttribute('aria-busy', 'true');
            const aviso = form.querySelector('.ep-cargando');
            if (aviso) aviso.hidden = false;
            form.querySelectorAll('button[type="submit"]').forEach(button => button.disabled = true);
        });
    });
    window.addEventListener('pageshow', limpiar);
})();
