# SpotiFlow – behawioralny asystent odtwarzania Spotify

Projekt zaliczeniowy z przedmiotu **Programowanie w Środowisku Windows** (2026/2027).
Technologia: **C# / .NET 10 / WinUI 3 (Windows App SDK) + MVVM (CommunityToolkit.Mvvm)**.
Specyfikacja wymagań: [`Opis Wstępny/Specyfikacja Wymagań Systemowych - Etap I.pdf`](Opis%20Wstępny/)

---

## Spis treści

1. [Co robi aplikacja](#1-co-robi-aplikacja)
2. [Cała logika w 6 krokach](#2-cała-logika-w-6-krokach)
3. [Wymagania](#3-wymagania)
4. [Konfiguracja konta Spotify Developer (jednorazowo)](#4-konfiguracja-konta-spotify-developer-jednorazowo)
5. [W jakim IDE uruchamiać](#5-w-jakim-ide-uruchamiać)
6. [Obsługa programu](#6-obsługa-programu)
7. [Struktura projektu](#7-struktura-projektu)
8. [Najważniejsze pliki](#8-najważniejsze-pliki)
9. [Wymagania ze specyfikacji → gdzie są w kodzie](#9-wymagania-ze-specyfikacji--gdzie-są-w-kodzie)
10. [Odstępstwa od specyfikacji (i dlaczego)](#10-odstępstwa-od-specyfikacji-i-dlaczego)
11. [Testy](#11-testy)
12. [Rozwiązywanie problemów](#12-rozwiązywanie-problemów)
13. [Słowniczek](#13-słowniczek)

---

## 1. Co robi aplikacja

SpotiFlow obserwuje Twój odtwarzacz Spotify i po każdej piosence dodaje do kolejki następną –
dobraną po tym, **jak długo słuchałeś** poprzedniej:

| Twoje zachowanie | Co z tego wnioskuje SpotiFlow | Co dodaje do kolejki |
|---|---|---|
| Przełączyłeś przed ćwiartką utworu (**W < 0,25**) | „Nie podoba mi się” | utwór o **przeciwnych** cechach: **A_nowe = 1 − A** |
| Słuchałeś co najmniej ćwiartki (**W ≥ 0,25**) | „Podoba mi się” | utwór **podobny**: najbliższy profilowi A |
| Kliknąłeś **Start** | – | od razu utwór podobny do tego, który gra |

- **W** (współczynnik odsłuchu) = ile słuchałeś / ile trwa utwór.
- **A** (profil) = 4 atrybuty utworu w skali 0–1: **taneczność, energia, nastrój, akustyczność**.

Aplikacja **nie odtwarza muzyki** – jest „pilotem” do Twojego Spotify (na komputerze, telefonie, głośniku).

---

## 2. Cała logika w 6 krokach

Co 1,5 s pętla (`PlaybackMonitor`) wykonuje jeden cykl:

```
1. GET /me/player                → co gra?  (ID utworu, postęp t, długość T)
2. ten sam utwór co poprzednio?  → zapamiętaj NAJWIĘKSZY postęp i koniec cyklu
3. INNY utwór                    → poprzedni się skończył:
4.    W = t / T                  → np. 10 s / 200 s = 0,05
5.    W < 0,25 ?                 → TAK: pominięty  → cel = 1 − A
                                   NIE: wysłuchany → cel = A (szukamy najbliższego)
6.    ReccoBeats: kandydaci blisko celu → odrzuć grane w ostatniej godzinie
      → wybierz NAJBLIŻSZY celowi → POST /me/player/queue
```

**Przykład liczbowy** (pominięcie):

| Krok | Wartość |
|---|---|
| Utwór A: t = 10 000 ms, T = 200 000 ms | W = 10 000 / 200 000 = **0,05** |
| 0,05 < 0,25 | **pominięty** |
| A = (taneczność 0,80 · energia 0,90 · nastrój 0,70 · akustyczność 0,10) | |
| cel = 1 − A | (0,20 · 0,10 · 0,30 · 0,90) – spokojny, akustyczny |
| ReccoBeats proponuje C (0,30 · 0,20 · 0,30 · 0,80) i D (0,75 · 0,85 · 0,65 · 0,15) | |
| odległość C od celu = 0,1 + 0,1 + 0 + 0,1 = **0,3**; D = **2,4** | wygrywa **C** |
| `POST /me/player/queue?uri=spotify:track:C` | C trafia do kolejki |

Dokładny przebieg linijka po linijce (z wynikami każdej linijki) jest w prezentacji „SpotiFlow – logika krok po kroku”.

---

## 3. Wymagania

| Co | Po co |
|---|---|
| **Windows 10 (1809+) lub 11** | WinUI 3 działa tylko na Windows; efekt Mica widać na Windows 11 |
| **Visual Studio 2026** z pakietami *.NET desktop development* i *Programowanie aplikacji systemu Windows* | kompilacja WinUI 3 |
| **Konto Spotify Premium** | Spotify blokuje sterowanie kolejką dla kont darmowych (RB-01) |
| **Aplikacja w Spotify Developer Dashboard** | daje *Client ID* potrzebny do logowania |
| **Internet** | Spotify API, ReccoBeats API (atrybuty i rekomendacje), okładki |

---

## 4. Konfiguracja konta Spotify Developer (jednorazowo)

1. Wejdź na **https://developer.spotify.com/dashboard** i zaloguj się kontem Spotify.
2. Kliknij **Create app**:
   - **App name / description:** dowolne,
   - **Redirect URIs:** **dokładnie** `http://127.0.0.1:5543/callback` → *Add*
     (adres jest też w oknie programu z przyciskiem **Kopiuj**; `localhost` NIE zadziała),
   - zaznacz **Web API**, zaakceptuj regulamin → **Save**.
3. W **Settings** skopiuj **Client ID** (*client secret nie jest potrzebny* – używamy PKCE).
4. **User Management**: dodaj e-mail konta, którym będziesz się logować – bez tego błąd 403.

---

## 5. W jakim IDE uruchamiać

**Visual Studio 2026 Community** (darmowe):
1. W instalatorze zaznacz pakiety *.NET desktop development* i *Programowanie aplikacji systemu Windows*.
2. Otwórz **`SpotiFlow.sln`**; projektem startowym musi być **SpotiFlow** (pogrubiony).
3. **F5** – uruchom. **Test → Run All Tests** – testy.

Alternatywy: JetBrains Rider albo VS Code + C# Dev Kit:

```bash
dotnet run --project SpotiFlow
```

---

## 6. Obsługa programu

1. Włącz Spotify na dowolnym urządzeniu i puść cokolwiek.
2. Uruchom SpotiFlow, wklej **Client ID** → **Zaloguj się przez Spotify** (masz 15 s na dokończenie w przeglądarce).
3. **Start**. Słuchaj normalnie – w **Historii** pojawiają się bloki utworów (najnowszy na górze).

Jeden utwór = jeden blok, który zmienia tylko plakietkę:

```
DODANO DO KOLEJKI  →  TERAZ GRA  →  POMINIĘTY (W = 0,05)  /  WYSŁUCHANY (W = 0,95)
```

- Nie pasuje propozycja? Po prostu ją **pomiń** w Spotify – to zwykłe pominięcie (W < 0,25),
  więc SpotiFlow sam doda jej **przeciwieństwo**.
- **Teraz gra**: okładka, pasek z bieżącym **W** i 4 paski atrybutów.
- Szare wiersze to komunikaty (np. czego szukam, co wybrałem), czerwone – błędy.
- **Stop** zatrzymuje pętlę. Client ID zapisuje się w `%AppData%\SpotiFlow\settings.json`
  (token logowania **nie** jest zapisywany – ze względów bezpieczeństwa).

---

## 7. Struktura projektu

```
PwSW_Projekt/
├── SpotiFlow.sln                    ← to otwierasz w Visual Studio
├── SpotiFlow/                       ← APLIKACJA (WinUI 3) – tylko interfejs
│   ├── App.xaml / App.xaml.cs       ← start aplikacji, ciemny motyw, zielony akcent, styl karty
│   ├── MainWindow.xaml              ← WYGLĄD okna
│   ├── MainWindow.xaml.cs           ← Mica, pasek tytułu, rozmiar okna
│   └── ViewModels/
│       ├── MainViewModel.cs         ← logowanie, Start/Stop, reakcje na zdarzenia pętli
│       └── HistoryItem.cs           ← jeden blok historii + AttributeView
│
├── SpotiFlow.Core/                  ← LOGIKA (bez interfejsu – da się testować)
│   ├── AppSettings.cs               ← zapis Client ID
│   ├── Spotify/
│   │   ├── SpotifyAuthService.cs    ← logowanie OAuth 2.0 (PKCE)
│   │   ├── SpotifyApiClient.cs      ← stan odtwarzacza, kolejka, przeskok
│   │   ├── SpotifyCoverService.cs   ← okładki (publiczny oEmbed Spotify)
│   │   ├── SpotifyModels.cs         ← klasy odpowiadające JSON-owi
│   │   └── SpotifyApiException.cs   ← błąd z kodem HTTP
│   ├── ReccoBeats/
│   │   └── ReccoBeatsClient.cs      ← atrybuty (A) + kandydaci
│   └── Logic/                       ← „MÓZG” – to warto znać na pamięć
│       ├── PlaybackMonitor.cs       ← pętla co 1,5 s: wykrycie zmiany utworu
│       ├── ListeningAnalyzer.cs     ← W = t / T  i  próg 0,25
│       ├── AudioProfile.cs          ← A, 1 − A, odległość
│       ├── RecommendationEngine.cs  ← dobór następnego utworu (5 kroków)
│       └── PlayHistory.cs           ← pamięć 1 h (RB-03)
│
└── SpotiFlow.Tests/                 ← TESTY (xUnit, 8 testów)
```

---

## 8. Najważniejsze pliki

### `Logic/PlaybackMonitor.cs` – pętla

```csharp
while (!ct.IsCancellationRequested)
{
    await PollOnceAsync(ct);          // jeden cykl
    await Task.Delay(Interval, ct);   // 1,5 s przerwy (RB-02)
}
```

W `PollOnceAsync` najważniejsze są dwie gałęzie:

```csharp
if (track.Id == _currentTrack.Id)                 // ten sam utwór
{
    _maxProgressMs = Math.Max(_maxProgressMs, progress);   // największy postęp
    return;
}

// ZMIANA UTWORU -> oceniamy ten, który się skończył
SpotifyTrack finished = _currentTrack;
double w = ListeningAnalyzer.CalculateListenRatio(_maxProgressMs, finished.DurationMs);
ListeningDecision decision = ListeningAnalyzer.Decide(w);
...
await QueueNextAsync(finished.Id, finished.Name, decision, ct);
```

`QueueNextAsync` prosi silnik o utwór, sprawdza, czy w kolejce nie czeka już nasz poprzedni
(`GET /me/player/queue` – najwyżej jedna propozycja naraz), i wysyła `POST /me/player/queue`.

### `Logic/ListeningAnalyzer.cs` – decyzja

```csharp
public const double SkipThreshold = 0.25;

public static double CalculateListenRatio(int listenedMs, int durationMs) =>
    durationMs <= 0 ? 0 : Math.Clamp((double)listenedMs / durationMs, 0, 1);   // W = t / T

public static ListeningDecision Decide(double w) =>
    w < SkipThreshold ? ListeningDecision.Skipped : ListeningDecision.Engaged;
```

### `Logic/AudioProfile.cs` – wzory

```csharp
public AudioProfile Inverted() => new(1 - Danceability, 1 - Energy, 1 - Valence, 1 - Acousticness);  // 1 − A
public double DistanceTo(AudioProfile other) => ...            // suma różnic |a − b|
```

### `Logic/RecommendationEngine.cs` – dobór (5 kroków)

```csharp
AudioProfile? old = await GetProfileAsync(sourceId, ct);                         // 1. A_stare
AudioProfile target = decision == Skipped ? old.Inverted() : old;                // 2. A_nowe
var candidates = await _recco.GetRecommendationsAsync(sourceId, target, 20, ct); // 3. kandydaci
candidates = candidates.Where(c => !_history.WasRecentlyPlayed(c.SpotifyId));    // 4. RB-03
RecommendedTrack best = ChooseClosest(candidates, _profiles, target);            // 5. najbliższy
```

Gdy ReccoBeats nie zna utworu (brak A), silnik pisze komunikat „brak propozycji” i nic nie dodaje –
pętla działa dalej.

### `ReccoBeats/ReccoBeatsClient.cs`

- `GET audio-features?ids=...` → atrybuty (A) utworów,
- `GET track/recommendation?seeds=ID&danceability=0.200&energy=0.100&...` → kandydaci blisko celu.

Liczby w adresie są formatowane z **kropką** (`InvariantCulture`) – polski Windows wstawiłby przecinek.

### `Spotify/SpotifyAuthService.cs` – logowanie (OAuth 2.0 + PKCE)

Program uruchamia mini-serwer na `127.0.0.1:5543`, otwiera przeglądarkę ze stroną logowania
Spotify, odbiera `code` z przekierowania i wymienia go na **access token** (1 h) i **refresh token**.
PKCE: do Spotify idzie tylko skrót SHA-256 losowego hasła, a samo hasło dopiero przy wymianie
kodu – przechwycony kod jest bezużyteczny. Po 15 s bez odpowiedzi logowanie się przerywa.

### `Spotify/SpotifyApiClient.cs`

Każde żądanie ma nagłówek `Authorization: Bearer <token>`. Przy **401** token jest raz odświeżany,
przy **5xx** zapytanie jest raz ponawiane po 1 s, a każdy inny błąd zamienia się w
`SpotifyApiException` z kodem HTTP (przy 429 – z nagłówkiem `Retry-After`).

### Interfejs (MVVM)

- **View** – `MainWindow.xaml`: kontrolki i wiązania `{x:Bind ViewModel...}`.
- **ViewModel** – `MainViewModel.cs`: właściwości `[ObservableProperty]` i komendy `[RelayCommand]`.
  Nasłuchuje zdarzeń pętli (`NowPlaying`, `TrackJudged`, `TrackQueued`…) i zmienia plakietki w historii.
- **Model** – `SpotiFlow.Core`: logika, która nie wie nic o oknie.

---

## 9. Wymagania ze specyfikacji → gdzie są w kodzie

| Wymaganie (specyfikacja) | Realizacja |
|---|---|
| 1.3.1 Autoryzacja OAuth 2.0 | `SpotifyAuthService` (PKCE) |
| 1.3.2 Asynchroniczna pętla odpytująca *Get Playback State* | `PlaybackMonitor.RunAsync` + `SpotifyApiClient.GetPlaybackStateAsync` |
| 1.3.3 Algorytm decyzyjny | `ListeningAnalyzer` |
| 1.3.4 Rekomendacje odwrócone / podobne + *Add to Queue* | `RecommendationEngine` + `SpotifyApiClient.AddToQueueAsync` |
| 2.1 Zapisywanie maksymalnego postępu | `PlaybackMonitor` (`_maxProgressMs = Math.Max(...)`) |
| 2.1 Detekcja zmiany ID | `PlaybackMonitor.PollOnceAsync` |
| 2.3 W = t_akt / t_całk | `ListeningAnalyzer.CalculateListenRatio` |
| 2.3 pominięcie → A_nowe = 1 − A_stare *(próg 0,25)* | `AudioProfile.Inverted` |
| 2.3 akceptacja → A_nowe = A_stare ± Δ *(W ≥ 0,25)* | cel = A i wybór najbliższego: `AudioProfile.DistanceTo`, `RecommendationEngine.ChooseClosest` |
| RB-01 Wymóg Premium | obsługa 403 w `PlaybackMonitor.RunAsync` |
| RB-02 Sztywny interwał | `PlaybackMonitor.Interval` = 1,5 s (stała) |
| RB-03 Brak duplikatów z ostatniej godziny | `PlayHistory` |
| 2.5 Brak Premium (403) | pętla zatrzymana + czerwony `InfoBar` |
| 2.5 Limit zapytań (429) | odczyt `Retry-After`, pauza pętli |
| 2.5 Wygaśnięcie tokenu (401) | automatyczne odświeżenie; gdy się nie uda – prośba o logowanie |
| 2.5 Podcast / reklama | `currently_playing_type != "track"` → czekanie na muzykę |
| 2.5 Brak aktywnego urządzenia | odpowiedź 204 → komunikat „uruchom Spotify” |

---

## 10. Odstępstwa od specyfikacji (i dlaczego)

### a) Implicit Grant Flow → Authorization Code with PKCE
Spotify wyłączył *Implicit Grant*. PKCE to oficjalny następca dla aplikacji bez serwera:
nie wymaga *client secret*, token nie pojawia się w adresie URL, jest *refresh token*.

### b) Spotify *Audio Features* i *Recommendations* → ReccoBeats API
Od 27.11.2024 te endpointy Spotify zwracają **403** dla nowych aplikacji. **ReccoBeats**
(https://reccobeats.com) to darmowe API bez klucza: przyjmuje ID utworów ze Spotify, zwraca te
same atrybuty 0–1 i rekomendacje z wartościami docelowymi. Sterowanie odtwarzaczem (stan,
kolejka) nadal idzie wyłącznie przez Spotify.

### c) Timer → `async/await` + `Task.Delay`
Pętla nie blokuje okna, łatwo ją przerwać (`CancellationToken`) i uśpić na `Retry-After`.

### d) Interwał 5 s (przykład) → stałe 1,5 s
≈ 20 zapytań na 30 s – bezpiecznie poniżej limitu, a pominięcia są wykrywane szybciej.

### e) Jeden próg 0,25 zamiast 0,15 / 0,80
Specyfikacja nie mówiła, co robić przy 0,15 ≤ W ≤ 0,80 – w tej strefie i tak trzeba było coś
dodać, a sensownie było dodać podobny utwór, czyli to samo co przy akceptacji. Dlatego zostaje
jeden próg: **W < 0,25 → 1 − A**, **W ≥ 0,25 → najbliższy A**.

### e2) A ± Δ → najbliższy kandydat
Zamiast sztywnego progu Δ (np. 0,15 na każdy atrybut) wybieramy kandydata o **najmniejszej odległości**
od A. Próg i tak niczego nie zmieniał – gdy nikt się w nim nie mieścił, trzeba było wziąć najbliższego,
a gdy ktoś się mieścił, najbliższy też był w progu. Najmniejsza odległość = najmniejsze możliwe Δ.

### f) Dodatki
- **Start** od razu dodaje utwór podobny do aktualnego.
- **Najwyżej jedna propozycja w kolejce** (sprawdzane przez *Get the User's Queue*).
- **Okładki** z publicznego oEmbed Spotify.

---

## 11. Testy

Projekt `SpotiFlow.Tests` (xUnit, **8 testów**):

```bash
dotnet test SpotiFlow.Tests
```

- **`LogicTests`** (5) – W = t / T, próg 0,25, 1 − A, odległość, historia 1 h (sztuczny zegar).
- **`PlaybackMonitorTests`** (3) – cała pętla na **udawanym internecie** (`FakeHttpHandler` zwraca
  przygotowany JSON zamiast łączyć się ze Spotify i ReccoBeats):
  szybkie pominięcie → przeciwieństwo, wysłuchanie → podobny (z pominięciem utworów z ostatniej godziny),
  błąd 403 → pętla się zatrzymuje.

Dzięki temu logikę da się sprawdzić bez konta Premium i bez internetu.

---

## 12. Rozwiązywanie problemów

| Problem | Rozwiązanie |
|---|---|
| `INVALID_CLIENT: Invalid redirect URI` | W panelu Spotify Redirect URI musi być **dokładnie** `http://127.0.0.1:5543/callback` |
| `INVALID_CLIENT: Invalid client` | Źle skopiowany Client ID |
| Błąd 403 po Start | Brak Premium **albo** konto nie jest dodane w *User Management* |
| „Brak aktywnego urządzenia” | Uruchom Spotify i puść cokolwiek |
| Timeout logowania | Dokończ logowanie w przeglądarce w ciągu 15 s (albo zwiększ `LoginTimeout`) |
| Nic nie trafia do kolejki | Sprawdź komunikaty w Historii – np. „ReccoBeats nie zna atrybutów…” |
| Kreski zamiast atrybutów | ReccoBeats nie zna tego utworu – to normalne dla mniej popularnych nagrań |
| Visual Studio: błąd budowania WinUI | Doinstaluj pakiet *Programowanie aplikacji systemu Windows* |

---

## 13. Słowniczek

- **W** – współczynnik odsłuchu: t / T (0–1).
- **A** – profil utworu: taneczność, energia, nastrój, akustyczność (0–1).
- **A ± Δ** – wzór ze specyfikacji; u nas: kandydat najbliższy A (bez sztywnego progu).
- **Ziarno (seed)** – utwór, od którego ReccoBeats zaczyna szukać kandydatów.
- **Odległość** – suma różnic czterech atrybutów; im mniejsza, tym bardziej podobne.
- **Polling** – cykliczne odpytywanie („czy coś się zmieniło?”).
- **OAuth 2.0 / PKCE** – logowanie bez podawania hasła aplikacji / zabezpieczenie dla aplikacji bez sekretu.
- **async/await** – czekanie (na internet, timer) bez blokowania okna.
- **CancellationToken** – sygnał „przerwij” (przycisk Stop).
- **Event (zdarzenie)** – sposób, w jaki pętla powiadamia okno („zmienił się utwór”).
- **MVVM** – Model (logika) – View (XAML) – ViewModel (dane i komendy dla okna).
- **HTTP 401 / 403 / 429 / 5xx** – wygasła sesja / brak uprawnień / za dużo zapytań / awaria serwera.
