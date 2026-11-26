# Mystery Boxes (.NET 10, bez Dockera)

## Uruchomienie
1. Zainstaluj .NET 10 SDK.
2. W `appsettings.json` ustaw silne `Admin:Password`.
3. `dotnet run`
4. Panel streamera: `http://localhost:57286/Admin`
5. Nakładka OBS (źródło „Przeglądarka”): `http://localhost:57286/Overlay`

## Jak to działa
- Widz wybiera numer paczki, a streamer klika ten kafelek w panelu i potwierdza.
- Nakładka pokazuje animację otwarcia z dźwiękiem tylko raz. Po odświeżeniu strony otwarte paczki są po prostu widoczne jako otwarte.
- Otwarta paczka zostaje otwarta do czasu użycia „Zakryj wszystkie paczki”.
- Przetasowanie jest możliwe tylko, gdy wszystkie paczki są zakryte.

## Panel administratora
- `/` przekierowuje do `/Admin`; niezalogowany użytkownik trafia na `/Login`.
- Można zmienić tytuł, kolory, liczbę kolumn, nazwy i obrazy 24 nagród oraz dźwięk animacji.
- Obrazy: PNG/JPG/WEBP/GIF do 5 MB. Dźwięk: MP3/WAV/OGG do 10 MB.
- Jeśli plik dźwiękowy nie istnieje, nakładka odtwarza krótki sygnał generowany przez przeglądarkę.
