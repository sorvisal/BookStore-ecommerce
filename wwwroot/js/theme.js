/* Dark mode toggle (storefront). The initial theme is set by an inline
   script in _Layout <head>; this file handles the toggle button. */
(function () {
    var el = document.documentElement;

    function current() {
        return el.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function apply(theme) {
        el.setAttribute('data-theme', theme);
        el.setAttribute('data-bs-theme', theme);
        try { localStorage.setItem('theme', theme); } catch (e) {}
        updateIcon(theme);
    }

    function updateIcon(theme) {
        var icon = document.querySelector('#nbThemeToggle i');
        if (!icon) return;
        icon.className = theme === 'dark' ? 'bi bi-sun' : 'bi bi-moon-stars';
    }

    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('nbThemeToggle');
        if (!btn) return;
        updateIcon(current());
        btn.addEventListener('click', function () {
            apply(current() === 'dark' ? 'light' : 'dark');
        });
    });
})();
