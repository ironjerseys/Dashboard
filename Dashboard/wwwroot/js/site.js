window.setDocLang = function (l) { document.documentElement.lang = l; };

// Navbar mobile : panneau qui glisse depuis la droite
// Uses event delegation on document to survive Blazor DOM reconciliation
(function () {
    var isOpen = false;

    function mobile() { return window.innerWidth < 992; }

    function getPanel() { return document.getElementById('mainNavbar'); }

    function applyMobileStyles() {
        var p = getPanel();
        if (!p) return;
        var navH = (document.querySelector('nav.navbar') || {}).offsetHeight || 56;
        p.style.cssText = [
            'display:block',
            'position:fixed',
            'top:' + navH + 'px',
            'right:0',
            'left:auto',
            'width:260px',
            'min-height:calc(100vh - ' + navH + 'px)',
            'overflow-y:auto',
            'z-index:1050',
            'background-color:rgb(26,26,26)',
            'border-left:1px solid rgba(0,0,0,.15)',
            'padding:12px 0',
            'box-shadow:-4px 0 20px rgba(0,0,0,.2)',
            'transition:transform .25s ease',
            'transform:translateX(105%)',
        ].join(';');
    }

    function resetDesktopStyles() {
        var p = getPanel();
        if (!p) return;
        p.removeAttribute('style');
        var nav = p.closest('nav');
        if (nav) nav.classList.remove('nav-slide-active');
    }

    function setAriaExpanded(value) {
        var btn = document.getElementById('navToggleBtn');
        if (btn) btn.setAttribute('aria-expanded', value ? 'true' : 'false');
    }

    function openNav() {
        isOpen = true;
        setAriaExpanded(true);
        applyMobileStyles();
        var p = getPanel();
        var nav = p && p.closest('nav');
        if (nav) nav.classList.add('nav-slide-active');
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                var p2 = getPanel();
                if (p2) p2.style.transform = 'translateX(0)';
            });
        });
    }

    function closeNav() {
        isOpen = false;
        setAriaExpanded(false);
        var p = getPanel();
        if (p) p.style.transform = 'translateX(105%)';
        setTimeout(function () {
            if (!isOpen) resetDesktopStyles();
        }, 260);
    }

    // Event delegation — survives Blazor DOM reconciliation
    document.addEventListener('click', function (e) {
        if (e.target.closest('#navToggleBtn')) {
            if (mobile()) { isOpen ? closeNav() : openNav(); }
            return;
        }
        if (!mobile() || !isOpen) return;
        var p = getPanel();
        if (!p || !p.contains(e.target)) {
            closeNav();
        } else {
            var link = e.target.closest('a');
            if (link && !link.dataset.bsToggle) closeNav();
        }
    });

    window.addEventListener('resize', function () {
        if (!mobile() && isOpen) {
            isOpen = false;
            setAriaExpanded(false);
            resetDesktopStyles();
        }
    });
})();
