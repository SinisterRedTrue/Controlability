using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Controlability.App.Controls;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.App.Views;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Controlability.App.Tests;

public sealed class DesignRenderingTests
{
    [Fact]
    public void ActualPagesAndEveryProfileTab_RenderInBothThemesAtDesktopAndSmallSizes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { RenderPages(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(60)).Should().BeTrue("WPF layout must finish");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void RenderPages()
    {
        // Load the production dictionaries without starting hardware, the tray or the user's settings store.
        var app = new Application
        {
            Resources = (ResourceDictionary)XamlReader.Parse("""
                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="clr-namespace:Wpf.Ui.Markup;assembly=Wpf.Ui">
                    <ResourceDictionary.MergedDictionaries>
                        <ui:ThemesDictionary Theme="Dark"/><ui:ControlsDictionary/>
                        <ResourceDictionary Source="/Controlability;component/Resources/Design.xaml"/>
                    </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
                """)
        };
        try
        {
            var engine = new FakeEngine();
            var pad = FakeEngine.MakeDevice("Controller with a long display name");
            engine.Devices = [pad];
            engine.Slots = [FakeEngine.Active(0, pad), .. engine.Slots.Skip(1)];
            engine.Live[pad.Id] = new InputState { LeftTrigger = 0.62f, RightTrigger = 0.2f };
            var store = new FakeProfileEditor();
            var keyboard = new Profile { Id = "default-keyboard", Name = "Keyboard and mouse default",
                AppliesTo = new ProfileTargets { ControllerTypes = [ControllerType.Keyboard] } };
            foreach (var profile in new[] { Profile.Generic, keyboard })
            {
                store.Profiles[profile.Id] = profile;
                store.Shipped[profile.Id] = profile;
            }
            var ui = new FakeUiThread();
            var clock = new FakeClock();
            var settings = new Settings { CloseToTray = false };
            var running = new FakeRunningApps("game");
            var dialogs = new FakeDialogs();
            var launcher = new FakeUrlLauncher();
            using var dashboard = new DashboardViewModel(engine, new FakeProfileCatalog(), ui, clock, launcher);
            using var steam = new SteamTipViewModel(running, settings, _ => { }, ui);
            using var devices = new DevicesViewModel(engine, store, ui, clock);
            using var profiles = new ProfilesViewModel(engine, store, running, dialogs, ui, clock);
            using var mapping = new SettingsViewModel(engine, settings, _ => { }, store, running, ui, clock);
            var logPath = Path.Combine(Path.GetTempPath(), "Controlability-design-" + Guid.NewGuid());
            var log = new RollingFileLog(logPath);
            var general = new GeneralSettingsViewModel(engine, settings, _ => { }, new FakeAutostart(),
                new FakeAppearance(), log, new DiagnosticsService(engine, new FakeDriverProbe(), settings, log, new FakeClipboard()),
                dialogs, launcher, new FakeUninstaller(), new FakeShell());
            var profilePage = new ProfilesPage(profiles);
            var pages = new Page[] { new DashboardPage(dashboard, steam), new DevicesPage(devices), profilePage,
                new SettingsPage(mapping, general) };

            RenderDevicesReference();
            RenderProfilesReference();
            VerifyPadBars();

            foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.System })
            {
                WpfAppearance.ApplyTheme(theme);
                foreach (var size in new[] { new Size(1080, 720), new Size(640, 480) })
                {
                    foreach (var page in pages) Render(page, size);
                    profiles.SelectedProfile = profiles.Profiles.Single(p => p.Id == keyboard.Id);
                    Render(profilePage, size);
                    var tabs = Descendants<TabControl>(profilePage).Single();
                    tabs.Items.Count.Should().Be(6);
                    foreach (TabItem tab in tabs.Items)
                    {
                        tabs.SelectedItem = tab;
                        Render(profilePage, size);
                    }
                    Descendants<Button>(profilePage).Single(b => b.Command == profiles.DeleteCommand).IsEnabled.Should().BeFalse();
                    Descendants<Button>(profilePage).Single(b => b.Command == profiles.Editor!.SaveCommand).IsEnabled.Should().BeFalse();
                    profiles.Editor!.TriggerDigital = !profiles.Editor.TriggerDigital;
                    Render(profilePage, size);
                    var save = Descendants<Button>(profilePage).Single(b => b.Command == profiles.Editor!.SaveCommand);
                    save.IsEnabled.Should().BeTrue();
                    ((SolidColorBrush)save.Foreground).Color.Should().Be(((SolidColorBrush)app.Resources["PrimaryTextBrush"]).Color);
                    Descendants<TextBlock>(save).Should().OnlyContain(label => label.Foreground == save.Foreground);
                    profiles.Editor.RevertCommand.Execute(null);
                }
            }

            var settingsPage = pages.OfType<SettingsPage>().Single();
            Render(settingsPage, new Size(1080, 720));
            Descendants<Button>(settingsPage).Single(b => Equals(b.Tag, "SettingsSection3"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Render(settingsPage, new Size(1080, 720));
            ((ScrollViewer)settingsPage.FindName("SettingsScroll")).VerticalOffset.Should().BeGreaterThan(0);

            engine.Devices = [];
            engine.DriverStatus = new(false, false);
            engine.IsPaused = true;
            dashboard.Refresh();
            devices.Refresh();
            Render(pages[0], new Size(640, 480));
            Render(pages[1], new Size(640, 480));
            dashboard.HasNoDevices.Should().BeTrue();
            dashboard.ShowViGEmBanner.Should().BeTrue();
            engine.Mode = KeyboardMode.KeyboardAsController;
            dashboard.Refresh();
            Render(pages[0], new Size(640, 480));
            dashboard.IsModeB.Should().BeTrue();

            var services = new ServiceCollection();
            foreach (var page in pages) services.AddSingleton(page.GetType(), page);
            using var provider = services.BuildServiceProvider();
            var window = new MainWindow(settings, _ => { }, new ShellController(provider), new DiPageProvider(provider));
            window.Icon.Should().BeOfType<DrawingImage>();
            Render((FrameworkElement)window.Content, new Size(760, 500));
            window.Close();

            // The same assignment menu is used from an empty player card and from Devices.
            engine.Devices = [pad];
            dashboard.Refresh();
            devices.Refresh();
            dashboard.Cards[3].AssignOptions.Single().Command.Execute(null);
            engine.Calls.Should().Contain($"Assign:{pad.Id}:3");
            devices.Devices.Single().AssignOptions[1].Command.Execute(null);
            engine.Calls.Should().Contain($"Assign:{pad.Id}:1");

            var panel = new AdaptivePanel { Breakpoint = 500 };
            panel.Children.Add(new Border { Height = 80 });
            panel.Children.Add(new Border { Height = 100 });
            panel.Measure(new Size(600, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, 600, panel.DesiredSize.Height));
            VisualTreeHelper.GetOffset(panel.Children[1]).X.Should().BeGreaterThan(0);
            panel.Measure(new Size(400, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, 400, panel.DesiredSize.Height));
            VisualTreeHelper.GetOffset(panel.Children[1]).Y.Should().Be(96);
        }
        finally { app.Shutdown(); }
    }

    private static void RenderProfilesReference()
    {
        WpfAppearance.ApplyTheme(AppTheme.Dark);
        var engine = new FakeEngine();
        var store = new FakeProfileEditor();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../profiles"));
        foreach (var file in Directory.GetFiles(root, "*.json"))
        {
            var profile = System.Text.Json.JsonSerializer.Deserialize(File.ReadAllText(file), ProfilesJsonContext.Default.Profile)!;
            store.Profiles[profile.Id] = profile;
            store.Shipped[profile.Id] = profile;
        }
        using var vm = new ProfilesViewModel(engine, store, new FakeRunningApps("game"), new FakeDialogs(), new FakeUiThread(), new FakeClock());
        vm.SelectedProfile = vm.Profiles.Single(p => p.Icon == "PlayStation");
        var page = new ProfilesPage(vm);
        Render(page, new Size(1351, 922));
        var tabs = Descendants<TabControl>(page).Single();
        foreach (var size in new[] { new Size(1351, 922), new Size(1080, 720), new Size(640, 480) })
        {
            for (var i = 0; i < tabs.Items.Count; i++)
            {
                if (i == 3) vm.SelectedProfile = vm.Profiles.Single(p => p.Icon == "Keyboard");
                else vm.SelectedProfile = vm.Profiles.Single(p => p.Icon == "PlayStation");
                tabs.SelectedIndex = i;
                Render(page, size);
                if (Environment.GetEnvironmentVariable("CONTROLABILITY_RENDER_DIR") is not { Length: > 0 } directory) continue;
                Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(page);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, $"Profiles-reference-{i}-{size.Width}.png"));
                encoder.Save(stream);
            }
        }
    }

    private static void RenderDevicesReference()
    {
        WpfAppearance.ApplyTheme(AppTheme.Dark);
        var keyboard = FakeEngine.MakeDevice("Keyboard + Mouse", ControllerType.Keyboard);
        var pad = FakeEngine.MakeDevice("PS4 Controller") with
        {
            Serial = "F8142E6A0D1A",
            HidPath = @"\\?\hid#vid_054c&pid_09cc#7&2d9a7f1b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
            Capabilities = DeviceCapabilities.Rumble | DeviceCapabilities.Led | DeviceCapabilities.Gyro | DeviceCapabilities.Touchpad,
        };
        var engine = new FakeEngine { Devices = [keyboard, pad] };
        engine.Slots = [FakeEngine.Active(0, keyboard), FakeEngine.Active(1, pad), .. engine.Slots.Skip(2)];
        engine.Hidden.Add(pad.Id);
        engine.Live[pad.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Full, 100) };
        using var vm = new DevicesViewModel(engine, new FakeProfileEditor(), new FakeUiThread(), new FakeClock());
        vm.Selected = vm.Devices.Single(d => d.Id == pad.Id);
        var page = new DevicesPage(vm);
        foreach (var size in new[] { new Size(1305, 932), new Size(1198, 820) })
        {
            Render(page, size);
            var scroll = Descendants<ScrollViewer>(page).First();
            scroll.ScrollableHeight.Should().Be(0, "the entire Devices section should fit a desktop window");
            scroll.ScrollableWidth.Should().Be(0);
            var swatch = Descendants<Button>(page).Single(b => b.DataContext is LedSwatchViewModel { IsSelected: true });
            ((System.Windows.Shapes.Ellipse)swatch.Template.FindName("Ring", swatch)).Visibility.Should().Be(Visibility.Visible);
            foreach (var diagram in Descendants<PadDiagram>(page))
            {
                var extent = diagram.TransformToAncestor(page).TransformBounds(new Rect(diagram.RenderSize));
                extent.Bottom.Should().BeLessThanOrEqualTo(size.Height);
            }
            if (Environment.GetEnvironmentVariable("CONTROLABILITY_RENDER_DIR") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(page);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(directory, $"Devices-reference-{size.Width}.png"));
                encoder.Save(output);
                var liveCard = Descendants<Border>(page).Single(b => Grid.GetRow(b) == 2 && b.MinHeight == 377);
                var visual = new DrawingVisual();
                using (var drawing = visual.RenderOpen())
                    drawing.DrawRectangle(new VisualBrush(liveCard), null, new Rect(liveCard.RenderSize));
                var crop = new RenderTargetBitmap((int)Math.Ceiling(liveCard.ActualWidth), (int)Math.Ceiling(liveCard.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                crop.Render(visual);
                var cropEncoder = new PngBitmapEncoder();
                cropEncoder.Frames.Add(BitmapFrame.Create(crop));
                using var liveOutput = File.Create(Path.Combine(directory, $"Devices-live-{size.Width}.png"));
                cropEncoder.Save(liveOutput);
                if (size.Width == 1305) RenderLogoAndIconReview(page, vm, directory);
            }
        }
    }

    private static void RenderLogoAndIconReview(Page page, DevicesViewModel vm, string directory)
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 24, 32)), null, new Rect(0, 0, 640, 440));
            Label("PlayStation: supplied silhouette", 24, 16);
            Label("Xbox: supplied silhouette", 350, 16);
            Logo("PlayStationBrandMark", new Rect(30, 55, 245, 190));
            Logo("XboxBrandMark", new Rect(350, 50, 200, 200));
            Logo("PlayStationBrandMark", new Rect(135, 275, 16, 16));
            Logo("XboxBrandMark", new Rect(435, 269, 28, 28));
            Label("At controller size", 90, 305);
            Label("At controller size", 390, 305);
            Icon(vm.Devices.Single(d => !d.IsKeyboard), new Rect(72, 340, 92, 76));
            Icon(vm.Devices.Single(d => d.IsKeyboard), new Rect(246, 340, 92, 76));
            Label("Device icons at 2×", 412, 365);

            void Logo(string key, Rect target)
            {
                var geometry = (Geometry)Descendants<PadDiagram>(page).First().FindResource(key);
                var bounds = geometry.Bounds;
                var scale = Math.Min(target.Width / bounds.Width, target.Height / bounds.Height);
                drawing.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                    target.X + (target.Width - bounds.Width * scale) / 2 - bounds.X * scale,
                    target.Y + (target.Height - bounds.Height * scale) / 2 - bounds.Y * scale));
                drawing.DrawGeometry(Brushes.White, null, geometry);
                drawing.Pop();
            }
            void Icon(DeviceDetailViewModel device, Rect target)
            {
                var icon = new ContentControl { Content = device, ContentTemplate = (DataTemplate)page.FindResource("DeviceGlyph") };
                Render(icon, target.Size);
                drawing.DrawRectangle(new VisualBrush(icon)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(target.Size), Stretch = Stretch.Uniform,
                }, null, target);
            }
            void Label(string value, double x, double y) => drawing.DrawText(
                new FormattedText(value, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 15, Brushes.LightSteelBlue, 1), new Point(x, y));
        }
        var bitmap = new RenderTargetBitmap(640, 440, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, "Controller-logos-and-icons.png"));
        encoder.Save(stream);
    }

    private static void VerifyPadBars()
    {
        var pad = FakeEngine.MakeDevice("Bar allocation test");
        var engine = new FakeEngine { Devices = [pad] };
        var live = new LivePadViewModel(engine) { Device = pad.Id };
        var physical = new PadDiagram { Kind = PadKind.PlayStation, Live = live };
        var output = new PadDiagram { Kind = PadKind.Xbox, Live = live, ShowOutput = true };
        Render(physical, new Size(600, 280));
        Render(output, new Size(600, 280));
        VerifyControllerGeometry(physical, output);
        var rawBars = Bars(physical);
        var outputBars = Bars(output);
        rawBars.Keys.Should().Equal("L2", "R2");
        outputBars.Keys.Should().Equal("LT", "LB", "RB", "RT");
        var body = (Canvas)output.FindName("XbCanvas");
        var bodyBounds = body.TransformToAncestor(output).TransformBounds(new Rect(body.RenderSize));
        foreach (var label in new[] { "LT", "LB" })
            outputBars[label].TransformToAncestor(output).TransformBounds(new Rect(outputBars[label].RenderSize))
                .Right.Should().BeLessThan(bodyBounds.Left);
        foreach (var label in new[] { "RB", "RT" })
            outputBars[label].TransformToAncestor(output).TransformBounds(new Rect(outputBars[label].RenderSize))
                .Left.Should().BeGreaterThan(bodyBounds.Right);
        // Both bars on the same side must have the reference's 24px gap.
        (outputBars["LB"].TransformToAncestor(output).Transform(new Point()).X -
            outputBars["LT"].TransformToAncestor(output).Transform(new Point()).X).Should().BeApproximately(56, 1);
        (outputBars["RT"].TransformToAncestor(output).Transform(new Point()).X -
            outputBars["RB"].TransformToAncestor(output).Transform(new Point()).X).Should().BeApproximately(56, 1);

        foreach (var input in new[] { "L2", "L1", "R1", "R2" })
        {
            engine.Live[pad.Id] = new InputState
            {
                LeftTrigger = input == "L2" ? 0.4f : 0,
                RightTrigger = input == "R2" ? 0.7f : 0,
                Buttons = input switch { "L1" => ButtonId.LeftShoulder, "R1" => ButtonId.RightShoulder, _ => ButtonId.None },
            };
            // Mapped output deliberately differs from raw input to catch a crossed panel source.
            engine.Mapped[pad.Id] = new XboxState { LeftTrigger = 51, RightTrigger = 204, Buttons = (ushort)XboxButton.RightShoulder };
            physical.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            output.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Check(rawBars["L2"], input == "L2" ? 0.4 : 0);
            Check(rawBars["R2"], input == "R2" ? 0.7 : 0);
            Check(outputBars["LT"], 0.2);
            Check(outputBars["LB"], 0);
            Check(outputBars["RB"], 1);
            Check(outputBars["RT"], 0.8);
            var shoulder = (System.Windows.Shapes.Path)physical.FindName(input == "R1" ? "PsRb" : "PsLb");
            if (input is "L1" or "R1") shoulder.Fill.Should().BeSameAs(physical.FindResource("AccentFillColorDefaultBrush"));
        }
        foreach (var input in new[] { "LT", "LB", "RB", "RT" })
        {
            engine.Mapped[pad.Id] = new XboxState
            {
                LeftTrigger = (byte)(input == "LT" ? 255 : 0),
                RightTrigger = (byte)(input == "RT" ? 255 : 0),
                Buttons = (ushort)(input switch { "LB" => XboxButton.LeftShoulder, "RB" => XboxButton.RightShoulder, _ => XboxButton.None }),
            };
            output.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            foreach (var (label, frame) in outputBars) Check(frame, label == input ? 1 : 0);
        }
        // Changing the source must redraw immediately, even when the controller pose did not otherwise change.
        output.ShowOutput = false;
        Check(outputBars["RT"], 0.7);
        Check(outputBars["LT"], 0);
        output.ShowOutput = true;
        Check(outputBars["RT"], 1);
        physical.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        output.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

        static Dictionary<string, Border> Bars(PadDiagram diagram) => Descendants<Border>(diagram)
            .Where(b => new[] { "L2", "R2", "LT", "LB", "RB", "RT" }.Contains(System.Windows.Automation.AutomationProperties.GetName(b)))
            .ToDictionary(b => System.Windows.Automation.AutomationProperties.GetName(b));
        static void Check(Border frame, double value)
        {
            ((Border)frame.Child).Height.Should().BeApproximately(109 * value, 0.001);
            ((TextBlock)((StackPanel)frame.Parent).Children[2]).Text.Should().Be($"{Math.Round(value * 100)}%");
        }
    }

    private static void VerifyControllerGeometry(PadDiagram physical, PadDiagram output)
    {
        var ps = (Canvas)physical.FindName("PsCanvas");
        var xbox = (Canvas)output.FindName("XbCanvas");
        SymmetricBody((System.Windows.Shapes.Path)physical.FindName("PsBody"), 197);
        SymmetricBody((System.Windows.Shapes.Path)output.FindName("XbBody"), 173);
        MirroredShoulders(physical, "PsLb", "PsRb", 394);
        MirroredShoulders(output, "XbLb", "XbRb", 346);
        Diamond(physical, ps, "PsTriangle", "PsSquare", "PsCircle", "PsCross");
        Diamond(output, xbox, "XbY", "XbX", "XbB", "XbA");
        var touchpad = (FrameworkElement)physical.FindName("PsTouchpad");
        var share = (FrameworkElement)physical.FindName("PsShare");
        var options = (FrameworkElement)physical.FindName("PsOptions");
        (Canvas.GetLeft(touchpad) - Canvas.GetLeft(share) - share.ActualWidth).Should().Be(11);
        (Canvas.GetLeft(options) - Canvas.GetLeft(touchpad) - touchpad.ActualWidth).Should().Be(11);
        (Center((FrameworkElement)physical.FindName("PsLsWell"), ps).X +
            Center((FrameworkElement)physical.FindName("PsRsWell"), ps).X).Should().Be(394);
        foreach (var name in new[] { "PsDpadRight", "PsDpadDown", "PsDpadLeft" })
            ((System.Windows.Shapes.Path)physical.FindName(name)).Data.Should()
                .BeSameAs(((System.Windows.Shapes.Path)physical.FindName("PsDpadUp")).Data);
        ((FrameworkElement)output.FindName("XbGripSeam")).Parent.Should().BeOfType<Canvas>()
            .Which.Clip.Should().BeSameAs(((System.Windows.Shapes.Path)output.FindName("XbBody")).Data);

        static Point Center(FrameworkElement element, Visual ancestor) =>
            element.TransformToAncestor(ancestor).Transform(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        static void Diamond(PadDiagram owner, Canvas canvas, string up, string left, string right, string down)
        {
            var names = new[] { up, left, right, down };
            var buttons = names.Select(n => (FrameworkElement)owner.FindName(n)).ToArray();
            buttons.Should().OnlyContain(b => b.ActualWidth == 24 && b.ActualHeight == 24);
            var p = buttons.Select(b => Center(b, canvas)).ToArray();
            p[0].X.Should().BeApproximately(p[3].X, 0.001);
            p[1].Y.Should().BeApproximately(p[2].Y, 0.001);
            ((p[1].X + p[2].X) / 2).Should().BeApproximately(p[0].X, 0.001);
            ((p[0].Y + p[3].Y) / 2).Should().BeApproximately(p[1].Y, 0.001);
            (p[2].X - p[1].X).Should().BeApproximately(p[3].Y - p[0].Y, 0.001);
        }
        static void MirroredShoulders(PadDiagram owner, string left, string right, double width)
        {
            var a = (System.Windows.Shapes.Path)owner.FindName(left);
            var b = (System.Windows.Shapes.Path)owner.FindName(right);
            a.Data.Should().BeSameAs(b.Data);
            b.RenderTransform.Value.Should().Be(new Matrix(-1, 0, 0, 1, width, 0));
        }
        static void SymmetricBody(System.Windows.Shapes.Path shape, double axis)
        {
            for (double x = 0.37; x < axis; x += 2)
                for (double y = 0.37; y < 232; y += 2)
                    shape.Data.FillContains(new Point(x, y)).Should().Be(
                        shape.Data.FillContains(new Point(2 * axis - x, y)),
                        $"{shape.Name} must mirror across x={axis}, including the grip tips");
        }
    }

    private static void Render(FrameworkElement view, Size size)
    {
        view.Measure(size);
        view.Arrange(new Rect(size));
        view.UpdateLayout();
        // A 192 dpi render also exercises the same vector controls at 200% display scaling.
        var bitmap = new RenderTargetBitmap((int)size.Width * 2, (int)size.Height * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(view);
        if (Environment.GetEnvironmentVariable("CONTROLABILITY_RENDER_DIR") is { Length: > 0 } directory
            && size.Width == 1080 && view is Page)
        {
            Directory.CreateDirectory(directory);
            var tab = Descendants<TabControl>(view).FirstOrDefault()?.SelectedIndex ?? -1;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, $"{view.GetType().Name}-{tab}.png"));
            encoder.Save(output);
        }
        bitmap.PixelWidth.Should().Be((int)size.Width * 2);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
