document.addEventListener('DOMContentLoaded', function () {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
            } else {
                entry.target.classList.remove('is-visible');
            }
        });
    }, { threshold: 0.3 });

    document.querySelectorAll('.afficher').forEach((el, i) => {
        el.setAttribute('data-index', String(i));
        observer.observe(el);
    });
});

window.cvFadeInit = () => {
    // Évite de recréer plusieurs observers si tu rappelles la fonction
    if (!window.__cvFadeObserver) {
        window.__cvFadeObserver = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) entry.target.classList.add('is-visible');
                else entry.target.classList.remove('is-visible');
            });
        }, { threshold: 0.3 });
    }

    const observer = window.__cvFadeObserver;

    document.querySelectorAll('.afficher').forEach((el, i) => {
        // Pour éviter de ré-observer 50 fois si OnAfterRender se déclenche à nouveau
        if (el.dataset.cvObserved === "1") return;

        el.dataset.cvObserved = "1";
        el.setAttribute('data-index', String(i));
        observer.observe(el);
    });
};

