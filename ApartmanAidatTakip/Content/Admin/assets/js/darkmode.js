/* ============================================================
   Dark Mode - localStorage tabanlı tema yönetimi
   Tüm layoutlarda ortak kullanılır.
   ============================================================ */
(function () {
    var STORAGE_KEY = 'tema'; // "dark" | "light"

    // Kaydedilen temayı erkenden uygula (bu script <head> içinde çağrılır,
    // böylece sayfa açılırken beyaz ekran parlaması (flash) olmaz).
    function uygula(tema) {
        var kok = document.documentElement;
        if (tema === 'dark') {
            kok.classList.add('dark-mode');
        } else {
            kok.classList.remove('dark-mode');
        }
    }

    var kayitli;
    try {
        kayitli = localStorage.getItem(STORAGE_KEY);
    } catch (e) {
        kayitli = null;
    }
    uygula(kayitli === 'dark' ? 'dark' : 'light');

    // Toggle butonunu DOM hazır olunca bağla
    function baglaToggle() {
        var btn = document.getElementById('darkModeToggle');
        if (!btn) return;
        btn.addEventListener('click', function () {
            var kok = document.documentElement;
            var yeni = kok.classList.contains('dark-mode') ? 'light' : 'dark';
            uygula(yeni);
            try {
                localStorage.setItem(STORAGE_KEY, yeni);
            } catch (e) { /* localStorage yoksa yoksay */ }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', baglaToggle);
    } else {
        baglaToggle();
    }
})();
