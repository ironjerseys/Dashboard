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
