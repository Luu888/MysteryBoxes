# Poprawka Twitch OAuth – port 57286

W tej wersji aplikacja nasłuchuje wyłącznie na `http://localhost:57286`.
Nie używa HTTPS na tym porcie, aby adres callbacku odpowiadał konfiguracji lokalnej.

W Twitch Developer Console ustaw OAuth Redirect URL dokładnie:
`http://localhost:57286/twitch/callback`

W `appsettings.json` uzupełnij:
- `Twitch:ClientId`
- `Twitch:ClientSecret` (wygeneruj nowy sekret)
- `Admin:Password`

Wymiana kodu OAuth została zmieniona na `application/x-www-form-urlencoded`, wymagane przez endpoint tokenów Twitch.

Uruchom:
`dotnet run`

Panel:
`http://localhost:57286/Admin`

UWAGA: W archiwum nie ma bazy danych ani katalogu `.git`. Sekrety zostały zastąpione placeholderami. Nie przywracaj starego ClientSecret, ponieważ został wcześniej ujawniony w rozmowie.
