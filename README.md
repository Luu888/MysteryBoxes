# Mystery Boxes (.NET 10, bez Dockera)

## Uruchomienie
1. Zainstaluj .NET 10 SDK.
2. `dotnet run`
3. Panel streamera: `http://localhost:57286/Admin`
4. Nakładka OBS (źródło „Przeglądarka”): `http://localhost:57286/Overlay`

## Nakładka w OBS
- Źródło „Przeglądarka”: 1920×1080, albo 2560×1440 / 3840×2160 dla ostrzejszego obrazu (układ skaluje się sam).
- Układ: paczki 1–12 po lewej, 13–24 po prawej, na środku tytuł, okno na kamerkę 16:9 i sponsorzy.
- Okno kamerki jest przezroczyste. Źródło kamery umieść na liście źródeł **pod** nakładką: pozycja X=600, Y=170, rozmiar 720×405 (dla płótna 1920×1080; dla większego płótna przeskaluj proporcjonalnie).

## Jak to działa
- Widz wybiera numer paczki, a streamer klika ten kafelek w panelu i potwierdza.
- Nakładka pokazuje animację otwarcia z dźwiękiem tylko raz. Po odświeżeniu strony otwarte paczki są po prostu widoczne jako otwarte.
- Otwarta paczka zostaje otwarta do czasu użycia „Zakryj wszystkie paczki”.
- Przetasowanie jest możliwe tylko, gdy wszystkie paczki są zakryte.

## Panel administratora
- Aplikacja jest lokalna i nie wymaga logowania; nasłuchuje tylko na `localhost`.
- `/` przekierowuje do `/Admin`.
- Można zmienić tytuł, kolory, liczbę kolumn, nazwy i obrazy 24 nagród, sponsorów z tekstem pod kamerką oraz dźwięk animacji.
- Obrazy: PNG/JPG/WEBP/GIF do 5 MB. Dźwięk: MP3/WAV/OGG do 10 MB.
- Jeśli plik dźwiękowy nie istnieje, nakładka odtwarza krótki sygnał generowany przez przeglądarkę.
