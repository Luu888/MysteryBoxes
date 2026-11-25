# Mystery Boxes – pełniejsze MVP (.NET 10, bez Dockera)

## Uruchomienie
1. Zainstaluj .NET 10 SDK.
2. W `appsettings.json` ustaw silne `Admin:Password`.
3. Skonfiguruj Twitch Developer Application:
   - OAuth Redirect URL musi być identyczny z `Twitch:RedirectUri`.
   - Ustaw `Twitch:ClientId` i `Twitch:ClientSecret`.
   - Scope autoryzacji: `user:read:chat user:write:chat`.
4. `dotnet restore`
5. `dotnet run --urls http://localhost:57286`
6. Panel: `/Admin` (przekieruje do `/Admin/Login`).
7. Widżet OBS: `/`.

## Twitch
- Połącz konto nadawcy przyciskiem w panelu.
- Aplikacja używa EventSub WebSocket i subskrypcji `channel.chat.message`.
- `!ticket` zapisuje widza do aktualnej rundy.
- Streamer otwiera zapisy, zamyka zapisy, losuje zwycięzcę.
- Zwycięzca wybiera paczkę: `!paczka 7`.
- Odpowiedzi są wysyłane przez Helix Send Chat Message API.
- Jeśli używasz osobnego konta bota, ustaw jego ID w `Twitch:BotUserId` i nadaj mu odpowiednie uprawnienia/scope. Domyślnie nadawca jest też botem.

## Ważne przed publicznym uruchomieniem
- Ustaw publiczny HTTPS i poprawny `RedirectUri`, np. `https://mystery.twojadomena.pl/twitch/callback`.
- W Twitch Developer Console dodaj dokładny redirect URL.
- Dla produkcji skonfiguruj proces jako usługę systemd i kopie zapasowe `mystery-boxes.db`.
- Pierwsza wersja nie implementuje automatycznego odnowienia subskrypcji EventSub po każdym rodzaju awarii/reconnect; monitoruj logi aplikacji. Token OAuth jest odświeżany automatycznie przy użyciu refresh token.
- Panel administratora ma logowanie cookie; pamiętaj o HTTPS na VPS.
- Upload ograniczony do 5 MB i formatów PNG/JPG/WEBP/GIF.

## Mechanika
- Uczestnictwo resetuje się w każdej nowej rundzie; ten sam widz może brać udział ponownie.
- Odkryte paczki pozostają odkryte w ramach wydarzenia.
- Przetasowanie możliwe wyłącznie, gdy wszystkie paczki są zakryte i nie ma aktywnej rundy.
- Nowe wydarzenie resetuje stan odkrycia paczek; następnie można przetasować pulę.

## Komendy i ograniczenia MVP
- Widz: `!ticket`
- Wylosowany zwycięzca: `!paczka 1` ... `!paczka 24`
- Zgłoszenia można otwierać i zamykać z panelu; każde losowanie ma osobną listę.
- W danej rundzie użytkownik identyfikowany jest po Twitch User ID, a nie nazwie.
- Chat bot wysyła wiadomości jako konto, które autoryzowało Twitch (konto nadawcy).

Przed uruchomieniem na żywo wykonaj test na prywatnym kanale. EventSub reconnect jest w tej wersji realizowany przez ponowne zestawienie sesji i ponowną subskrypcję, co może spowodować krótką przerwę.

## SubLuck (mnożnik szansy subskrybenta)
W panelu administratora ustawiasz `SubLuck` od 1 do 100:
- `1` = zwykła szansa (bez bonusu),
- `2` = sub otrzymuje wagę 2 losów,
- `3` = sub otrzymuje wagę 3 losów itd.
Status subskrybenta jest odczytywany z badge'ów wiadomości Twitch EventSub (`subscriber` / `founder`). Użytkownik nadal ma tylko jedno zgłoszenie w rundzie; mnożnik wpływa wyłącznie na wagę przy losowaniu.

## Animacja odkrycia nagrody
Po odkryciu paczki widżet wyświetla pełnoekranową animację: potrząsanie skrzynką, błysk, wejście karty nagrody i delikatne unoszenie grafiki. W panelu administratora w sekcji „Animacja i dźwięk nagrody” można wgrać własny plik MP3, WAV lub OGG (do 10 MB), odsłuchać go, albo przywrócić domyślny dźwięk. Odtwarzanie odbywa się w źródle przeglądarki OBS. Jeśli domyślny plik dźwiękowy nie istnieje, aplikacja odtwarza krótki sygnał generowany przez przeglądarkę.

## Panel administratora i adres OBS
Strona główna (`/`) przekierowuje do panelu (`/Admin`); niezalogowany użytkownik trafia do logowania. Overlay jest dostępny pod `/Overlay`. Panel zawiera przyciski otwierania overlay w nowej karcie i kopiowania pełnego adresu do OBS. Przekazana grafika jest subtelnym, półprzezroczystym znakiem wodnym wyłącznie na zakrytych polach; numer pozostaje na wierzchu.


## Logowanie administratora
Panel `/Admin` jest chroniony logowaniem hasłem z `Admin:Password` w `appsettings.json`.
Przy pierwszym wejściu aplikacja przekieruje na `/Login`. Po zalogowaniu przeglądarka
otrzyma cookie `MysteryBoxes.Admin`, a panel i endpointy `/api/admin/*` będą dostępne.
Po zalogowaniu połącz konto Twitch przyciskiem w panelu.
