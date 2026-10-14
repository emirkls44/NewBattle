using System.Collections.Generic;
using System.Text;

namespace NewBattle.Progression
{
    /// <summary>
    /// Oyuncu isimlerinde kufur / hakaret / uygunsuz kelime engeli (Turkce + Ingilizce).
    /// Isimler diger oyunculara gorundugu icin Play Store kurallari bunu istiyor.
    ///
    /// Kontrolden once isim sadelestirilir: kucuk harf, Turkce harfler (ş→s, ğ→g, ü→u,
    /// ö→o, ç→c), rakam/simge hileleri (0→o, 1→i, 3→e, 4→a, 5→s, 7→t, @→a, $→s) ve
    /// tekrarlanan harfler (orrrospu → orospu).
    ///   - <see cref="Contains"/> listesi: ismin herhangi bir yerinde gecerse engellenir
    ///     (bosluk/nokta ile ayirmak da ise yaramaz: "o.r.o.s.p.u").
    ///   - <see cref="Words"/> listesi: kisa kokler, sadece tek basina kelime olarak
    ///     engellenir ("pic" engelli ama "Epic" serbest; "sik" engelli ama "Işık" serbest).
    /// Listeye ekleme yaparken kelimeyi yukaridaki sadelestirilmis haliyle yaz.
    /// </summary>
    public static class NameFilter
    {
        // "sik" ailesi: ı korunarak aranir, yoksa "Işık", "Işıker", "Işıkım" engellenirdi.
        private static readonly string[] ContainsKeepI =
        {
            "sikt", "siker", "sikey", "sikiy", "sikis", "sikik", "sikim", "siko", "sikem",
        };

        private static readonly string[] Contains =
        {
            // Turkce
            "orospu", "orosbu", "orspu", "yarak", "dalyarak", "amcik", "amck", "aminakoy", "aminako",
            "amnakoy", "aminoglu", "aminagoy", "sokuk", "pezevenk", "pezeveng", "kahpe", "ibne", "gavat",
            "kaltak", "fahise", "surtuk", "yavsak", "serefsiz", "tasak", "tasag", "porno", "anani", "ananin",
            "bacini", "bacinin", "gotveren", "gotunu", "gotune", "gotlek", "kerane", "godos",
            // Ingilizce
            "fuck", "shit", "bitch", "biatch", "cunt", "pusy", "niger", "whore", "slut", "ashole", "bastard",
            "penis", "vajina", "vagina", "dildo", "hitler", "motherf", "jackof", "blowjob", "horny", "rapist",
        };

        private static readonly string[] Words =
        {
            "sik", "sk", "amk", "amq", "aq", "mk", "oc", "pic", "got", "pust", "anan", "anani", "am", "amına",
            "meme", "dol", "seks", "sex", "sexy", "seksi", "dick", "cock", "fag", "fagot", "nazi", "rape",
            "porn", "tits", "cum", "anal", "niga", "nigas", "pkk", "isid",
        };

        private static readonly HashSet<string> WordSet = new(Words);

        /// <summary>Isim engelli bir kelime iceriyorsa true.</summary>
        public static bool IsBlocked(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // 1) Tek tek kelimeler (ı korunur: "sık" ile "sik" karismasin).
            List<string> words = Words_(name, dottedI: false);
            foreach (string word in words)
                if (WordSet.Contains(word))
                    return true;

            // 2) Tum isim tek parca (ayraclar atilir): uzun kokler her yerde aranir.
            string keepI = Collapse(string.Concat(words));
            foreach (string root in ContainsKeepI)
                if (keepI.Contains(root))
                    return true;

            string flat = Collapse(string.Concat(Words_(name, dottedI: true)));
            foreach (string root in Contains)
                if (flat.Contains(root))
                    return true;

            return false;
        }

        /// <summary>Engelli isim yerine kullanilacak ad (sunucu tarafinda).</summary>
        public static string Replacement(int seed) => $"Oyuncu#{1000 + (seed & 0x7fffffff) % 9000}";

        private static List<string> Words_(string name, bool dottedI)
        {
            var words = new List<string>();
            var current = new StringBuilder();

            foreach (char raw in name)
            {
                char c = Normalize(raw, dottedI);
                if (c >= 'a' && c <= 'z' || c == 'ı')
                {
                    current.Append(c);
                }
                else if (current.Length > 0)
                {
                    words.Add(Collapse(current.ToString()));
                    current.Clear();
                }
            }

            if (current.Length > 0)
                words.Add(Collapse(current.ToString()));
            return words;
        }

        private static char Normalize(char c, bool dottedI)
        {
            switch (c)
            {
                case 'I': return dottedI ? 'i' : 'ı';
                case 'İ': case 'i': case 'î': case 'ì': case 'í': return 'i';
                case 'ı': return dottedI ? 'i' : 'ı';
                case 'Ş': case 'ş': case '$': case '5': return 's';
                case 'Ğ': case 'ğ': return 'g';
                case 'Ü': case 'ü': case 'û': return 'u';
                case 'Ö': case 'ö': case '0': return 'o';
                case 'Ç': case 'ç': return 'c';
                case 'Â': case 'â': case '@': case '4': return 'a';
                case '1': case '!': case '|': return 'i';
                case '3': return 'e';
                case '7': return 't';
                case '8': return 'b';
                case '9': return 'g';
            }

            return char.ToLowerInvariant(c);
        }

        /// <summary>Art arda ayni harfleri teke indirir: "orrrospu" → "orospu".</summary>
        private static string Collapse(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (char c in text)
                if (result.Length == 0 || result[result.Length - 1] != c)
                    result.Append(c);
            return result.ToString();
        }
    }
}
