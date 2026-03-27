"""
MEB Okul Turlerine Gore Ders Havuzu SQLite Veritabanlari Olusturma Scripti
Kaynak: MEB Talim ve Terbiye Kurulu Baskanligi 2024-2025 Haftalik Ders Cizelgeleri
"""
import sqlite3
import os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

def create_db(filename, lessons):
    """Verilen ders listesiyle SQLite veritabani olusturur."""
    path = os.path.join(SCRIPT_DIR, filename)
    if os.path.exists(path):
        os.remove(path)
    conn = sqlite3.connect(path)
    c = conn.cursor()
    c.execute("""
        CREATE TABLE IF NOT EXISTS ders (
            id INTEGER PRIMARY KEY,
            kod TEXT,
            ad TEXT,
            varsayilan_blok TEXT,
            sabah_onceligi INTEGER
        )
    """)
    for lesson in lessons:
        c.execute(
            "INSERT INTO ders (kod, ad, varsayilan_blok, sabah_onceligi) VALUES (?, ?, ?, ?)",
            lesson
        )
    conn.commit()
    conn.close()
    print(f"  {filename}: {len(lessons)} ders")

# ============================================================
# 1. ILKOKUL (1-4. Sinif)
# ============================================================
print("1. Ilkokul")
create_db("ilkokul.sqlite", [
    # (kod, ad, varsayilan_blok, sabah_onceligi)
    ("TUR", "Turkce", "2+2", 8),
    ("MAT", "Matematik", "2+2+1", 9),
    ("HYB", "Hayat Bilgisi", "2+1", 6),
    ("FEN", "Fen Bilimleri", "2+1", 7),
    ("SOS", "Sosyal Bilgiler", "2+1", 6),
    ("ING", "Yabanci Dil (Ingilizce)", "2", 5),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("GRS", "Gorsel Sanatlar", "1", 2),
    ("MUZ", "Muzik", "1", 2),
    ("BEO", "Beden Egitimi ve Oyun", "2", 1),
    ("TRG", "Trafik Guvenligi", "1", 2),
    ("IHD", "Insan Haklari Yurttaslik ve Demokrasi", "1", 3),
    ("SER", "Serbest Etkinlikler", "2", 0),
])

# ============================================================
# 2. ORTAOKUL (5-8. Sinif)
# ============================================================
print("2. Ortaokul")
create_db("ortaokul.sqlite", [
    ("TUR", "Turkce", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+1", 9),
    ("FEN", "Fen Bilimleri", "2+2", 8),
    ("SOS", "Sosyal Bilgiler", "2+1", 6),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 6),
    ("ING", "Yabanci Dil (Ingilizce)", "2+1", 5),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("GRS", "Gorsel Sanatlar", "1", 2),
    ("MUZ", "Muzik", "1", 2),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("BTY", "Bilisim Teknolojileri ve Yazilim", "2", 4),
    ("TET", "Teknoloji ve Tasarim", "2", 4),
    # Secmeli dersler
    ("HKU", "Halk Kulturu", "2", 2),
    ("KKU", "Kent Kulturu", "2", 2),
    ("KUR", "Kuran-i Kerim", "2", 3),
    ("PYH", "Peygamberimizin Hayati", "2", 3),
    ("TDB", "Temel Dini Bilgiler", "2", 3),
    ("DRA", "Drama", "2", 2),
    ("SFE", "Spor ve Fiziki Etkinlikler", "2", 1),
    ("YDE", "Yabanci Dil (Ek)", "2", 5),
    ("ZKA", "Zeka Oyunlari", "2", 4),
    ("BDI", "Bilim ve Doga Incelemeleri", "2", 4),
])

# ============================================================
# 3. IMAM HATIP ORTAOKULU (5-8. Sinif)
# ============================================================
print("3. Imam Hatip Ortaokulu")
create_db("imam_hatip_ortaokulu.sqlite", [
    ("TUR", "Turkce", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+1", 9),
    ("FEN", "Fen Bilimleri", "2+2", 8),
    ("SOS", "Sosyal Bilgiler", "2+1", 6),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 6),
    ("ING", "Yabanci Dil (Ingilizce)", "2+1", 5),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("KUR", "Kuran-i Kerim", "2", 7),
    ("ARA", "Arapca", "2", 6),
    ("PYH", "Peygamberimizin Hayati", "2", 5),
    ("TDB", "Temel Dini Bilgiler", "1", 4),
    ("GRS", "Gorsel Sanatlar", "1", 2),
    ("MUZ", "Muzik", "1", 2),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("BTY", "Bilisim Teknolojileri ve Yazilim", "2", 4),
    ("TET", "Teknoloji ve Tasarim", "2", 4),
    ("RKP", "Rehberlik ve Kariyer Planlama", "1", 2),
])

# ============================================================
# 4. ANADOLU LISESI (9-12. Sinif)
# ============================================================
print("4. Anadolu Lisesi")
create_db("anadolu_lisesi.sqlite", [
    # Zorunlu dersler
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+2", 9),
    ("FIZ", "Fizik", "2", 7),
    ("KIM", "Kimya", "2", 7),
    ("BIY", "Biyoloji", "2", 7),
    ("TAR", "Tarih", "2", 6),
    ("COG", "Cografya", "2", 5),
    ("FEL", "Felsefe", "2", 5),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 6),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("GRS", "Gorsel Sanatlar", "2", 2),
    ("MUZ", "Muzik", "2", 2),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 6),
    ("REH", "Rehberlik", "1", 0),
    # Secmeli dersler
    ("SMA", "Secmeli Matematik", "2+2+2", 9),
    ("SFI", "Secmeli Fizik", "2+2", 7),
    ("SKI", "Secmeli Kimya", "2+2", 7),
    ("SBI", "Secmeli Biyoloji", "2+2", 7),
    ("STA", "Secmeli Tarih", "2", 5),
    ("SCO", "Secmeli Cografya", "2+2", 5),
    ("STD", "Secmeli Turk Dili ve Edebiyati", "2+2+1", 8),
    ("PSI", "Psikoloji", "2", 4),
    ("SOY", "Sosyoloji", "2", 4),
    ("MAN", "Mantik", "2", 4),
    ("AST", "Astronomi ve Uzay Bilimleri", "2", 5),
    ("MTU", "Matematik Tarihi ve Uygulamalari", "2", 4),
    ("FTU", "Fen Bilimleri Tarihi ve Uygulamalari", "2", 4),
    ("DHI", "Diksiyon ve Hitabet", "2", 3),
    ("OST", "Osmanli Turkcesi", "2", 3),
    ("TDI", "Temel Dini Bilgiler (Islam)", "2", 3),
    ("PYH", "Peygamberimizin Hayati", "2", 3),
    ("IBT", "Islam Bilim Tarihi", "2", 3),
    ("TKM", "Turk Kultur ve Medeniyet Tarihi", "2", 3),
    ("CTD", "Cagdas Turk ve Dunya Tarihi", "2", 4),
    ("IKD", "Ikinci Yabanci Dil (Almanca)", "2", 3),
    ("SAT", "Sanat Tarihi", "2", 2),
])

# ============================================================
# 5. FEN LISESI (9-12. Sinif)
# ============================================================
print("5. Fen Lisesi")
create_db("fen_lisesi.sqlite", [
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("FMA", "Fen Lisesi Matematik", "2+2+2", 10),
    ("FFI", "Fen Lisesi Fizik", "2+2", 9),
    ("FKI", "Fen Lisesi Kimya", "2+2", 9),
    ("FBI", "Fen Lisesi Biyoloji", "2+2", 9),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("TAR", "Tarih", "2", 5),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 5),
    ("COG", "Cografya", "2", 4),
    ("FEL", "Felsefe", "2", 4),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 6),
    ("BTY", "Bilisim Teknolojileri ve Yazilim", "2", 5),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("GRS", "Gorsel Sanatlar", "2", 2),
    ("MUZ", "Muzik", "2", 2),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("REH", "Rehberlik", "1", 0),
    # Secmeli
    ("BAP", "Bilimsel Arastirma Projeleri", "2", 6),
    ("ROK", "Robotik ve Kodlama", "2", 6),
    ("AST", "Astronomi ve Uzay Bilimleri", "2", 5),
])

# ============================================================
# 6. SOSYAL BILIMLER LISESI (9-12. Sinif)
# ============================================================
print("6. Sosyal Bilimler Lisesi")
create_db("sosyal_bilimler_lisesi.sqlite", [
    # Anadolu Lisesi ortak dersleri
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+2", 7),
    ("FIZ", "Fizik", "2", 5),
    ("KIM", "Kimya", "2", 5),
    ("BIY", "Biyoloji", "2", 5),
    ("TAR", "Tarih", "2+2", 7),
    ("COG", "Cografya", "2+2", 6),
    ("FEL", "Felsefe", "2", 6),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 6),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("GRS", "Gorsel Sanatlar", "2", 2),
    ("MUZ", "Muzik", "2", 2),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 6),
    ("REH", "Rehberlik", "1", 0),
    # Sosyal Bilimler ozel dersleri
    ("SBC", "Sosyal Bilim Calismalari", "2", 5),
    ("OST", "Osmanli Turkcesi", "2", 4),
    ("PSI", "Psikoloji", "2", 5),
    ("SOY", "Sosyoloji", "2", 5),
    ("MAN", "Mantik", "2", 5),
    ("SAT", "Sanat Tarihi", "2", 3),
    # Secmeli
    ("STA", "Secmeli Tarih", "2", 5),
    ("SCO", "Secmeli Cografya", "2+2", 5),
    ("STD", "Secmeli Turk Dili ve Edebiyati", "2+2+1", 8),
    ("TKM", "Turk Kultur ve Medeniyet Tarihi", "2", 3),
    ("CTD", "Cagdas Turk ve Dunya Tarihi", "2", 4),
    ("DHI", "Diksiyon ve Hitabet", "2", 3),
])

# ============================================================
# 7. ANADOLU IMAM HATIP LISESI (9-12. Sinif)
# ============================================================
print("7. Anadolu Imam Hatip Lisesi")
create_db("imam_hatip_lisesi.sqlite", [
    # Ortak dersler (Anadolu Lisesi ile ayni)
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+2", 9),
    ("FIZ", "Fizik", "2", 6),
    ("KIM", "Kimya", "2", 6),
    ("BIY", "Biyoloji", "2", 6),
    ("TAR", "Tarih", "2", 5),
    ("COG", "Cografya", "2", 4),
    ("FEL", "Felsefe", "2", 4),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 5),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("GRS", "Gorsel Sanatlar", "2", 2),
    ("MUZ", "Muzik", "2", 2),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 5),
    ("REH", "Rehberlik", "1", 0),
    # Meslek dersleri
    ("KUR", "Kuran-i Kerim", "2+2+1", 8),
    ("ARA", "Arapca", "2+2", 7),
    ("MAR", "Mesleki Arapca", "2+1", 6),
    ("TDB", "Temel Dini Bilgiler", "1", 4),
    ("SIY", "Siyer", "2", 5),
    ("FIK", "Fikih", "2", 5),
    ("HAD", "Hadis", "2", 5),
    ("OST", "Osmanli Turkcesi", "1", 3),
    ("AKA", "Akaid", "1", 4),
    ("TEF", "Tefsir", "2", 5),
    ("HMU", "Hitabet ve Mesleki Uygulama", "2", 4),
    ("KEL", "Kelam", "2", 5),
    ("DNT", "Dinler Tarihi", "2", 4),
    ("IKM", "Islam Kultur ve Medeniyeti", "2", 4),
    # Secmeli
    ("SMA", "Secmeli Matematik", "2+2+2", 9),
    ("SFI", "Secmeli Fizik", "2+2", 7),
    ("SKI", "Secmeli Kimya", "2+2", 7),
    ("SBI", "Secmeli Biyoloji", "2+2", 7),
    ("PYH", "Peygamberimizin Hayati", "2", 3),
    ("IBT", "Islam Bilim Tarihi", "2", 3),
])

# ============================================================
# 8. GUZEL SANATLAR LISESI (9-12. Sinif)
# ============================================================
print("8. Guzel Sanatlar Lisesi")
create_db("guzel_sanatlar_lisesi.sqlite", [
    # Ortak dersler
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+2", 7),
    ("FIZ", "Fizik", "2", 5),
    ("KIM", "Kimya", "2", 5),
    ("BIY", "Biyoloji", "2", 5),
    ("TAR", "Tarih", "2", 5),
    ("COG", "Cografya", "2", 4),
    ("FEL", "Felsefe", "2", 4),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 5),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 5),
    ("REH", "Rehberlik", "1", 0),
    # Gorsel Sanatlar Bolumu alan dersleri
    ("DES", "Desen", "2", 6),
    ("TSE", "Temel Sanat Egitimi", "2", 6),
    ("IMR", "Imgesel Resim", "2", 5),
    ("HEY", "Heykel", "2", 4),
    ("GRT", "Grafik Tasarim", "2", 4),
    ("SAT", "Sanat Tarihi", "2", 4),
    ("DSC", "Desen Calismalari", "2", 5),
    ("DGR", "Dijital Grafik", "2", 3),
    ("FOT", "Fotograf", "2", 3),
    ("MZE", "Muze Egitimi", "2", 3),
    ("SEI", "Sanat Eserlerini Inceleme", "2", 3),
    ("2BA", "Iki Boyutlu Sanat Atolye", "2", 4),
    ("3BA", "Uc Boyutlu Sanat Atolye", "2", 4),
    # Muzik Bolumu alan dersleri
    ("PIY", "Piyano", "1", 5),
    ("CAL", "Calgi Egitimi", "1", 5),
    ("BSE", "Bireysel Ses Egitimi", "1", 5),
    ("BMT", "Bati Muzigi Teori ve Uygulamasi", "2", 6),
    ("TMT", "Turk Muzigi Teori ve Uygulamasi", "2", 5),
    ("MZT", "Muzik Tarihi", "2", 4),
    ("MIO", "Muziksel Isitme Okuma ve Yazma", "2", 6),
])

# ============================================================
# 9. SPOR LISESI (9-12. Sinif)
# ============================================================
print("9. Spor Lisesi")
create_db("spor_lisesi.sqlite", [
    # Ortak dersler
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2+2", 7),
    ("FIZ", "Fizik", "2", 5),
    ("KIM", "Kimya", "2", 5),
    ("BIY", "Biyoloji", "2", 5),
    ("TAR", "Tarih", "2", 5),
    ("COG", "Cografya", "2", 4),
    ("FEL", "Felsefe", "2", 4),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 5),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 5),
    ("REH", "Rehberlik", "1", 0),
    # Spor alan dersleri
    ("TAS", "Takim Sporlari", "2+2", 3),
    ("BIS", "Bireysel Sporlar", "2+2", 3),
    ("SPU", "Spor Uygulamalari", "1", 2),
    ("EGO", "Egitsel Oyunlar", "2", 2),
    ("GJI", "Genel Jimnastik", "2", 2),
    ("ATL", "Atletizm", "2", 3),
    ("SBE", "Spor ve Beslenme", "2", 4),
    ("RED", "Ritim Egitimi ve Halk Danslari", "2+1", 2),
    ("AJI", "Artistik Jimnastik", "2", 2),
    # Secmeli
    ("SMA", "Secmeli Matematik", "2+2+2", 9),
    ("PSI", "Psikoloji", "2", 4),
    ("SOY", "Sosyoloji", "2", 4),
])

# ============================================================
# 10. MESLEKI VE TEKNIK ANADOLU LISESI (9-12. Sinif)
# ============================================================
print("10. Mesleki ve Teknik Anadolu Lisesi")
create_db("mesleki_teknik_lise.sqlite", [
    # Ortak (kultur) dersleri - tum alanlarda ortak
    ("TDE", "Turk Dili ve Edebiyati", "2+2+1", 8),
    ("MAT", "Matematik", "2+2", 9),
    ("FIZ", "Fizik", "2", 6),
    ("KIM", "Kimya", "2", 6),
    ("BIY", "Biyoloji", "2", 5),
    ("TAR", "Tarih", "2", 5),
    ("COG", "Cografya", "2", 4),
    ("FEL", "Felsefe", "2", 4),
    ("DKA", "Din Kulturu ve Ahlak Bilgisi", "2", 3),
    ("ING", "Birinci Yabanci Dil (Ingilizce)", "2+2", 5),
    ("BES", "Beden Egitimi ve Spor", "2", 1),
    ("GRS", "Gorsel Sanatlar", "2", 2),
    ("MUZ", "Muzik", "2", 2),
    ("SBT", "Saglik Bilgisi ve Trafik Kulturu", "1", 2),
    ("ITA", "T.C. Inkilap Tarihi ve Ataturkculuk", "2", 5),
    ("REH", "Rehberlik", "1", 0),
    # Ortak meslek dersleri (9. sinif - tum alanlar)
    ("MTO", "Mesleki Teknoloji Okuryazarligi", "2", 4),
    ("IGS", "Is Guvenligi ve Sagligi", "2", 3),
    ("GIR", "Girisimcilik", "2", 3),
    ("MES", "Mesleki Gelisim", "2", 3),
])

print("\nTamamlandi! Tum veritabanlari 'dershavuzu/' klasorunde olusturuldu.")
