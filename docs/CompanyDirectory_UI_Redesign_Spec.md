# CompanyDirectory — Specyfikacja Redesignu UI/UX

## Kontekst projektu
Aplikacja desktopowa WinUI 3 (Windows App SDK), .NET, C#, MVVM (CommunityToolkit.Mvvm).
Obecny stan: SearchWindow.xaml, SettingsWindow.xaml, ChatWindow.xaml, SmsWindow.xaml, AdminMessageWindow.xaml, InboxWindow.xaml, SentMessagesWindow.xaml — osobne okna.

## CEL REDESIGNU
Zastąpić wielookienkową architekturę **jednym oknem z boczną nawigacją (NavigationView)** i widokami przełączanymi w content area. Dodać tryb Light/Dark. Zachować 100% istniejącej logiki biznesowej (ViewModels, Services).

---

## 1. ARCHITEKTURA OKNA

### 1.1 Główne okno — `MainWindow.xaml`
Zastępuje dotychczasowe SearchWindow jako główne okno aplikacji.

```
┌──────────────────────────────────────────────┐
│  [Mica Backdrop]                             │
│ ┌────────┬───────────────────────────────┐   │
│ │SIDEBAR │  CONTENT AREA                 │   │
│ │160px   │  (Frame / ContentControl)     │   │
│ │        │                               │   │
│ │ Szukaj │  [aktywny widok]              │   │
│ │Struktu.│                               │   │
│ │Ulubieni│                               │   │
│ │Ostatnie│                               │   │
│ │────────│                               │   │
│ │ Czat   │                               │   │
│ │ SMS    │                               │   │
│ │Odebrane│                               │   │
│ │        │                               │   │
│ │════════│                               │   │
│ │Ustawien│                               │   │
│ │[◐ Dark]│                               │   │
│ └────────┴───────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

### 1.2 Kontrolka WinUI 3
```xml
<NavigationView
    PaneDisplayMode="Left"
    OpenPaneLength="160"
    IsPaneToggleButtonVisible="False"
    IsBackButtonVisible="Collapsed"
    IsSettingsVisible="False">
    
    <NavigationView.MenuItems>
        <NavigationViewItem Content="Szukaj" Icon="Find" Tag="search"/>
        <NavigationViewItem Content="Struktura" Tag="org">
            <NavigationViewItem.Icon>
                <FontIcon Glyph="&#xE8D8;"/>
            </NavigationViewItem.Icon>
        </NavigationViewItem>
        <NavigationViewItem Content="Ulubieni" Icon="Favorite" Tag="fav"/>
        <NavigationViewItem Content="Ostatnie" Icon="Clock" Tag="recent"/>
        <NavigationViewItemSeparator/>
        <NavigationViewItem Content="Czat" Icon="Message" Tag="chat"/>
        <NavigationViewItem Content="SMS" Tag="sms">
            <NavigationViewItem.Icon>
                <FontIcon Glyph="&#xE8EA;"/>
            </NavigationViewItem.Icon>
        </NavigationViewItem>
        <NavigationViewItem Content="Odebrane" Icon="Mail" Tag="inbox">
            <NavigationViewItem.InfoBadge>
                <InfoBadge Value="{x:Bind ViewModel.UnreadCount, Mode=OneWay}"/>
            </NavigationViewItem.InfoBadge>
        </NavigationViewItem>
    </NavigationView.MenuItems>
    
    <NavigationView.FooterMenuItems>
        <NavigationViewItem Content="Ustawienia" Icon="Setting" Tag="settings"/>
    </NavigationView.FooterMenuItems>
    
    <NavigationView.PaneFooter>
        <ToggleSwitch Header="Tryb ciemny" 
                      IsOn="{x:Bind ViewModel.IsDarkMode, Mode=TwoWay}"
                      Margin="16,0,0,12"/>
    </NavigationView.PaneFooter>
    
    <Frame x:Name="ContentFrame"/>
</NavigationView>
```

### 1.3 Aktywny widok (SelectionChanged)
Nawigacja `Tag` → Page:
- `search` → `SearchPage.xaml` (dawne SearchWindow)
- `org` → `OrgChartPage.xaml` (NOWY)
- `fav` → `FavoritesPage.xaml` (NOWY, korzysta z istniejącego FavoriteService)
- `recent` → `RecentPage.xaml` (NOWY, korzysta z istniejącego RecentlyViewedService)
- `chat` → `ChatPage.xaml` (dawne ChatWindow)
- `sms` → `SmsPage.xaml` (dawne SmsWindow)
- `inbox` → `InboxPage.xaml` (dawne InboxWindow)
- `settings` → `SettingsPage.xaml` (dawne SettingsWindow)

---

## 2. SYSTEM MOTYWÓW (LIGHT / DARK)

### 2.1 Implementacja WinUI 3
```csharp
// W App.xaml.cs lub SettingsService
public void SetTheme(ElementTheme theme)
{
    if (MainWindow.Content is FrameworkElement root)
        root.RequestedTheme = theme;
}
```

### 2.2 Toggle w NavigationView PaneFooter
`ToggleSwitch` bindowany do `SettingsService.IsDarkMode`.
Persystencja w `appsettings.json` → nowy klucz `"Ui": { "Theme": "Light" }`.

### 2.3 MicaBackdrop
Zachować istniejący `MicaBackdrop` — działa automatycznie z Light/Dark.

---

## 3. WIDOK SZUKAJ (SearchPage.xaml)

### 3.1 Layout
```
[Pasek Sync: status, ostatnia synchronizacja, liczba użytkowników]
[AutoSuggestBox — wyszukiwarka Fluent]
[Filtry działów — chipsy/ToggleButtons]
[Panel szczegółów — jeśli wybrany pracownik]
[Siatka kart pracowników — GridView / ItemsRepeater]
```

### 3.2 Pasek wyszukiwania
```xml
<AutoSuggestBox
    PlaceholderText="Szukaj pracownika (imię, nazwisko, login, e-mail)…"
    QueryIcon="Find"
    TextChanged="{x:Bind ViewModel.OnSearchTextChanged}"
    QuerySubmitted="{x:Bind ViewModel.OnSearchSubmitted}"/>
```
**WAŻNE**: `AutoSuggestBox` w WinUI 3 automatycznie obsługuje Fluent Design TextBox z wypełnionym tłem i akcentowym focus — nie trzeba ręcznie stylować.

### 3.3 Filtry działów
```xml
<ItemsRepeater ItemsSource="{x:Bind ViewModel.Departments}">
    <ItemsRepeater.Layout>
        <StackLayout Orientation="Horizontal" Spacing="4"/>
    </ItemsRepeater.Layout>
    <ItemsRepeater.ItemTemplate>
        <DataTemplate>
            <ToggleButton Content="{Binding Name}" 
                          IsChecked="{Binding IsSelected, Mode=TwoWay}"
                          CornerRadius="14"
                          Padding="4,2"/>
        </DataTemplate>
    </ItemsRepeater.ItemTemplate>
</ItemsRepeater>
```

### 3.4 Karty pracowników
```xml
<ItemsRepeater ItemsSource="{x:Bind ViewModel.FilteredEmployees, Mode=OneWay}">
    <ItemsRepeater.Layout>
        <UniformGridLayout MinItemWidth="200" MinColumnSpacing="6" MinRowSpacing="6"/>
    </ItemsRepeater.Layout>
    <ItemsRepeater.ItemTemplate>
        <DataTemplate x:DataType="models:Employee">
            <!-- Karta pracownika -->
            <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                    BorderThickness="1" CornerRadius="8" Padding="10">
                <!-- Flex column z margin-top:auto na przyciskach -->
                <Grid RowDefinitions="Auto,*,Auto">
                    <!-- Row 0: Avatar + Nazwa -->
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <PersonPicture DisplayName="{x:Bind DisplayName}"
                                       ProfilePicture="{x:Bind PhotoSource}"
                                       Width="32" Height="32"/>
                        <StackPanel>
                            <TextBlock Text="{x:Bind DisplayName}" FontWeight="SemiBold" FontSize="12"/>
                            <TextBlock Text="{x:Bind Title}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" FontSize="11"/>
                        </StackPanel>
                    </StackPanel>
                    
                    <!-- Row 1: Meta (flex:1 equivalent) -->
                    <StackPanel Grid.Row="1" Spacing="2" Margin="0,6,0,0">
                        <TextBlock FontSize="11" Foreground="{ThemeResource TextFillColorSecondaryBrush}">
                            <Run Text="{x:Bind Department}"/> · <Run Text="{x:Bind Office}"/>
                        </TextBlock>
                        <TextBlock Text="{x:Bind Email}" FontSize="11"/>
                        <!-- Mobile — widoczny warunkowo -->
                    </StackPanel>
                    
                    <!-- Row 2: Quick Actions — ZAWSZE na dole -->
                    <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="3"
                                BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                                BorderThickness="0,1,0,0" Padding="0,6,0,0" Margin="0,6,0,0">
                        <!-- KOLEJNOŚĆ: Czat → SMS → E-mail -->
                        <Button Content="Czat" Command="{...}" HorizontalAlignment="Stretch">
                            <Button.Icon><FontIcon Glyph="&#xE8BD;"/></Button.Icon>
                        </Button>
                        <Button Content="SMS" Command="{...}" HorizontalAlignment="Stretch">
                            <Button.Icon><FontIcon Glyph="&#xE8EA;"/></Button.Icon>
                        </Button>
                        <Button Content="E-mail" Command="{...}" HorizontalAlignment="Stretch">
                            <Button.Icon><FontIcon Glyph="&#xE715;"/></Button.Icon>
                        </Button>
                    </StackPanel>
                </Grid>
                
                <!-- Gwiazdka ulubione — prawy górny róg -->
                <Border.Child> <!-- użyj overlay -->
                    <ToggleButton Style="{StaticResource FavoriteToggleButton}"
                                  IsChecked="{x:Bind IsFavorite, Mode=TwoWay}"/>
                </Border.Child>
            </Border>
        </DataTemplate>
    </ItemsRepeater.ItemTemplate>
</ItemsRepeater>
```

### 3.5 Panel szczegółów pracownika
Wyświetlany NAD siatką kart (nie w osobnym oknie). Kontrolka `Expander` lub `Border` z przyciskiem zamknięcia.

**Sekcje:**
1. **Header**: Avatar (PersonPicture 44px) + Nazwa + Stanowisko + przycisk X zamknięcia
2. **Kontakt**: E-mail, Telefon, Komórka (warunkowo) — każde pole z przyciskiem Kopiuj
3. **Organizacja**: Dział, Firma, Biuro, Przełożony (jako HyperlinkButton → nawiguje do karty przełożonego), Login (monospace, kopiuj)
4. **Karta pracownicza** (warunkowo, zależy od ustawienia ShowEmployeeCard): Nr AGRO XXXX
5. **IT** (warunkowo, tylko dla członków grupy IT): SID
6. **Akcje**: Czat → SMS → E-mail → Eksport CSV → vCard

### 3.6 Quick Actions — kolejność
Na kartach i w panelu szczegółów ZAWSZE:
1. **Czat** (ikona message-circle) — otwiera widok Czat z wybraną osobą
2. **SMS** (ikona device-mobile-message) — otwiera widok SMS z dodanym odbiorcą
3. **E-mail** (ikona mail) — otwiera klienta poczty

---

## 4. WIDOK STRUKTURA (OrgChartPage.xaml) — NOWY

### 4.1 Implementacja
`TreeView` z `ItemsSource` bindowanym do hierarchii z pola `Manager`/`ManagerDN`.

```xml
<TreeView ItemsSource="{x:Bind ViewModel.OrgTree}">
    <TreeView.ItemTemplate>
        <DataTemplate x:DataType="vm:OrgNode">
            <TreeViewItem ItemsSource="{x:Bind Children}">
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <PersonPicture DisplayName="{x:Bind Name}" Width="28" Height="28"/>
                    <StackPanel>
                        <TextBlock Text="{x:Bind Name}" FontWeight="SemiBold" FontSize="11"/>
                        <TextBlock Text="{x:Bind Role}" FontSize="11" Opacity="0.7"/>
                    </StackPanel>
                </StackPanel>
            </TreeViewItem>
        </DataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

### 4.2 Kliknięcie w osobę
Nawiguje do SearchPage i otwiera panel szczegółów dla klikniętego pracownika.

---

## 5. WIDOK ULUBIENI (FavoritesPage.xaml) — NOWY

Korzysta z istniejącego `FavoriteService` i `IFavoriteRepository`.
Wyświetla te same karty co SearchPage, ale tylko z `IsFavorite == true`.
Pusty stan: ikona gwiazdki + "Kliknij gwiazdkę na karcie pracownika."

---

## 6. WIDOK OSTATNIE (RecentPage.xaml) — NOWY

Korzysta z istniejącego `RecentlyViewedService`.
Karty w kolejności ostatnio przeglądanych (max 10).
Pusty stan: ikona zegara + "Brak historii przeglądania."

---

## 7. WIDOK CZAT (ChatPage.xaml)

### 7.1 Layout — dwupanelowy
```
┌─────────────┬──────────────────────────────┐
│ Lista       │ Nagłówek: Imię Nazwisko      │
│ rozmów      │──────────────────────────────│
│             │ Bąbelki wiadomości           │
│ [JK] Jan K. │ (lewo=odebrane, prawo=moje) │
│ [RW] Robert │                              │
│             │──────────────────────────────│
│             │ [Input wiadomości] [Wyślij]  │
└─────────────┴──────────────────────────────┘
```

### 7.2 Migracja z ChatWindow.xaml
Przenieść istniejący layout z ChatWindow do ChatPage. Zachować:
- `ChatViewModel` bez zmian
- SignalR `ChatHubService` bez zmian
- `ListView` z `ChatMessageTemplateSelector` (OwnMessage/OtherMessage)
- TextBox + przycisk Wyślij

---

## 8. WIDOK SMS (SmsPage.xaml)

### 8.1 Layout — dwukolumnowy
Lewa: wyszukiwarka odbiorców + lista wybranych
Prawa: textarea treści + licznik znaków + przycisk Wyślij

### 8.2 Migracja z SmsWindow.xaml
Przenieść layout. Zachować `SmsViewModel`, `SmsApiService` bez zmian.

---

## 9. WIDOK ODEBRANE (InboxPage.xaml)

Połączyć InboxWindow + SentMessagesWindow w jeden widok z `Pivot` lub `TabView`:
- Tab "Odebrane" — lista wiadomości z niebieskim kropką (nieprzeczytane)
- Tab "Wysłane" — historia wysłanych

Badge w NavigationView.MenuItems aktualizowany przez `SignalR` → `MessageHubService`.

---

## 10. WIDOK USTAWIENIA (SettingsPage.xaml)

### 10.1 Sekcje (zachować wszystkie z SettingsWindow)

**System:**
- Autostart (RunAtStartup) — ToggleSwitch
- Minimalizuj do tray (StartMinimizedToTray) — ToggleSwitch
- Close-to-tray (CloseToTray) — ToggleSwitch
- Karta pracownicza (ShowEmployeeCard) — ToggleSwitch

**Skrót klawiszowy:**
- Aktywacja: wyświetlanie `Ctrl+Alt+Space` (read-only, z KeyboardAcceleratorTextOverride)

**Synchronizacja:**
- Interwał: ComboBox [15/30/60/240 min]
- Przycisk "Synchronizuj teraz"
- Status: liczba użytkowników w cache

**Diagnostyka:**
- Wersja aplikacji
- Status SignalR (Connected/Disconnected)
- Ścieżka bazy SQLite
- Czas ostatniej synchronizacji
- Liczba użytkowników w cache
- Przycisk "Otwórz folder logów"

### 10.2 Przyciski Zapisz/Anuluj
Na dole strony, wyrównane do prawej.

---

## 11. MIGRACJA — KOLEJNOŚĆ PRAC

### Faza 1: Szkielet
1. Utworzyć `MainWindow.xaml` z `NavigationView` (sidebar)
2. Utworzyć puste Page'y: SearchPage, OrgChartPage, FavoritesPage, RecentPage, ChatPage, SmsPage, InboxPage, SettingsPage
3. Podpiąć nawigację `SelectionChanged` → `Frame.Navigate()`
4. Dodać ToggleSwitch dark/light w PaneFooter
5. Zachować TrayIconService, HotkeyService, AppActivationService — zmienić referencje z SearchWindow na MainWindow

### Faza 2: Przeniesienie widoków
6. Przenieść XAML z SearchWindow → SearchPage (zmienić Window na Page)
7. Przenieść ChatWindow → ChatPage
8. Przenieść SmsWindow → SmsPage
9. Przenieść InboxWindow + SentMessagesWindow → InboxPage
10. Przenieść SettingsWindow → SettingsPage

### Faza 3: Nowe widoki
11. Zbudować OrgChartPage z TreeView
12. Zbudować FavoritesPage (reuse karty z SearchPage)
13. Zbudować RecentPage (reuse karty z SearchPage)

### Faza 4: Ulepszenia kart
14. Zmienić ListView na ItemsRepeater + UniformGridLayout (widok kart)
15. Dodać quick actions: Czat → SMS → E-mail (kolejność!)
16. Wyrównać przyciski do dołu karty (Grid z RowDefinitions="Auto,*,Auto")
17. Dodać gwiazdkę ulubionych na kartach

### Faza 5: Cleanup
18. Usunąć stare okna (SearchWindow, ChatWindow, SmsWindow, itd.)
19. Zaktualizować DI registration w App.xaml.cs
20. Przetestować: tray icon, hotkey, SignalR, synchronizację

---

## 12. PALETA KOLORÓW

Używać standardowych zasobów WinUI 3 `ThemeResource` — automatycznie przełączają się Light/Dark:

| Element | ThemeResource |
|---------|--------------|
| Tło karty | `CardBackgroundFillColorDefaultBrush` |
| Border karty | `CardStrokeColorDefaultBrush` |
| Tekst primary | `TextFillColorPrimaryBrush` |
| Tekst secondary | `TextFillColorSecondaryBrush` |
| Tekst tertiary | `TextFillColorTertiaryBrush` |
| Accent | `AccentFillColorDefaultBrush` |
| Separator | `DividerStrokeColorDefaultBrush` |
| Surface | `LayerFillColorDefaultBrush` |

**NIE** definiować własnych kolorów — WinUI 3 ThemeResources obsługują Light/Dark automatycznie.

---

## 13. ZACHOWANE BEZ ZMIAN

- Wszystkie ViewModels (SearchViewModel, ChatViewModel, SmsViewModel, itd.)
- Wszystkie Services (SyncService, LdapService, ChatHubService, SmsApiService, FavoriteService, RecentlyViewedService, TrayIconService, HotkeyService)
- Modele (Employee, ChatMessage, InboxMessage)
- SQLite cache (DirectoryDbContext)
- appsettings.json (dodać tylko klucz Theme)
- Polish character normalization w wyszukiwaniu
