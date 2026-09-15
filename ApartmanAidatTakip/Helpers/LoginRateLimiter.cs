using System;
using System.Collections.Concurrent;

namespace ApartmanAidatTakip.Helpers
{
    /// <summary>
    /// Bellek (RAM) içinde tutulan basit giriş hız sınırlayıcı (rate limit).
    /// Kural: her 5 hatalı denemede bir kilit uygulanır. İlk kilit 2 dakika,
    /// sonraki kilitler 10 dakikadır. Kayıtlar tarayıcı/istemci anahtarına
    /// (IP adresi) göre tutulur; uygulama yeniden başlarsa sıfırlanır.
    /// </summary>
    public static class LoginRateLimiter
    {
        private const int Esik = 5;              // kaç hatada bir kilit
        private const int IlkKilitDakika = 2;    // ilk kilit süresi
        private const int SonrakiKilitDakika = 10; // sonraki kilitler

        private class Kayit
        {
            public int HataSayisi;
            public int KilitKademe;
            public DateTime? KilitBitis;
        }

        private static readonly ConcurrentDictionary<string, Kayit> _kayitlar =
            new ConcurrentDictionary<string, Kayit>();

        /// <summary>
        /// İstemci kilitliyse kalan saniyeyi döner; kilitli değilse 0.
        /// </summary>
        public static int KalanKilitSaniye(string anahtar)
        {
            if (string.IsNullOrEmpty(anahtar)) return 0;
            if (_kayitlar.TryGetValue(anahtar, out var k) && k.KilitBitis.HasValue)
            {
                double kalan = (k.KilitBitis.Value - DateTime.UtcNow).TotalSeconds;
                if (kalan > 0) return (int)Math.Ceiling(kalan);
            }
            return 0;
        }

        /// <summary>
        /// Başarısız bir giriş denemesini kaydeder; eşiğe ulaşıldıysa kilit uygular.
        /// </summary>
        public static void HataKaydet(string anahtar)
        {
            if (string.IsNullOrEmpty(anahtar)) return;
            var k = _kayitlar.GetOrAdd(anahtar, _ => new Kayit());
            lock (k)
            {
                k.HataSayisi++;
                if (k.HataSayisi % Esik == 0)
                {
                    k.KilitKademe++;
                    int dakika = k.KilitKademe == 1 ? IlkKilitDakika : SonrakiKilitDakika;
                    k.KilitBitis = DateTime.UtcNow.AddMinutes(dakika);
                }
            }
        }

        /// <summary>
        /// Başarılı giriş sonrası istemcinin kaydını temizler.
        /// </summary>
        public static void Sifirla(string anahtar)
        {
            if (string.IsNullOrEmpty(anahtar)) return;
            _kayitlar.TryRemove(anahtar, out _);
        }

        /// <summary>
        /// Kalan saniyeyi kullanıcıya gösterilecek Türkçe bir mesaja çevirir.
        /// </summary>
        public static string KilitMesaji(int kalanSaniye)
        {
            if (kalanSaniye <= 0) return null;
            if (kalanSaniye >= 60)
            {
                int dakika = (int)Math.Ceiling(kalanSaniye / 60.0);
                return $"Çok fazla hatalı deneme yaptınız. Lütfen {dakika} dakika sonra tekrar deneyin.";
            }
            return $"Çok fazla hatalı deneme yaptınız. Lütfen {kalanSaniye} saniye sonra tekrar deneyin.";
        }
    }
}
