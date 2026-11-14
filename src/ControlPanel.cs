using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WtTactical {
    public class ControlPanel : Window {
        readonly MapLibrary library;
        readonly string root,userFile;
        readonly RecognitionCatalog recognition;
        UserData user=new UserData();
        bool saveAllowed=true,requested,manualConfirmed,polling,closed;
        bool sessionStarted, externalStartup;
        ExternalMapWindow externalWindow;
        readonly ActivationGate liveGate=new ActivationGate();
        readonly List<double[]> draftRoute=new List<double[]>();
        readonly CheckBox drawRoute=Check("连续点选绘制本人路线",false);
        MapSnapshot snapshot;
        string armedSession;
        bool autoSelecting;
        string matchedImageKey,previewImageKey;
        RecognitionResult imageMatch;
        Profile adoptedProfile;
        bool cornerArmed,calibrationPreview;
        Profile capturedScreen;
        readonly DispatcherTimer visibilityTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
        readonly DispatcherTimer pollTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        public readonly OverlayWindow Overlay=new OverlayWindow();
        readonly TacticalCanvas preview=new TacticalCanvas();
        readonly ComboBox mapSelect=new ComboBox{MinHeight=32,Margin=new Thickness(0,5,0,8)};
        readonly ComboBox contextSelect=new ComboBox{MinHeight=28,Margin=new Thickness(0,2,0,3)};
        readonly ComboBox perspective=new ComboBox{ItemsSource=new[]{"双方参考","资料红方出生视角","资料蓝方出生视角"},SelectedIndex=0,MinHeight=28,Margin=new Thickness(0,2,0,4)};
        readonly CheckBox conditional=Check("条件性风险",true),strategic=Check("战略区域",false);
        readonly TextBlock tactics=Text("",11);
        readonly TextBox search=new TextBox{Height=29,ToolTip="输入中文/英文地图名称搜索",Margin=new Thickness(0,0,0,1)};
        readonly ListBox markerList=new ListBox{Height=115,Margin=new Thickness(0,8,0,0)};
        readonly TextBlock mapInfo=Text("",12),status=Text("尚未启用浮层",12),liveStatus=Text("8111 未连接；可使用手动模式",11),detail=Text("选择一个标记查看说明。",12);
        readonly CheckBox pins=Check("点位",true),routes=Check("路线",true),danger=Check("危险区",true),labels=Check("编号",true),edit=Check("浮层可交互（取消鼠标穿透）",false),always=Check("校准预览：切出游戏时仍显示",false),live=Check("自动识别地图（8111）",true);
        readonly TextBox left=Input("0"),top=Input("0"),width=Input("320"),height=Input("320"),x0=Input("0"),y0=Input("0"),x1=Input("1"),y1=Input("1");
        readonly TextBox markerX=Input("0.5"),markerY=Input("0.5"),radius=Input("0.035"),title=Input("自定义危险区"),note=new TextBox{Text="",Height=44,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,4)};
        readonly ComboBox kind=new ComboBox{ItemsSource=new[]{new KindChoice("危险区域","danger"),new KindChoice("狙击/远射点","sniper"),new KindChoice("观察/架线点","overwatch"),new KindChoice("卖头位置","hulldown"),new KindChoice("普通战术点","position")},SelectedIndex=0,Height=28};
        IntPtr handle;
        readonly List<int> registered=new List<int>();
        public ControlPanel(MapLibrary library,string root,bool connectLive=true){
            this.library=library;this.root=root;userFile=Path.Combine(root,"user-data","annotations.json");
            string catalogFile=Path.Combine(root,"data","recognition.json");recognition=File.Exists(catalogFile)?JsonStore.Load<RecognitionCatalog>(catalogFile):new RecognitionCatalog();
            live.IsChecked=connectLive;liveGate.SetEnabled(connectLive);
            Title="WT 战术地图台 V0.5.1";Width=1080;Height=860;MinWidth=950;MinHeight=700;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=new SolidColorBrush(Color.FromRgb(13,21,31));Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;
            ApplyTheme();LoadUser();Content=BuildLayout();foreach(var input in new[]{x0,y0,x1,y1})input.IsReadOnly=live.IsChecked==true;
            mapSelect.ItemsSource=BrowsableMaps().OrderBy(m=>m.Status=="no_tactical_source").ThenBy(m=>m.English).ToList();
            mapSelect.SelectionChanged+=delegate{SelectMap();};
            mapSelect.SelectedItem=library.Maps.FirstOrDefault(m=>m.Id=="abandonedfactory")??library.Maps.First();
            markerList.SelectionChanged+=delegate{var item=markerList.SelectedItem as AnnotationItem;if(item!=null)detail.Text=item.Title+"\n"+item.Note;};
            preview.Pick+=delegate(double x,double y){markerX.Text=F(x);markerY.Text=F(y);if(drawRoute.IsChecked==true){draftRoute.Add(new[]{x,y});preview.DraftRoute=draftRoute;preview.InvalidateVisual();status.Text="路线草稿 "+draftRoute.Count+" 个点；至少两点后点击保存路线。";}else status.Text="已拾取源图坐标；填写说明后点击“添加标注”。";};
            foreach(var c in new[]{pins,routes,danger,labels,conditional,strategic})c.Click+=delegate{RefreshCanvases();RefreshList();};
            contextSelect.SelectionChanged+=delegate{RefreshCanvases();RefreshList();};perspective.SelectionChanged+=delegate{RefreshCanvases();RefreshList();};
            edit.Click+=delegate{Overlay.SetPassthrough(edit.IsChecked!=true);UpdateVisibility();};
            live.Click+=delegate{liveGate.SetEnabled(live.IsChecked==true);foreach(var input in new[]{x0,y0,x1,y1})input.IsReadOnly=live.IsChecked==true;requested=false;armedSession=null;snapshot=null;if(live.IsChecked==true){pollTimer.Start();Poll();}else{pollTimer.Stop();liveStatus.Text="手动模式：换图/缩放后请停用并重新校准。";}UpdateVisibility();RefreshExternalMap();};
            SourceInitialized+=delegate{handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);RegisterKeys();};
            Loaded+=delegate{StartSession();};
            visibilityTimer.Tick+=delegate{UpdateVisibility();};pollTimer.Tick+=delegate{Poll();};
            Closing+=delegate{closed=true;liveGate.Close();visibilityTimer.Stop();pollTimer.Stop();foreach(int id in registered)Native.UnregisterHotKey(handle,id);if(externalWindow!=null)externalWindow.Close();Overlay.Close();};
        }

        void StartSession(){
            if(sessionStarted)return;
            sessionStarted=true;
            new WindowInteropHelper(this).EnsureHandle();
            new WindowInteropHelper(Overlay).EnsureHandle();
            visibilityTimer.Start();
            if(live.IsChecked==true){pollTimer.Start();Poll();}
        }

        public void OpenExternalMap(bool standalone=false){
            if(standalone)externalStartup=true;
            if(externalWindow==null){
                externalWindow=new ExternalMapWindow();
                externalWindow.PlaceOnSecondary();
                externalWindow.ControlRequested+=ShowControlPanel;
                externalWindow.Closed+=delegate{
                    externalWindow=null;
                    if(externalStartup&&!IsVisible&&!closed)Close();
                };
            }
            StartSession();
            RefreshExternalMap();
            externalWindow.Show();
            if(externalWindow.WindowState==WindowState.Minimized)externalWindow.WindowState=WindowState.Normal;
            externalWindow.Activate();
        }

        void ShowControlPanel(){
            Show();
            WindowState=WindowState.Normal;
            Activate();
        }
        static TextBlock Text(string s,double size){return new TextBlock{Text=s,FontSize=size,TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(192,208,222)),Margin=new Thickness(0,3,0,4)};}
        void ApplyTheme(){
            var inputStyle=new Style(typeof(TextBox));inputStyle.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(27,41,56))));inputStyle.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));inputStyle.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(68,90,109))));Resources.Add(typeof(TextBox),inputStyle);
            var buttonStyle=new Style(typeof(Button));buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(37,57,75))));buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(77,108,129))));
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);template.VisualTree=border;buttonStyle.Setters.Add(new Setter(Control.TemplateProperty,template));var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(48,80,102))));buttonStyle.Triggers.Add(hover);Resources.Add(typeof(Button),buttonStyle);
            var listStyle=new Style(typeof(ListBox));listStyle.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(23,35,48))));listStyle.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));listStyle.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(48,67,85))));Resources.Add(typeof(ListBox),listStyle);
        }
        static TextBox Input(string value){return new TextBox{Text=value,Height=26,Margin=new Thickness(0,2,5,4),Padding=new Thickness(5,2,5,2)};}
        static CheckBox Check(string s,bool value){return new CheckBox{Content=s,IsChecked=value,Foreground=Brushes.White,Margin=new Thickness(0,3,12,5)};}
        Button Button(string s,Action a){var b=new Button{Content=s,Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,3,7,4),MinHeight=29};b.Click+=delegate{Guard(a);};return b;}
        static string F(double n){return n.ToString("0.########",CultureInfo.InvariantCulture);}
        static double Number(TextBox b){double n;if(!double.TryParse(b.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||!Viewport.Finite(n))throw new ArgumentException("请输入有效数字，使用英文小数点。");return n;}
        void Guard(Action action){try{action();}catch(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Information);}}
        UIElement BuildLayout(){
            var main=new Grid{Margin=new Thickness(22,16,22,14),Background=Background};main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var head=new StackPanel();head.Children.Add(new TextBlock{Text="战术地图台",FontSize=27,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White});head.Children.Add(Text("WAR THUNDER  /  小地图战术参考浮层",11));head.Children.Add(Text("社区点位 · 非实时敌情 · 先校准再启用",12));Grid.SetRow(head,0);main.Children.Add(head);
            var body=new Grid{Margin=new Thickness(0,12,0,8)};body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(350)});Grid.SetRow(body,1);main.Children.Add(body);
            var mapSide=new Grid{Margin=new Thickness(0,0,20,0)};mapSide.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});mapSide.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});mapSide.RowDefinitions.Add(new RowDefinition());mapSide.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});mapSide.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(mapSide);
            search.TextChanged+=delegate{string q=search.Text.Trim();mapSelect.ItemsSource=BrowsableMaps().Where(m=>(m.Name+" "+m.English+" "+m.Id).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(m=>m.Status=="no_tactical_source").ThenBy(m=>m.English).ToList();};
            var selector=new StackPanel();selector.Children.Add(Text("地图搜索 / 选择",12));selector.Children.Add(search);selector.Children.Add(mapSelect);Grid.SetRow(selector,0);mapSide.Children.Add(selector);
            Grid.SetRow(mapInfo,1);mapSide.Children.Add(mapInfo);
            var viewbox=new Viewbox{Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=preview};preview.Width=preview.Height=520;Grid.SetRow(viewbox,2);mapSide.Children.Add(viewbox);
            var legend=new StackPanel();legend.Children.Add(Text("红色填充：危险区　红虚线带：危险通路　△：风险点（无射程含义）",11));legend.Children.Add(Text("黄点：远射　蓝点：观察　绿线：掩护　橙线：进攻　黄线：侧翼",11));var sourceButtons=new WrapPanel();sourceButtons.Children.Add(Button("打开副屏地图",delegate{OpenExternalMap();}));sourceButtons.Children.Add(Button("原始战术来源 ↗",OpenGuide));sourceButtons.Children.Add(Button("载入参考底图",delegate{LoadReference();}));legend.Children.Add(sourceButtons);Grid.SetRow(legend,3);mapSide.Children.Add(legend);
            var list=new StackPanel();list.Children.Add(markerList);list.Children.Add(new ScrollViewer{Content=detail,Height=96,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetRow(list,4);mapSide.Children.Add(list);
            var options=new StackPanel();options.Children.Add(Text("01  显示图层",15));var filters=new WrapPanel();foreach(var c in new[]{pins,routes,danger,labels})filters.Children.Add(c);options.Children.Add(filters);options.Children.Add(edit);options.Children.Add(always);
            var riskFilters=new WrapPanel();riskFilters.Children.Add(conditional);riskFilters.Children.Add(strategic);options.Children.Add(riskFilters);options.Children.Add(Text("战术方案（自动识别地形；默认方案不代表已识别本局模式）",11));options.Children.Add(contextSelect);options.Children.Add(perspective);options.Children.Add(Text("红/蓝是资料出生视角，非实时敌我。双方参考同时展示各自适用位置。",10));options.Children.Add(tactics);
            options.Children.Add(Text("02  贴合原生小地图",15));options.Children.Add(Text("Ctrl+Alt+1 指向地图左上角；Ctrl+Alt+2 指向右下角。框选实际地图内容，不含边框/标签。",11));options.Children.Add(Row(new[]{"左","上","宽","高"},new[]{left,top,width,height}));
            options.Children.Add(Button("查看 / 隐藏校准边框",ToggleCalibrationFrame));options.Children.Add(Text("自动模式按当前网格范围换算视野，无需手填以下裁切值。默认矩形不算完成校准，请用两个角框选。",11));options.Children.Add(Row(new[]{"X0","Y0","X1","Y1"},new[]{x0,y0,x1,y1}));options.Children.Add(Button("保存对齐配置",ApplyProfile));
            options.Children.Add(Text("改变原生小地图大小/手动缩放后，请检查校准边框与网格是否仍重合。",11));
            options.Children.Add(live);options.Children.Add(liveStatus);options.Children.Add(Text("自动识别地图、换算当前网格视野；同一显示器复用已框选的小地图位置。不确定时隐藏。",11));options.Children.Add(Button("保存当前校准",BindCurrent));
            var actions=new WrapPanel();actions.Children.Add(Button("启用 / 停用浮层",Toggle));actions.Children.Add(Button("立即隐藏",delegate{requested=false;UpdateVisibility();}));options.Children.Add(actions);
            options.Children.Add(Text("03  可选个人笔记（战术库已内置）",15));options.Children.Add(kind);options.Children.Add(Row(new[]{"X","Y","区域半径"},new[]{markerX,markerY,radius}));options.Children.Add(title);note.ToolTip="点位说明（可留空）";options.Children.Add(note);var editButtons=new WrapPanel();editButtons.Children.Add(Button("添加标注",AddMarker));editButtons.Children.Add(Button("删除选中的本人标注",DeleteMarker));options.Children.Add(editButtons);
            options.Children.Add(Text("区域半径是源图比例，不能视为火力射程。预览图点选位置后，填写名称和说明再添加。",10));
            options.Children.Add(drawRoute);var routeButtons=new WrapPanel();routeButtons.Children.Add(Button("保存本人路线",AddRoute));routeButtons.Children.Add(Button("清空草稿",delegate{draftRoute.Clear();preview.InvalidateVisual();}));options.Children.Add(routeButtons);options.Children.Add(Text("勾选后在预览图依次点选路径；名称和说明沿用上方输入框。本人路线显示为紫色。",10));
            var scroll=new ScrollViewer{Content=options,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(scroll,1);body.Children.Add(scroll);
            var foot=new StackPanel();foot.Children.Add(status);foot.Children.Add(Text("Ctrl+Alt+F8 开关　F9 穿透/交互　F10 控制台　F11 退出（均需 Ctrl+Alt）",11));Grid.SetRow(foot,2);main.Children.Add(foot);return main;
        }
        static UIElement Row(string[] names,TextBox[] fields){var g=new Grid();for(int i=0;i<fields.Length;i++){g.ColumnDefinitions.Add(new ColumnDefinition());var s=new StackPanel();s.Children.Add(Text(names[i],10));s.Children.Add(fields[i]);Grid.SetColumn(s,i);g.Children.Add(s);}return g;}
        MapDefinition Current {get{return mapSelect.SelectedItem as MapDefinition;}}
        void LoadUser(){if(!File.Exists(userFile))return;try{user=JsonStore.Load<UserData>(userFile);Validation.User(user,library);}
            catch(Exception e){saveAllowed=false;user=new UserData();status.Text="用户配置损坏，保留原文件；本次禁止写入。";Dispatcher.BeginInvoke(new Action(delegate{MessageBox.Show(this,e.Message+"\n原文件已保留。请关闭程序后检查 user-data/annotations.json 及 .bak 备份。","配置读取失败");}));}}
        void Save(){if(!saveAllowed)throw new InvalidOperationException("配置损坏，禁止覆盖原文件。请关闭后修复或恢复 .bak。");JsonStore.Save(userFile,user);}
        IEnumerable<MapDefinition> BrowsableMaps(){return library.Maps.Where(m=>m.Status!="compatibility_alias"||user.Markers.Any(p=>p.MapId==m.Id)||user.Routes.Any(r=>r.MapId==m.Id));}
        Binding KnownBinding(MapSnapshot current){var b=BindingGate.Match(current.Fingerprint,user.Bindings);return b!=null&&library.Maps.Any(m=>m.Id==b.MapId&&m.Status!="compatibility_alias")?b:null;}
        void SelectMap(){if(!autoSelecting)requested=false;manualConfirmed=false;armedSession=null;draftRoute.Clear();previewImageKey=null;preview.BackgroundImage=null;if(Current==null){preview.Map=null;Overlay.Canvas.Map=null;markerList.ItemsSource=null;mapInfo.Text="没有匹配地图";RefreshCanvases();UpdateVisibility();return;}
            Profile p=SavedProfile(Current.Id,snapshot,true);if(p==null&&CalibrationPolicy.CanReuse(user.ScreenCalibration,DisplayStamp())){p=CalibrationPolicy.ScreenCopy(user.ScreenCalibration);p.MapId=Current.Id;}if(p==null)p=new Profile{MapId=Current.Id,Left=Math.Max(0,SystemParameters.PrimaryScreenWidth-350),Top=Math.Max(0,SystemParameters.PrimaryScreenHeight-360)};
            AdoptProfileInputs(p);
            mapInfo.Text=Current.Status=="no_tactical_source"?"此关卡资料待补，当前不显示推测点位。":"内置参考 · "+Current.Pins.Count+" 点 / "+Current.Routes.Count+" 路线 / "+Current.Zones.Count(z=>z.Kind!="strategic")+" 风险标记 · 非实时敌情";
            contextSelect.ItemsSource=Current.Contexts??new[]{"domination-1"};contextSelect.SelectedItem=Current.DefaultContext??"domination-1";if(contextSelect.SelectedIndex<0)contextSelect.SelectedIndex=0;tactics.Text=string.Join("\n",Current.Tactics??new List<string>());
            preview.Map=Overlay.Canvas.Map=Current;preview.View=Current.CoordinateBounds==null?new Viewport():Current.CoordinateBounds.Copy();RefreshCanvases();RefreshList();UpdateVisibility();}
        bool InContext(string value){return TacticalPresentation.InContext(value,contextSelect.SelectedItem as string,perspective.SelectedIndex==1?"Red":perspective.SelectedIndex==2?"Blue":null);}
        void RefreshList(){if(Current==null)return;var items=new List<AnnotationItem>();int i=0;foreach(var p in Current.Pins.Concat(CustomPins())){i++;if(!InContext(p.Context))continue;items.Add(new AnnotationItem{Title=i+"  "+p.Title,Note=TacticalPresentation.Description(p.Title,p.Note,p.Conditions,p.Evidence,p.Source),Pin=p});}foreach(var z in Current.Zones.Where(z=>InContext(z.Context)&&(conditional.IsChecked==true||z.Evidence!="source_inference")))items.Add(new AnnotationItem{Title=(z.Kind=="strategic"?"◇ ":"⚠ ")+z.Title,Note=TacticalPresentation.Description(z.Title,z.Note,z.Conditions,z.Evidence,z.Source),Zone=z});foreach(var r in Current.Routes.Concat(CustomRoutes()).Where(r=>InContext(r.Context)))items.Add(new AnnotationItem{Title="↳ "+r.Title,Note=TacticalPresentation.Description(r.Title,r.Note,r.Conditions,null,r.Source),Route=r});markerList.ItemsSource=items;}
        List<Pin> CustomPins(){return Current==null?new List<Pin>():user.Markers.Where(p=>p.MapId==Current.Id).Select(p=>p.Marker).ToList();}
        List<Route> CustomRoutes(){return Current==null?new List<Route>():user.Routes.Where(p=>p.MapId==Current.Id).Select(p=>p.Path).ToList();}
        void RefreshCanvases(){
            ApplyLayerSettings(preview);
            ApplyLayerSettings(Overlay.Canvas);
            RefreshExternalMap();
        }

        void ApplyLayerSettings(TacticalCanvas canvas){
            canvas.Custom=canvas.Map==null?new List<Pin>():CustomPins();
            canvas.CustomRoutes=canvas.Map==null?new List<Route>():CustomRoutes();
            canvas.ShowPins=pins.IsChecked==true;
            canvas.ShowRoutes=routes.IsChecked==true;
            canvas.ShowDanger=danger.IsChecked==true;
            canvas.ShowConditional=conditional.IsChecked==true;
            canvas.ShowStrategic=strategic.IsChecked==true;
            canvas.ActiveContext=contextSelect.SelectedItem as string;
            canvas.Perspective=perspective.SelectedIndex==1?"Red":perspective.SelectedIndex==2?"Blue":null;
            canvas.Labels=labels.IsChecked==true;
            canvas.InvalidateVisual();
        }

        void RefreshExternalMap(){
            if(externalWindow==null||closed)return;
            var current=live.IsChecked==true?snapshot:null;
            var map=Current;
            if(current==null||map==null||ResolvedMapId(current)!=map.Id)map=null;
            var bounds=map==null?null:SourceBoundsFor(current,map.Id);
            string message="当前战斗底图 · 等待战术资料定位";
            if(current==null)message=live.IsChecked==true?"等待读取游戏地图":"请在控制台开启自动识别地图";
            externalWindow.Present(current,map,bounds,message);
            ApplyLayerSettings(externalWindow.Canvas);
        }
        string DisplayStamp(){double scale=Native.GetDpiForSystem()/96.0;return string.Join("|",SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight,scale,scale);}
        void AdoptProfileInputs(Profile p){if(object.ReferenceEquals(adoptedProfile,p))return;left.Text=F(p.Left);top.Text=F(p.Top);width.Text=F(p.Width);height.Text=F(p.Height);x0.Text=F(p.View.X0);y0.Text=F(p.View.Y0);x1.Text=F(p.View.X1);y1.Text=F(p.View.Y1);adoptedProfile=p;capturedScreen=null;}
        Profile SavedProfile(string mapId,MapSnapshot map,bool fallback=false){var all=user.Profiles.Where(p=>p.MapId==mapId).Reverse();var exact=map==null?null:all.FirstOrDefault(p=>CalibrationPolicy.CanReuse(p,DisplayStamp(),map.Fingerprint));return exact??(fallback?all.FirstOrDefault():null);}
        void StoreProfile(Profile p){user.Profiles.RemoveAll(a=>a.MapId==p.MapId&&a.DisplayStamp==p.DisplayStamp&&a.MapLayoutStamp==p.MapLayoutStamp);user.Profiles.Add(p);}
        Profile ReadScreenProfile(){if(Current==null)throw new ArgumentException("请先选择地图。");var p=new Profile{MapId=Current.Id,DisplayStamp=DisplayStamp(),MapLayoutStamp=live.IsChecked==true&&snapshot!=null?snapshot.Fingerprint:null,Left=Number(left),Top=Number(top),Width=Number(width),Height=Number(height)};if(p.Width<80||p.Height<80||p.Width>3000||p.Height>3000)throw new ArgumentException("浮层尺寸需在 80–3000 之间。");p.PositionVerified=CalibrationPolicy.SameScreen(p,capturedScreen)||(adoptedProfile!=null&&adoptedProfile.PositionVerified&&CalibrationPolicy.SameScreen(p,adoptedProfile));return p;}
        Viewport SourceBoundsFor(MapSnapshot current,string mapId){if(current==null)return null;if(matchedImageKey==current.ImageFingerprint&&imageMatch!=null&&imageMatch.Accepted&&imageMatch.Match.MapId==mapId){var m=imageMatch.Match;return new Viewport{X0=m.X,Y0=m.Y,X1=m.X+m.Width,Y1=m.Y+m.Height};}var b=BindingGate.Match(current.Fingerprint,user.Bindings);return b!=null&&b.MapId==mapId?b.SourceBounds:null;}
        Viewport AutomaticView(MapSnapshot current,string mapId){return NativeGridMapping.ToReference(current==null?null:current.GridView,SourceBoundsFor(current,mapId));}
        string AutomaticReadiness(){if(Current==null)return "等待选择地图。";if(snapshot==null)return "等待游戏提供有效战斗地图，程序会自动重试。";if(snapshot.GridView==null)return "等待原生小地图网格。";if(ResolvedMapId(snapshot)!=Current.Id)return "等待识别当前地图并切换对应资料。";if(SourceBoundsFor(snapshot,Current.Id)==null)return "等待资料底图定位。";try{AutomaticView(snapshot,Current.Id);return null;}catch(ArgumentException){return "当前网格与底图定位尚不可用，等待重新识别。";}}
        Profile ReadProfile(){var p=ReadScreenProfile();if(live.IsChecked==true){string pending=AutomaticReadiness();if(pending!=null)throw new InvalidOperationException(pending+" 当前未修改地图绑定。");p.View=AutomaticView(snapshot,p.MapId);}else p.View=new Viewport{X0=Number(x0),Y0=Number(y0),X1=Number(x1),Y1=Number(y1)};p.View.Validate();return p;}
        void ApplyProfile(){bool wasRequested=requested;requested=false;armedSession=null;var p=ReadScreenProfile();if(!p.PositionVerified)throw new InvalidOperationException("默认矩形尚未校准。请先用 Ctrl+Alt+1 和 Ctrl+Alt+2 框选实际小地图内容。此步骤只需在位置/大小变化后做一次。");string pending=live.IsChecked==true?AutomaticReadiness():null;if(pending!=null){user.ScreenCalibration=CalibrationPolicy.ScreenCopy(p);Save();AdoptProfileInputs(p);calibrationPreview=false;Overlay.Canvas.OutlineOnly=false;requested=wasRequested;UpdateVisibility();status.Text="小地图屏幕位置已保存；"+pending;return;}p=ReadProfile();StoreProfile(p);user.ScreenCalibration=CalibrationPolicy.ScreenCopy(p);Save();AdoptProfileInputs(p);Overlay.Apply(p);calibrationPreview=false;Overlay.Canvas.OutlineOnly=false;if(live.IsChecked==true&&snapshot!=null&&ResolvedMapId(snapshot)==p.MapId)Arm(snapshot,p);status.Text="小地图位置已保存；视野按当前网格自动换算。";UpdateVisibility();}
        void Toggle(){if(requested){requested=false;UpdateVisibility();status.Text="浮层已停用。";return;}var screen=ReadScreenProfile();if(!screen.PositionVerified)throw new InvalidOperationException("小地图位置尚未框选，不能使用默认矩形。请先用 Ctrl+Alt+1 和 Ctrl+Alt+2 校准。");if(live.IsChecked==true&&AutomaticReadiness()!=null){requested=true;armedSession=null;calibrationPreview=false;UpdateVisibility();return;}var p=ReadProfile();if(live.IsChecked==true){if(snapshot==null||!BindingGate.CanDisplay(snapshot.Fingerprint,snapshot.Session,armedSession,Current.Id,p.View,user.Bindings))throw new InvalidOperationException("等待自动识别；首次使用该地图请先保存校准。");if(!CalibrationPolicy.SameGeometry(p,SavedProfile(Current.Id,snapshot)))throw new InvalidOperationException("校准输入已更改，请先保存对齐配置，再启用浮层。");}
            else if(!manualConfirmed){if(MessageBox.Show(this,"手动模式不会自动识别换图。请确认所选地图、出生布局和缩放一致；换图或缩放前关闭浮层。\n现在启用？","确认手动地图",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;manualConfirmed=true;}
            Overlay.Apply(p);requested=true;calibrationPreview=false;UpdateVisibility();}
        void UpdateVisibility(){if(closed)return;if(calibrationPreview){Overlay.Canvas.OutlineOnly=true;if(!Overlay.IsVisible)Overlay.Show();return;}Overlay.Canvas.OutlineOnly=false;bool valid=Current!=null&&(live.IsChecked==true?(snapshot!=null&&SavedProfile(Current.Id,snapshot)!=null&&BindingGate.CanDisplay(snapshot.Fingerprint,snapshot.Session,armedSession,Current.Id,Overlay.Canvas.View,user.Bindings)):manualConfirmed);
            bool visible=requested&&valid&&(always.IsChecked==true||edit.IsChecked==true||Native.GameForeground());if(visible){if(!Overlay.IsVisible)Overlay.Show();}else if(Overlay.IsVisible)Overlay.Hide();
            if(requested)status.Text=visible?(Overlay.Passthrough?"浮层显示中 · 鼠标穿透":"浮层交互中 · 悬浮标记可查看说明"):(valid?"浮层待命：游戏处于前台时显示":("已启用，等待自动对齐；浮层暂时隐藏。"+(live.IsChecked==true?AutomaticReadiness()??"等待校准就绪。":"等待地图确认。")));}
        static string SessionKey(MapSnapshot s){return s.Fingerprint+":"+s.Session;}
        async void Poll(){await RefreshAutoMap();}
        string ResolvedMapId(MapSnapshot current){var binding=KnownBinding(current);if(binding!=null)return binding.MapId;return matchedImageKey==current.ImageFingerprint&&imageMatch!=null&&imageMatch.Accepted?imageMatch.Match.MapId:null;}
        Profile AutomaticProfile(MapSnapshot current,string mapId){Viewport view;try{view=AutomaticView(current,mapId);}catch(ArgumentException){return null;}var old=SavedProfile(mapId,current);var screen=CalibrationPolicy.CanReuse(user.ScreenCalibration,DisplayStamp())?user.ScreenCalibration:user.Profiles.AsEnumerable().Reverse().FirstOrDefault(p=>CalibrationPolicy.CanReuse(p,DisplayStamp()));if(screen==null)return null;
            var next=new Profile{MapId=mapId,DisplayStamp=DisplayStamp(),MapLayoutStamp=current.Fingerprint,PositionVerified=true,Left=screen.Left,Top=screen.Top,Width=screen.Width,Height=screen.Height,View=view};if(old!=null&&CalibrationPolicy.SameGeometry(old,next))return old;StoreProfile(next);Save();return next;}
        void Arm(MapSnapshot current,Profile profile){if(!CalibrationPolicy.CanReuse(profile,DisplayStamp(),current.Fingerprint))return;var source=SourceBoundsFor(current,profile.MapId);var existing=BindingGate.Match(current.Fingerprint,user.Bindings);if(existing==null||existing.MapId!=profile.MapId||!BindingGate.CanDisplay(current.Fingerprint,current.Session,SessionKey(current),profile.MapId,profile.View,user.Bindings)){
                user.Bindings.RemoveAll(b=>b.Fingerprint==current.Fingerprint);user.Bindings.Add(new Binding{Fingerprint=current.Fingerprint,MapId=profile.MapId,View=profile.View.Copy(),SourceBounds=source});Save();}else if(existing.SourceBounds==null){existing.SourceBounds=source;Save();}AdoptProfileInputs(profile);Overlay.Apply(profile);armedSession=SessionKey(current);}
        public async Task RefreshAutoMap(){if(polling||closed||live.IsChecked!=true)return;polling=true;int generation=liveGate.Generation;try{
                var next=await LiveMap.Poll();if(!liveGate.Accept(generation))return;if(snapshot==null||snapshot.Fingerprint!=next.Fingerprint||snapshot.Session!=next.Session){snapshot=next;armedSession=null;UpdateVisibility();RefreshExternalMap();}else snapshot=next;
                var known=KnownBinding(next);if((known==null||known.SourceBounds==null)&&matchedImageKey!=next.ImageFingerprint){
                    liveStatus.Text="正在自动匹配地图…";var selectedCatalog=known==null?recognition:new RecognitionCatalog{Templates=recognition.Templates.Where(t=>t.MapId==known.MapId).ToList()};var result=await Task.Run(()=>MapRecognizer.Recognize(next.Image,selectedCatalog));if(!liveGate.Accept(generation))return;
                    var stable=await LiveMap.Poll();if(!liveGate.Accept(generation))return;if(stable.Fingerprint!=next.Fingerprint||stable.Session!=next.Session){snapshot=stable;armedSession=null;liveStatus.Text="地图正在切换，等待稳定后重新识别。";return;}
                    imageMatch=result;matchedImageKey=next.ImageFingerprint;
                }
                string mapId=ResolvedMapId(next);var map=library.Maps.FirstOrDefault(m=>m.Id==mapId);if(map==null){armedSession=null;liveStatus.Text=imageMatch!=null&&imageMatch.Match!=null?"未可靠识别（最佳候选 "+imageMatch.Match.MapId+"，"+(imageMatch.Match.Score*100).ToString("0.0")+"%）；浮层隐藏，可手动学习。":"尚无此地图的可靠匹配；浮层隐藏，可手动学习。";return;}
                if(Current!=map){autoSelecting=true;try{search.Text="";mapSelect.ItemsSource=BrowsableMaps().OrderBy(m=>m.Status=="no_tactical_source").ThenBy(m=>m.English).ToList();mapSelect.SelectedItem=map;}finally{autoSelecting=false;}}
                string confidence=matchedImageKey==next.ImageFingerprint&&imageMatch!=null&&imageMatch.Accepted?" · 匹配 "+(imageMatch.Match.Score*100).ToString("0.0")+"%":" · 已学习指纹";
                liveStatus.Text="已自动识别："+map.Name+confidence+UpdateNativeMap(next,mapId);
            }catch(Exception e){if(liveGate.Accept(generation)){snapshot=null;armedSession=null;liveStatus.Text="8111 暂不可用，浮层隐藏；连接恢复后自动重试。"+e.Message;}}finally{polling=false;UpdateVisibility();RefreshExternalMap();}}
        string UpdateNativeMap(MapSnapshot current,string mapId){
            try{
                var profile=AutomaticProfile(current,mapId);
                if(profile!=null)Arm(current,profile);
                else armedSession=null;
                SetMatchedPreview(current,mapId);
                return profile!=null?" · 位置已复用，网格范围已换算":" · 请用两个角框选小地图位置";
            }catch(Exception e){
                armedSession=null;
                return " · 主屏浮层暂无法对齐。"+e.Message;
            }
        }
        void SetMatchedPreview(MapSnapshot current,string mapId){string key=current.ImageFingerprint+":"+mapId+":"+current.Fingerprint;if(previewImageKey==key)return;var grid=current.GridView;Viewport view;try{view=AutomaticView(current,mapId);}catch(ArgumentException){return;}if(grid==null||grid.X0<0||grid.Y0<0||grid.X1>1||grid.Y1>1)return;using(var stream=new MemoryStream(current.Image)){
                var source=new BitmapImage();source.BeginInit();source.CacheOption=BitmapCacheOption.OnLoad;source.StreamSource=stream;source.EndInit();int px=Math.Max(0,(int)Math.Round(grid.X0*source.PixelWidth)),py=Math.Max(0,(int)Math.Round(grid.Y0*source.PixelHeight));int w=Math.Min(source.PixelWidth-px,(int)Math.Round((grid.X1-grid.X0)*source.PixelWidth)),h=Math.Min(source.PixelHeight-py,(int)Math.Round((grid.Y1-grid.Y0)*source.PixelHeight));if(w<1||h<1)return;var crop=new CroppedBitmap(source,new Int32Rect(px,py,w,h));crop.Freeze();preview.BackgroundImage=crop;preview.View=view;previewImageKey=key;preview.InvalidateVisual();}}
        public Dictionary<string,object> RecognitionDiagnostic(){return new Dictionary<string,object>{{"SelectedMap",Current==null?null:Current.Id},{"Connected",snapshot!=null},{"Recognized",snapshot!=null&&ResolvedMapId(snapshot)!=null},{"CalibrationReusable",snapshot!=null&&Current!=null&&SavedProfile(Current.Id,snapshot)!=null},{"RecognitionStatus",liveStatus.Text},{"OverlayRequested",requested},{"OverlayVisible",Overlay.IsVisible},{"Match",imageMatch},{"ReferenceTemplates",recognition.Templates.Count}};}
        void BindCurrent(){if(live.IsChecked!=true)throw new InvalidOperationException("请勾选自动识别地图。");if(AutomaticReadiness()!=null){ApplyProfile();return;}var p=ReadProfile();if(!p.PositionVerified)throw new InvalidOperationException("请先用两个角框选小地图；默认矩形不算校准。");var confirmed=snapshot;var source=SourceBoundsFor(confirmed,p.MapId);string confirmedKey=SessionKey(confirmed);int generation=liveGate.Generation;
            if(MessageBox.Show(this,"确认当前游戏地图就是“"+Current.Name+"”，并已校准本局原生小地图的裁切与缩放？","确认本局",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
            if(!liveGate.Accept(generation)||snapshot==null||SessionKey(snapshot)!=confirmedKey||AutomaticReadiness()!=null){armedSession=null;UpdateVisibility();status.Text="确认期间地图或连接已变化，未修改地图绑定；请等待重新识别。";return;}
            if(!BindingGate.CanConfirm(confirmedKey,snapshot==null?null:SessionKey(snapshot),p,ReadProfile(),liveGate.Accept(generation)))throw new InvalidOperationException("确认期间地图、模式或校准发生变化；请重新确认。");
            StoreProfile(p);user.ScreenCalibration=CalibrationPolicy.ScreenCopy(p);user.Bindings.RemoveAll(b=>b.Fingerprint==confirmed.Fingerprint);user.Bindings.Add(new Binding{Fingerprint=confirmed.Fingerprint,MapId=p.MapId,View=p.View.Copy(),SourceBounds=source});Save();AdoptProfileInputs(p);armedSession=confirmedKey;Overlay.Apply(p);status.Text="已保存位置，以后自动识别并换算当前网格。";}
        void AddMarker(){if(Current==null)throw new ArgumentException("请选择地图。");string selectedKind=((KindChoice)kind.SelectedItem).Key;var p=new Pin{Id="u-"+Guid.NewGuid().ToString("N"),Kind=selectedKind,Title=title.Text.Trim(),Note=note.Text.Trim(),X=Number(markerX),Y=Number(markerY),Radius=selectedKind=="danger"?Number(radius):0,Source="user"};Validation.Marker(p);if(p.Title.Length==0)throw new ArgumentException("请填写标注名称。");var record=new PinRecord{MapId=Current.Id,Marker=p};user.Markers.Add(record);try{Save();}catch{user.Markers.Remove(record);throw;}RefreshCanvases();RefreshList();status.Text="本人标注已保存。";}
        void AddRoute(){if(Current==null)throw new ArgumentException("请选择地图。");if(string.IsNullOrWhiteSpace(title.Text))throw new ArgumentException("请填写路线名称。");var path=new Route{Id="u-r-"+Guid.NewGuid().ToString("N"),Kind="route",Title=title.Text.Trim(),Note=note.Text.Trim(),Source="user",Points=draftRoute.Select(p=>new[]{p[0],p[1]}).ToArray()};Validation.ValidateRoute(path);var record=new RouteRecord{MapId=Current.Id,Path=path};user.Routes.Add(record);try{Save();}catch{user.Routes.Remove(record);throw;}draftRoute.Clear();RefreshCanvases();RefreshList();status.Text="本人路线已保存。";}
        void DeleteMarker(){var item=markerList.SelectedItem as AnnotationItem;if(item==null)throw new ArgumentException("请选择本人添加的标注或路线。");if(item.Route!=null&&item.Route.Source=="user"){var r=user.Routes.First(a=>a.Path==item.Route);user.Routes.Remove(r);try{Save();}catch{user.Routes.Add(r);throw;}}
            else{if(item.Pin==null||item.Pin.Source!="user")throw new ArgumentException("社区原资料保持只读，请选择本人标注或路线。");var record=user.Markers.First(a=>a.Marker==item.Pin);user.Markers.Remove(record);try{Save();}catch{user.Markers.Add(record);throw;}}RefreshCanvases();RefreshList();}
        void OpenGuide(){if(Current==null||string.IsNullOrEmpty(Current.GuideUrl))throw new ArgumentException("此关卡暂无来源指南。");Process.Start(new ProcessStartInfo(Current.GuideUrl){UseShellExecute=true});}
        async void LoadReference(){var map=Current;if(map==null||string.IsNullOrEmpty(map.ReferenceImageUrl)){status.Text="暂无参考底图。";return;}try{var uri=new Uri(map.ReferenceImageUrl);if(uri.Scheme!="https")throw new InvalidDataException("参考图来源不受支持。");if(uri.Host=="wardevops.github.io"){OpenGuide();status.Text="已打开该地图原始战术图；入局后控制台自动载入8111底图。";return;}if(uri.Host!="firebasestorage.googleapis.com"&&!(uri.Host=="storage.googleapis.com"&&uri.AbsolutePath.StartsWith("/wt-map-files/maps/",StringComparison.Ordinal)))throw new InvalidDataException("参考图来源不受支持。");
                using(var client=new System.Net.Http.HttpClient()){client.Timeout=TimeSpan.FromSeconds(15);var bytes=await client.GetByteArrayAsync(uri);if(closed||Current!=map)return;using(var stream=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();preview.BackgroundImage=image;preview.View=new Viewport();previewImageKey=null;preview.InvalidateVisual();}status.Text="参考底图已临时加载（不打包、不写入游戏目录）。";}}
            catch(Exception e){status.Text="参考底图加载失败，可打开原始指南查看："+e.Message;}}
        void RegisterKeys(){var failed=new List<int>();foreach(var pair in new[]{new[]{1,0x77},new[]{2,0x78},new[]{3,0x79},new[]{4,0x7A},new[]{5,0x31},new[]{6,0x32}}){if(Native.RegisterHotKey(handle,pair[0],0x4003,(uint)pair[1]))registered.Add(pair[0]);else failed.Add(pair[0]);}if(failed.Count>0)status.Text="部分快捷键被其他程序占用（"+string.Join(",",failed)+"），请用控制台操作。";}
        IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x312){handled=true;int id=wp.ToInt32();Guard(delegate{switch(id){case 1:Toggle();break;case 2:edit.IsChecked=edit.IsChecked!=true;Overlay.SetPassthrough(edit.IsChecked!=true);break;case 3:if(IsVisible&&WindowState!=WindowState.Minimized)Hide();else{Show();WindowState=WindowState.Normal;Activate();}break;case 4:Close();break;case 5:CaptureCorner(false);break;case 6:CaptureCorner(true);break;}});}return IntPtr.Zero;}
        void ToggleCalibrationFrame(){calibrationPreview=!calibrationPreview;if(calibrationPreview){requested=false;Overlay.Apply(ReadScreenProfile());Overlay.Canvas.OutlineOnly=true;Overlay.Canvas.InvalidateVisual();}UpdateVisibility();}
        void CaptureCorner(bool bottomRight){requested=false;armedSession=null;Native.POINT point;Native.GetCursorPos(out point);var source=PresentationSource.FromVisual(Overlay);var p=source.CompositionTarget.TransformFromDevice.Transform(new Point(point.X,point.Y));
            if(!bottomRight){cornerArmed=true;capturedScreen=null;left.Text=F(p.X);top.Text=F(p.Y);status.Text="已捕获左上角；移动指针到右下角后按 Ctrl+Alt+2。";}else{if(!cornerArmed)throw new InvalidOperationException("请先用 Ctrl+Alt+1 捕获左上角。");width.Text=F(p.X-Number(left));height.Text=F(p.Y-Number(top));capturedScreen=ReadScreenProfile();cornerArmed=false;Guard(ApplyProfile);}UpdateVisibility();}
        public Dictionary<string,object> Smoke(){
            var results=new Dictionary<string,object>();Overlay.Show();Overlay.SetPassthrough(true);var h=new WindowInteropHelper(Overlay).Handle;long pass=Native.GetWindowLongPtr(h,Native.GWL_EXSTYLE).ToInt64();
            results["LayeredWindow"]=(pass&0x80000)!=0;results["MousePassthrough"]=(pass&Native.WS_EX_TRANSPARENT)!=0;results["NoActivate"]=(pass&Native.WS_EX_NOACTIVATE)!=0;Overlay.SetPassthrough(false);results["InteractiveStyle"]=(Native.GetWindowLongPtr(h,Native.GWL_EXSTYLE).ToInt64()&Native.WS_EX_TRANSPARENT)==0;Overlay.SetPassthrough(true);Overlay.Hide();results["HideWorks"]=!Overlay.IsVisible;results["HotkeysRegistered"]=registered.Count;results["NoOverlayAtStartup"]=!requested;return results;
        }
        class AnnotationItem{public string Title,Note;public Pin Pin;public Route Route;public TacticalZone Zone;public override string ToString(){return Title;}}
        class KindChoice{public string Label,Key;public KindChoice(string label,string key){Label=label;Key=key;}public override string ToString(){return Label;}}
    }
}
