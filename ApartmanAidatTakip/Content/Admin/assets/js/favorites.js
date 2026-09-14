/* ============================================================
   Favori Sayfalar - localStorage tabanlı favori sayfa yönetimi
   Karanlık/aydınlık tema butonunun yanındaki dropdown ile çalışır.
   Tüm layoutlarda ortak kullanılır (darkmode.js ile aynı mantık).

   Kullanım: header nav içine tetikleyici buton koymak yeterli:
       <button id="favToggle" ...><i class="bi bi-star-fill"></i></button>
   Panel ve tüm etkileşim bu script tarafından oluşturulur.
   ============================================================ */
(function () {
    var STORAGE_KEY = 'favoriSayfalar'; // [{ id, url, name }]

    function oku() {
        try {
            var raw = localStorage.getItem(STORAGE_KEY);
            var arr = raw ? JSON.parse(raw) : [];
            return Array.isArray(arr) ? arr : [];
        } catch (e) {
            return [];
        }
    }

    function yaz(list) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
        } catch (e) { /* localStorage yoksa yoksay */ }
    }

    function suankiUrl() {
        return window.location.pathname + window.location.search;
    }

    // Sayfa başlığından uygulama adını ayıklayıp temiz bir isim üret
    function suankiBaslik() {
        var t = (document.title || '').trim();
        // "@ViewBag.Title - Apartman Aidat Yazılımı" gibi son eki temizle
        t = t.split(' - ')[0].trim();
        if (!t) {
            t = suankiUrl();
        }
        return t;
    }

    function yeniId() {
        return 'f' + Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
    }

    function esc(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function baglat() {
        var toggle = document.getElementById('favToggle');
        if (!toggle) return;

        // Paneli oluştur
        var panel = document.createElement('div');
        panel.className = 'fav-panel';
        panel.setAttribute('hidden', '');
        panel.innerHTML =
            '<div class="fav-panel-header">' +
                '<span><i class="bi bi-star-fill"></i> Favori Sayfalar</span>' +
                '<button type="button" class="fav-add-btn" title="Bu sayfayı favorilere ekle">' +
                    '<i class="bi bi-plus-lg"></i> Ekle' +
                '</button>' +
            '</div>' +
            '<ul class="fav-list"></ul>' +
            '<div class="fav-empty">Henüz favori sayfa yok.<br>Bulunduğunuz sayfayı eklemek için “Ekle”ye basın.</div>';
        document.body.appendChild(panel);

        var liste = panel.querySelector('.fav-list');
        var bosMesaj = panel.querySelector('.fav-empty');
        var ekleBtn = panel.querySelector('.fav-add-btn');

        var suruklenen = null;

        function konumla() {
            var r = toggle.getBoundingClientRect();
            panel.style.top = (r.bottom + 8) + 'px';
            // Panel sağ kenarını butona hizala, ekran dışına taşmasını engelle
            var sag = window.innerWidth - r.right;
            panel.style.right = Math.max(10, sag) + 'px';
        }

        function ac() {
            konumla();
            panel.removeAttribute('hidden');
            ciz();
        }
        function kapat() {
            panel.setAttribute('hidden', '');
        }
        function acKapat() {
            if (panel.hasAttribute('hidden')) { ac(); } else { kapat(); }
        }

        function ciz() {
            var list = oku();
            liste.innerHTML = '';
            bosMesaj.style.display = list.length ? 'none' : 'block';

            list.forEach(function (item, idx) {
                var li = document.createElement('li');
                li.className = 'fav-item';
                li.setAttribute('draggable', 'true');
                li.dataset.id = item.id;
                li.innerHTML =
                    '<i class="bi bi-grip-vertical fav-drag" title="Sürükleyerek sırala"></i>' +
                    '<a href="' + esc(item.url) + '" class="fav-link" title="' + esc(item.url) + '">' + esc(item.name) + '</a>' +
                    '<span class="fav-actions">' +
                        '<button type="button" class="fav-icon-btn fav-edit" title="Adını değiştir"><i class="bi bi-pencil"></i></button>' +
                        '<button type="button" class="fav-icon-btn fav-del" title="Sil"><i class="bi bi-trash"></i></button>' +
                    '</span>';
                liste.appendChild(li);

                // --- Yeniden adlandırma ---
                li.querySelector('.fav-edit').addEventListener('click', function (e) {
                    e.stopPropagation();
                    duzenlemeyeGec(li, item);
                });

                // --- Silme ---
                li.querySelector('.fav-del').addEventListener('click', function (e) {
                    e.stopPropagation();
                    var l = oku().filter(function (x) { return x.id !== item.id; });
                    yaz(l);
                    ciz();
                });

                // --- Sürükle-bırak ile sıralama ---
                li.addEventListener('dragstart', function () {
                    suruklenen = li;
                    setTimeout(function () { li.classList.add('fav-dragging'); }, 0);
                });
                li.addEventListener('dragend', function () {
                    li.classList.remove('fav-dragging');
                    suruklenen = null;
                    sirayiKaydet();
                });
                li.addEventListener('dragover', function (e) {
                    e.preventDefault();
                    if (!suruklenen || suruklenen === li) return;
                    var r = li.getBoundingClientRect();
                    var ortasiGecti = (e.clientY - r.top) > r.height / 2;
                    if (ortasiGecti) {
                        li.parentNode.insertBefore(suruklenen, li.nextSibling);
                    } else {
                        li.parentNode.insertBefore(suruklenen, li);
                    }
                });
            });
        }

        function duzenlemeyeGec(li, item) {
            var link = li.querySelector('.fav-link');
            var input = document.createElement('input');
            input.type = 'text';
            input.className = 'fav-rename-input';
            input.value = item.name;
            link.replaceWith(input);
            input.focus();
            input.select();

            function kaydet() {
                var yeniAd = input.value.trim();
                var l = oku().map(function (x) {
                    if (x.id === item.id) { x.name = yeniAd || x.name; }
                    return x;
                });
                yaz(l);
                ciz();
            }
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { kaydet(); }
                else if (e.key === 'Escape') { ciz(); }
            });
            input.addEventListener('blur', kaydet);
            input.addEventListener('click', function (e) { e.stopPropagation(); });
        }

        function sirayiKaydet() {
            var kayitli = oku();
            var haritasi = {};
            kayitli.forEach(function (x) { haritasi[x.id] = x; });
            var yeniSira = [];
            liste.querySelectorAll('.fav-item').forEach(function (el) {
                var it = haritasi[el.dataset.id];
                if (it) { yeniSira.push(it); }
            });
            if (yeniSira.length === kayitli.length) { yaz(yeniSira); }
        }

        // --- Bu sayfayı ekle ---
        ekleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            var url = suankiUrl();
            var list = oku();
            if (list.some(function (x) { return x.url === url; })) {
                // Zaten varsa kısa uyarı animasyonu
                ekleBtn.classList.add('fav-add-warn');
                setTimeout(function () { ekleBtn.classList.remove('fav-add-warn'); }, 900);
                return;
            }
            list.push({ id: yeniId(), url: url, name: suankiBaslik() });
            yaz(list);
            ciz();
        });

        // --- Panel açma/kapama ---
        toggle.addEventListener('click', function (e) {
            e.stopPropagation();
            acKapat();
        });
        panel.addEventListener('click', function (e) { e.stopPropagation(); });
        document.addEventListener('click', function () { kapat(); });
        window.addEventListener('resize', function () {
            if (!panel.hasAttribute('hidden')) { konumla(); }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { kapat(); }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', baglat);
    } else {
        baglat();
    }
})();
