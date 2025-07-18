﻿using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SystemInfoViewer.Helpers;

namespace SystemInfoViewer
{
    public sealed partial class MainWindow : Window
    {
        private const string THEME_SETTING_KEY = "AppTheme";
        private bool _isDarkTheme = false;
        private IntPtr _hWnd;
        private AppWindow _appWindow;
        private bool _isFirstActivation = true;

        public MainWindow()
        {
            this.InitializeComponent();
            _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeAppWindow();

            SetWindowSize(1100, 760);
            InitializeNavigation();

            LoadSavedTheme();
            DispatcherQueue.TryEnqueue(() => ForceTitleBarUpdate(_isDarkTheme));

            this.Activated += MainWindow_Activated;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_isFirstActivation)
            {
                ForceTitleBarUpdate(_isDarkTheme);
                _isFirstActivation = false;
            }
        }

        private void InitializeAppWindow()
        {
            try
            {
                var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
                _appWindow = AppWindow.GetFromWindowId(windowId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化AppWindow失败: {ex.Message}");
            }
        }

        private async void ForceTitleBarUpdate(bool isDark)
        {
            if (_hWnd == IntPtr.Zero) return;

            SetDwmDarkMode(isDark);
            UpdateAppWindowTitleBar(isDark);
            RedrawWindowImmediately();

            nint lParam = (nint)(0x0100 | 0x0200);
            PostMessage(_hWnd, WM_WINDOWPOSCHANGED, IntPtr.Zero, lParam);

            await Task.Delay(50);
            RedrawWindowImmediately();

            ShowWindow(_hWnd, SW_HIDE);
            ShowWindow(_hWnd, SW_SHOW);
        }

        private void UpdateAppWindowTitleBar(bool isDark)
        {
            if (_appWindow?.TitleBar == null) return;

            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = false;

            if (isDark)
            {
                titleBar.BackgroundColor = Microsoft.UI.Colors.DarkGray;
                titleBar.ForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.DarkGray;
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.Gray;
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Gray;
                titleBar.InactiveForegroundColor = Microsoft.UI.Colors.LightGray;
            }
            else
            {
                titleBar.BackgroundColor = Microsoft.UI.Colors.White;
                titleBar.ForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.Colors.LightGray;
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.Colors.Gray;
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.LightGray;
                titleBar.InactiveForegroundColor = Microsoft.UI.Colors.DarkGray;
            }
        }

        private void RedrawWindowImmediately()
        {
            if (_hWnd == IntPtr.Zero) return;

            RedrawWindow(
                _hWnd,
                IntPtr.Zero,
                IntPtr.Zero,
                RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN
            );

            DwmFlush();
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme ? ElementTheme.Dark : ElementTheme.Light);
            DispatcherQueue.TryEnqueue(() => ForceTitleBarUpdate(_isDarkTheme));
        }

        private void ApplyTheme(ElementTheme theme)
        {
            if (Content is FrameworkElement frameworkElement)
            {
                frameworkElement.RequestedTheme = theme;
            }
            UpdateThemeIcon(theme);
            SaveThemeSetting(theme);
        }

        private void SetDwmDarkMode(bool enable)
        {
            try
            {
                int attribute = Environment.OSVersion.Version.Build >= 18985 ? 20 : 19;
                int value = enable ? 1 : 0;
                DwmSetWindowAttribute(_hWnd, attribute, ref value, sizeof(int));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DWM设置失败: {ex.Message}");
            }
        }

        private void InitializeNavigation()
        {
            if (MainContentFrame != null)
            {
                NavigationService.Instance.Initialize(MainContentFrame);
                try { NavigationService.Instance.Navigate(typeof(HomePage)); }
                catch (Exception ex) { Debug.WriteLine($"导航失败: {ex.Message}"); }
            }
        }

        private void LoadSavedTheme()
        {
            ElementTheme appliedTheme = ElementTheme.Light;
            try
            {
                string savedTheme = FileHelper.ReadIniValue("Theme", "CurrentTheme");
                if (Enum.TryParse<ElementTheme>(savedTheme, out var theme))
                    appliedTheme = theme;
                else
                    appliedTheme = ((FrameworkElement)Content).ActualTheme;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载主题失败: {ex.Message}");
            }
            _isDarkTheme = appliedTheme == ElementTheme.Dark;
            ApplyTheme(appliedTheme);
        }

        private void UpdateThemeIcon(ElementTheme theme)
        {
            string iconPath = theme == ElementTheme.Dark
                ? "M752.41931153 240.7063601c8.36993432 0 15.62255883 3.08990502 21.72821045 9.16589356 6.1353147 6.08093262 9.18566918 13.3928833 9.18566918 21.73315382 0 8.55285621-3.05035376 15.85986352-9.18566918 21.94079614l-43.67394996 43.66900658c-5.93261719 5.97216773-13.17041016 8.95825195-21.72326709 8.95825195-8.87420678 0-16.20098901-2.98608422-22.08911085-8.75061035-5.88317847-5.87329102-8.80004906-13.2890625-8.80004906-22.14843774 0-8.54791283 2.95642114-15.75604272 8.91375732-21.72821044l43.66900659-43.67394996c6.14025879-6.07598853 13.44726539-9.16589356 21.98034668-9.16589356z m37.66223167 240.39459181h61.79809547c8.52813721 0 15.82031227 2.98608422 21.83697534 9.06207276C879.78271508 496.24395728 882.78857422 503.45208717 882.78857422 512c0 8.55285621-3.00585914 15.85986352-9.07196021 21.83697533-6.01666236 6.07598853-13.30883813 9.06207276-21.83697534 9.06207276h-61.79809547c-8.52813721 0-15.80053734-2.98608422-21.86663841-9.06207276-6.01666236-5.97711182-9.05712867-13.2890625-9.05712867-21.83697533 0-8.55285621 3.04046631-15.76098609 9.05712867-21.83697533 6.06610107-6.07598853 13.33850122-9.06207276 21.86663841-9.06207276zM512 141.21142578c8.52813721 0 15.79064917 3.08990502 21.85180688 9.06207276 6.02160645 6.08093262 9.05712867 13.3928833 9.05712867 21.83697533v61.79809546c0 8.55285621-3.03552222 15.85986352-9.0521853 21.83697534-6.06610107 6.07598853-13.32861305 9.16589356-21.85675025 9.16589355-8.53802467 0-15.80053734-3.08990502-21.86663842-9.16589355-6.01666236-5.97711182-9.0521853-13.2890625-9.05218458-21.83697534V172.11047387c0-8.44409203 3.03552222-15.76098609 9.05218458-21.83697533C496.20440674 144.3013308 503.46691871 141.21142578 512.00494408 141.21142578zM271.81304907 240.7063601c8.36499023 0 15.61267066 3.08990502 21.743042 9.16589356l43.66900659 43.67394996c6.14025879 6.07598853 9.17083764 13.38793922 9.17083764 21.72821044 0 8.55285621-3.01080323 15.86480689-9.05712938 21.83697534-6.03149391 6.08093262-13.30389404 9.06207276-21.85180617 9.06207275-8.69622827 0-16.02795386-2.98608422-21.95068359-8.85443115l-43.68383814-43.67395068c-5.97216773-5.97216773-8.92858886-13.28411842-8.92858886-22.03967284 0-8.55285621 3.00585914-15.76098609 9.07196021-21.83697462 6.01666236-5.97216773 13.30883813-9.06207276 21.85675096-9.06207276h-0.03955126z m436.93725586 436.91253615c8.36499023 0 15.60772729 2.98608422 21.72326709 9.16589355l43.67394995 43.67395067c6.1353147 6.17980933 9.18566918 13.38793922 9.18566919 21.93585205 0 8.34521461-3.05035376 15.65716529-9.18566919 21.73315382-6.1105957 6.17980933-13.35827613 9.16589356-21.72326636 9.16589356-8.52813721 0-15.84008789-2.98608422-21.98034668-9.16589356l-43.66900659-43.66900587c-5.95733618-5.87329102-8.91375732-13.1852417-8.91375732-21.73315453 0-8.55285621 3.01080323-15.86480689 9.05712866-21.94079614 6.03149391-6.07598853 13.32861305-9.16589356 21.85180689-9.16589355h-0.01977564zM512.00494408 388.40380836c-34.12243628 0-63.22192359 12.05310035-87.39239525 36.2532351-24.1358645 24.10125732-36.21368384 53.25018335-36.21368384 87.34295654 0 34.0927732 12.07782007 63.24169922 36.21368384 87.44677734C448.78796387 623.53814721 477.88250709 635.59619164 512 635.59619164c34.12243628 0 63.23181176-12.05310035 87.4072268-36.15435838C623.51837158 575.2466433 635.59619164 546.09771729 635.59619164 512c0-34.0927732-12.07782007-63.24169922-36.18896484-87.34295654C575.23181176 400.45690942 546.12243628 388.40380836 512 388.40380836zM172.12036133 481.10095191h61.79809547c8.53802467 0 15.80053734 2.98608422 21.86663841 9.06207276 6.01666236 6.08093262 9.05712867 13.2890625 9.05712867 21.83697533 0 8.55285621-3.04046631 15.85986352-9.05712867 21.83697533-6.06610107 6.07598853-13.32861305 9.06207276-21.86663841 9.06207276h-61.79809547c-8.52813721 0-15.81042481-2.98608422-21.83697534-9.06207276C144.21728492 527.85986352 141.21142578 520.54791283 141.21142578 512c0-8.55285621 3.00585914-15.76098609 9.07196021-21.83697533 6.03149391-6.07598853 13.30883813-9.06207276 21.83697534-9.06207276zM512.00494408 759.19238258c8.52813721 0 15.79064917 2.98608422 21.85180617 9.06207274 6.02160645 6.08093262 9.05712867 13.2890625 9.05712938 21.83697535v61.79809546c0 8.55285621-3.03552222 15.85986352-9.0521853 21.83697533C527.79559326 879.80249 520.53308129 882.78857422 512 882.78857422c-8.53802467 0-15.80053734-2.98608422-21.86663842-9.06207276-6.01666236-5.97711182-9.0521853-13.2890625-9.05218458-21.83697533v-61.79809546c0-8.55285621 3.03552222-15.76098609 9.05218458-21.83697534 6.06610107-6.07598853 13.32861305-9.06207276 21.86663842-9.06207275z m-196.47839379-81.57348633c8.5034182 0 15.80053734 2.98608422 21.84686279 9.16589355 6.03149391 6.08093262 9.07196021 13.3928833 9.07196021 21.94079614 0 8.44409203-3.08001685 15.65222192-9.19555664 21.72821044l-43.67394995 43.67394996c-6.10565162 6.17980933-13.34838867 9.16589356-21.72326637 9.16589356-8.54791283 0-15.84008789-2.98608422-21.85180687-8.96319533-6.07104516-6.07598853-9.0769043-13.38793922-9.0769043-21.93585205 0-8.65173364 2.95642114-15.96862769 8.92858887-21.94079614l43.68383813-43.66900658c6.10565162-6.17980933 13.44232202-9.16589356 21.95068359-9.16589355h0.03955054zM512 326.60571289c33.628052 0 64.6506958 8.34521461 93.0481565 24.81811547 28.42712378 16.69042992 50.92163109 39.1404419 67.4637456 67.56756568 16.57177758 28.32824708 24.86755347 59.32617188 24.86755347 93.00860596 0 33.68243408-8.27600098 64.68035888-24.86755347 93.10748267-16.58166504 28.32824708-39.08605981 50.77825904-67.4637456 67.4637456-28.36285424 16.58166504-59.37561059 24.82305884-93.0481565 24.82305884-33.66760254 0-64.67047143-8.24139381-93.05804468-24.81811547-28.37768578-16.69042992-50.86230492-39.14538598-67.46374487-67.46868897-16.58166504-28.42712378-24.86755347-59.42504859-24.86755348-93.10748267 0-33.68243408 8.31555152-64.68035888 24.86755348-93.00860596 16.55200195-28.42712378 39.03662109-50.87713647 67.46374487-67.56262231C447.35424828 334.94598413 478.371948 326.60571289 512 326.60571289z"
                : "M439.04383639 224.33271976a289.43717504 289.43717504 0 0 0-89.06329676 39.44690807c-27.5075319 18.12412161-51.158867 39.64960558-70.96389486 64.57645191-19.79019562 24.92684633-35.31880082 53.25011237-46.62042137 84.97474006C221.10449032 445.05050411 215.45368042 478.01109252 215.45368042 512c0 40.16870887 7.81621301 78.48347702 23.45358166 115.04318192 15.64725682 36.67341315 36.77228986 68.19039996 63.2910532 94.76354538 26.53359488 26.56820206 58.11485196 47.57952611 94.69927585 63.23667111 36.59431134 15.65714501 75.00301286 23.48324474 115.16183354 23.48324475 33.97407594 0 66.85061917-5.56182137 98.62468557-16.89310498 31.76417894-11.32633953 60.10227581-26.77584294 85.01429133-46.65502781 24.87740834-19.77536407 46.43255539-43.46625042 64.57150781-70.96389488a290.15403174 290.15403174 0 0 0 39.43701989-88.98913905c-8.3056532 0.72180079-17.71378249 1.13213991-28.0958481 1.1321392-45.21637032 0-88.35138342-8.85936294-129.45942212-26.46932463-41.1327577-17.71872658-76.62459295-41.50848964-106.44089858-71.27535655C505.84007277 458.54719011 482.10963587 423.01086095 464.42057238 381.91765379 446.7562286 340.81455917 437.94135957 297.65977044 437.94135957 252.4483442c0-10.40184195 0.39550758-19.77536407 1.15685891-28.01674772l-0.049438-0.09887672zM512.02976157 141.21192041c8.32542811 0 16.51242896 0.31146168 24.6054972 0.92944167-16.4036648 35.32374419-24.60549719 72.09603405-24.60549719 110.30698212 0 35.12104668 6.84227598 68.69961578 20.58121076 100.83458258 13.70927096 32.03114601 32.15474243 59.63261126 55.35124448 82.80933769 23.16683898 23.17178306 50.76830423 41.60736635 82.82416923 55.30674985 32.02125855 13.69938351 65.654209 20.60098567 100.78514387 20.60098568 38.23072298 0 75.03267594-8.24138293 110.35147675-24.62032874 0.59326099 8.14250622 0.86517214 16.28006837 0.86517213 24.62032874 0 33.57856837-4.43956893 66.43533597-13.29893256 98.46648269-8.90880166 32.03114601-21.28817928 61.69419283-37.24195443 88.78149743-15.92411206 27.19112615-35.31880082 52.11797247-58.09013296 74.87941716-22.77627549 22.76144395-47.74267309 42.12152555-74.87447306 58.09013223-27.15157487 15.96366261-56.75529553 28.32326532-88.78644152 37.28644905A368.16784419 368.16784419 0 0 1 512.00009921 882.78807959a368.20739474 368.20739474 0 0 1-98.48625832-13.28904439c-32.03609009-8.95824037-61.6249792-21.31784236-88.79138561-37.28150569-27.12685589-15.96860669-52.08336529-35.32868827-74.89919206-58.09013223-22.7515565-22.76144395-42.13635709-47.68829101-58.05058169-74.87941715-15.96366261-27.0922487-28.34304097-56.75529553-37.2518426-88.78149744A367.9354836 367.9354836 0 0 1 141.21201889 512c0-33.57856837 4.4494571-66.43533597 13.30882004-98.46648269 8.90880166-32.03114601 21.28817928-61.69419283 37.25184259-88.78149744 15.91916798-27.19112615 35.29902519-52.11797247 58.0505817-74.87941715 22.81582676-22.86526476 47.77233617-42.22534634 74.89919206-58.09013223C351.89380578 175.81880789 381.4826949 163.36032846 413.51878498 154.50096479A368.20739474 368.20739474 0 0 1 512.00009921 141.21192041h0.02966236z";

            ThemeIcon.Data = (Geometry)XamlReader.Load(
                $"<Geometry xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{iconPath}</Geometry>");
        }

        private void SaveThemeSetting(ElementTheme theme)
        {
            try { FileHelper.WriteIniValue("Theme", "CurrentTheme", theme.ToString()); }
            catch (Exception ex) { Debug.WriteLine($"保存主题失败: {ex.Message}"); }
        }

        private void SetWindowSize(int width, int height)
        {
            SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, width, height, 0x0002 | 0x0004 | 0x0040);
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton button)
            {
                Type pageType = button.Name switch
                {
                    "HomeNavButton" => typeof(HomePage),
                    "ToolsNavButton" => typeof(ToolsPage),
                    "SettingsNavButton" => typeof(SettingsPage),
                    "AboutButton" => typeof(AboutPage),
                    _ => null
                };
                if (pageType != null) NavigationService.Instance.Navigate(pageType);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("dwmapi.dll")]
        private static extern void DwmFlush();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, nint lParam);

        private const uint RDW_FRAME = 0x0020;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint WM_WINDOWPOSCHANGED = 0x0047;
    }

    public class NavigationService
    {
        private static readonly Lazy<NavigationService> _instance = new(() => new NavigationService());
        public static NavigationService Instance => _instance.Value;
        private Frame _frame;
        private NavigationService() { }
        public void Initialize(Frame frame) => _frame = frame;
        public void Navigate(Type pageType) => _frame?.Navigate(pageType);
    }
}