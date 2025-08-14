// IntersectionObserver pour ajouter/retirer la classe fade-in
document.addEventListener('DOMContentLoaded', function () {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in');
            }
            else {
                entry.target.classList.remove('fade-in');
            }
        });
    }, { threshold: 0.3 });

    document.querySelectorAll('.item').forEach((el, i) => {
        el.setAttribute('data-index', String(i));
        observer.observe(el);
    });
});