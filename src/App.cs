using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WtTactical {
    public static class Program {
        [STAThread] public static int Main(string[] args){
            Native.SetProcessDPIAware();string root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);if(!File.Exists(Path.Combine(root,"data","maps.json")))root=Directory.GetParent(root).FullName;
            bool created;using(var mutex=new Mutex(true,"Local\\WtTacticalOverlay-Portable-V041",out created)){
                if(!created){MessageBox.Show("战术浮层已经运行，请使用 Ctrl+Alt+F10 显示控制台。退出快捷键：Ctrl+Alt+F11。","已在运行");return 2;}
                try{var library=JsonStore.Load<MapLibrary>(Path.Combine(root,"data","maps.json"));Validation.Library(library);var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
                    app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e){e.Handled=true;File.AppendAllText(Path.Combine(root,"error.log"),DateTimeOffset.Now+" "+e.Exception+Environment.NewLine);MessageBox.Show("程序出现错误，已停止浮层。详情见 error.log。","战术浮层");app.Shutdown(1);};
                    bool probe=args.Length==2&&args[0]=="--auto-probe";var panel=new ControlPanel(library,root,args.Length==0||probe);app.MainWindow=panel;
                    if(probe){app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Dispatcher.BeginInvoke(new Action(async delegate{try{await panel.RefreshAutoMap();JsonStore.Save(args[1],panel.RecognitionDiagnostic());}finally{panel.Close();app.Shutdown();}}));app.Run();return 0;}
                    if(args.Length==2&&args[0]=="--render-preview"){
                        var content=(FrameworkElement)panel.Content;content.Measure(new Size(1030,830));content.Arrange(new Rect(0,0,1030,830));content.UpdateLayout();var image=new RenderTargetBitmap(1030,830,96,96,PixelFormats.Pbgra32);image.Render(content);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(args[1]))png.Save(output);panel.Close();return 0;
                    }
                    if(args.Length==2&&args[0]=="--smoke-test")panel.Loaded+=delegate{var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(800)};timer.Tick+=delegate{timer.Stop();try{var result=panel.Smoke();JsonStore.Save(args[1],result);}finally{panel.Close();}};timer.Start();};
                    app.Run(panel);return 0;
                }catch(Exception e){try{File.AppendAllText(Path.Combine(root,"error.log"),DateTimeOffset.Now+" "+e+Environment.NewLine);}catch{}MessageBox.Show(e.Message,"无法启动战术浮层");return 1;}
            }
        }
    }
}
