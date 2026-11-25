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

## Diagnostyka Client ID
Callback sprawdza teraz, czy ClientId i ClientSecret zostały wczytane. W konsoli wypisuje obecność i długość ClientId oraz Content-Type żądania, bez wypisywania sekretu, kodu OAuth ani tokenów.
Jeśli nadal pojawi się `missing client id`, wykonaj `dotnet clean`, usuń foldery `bin` i `obj`, a następnie `dotnet run`. Sprawdź też, czy uruchamiasz projekt z tego archiwum, a nie starą kopię.

## Dodatkowe poprawki po logach
- Dodano `@model` do `Pages/Login.cshtml` i `Pages/Admin.cshtml`. Dzięki temu PageModel logowania i `[Authorize]` panelu są faktycznie podłączone.
- Callback obsługuje odmowę zgody Twitcha.
- Wymiana tokenu ma `client_id` zarówno w wymaganym body `application/x-www-form-urlencoded`, jak i dodatkowo w query string. Loguje obecność i długość Client ID oraz typ/rozmiar body, bez ujawniania sekretu i kodu.
- Niezalogowane żądania `/api/*` zwracają 401 zamiast przekierowania fetch do strony logowania.

## Poprawki panelu
- Funkcje JS odczytują treść odpowiedzi HTTP tylko raz, więc nie występuje `Response body stream already read`.
- Obsłużone są zarówno odpowiedzi JSON, jak i tekstowe/ProblemDetails; błędy HTTP pokazują status i szczegóły.
- Panel pokazuje czytelny status Twitch: połączono / nie połączono / błąd sprawdzania, nazwę konta i czas wygaśnięcia tokenu.
- Status Twitch odświeża się co 10 sekund bez przeładowywania całego panelu.
