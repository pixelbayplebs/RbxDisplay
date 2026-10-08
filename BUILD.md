# Build RbxDisplay 2.0.1

To paczka źródłowa: zawiera kod, loga, ikony, testy oraz skrypty. Gotowa aplikacja powstanie po wykonaniu builda.

## Wymagania

- Windows 10 w wersji 2004 lub nowszej albo Windows 11, x64.
- **[.NET 10 SDK, x64](https://dotnet.microsoft.com/download/dotnet/10.0)** — zainstaluj SDK, nie sam Runtime.
- Windows PowerShell, dostępny standardowo w Windowsie.
- Dostęp do NuGet podczas przywracania zależności. Projekt wskazuje `Microsoft.WindowsAppSDK` w wersji `2.5.1`.

Visual Studio jest opcjonalne; poniższa instrukcja używa CLI `dotnet`. Pełny build wykonuj na Windowsie, ponieważ generowanie zasobów WinUI korzysta z natywnych narzędzi Windows SDK.

## Najprostszy build

1. Wypakuj archiwum. W katalogu źródeł powinien znajdować się plik `Build.cmd` oraz katalogi `src` i `tests`.
2. Po instalacji SDK otwórz nowy terminal PowerShell.
3. Przejdź do tego katalogu i uruchom:

```powershell
cd ścieżka\do\katalogu\źródeł
dotnet --version
.\Build.cmd
```

W folderze projektu `dotnet --version` powinno pokazać stabilną wersję `10.0.x`. Plik `global.json` dopuszcza nowsze stabilne SDK z linii 10.0.

`Build.cmd` uruchamia `Build.ps1`. Skrypt wykonuje testy xUnit, publikuje aplikację WinUI, watchdog i program testowy, sprawdza obecność zasobów i składa kompletną paczkę w katalogu `build`. Jeśli któryś krok zakończy się błędem, build zostaje przerwany.

Po komunikacie `Build complete` uruchom:

```powershell
.\build\RbxDisplay.exe
```

Kompletna paczka wynikowa znajduje się w **`build\`**, razem z watchdogiem i programem testowym. Aby przenieść aplikację na drugi komputer, skopiuj całą zawartość tego katalogu, wraz z DLL-ami, folderem `Assets` i plikami zasobów.

Build na Windowsie tworzy `resources.pri` od razu; nie wymaga przygotowania indeksu przy pierwszym uruchomieniu. Jest to aplikacja folderowa — natywne zależności WinUI muszą pozostać obok EXE. Instalator Windows App SDK nie jest potrzebny. Komputer, który uruchamia skopiowany katalog, potrzebuje zainstalowanego x64 .NET Runtime 10, czyli frameworka `Microsoft.NETCore.App`. .NET Desktop Runtime też zawiera ten framework. [Dokumentacja Microsoft](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).

## Same testy

```powershell
dotnet test .\tests\RbxDisplay.Core.Tests\RbxDisplay.Core.Tests.csproj -c Release
```

`dotnet test` uruchamia te same sprawdzenia logiki co wcześniej: tryb ekranu, wykrywanie gry, odzyskiwanie, profile, upgrade schematu ustawień i blokadę przywracania. Testy nie zmieniają rozdzielczości ani kolorów. Nie zastępują testu okna i sterownika na Windowsie. Gotowy program testowy w katalogu `build` uruchamia `SelfTest.cmd`.

## Praca nad kodem

Interfejs znajduje się w `src\RbxDisplay.App\MainWindow.cs`, kolory w `Theme.cs`, a ustawienia gier w `src\RbxDisplay.Core\GameProfile.cs` i `AppSettings.cs`. Po zmianach uruchom ponownie `Build.cmd`.

Wersję aplikacji definiuje `Directory.Build.props`. Parametry publikacji, architekturę `win-x64` i zależność WinUI definiują pliki `.csproj`.

## Gdy build zgłasza błąd

- **Brak SDK lub niewłaściwa wersja:** wykonaj `dotnet --list-sdks`, zainstaluj .NET 10 SDK x64 i otwórz terminal ponownie.
- **Błąd przywracania NuGet:** sprawdź połączenie oraz komunikat konkretnego pakietu; następnie uruchom build ponownie.
- **Błąd narzędzi zasobów / brak `resources.pri`:** wykonuj build na Windowsie i zachowaj pełny komunikat. Nie pomijaj tego etapu — skrypt wymaga poprawnego indeksu zasobów przed skopiowaniem wyników.

Źródła tej wersji kompilują się z analizatorami jako błędami, a testy logiki xUnit przechodzą. Pełny natywny build i działanie GUI na Windowsie pozostają do weryfikacji; szczegóły są w `Verification.md`.
