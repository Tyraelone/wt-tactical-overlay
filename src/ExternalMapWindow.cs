using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Screen = System.Windows.Forms.Screen;

namespace WtTactical {
    public sealed class ExternalMapWindow : Window {
        public readonly TacticalCanvas Canvas = new TacticalCanvas {
            Width = 800, Height = 800, MinWidth = 0, MinHeight = 0, GridLines = false
        };
        public event Action ControlRequested;
        readonly TextBlock heading;
        readonly TextBlock status;
        readonly TextBlock waiting;
        string frameKey;
        Viewport imageBounds;

        public ExternalMapWindow() {
            Title = "WT 副屏战术地图";
            WindowStartupLocation = WindowStartupLocation.Manual;
            MinWidth = 340;
            MinHeight = 300;
            Width = 880;
            Height = 920;
            Background = Brush(13, 21, 31);
            FontFamily = new FontFamily("Microsoft YaHei UI");

            heading = Text("副屏战术地图", 22);
            heading.FontWeight = FontWeights.SemiBold;
            status = Text("等待读取游戏地图", 12);
            waiting = Text("进入战斗后自动加载地图", 17);
            waiting.HorizontalAlignment = HorizontalAlignment.Center;
            waiting.VerticalAlignment = VerticalAlignment.Center;

            var root = new Grid { Margin = new Thickness(16, 12, 16, 10) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var topmost = new CheckBox { Content = "置顶", Foreground = Brushes.White, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            topmost.Click += delegate { Topmost = topmost.IsChecked == true; };
            controls.Children.Add(topmost);
            var console = new Button { Content = "控制台", Padding = new Thickness(9, 4, 9, 4) };
            console.Click += delegate { if (ControlRequested != null) ControlRequested(); };
            controls.Children.Add(console);

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(controls, Dock.Right);
            header.Children.Add(controls);
            header.Children.Add(heading);
            root.Children.Add(header);

            var mapArea = new Grid { Background = Brush(18, 28, 38), ClipToBounds = true };
            mapArea.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = Canvas });
            mapArea.Children.Add(waiting);
            Grid.SetRow(mapArea, 1);
            root.Children.Add(mapArea);

            var footer = new StackPanel { Margin = new Thickness(0, 7, 0, 0) };
            footer.Children.Add(status);
            footer.Children.Add(Text("红色危险区 · 黄点远射 · 橙线进攻 · 悬浮查看说明", 11));
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            Content = root;
        }

        static SolidColorBrush Brush(byte r, byte g, byte b) {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        static TextBlock Text(string value, double size) {
            return new TextBlock { Text = value, FontSize = size, Foreground = Brush(212, 226, 238), TextWrapping = TextWrapping.Wrap };
        }

        internal void PlaceOnSecondary() {
            var display = Screen.AllScreens.FirstOrDefault(screen => !screen.Primary) ?? Screen.PrimaryScreen;
            if (display == null) return;
            var area = display.WorkingArea;
            double scale = Native.GetDpiForSystem() / 96.0;
            double workWidth = area.Width / scale, workHeight = area.Height / scale;
            MinWidth = Math.Min(MinWidth, workWidth);
            MinHeight = Math.Min(MinHeight, workHeight);
            Width = Math.Max(MinWidth, Math.Min(Width, workWidth * .94));
            Height = Math.Max(MinHeight, Math.Min(Height, workHeight * .94));
            Left = area.Left / scale + (workWidth - Width) / 2;
            Top = area.Top / scale + (workHeight - Height) / 2;
        }

        internal void Present(MapSnapshot snapshot, MapDefinition map, Viewport sourceBounds, string message) {
            if (snapshot == null) {
                Clear(message);
                return;
            }

            string key = snapshot.ImageFingerprint + ":" + snapshot.Fingerprint;
            if (frameKey != key) {
                Canvas.ClearHover();
                try {
                    LoadImage(snapshot);
                    frameKey = key;
                } catch (IOException) {
                    Clear("等待读取可用的地图底图");
                    return;
                } catch (NotSupportedException) {
                    Clear("等待读取可用的地图底图");
                    return;
                } catch (FormatException) {
                    Clear("等待读取可用的地图底图");
                    return;
                }
            }

            Viewport view = null;
            if (map != null && sourceBounds != null) {
                try { view = NativeGridMapping.ToReference(imageBounds, sourceBounds); }
                catch (ArgumentException) { }
            }
            var displayedMap = view == null ? null : map;
            if (Canvas.Map != displayedMap) Canvas.ClearHover();
            Canvas.Map = displayedMap;
            Canvas.View = view ?? new Viewport();
            heading.Text = displayedMap == null ? "副屏战术地图" : displayedMap.Name;
            status.Text = displayedMap == null ? message : "当前战斗 · 静态战术参考";
            waiting.Visibility = Visibility.Collapsed;
            Canvas.InvalidateVisual();
        }

        void Clear(string message) {
            frameKey = null;
            imageBounds = null;
            Canvas.ClearHover();
            Canvas.Map = null;
            Canvas.BackgroundImage = null;
            heading.Text = "副屏战术地图";
            status.Text = message;
            waiting.Visibility = Visibility.Visible;
            Canvas.InvalidateVisual();
        }

        void LoadImage(MapSnapshot snapshot) {
            BitmapSource image;
            using (var stream = new MemoryStream(snapshot.Image)) {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                image = bitmap;
            }

            int width = image.PixelWidth, height = image.PixelHeight;
            var grid = snapshot.GridView ?? new Viewport();
            int left = Pixel(grid.X0, width), top = Pixel(grid.Y0, height);
            int right = Pixel(grid.X1, width), bottom = Pixel(grid.Y1, height);
            // A grid outside the image has no usable crop. Use the entire live map.
            if (right <= left || bottom <= top) {
                left = top = 0;
                right = width;
                bottom = height;
            }
            imageBounds = new Viewport {
                X0 = (double)left / width, Y0 = (double)top / height,
                X1 = (double)right / width, Y1 = (double)bottom / height
            };
            if (left != 0 || top != 0 || right != width || bottom != height) {
                image = new CroppedBitmap(image, new Int32Rect(left, top, right - left, bottom - top));
                image.Freeze();
            }
            double scale = 800.0 / Math.Max(image.PixelWidth, image.PixelHeight);
            Canvas.Width = image.PixelWidth * scale;
            Canvas.Height = image.PixelHeight * scale;
            Canvas.BackgroundImage = image;
        }

        static int Pixel(double fraction, int size) {
            return (int)Math.Round(Math.Max(0, Math.Min(1, fraction)) * size);
        }
    }
}
