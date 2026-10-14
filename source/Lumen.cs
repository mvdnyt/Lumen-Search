using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Lumen")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
namespace Lumen {
 public class Result {
  public string Name { get; set; } public string Detail { get; set; } public string Glyph { get; set; }
  public string Target; public string Kind; public int Score;
 }
 public static class Search {
  static Result[] apps = new Result[0]; public static string IndexStatus = "Files use the Windows Search index"; public static string IndexDiagnostic="Not queried";static string fallbackReason="Local folders";
  public static int AppCount { get { return apps.Length; } }
  public static void LoadApps() {
   var found = new Dictionary<string,Result>(StringComparer.OrdinalIgnoreCase);
   string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) };
   foreach (string root in roots) Walk(root,found,0);
   Add(found,"Settings","ms-settings:","Windows settings"); Add(found,"Calculator","calc.exe","Windows app");
   Add(found,"Notepad","notepad.exe","Windows app"); Add(found,"File Explorer","explorer.exe","Windows app");
   apps = found.Values.OrderBy(x=>x.Name).ToArray();
  }
  static void Add(Dictionary<string,Result> d,string name,string path,string detail) {
   if (!d.ContainsKey(name)) d[name]=new Result {Name=name,Target=path,Detail=detail,Kind="app",Glyph="\u25A6"};
  }
  static void Walk(string root,Dictionary<string,Result> d,int depth) {
   if (depth>10 || !Directory.Exists(root)) return;
   try {
    foreach(string file in Directory.GetFiles(root,"*.lnk")) {
     string name=Path.GetFileNameWithoutExtension(file);
     if (name.IndexOf("uninstall",StringComparison.OrdinalIgnoreCase)<0) Add(d,name,file,"Application");
    }
    foreach(string sub in Directory.GetDirectories(root)) if ((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0) Walk(sub,d,depth+1);
   } catch (UnauthorizedAccessException) {} catch (IOException) {}
  }
  public static int Match(string name,string query) {
   name=name.ToLowerInvariant();query=query.ToLowerInvariant();
   if (name==query) return 1000;
   if(name.StartsWith(query)) return 850-query.Length;
   int at=name.IndexOf(query,StringComparison.Ordinal); if(at>=0) return 700-at;
   int pos=0,gaps=0,prev=-1;
   foreach(char c in query) {int i=name.IndexOf(c,pos);if(i<0)return 0;gaps+=i-prev-1;prev=i;pos=i+1;}
   return Math.Max(1,300-gaps*5-name.Length);
  }
  public static List<Result> Local(string query) {
   var results=new List<Result>(); query=query.Trim();
   if(query.Length==0) return results;
   double val;
   if(Calculator.Try(query,out val)) results.Add(new Result {Name=val.ToString("G12",CultureInfo.InvariantCulture),Detail=query+"  ·  Enter to copy",Kind="copy",Target=val.ToString("G12",CultureInfo.InvariantCulture),Glyph="=",Score=2000});
   string expanded=Environment.ExpandEnvironmentVariables(query.Trim('"'));
   if(Path.IsPathRooted(expanded) && (File.Exists(expanded)||Directory.Exists(expanded))) results.Add(new Result {Name=Path.GetFileName(expanded),Detail=expanded,Target=expanded,Kind="file",Glyph="\u25A1",Score=1500});
   results.AddRange(SettingsCatalog.Find(query));
   results.AddRange(apps.Select(x=>new Result {Name=x.Name,Target=x.Target,Detail=x.Detail,Glyph=x.Glyph,Kind=x.Kind,Score=Match(x.Name,query)}).Where(x=>x.Score>0).OrderByDescending(x=>x.Score).ThenBy(x=>x.Name).Take(7));
   Uri uri;
   if(Uri.TryCreate(query,UriKind.Absolute,out uri) && (uri.Scheme=="https"||uri.Scheme=="http")) results.Add(new Result {Name="Open website",Detail=uri.Host,Target=uri.AbsoluteUri,Kind="web",Glyph="\u2197",Score=500});
   results.Add(new Result {Name="Search the web",Detail=query,Target="https://www.bing.com/search?q="+Uri.EscapeDataString(query),Kind="web",Glyph="\u2197",Score=-1});
   return results.OrderByDescending(x=>x.Score).ThenBy(x=>x.Name).Take(9).ToList();
  }
  public static List<Result> Files(string query,CancellationToken token) {
   var found=new List<Result>(); if(query.Length<2||query.Length>160)return found;
   try {
    using(var connection=new OleDbConnection("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';")) {
     connection.Open();token.ThrowIfCancellationRequested();
     string literal=query.Replace("'","''").Replace("%","").Replace("_","").Replace("[","").Replace("]","");
     using(var command=connection.CreateCommand()) {
      command.CommandTimeout=2;
      command.CommandText="SELECT TOP 6 System.ItemNameDisplay, System.ItemUrl FROM SYSTEMINDEX WHERE System.ItemNameDisplay LIKE '%"+literal+"%' AND System.ItemUrl LIKE 'file:%' ORDER BY System.DateModified DESC";
      using(var reader=command.ExecuteReader()) while(reader.Read()) {
       token.ThrowIfCancellationRequested();Uri uri;
       string url=Convert.ToString(reader.GetValue(1));
       if(Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.IsFile) found.Add(new Result {Name=Convert.ToString(reader.GetValue(0)),Detail=uri.LocalPath,Target=uri.LocalPath,Kind="file",Glyph="\u25A1",Score=400});
      }
     }
    } IndexStatus="Files use the Windows Search index";IndexDiagnostic="Windows Search query succeeded";
   } catch(OperationCanceledException){throw;} catch(OleDbException ex){IndexDiagnostic=ex.Message+" (0x"+ex.ErrorCode.ToString("X8")+")";fallbackReason=ex.ErrorCode==unchecked((int)0x80070005)?"Index access denied · local folders":"Index query failed · local folders";return Fallback(query,token);} catch(InvalidOperationException ex){IndexDiagnostic=ex.Message;fallbackReason="Index provider unavailable · local folders";return Fallback(query,token);}
   return found;
  }
  static List<Result> Fallback(string query,CancellationToken token) {
   IndexStatus=fallbackReason;
   var found=new List<Result>();var queue=new Queue<Tuple<string,int>>();
   string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(profile,"Downloads")};
   foreach(var root in roots)if(!string.IsNullOrEmpty(root)&&Directory.Exists(root))queue.Enqueue(Tuple.Create(root,0));
   int visited=0;var timer=Stopwatch.StartNew();
   while(queue.Count>0&&visited<2000&&timer.ElapsedMilliseconds<250&&found.Count<6){var folder=queue.Dequeue();
    try {foreach(string path in Directory.EnumerateFileSystemEntries(folder.Item1)){
     token.ThrowIfCancellationRequested();if(++visited>2000||timer.ElapsedMilliseconds>=250)break;
     var attributes=File.GetAttributes(path);bool directory=(attributes&FileAttributes.Directory)!=0;
     if(Path.GetFileName(path).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)found.Add(new Result {Name=Path.GetFileName(path),Detail=path,Target=path,Kind="file",Glyph="\u25A1",Score=400});
     if(found.Count>=6)break;
     if(directory&&folder.Item2<2&&(attributes&FileAttributes.ReparsePoint)==0)queue.Enqueue(Tuple.Create(path,folder.Item2+1));
    }}catch(UnauthorizedAccessException){}catch(IOException){}
   }return found;
  }
 }
 public sealed class Calculator {
  string text;int p;int depth;
  Calculator(string s){text=s;}
  void Space(){while(p<text.Length&&char.IsWhiteSpace(text[p]))p++;}
  bool Eat(char c){Space();if(p<text.Length&&text[p]==c){p++;return true;}return false;}
  double Sum(){double a=Product();while(true){if(Eat('+'))a+=Product();else if(Eat('-'))a-=Product();else return a;}}
  double Product(){double a=Power();while(true){if(Eat('*'))a*=Power();else if(Eat('/'))a/=Power();else return a;}}
  double Power(){double a=Atom();if(Eat('^'))a=Math.Pow(a,Power());return a;}
  double Atom(){if(++depth>32)throw new FormatException();double a;
   if(Eat('+'))a=Atom();else if(Eat('-'))a=-Atom();else if(Eat('(')){a=Sum();if(!Eat(')'))throw new FormatException();}
   else {Space();int start=p;while(p<text.Length&&(char.IsDigit(text[p])||text[p]=='.'))p++;if(start==p)throw new FormatException();a=double.Parse(text.Substring(start,p-start),CultureInfo.InvariantCulture);}
   if(Eat('%'))a/=100;depth--;return a;
  }
  public static bool Try(string s,out double value){value=0;if(s.Length>160||!s.Any(c=>"+-*/^%".Contains(c)))return false;try {var c=new Calculator(s);value=c.Sum();c.Space();return c.p==s.Length&&!double.IsNaN(value)&&!double.IsInfinity(value);}catch(FormatException){return false;}catch(OverflowException){return false;}}
 }


 // Native shell icons are fetched off the UI thread, at display size, with a bounded cache.
 sealed class ShellIcons : IDisposable {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Info {public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attrs,out Info info,uint size,uint flags);
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  [DllImport("ole32.dll")]static extern int OleInitialize(IntPtr reserved);
  [DllImport("ole32.dll")]static extern void OleUninitialize();
  readonly Forms.Control owner;readonly Action changed;readonly Queue<string> requests=new Queue<string>();readonly HashSet<string> pending=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,System.Drawing.Bitmap> cache=new Dictionary<string,System.Drawing.Bitmap>(StringComparer.OrdinalIgnoreCase);readonly Queue<string> age=new Queue<string>();
  readonly AutoResetEvent wake=new AutoResetEvent(false);volatile bool disposed;readonly int pixels;
  public int Count {get {return cache.Count;}}
  public ShellIcons(Forms.Control owner,int pixels,Action changed){this.owner=owner;this.pixels=pixels;this.changed=changed;var worker=new Thread(Work){IsBackground=true,Name="Lumen shell icons"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}
  public System.Drawing.Bitmap Get(string target){if(disposed||string.IsNullOrEmpty(target))return null;if(target.StartsWith("ms-settings:",StringComparison.OrdinalIgnoreCase))target="ms-settings:";System.Drawing.Bitmap image;if(cache.TryGetValue(target,out image))return image;if(pending.Add(target)){lock(requests)requests.Enqueue(target);wake.Set();}return null;}
  void Work(){int hr=OleInitialize(IntPtr.Zero);try {while(!disposed){string target=null;lock(requests){if(requests.Count>0)target=requests.Dequeue();}if(target==null){wake.WaitOne();continue;}
    System.Drawing.Bitmap bitmap=null;try {string path=Resolve(target);Info info;if(path!=null&&SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(Info)),0x100)!=IntPtr.Zero&&info.Icon!=IntPtr.Zero){try {using(var native=System.Drawing.Icon.FromHandle(info.Icon))using(var original=native.ToBitmap()){bitmap=new System.Drawing.Bitmap(pixels,pixels,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(original,0,0,pixels,pixels);}}}finally{DestroyIcon(info.Icon);}}}catch(ArgumentException){}catch(ExternalException){}catch(IOException){}
    if(disposed){if(bitmap!=null)bitmap.Dispose();break;}var result=bitmap;string key=target;
    try {owner.BeginInvoke(new Action(delegate {if(disposed){if(result!=null)result.Dispose();return;}pending.Remove(key);if(cache.Count>=64){string oldest=age.Dequeue();if(cache[oldest]!=null)cache[oldest].Dispose();cache.Remove(oldest);}cache[key]=result;age.Enqueue(key);changed();}));}catch(InvalidOperationException){if(result!=null)result.Dispose();}
   }}finally{if(hr>=0)OleUninitialize();wake.Dispose();}}
  static string Resolve(string target){if(target=="ms-settings:")return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"ImmersiveControlPanel\\SystemSettings.exe");if(File.Exists(target)||Directory.Exists(target))return target;
   if(target.IndexOfAny(new[]{'\\','/',':'})>=0)return null;string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),target);if(File.Exists(path))return path;path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),target);return File.Exists(path)?path:null;}
  public void Dispose(){if(disposed)return;disposed=true;wake.Set();foreach(var image in cache.Values)if(image!=null)image.Dispose();cache.Clear();}
 }
 public class Launcher : Forms.Form {
  [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
  [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
  [DllImport("user32.dll")]static extern bool SystemParametersInfo(uint action,uint param,out bool value,uint flags);
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
  [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
  [StructLayout(LayoutKind.Sequential)]struct NativeSize {public int Width,Height;}
  [StructLayout(LayoutKind.Sequential,Pack=1)]struct Blend {public byte Operation,Flags,Alpha,Format;}
  [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr screen,ref NativePoint destination,ref NativeSize size,IntPtr dc,ref NativePoint origin,uint key,ref Blend blend,uint flags);
  // Optional native backdrop. Missing/unsupported implementations retain per-pixel translucency.
  [StructLayout(LayoutKind.Sequential)]struct Accent {public int State,Flags,Color,Animation;}
  [StructLayout(LayoutKind.Sequential)]struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
  [DllImport("user32.dll")]static extern bool SetWindowCompositionAttribute(IntPtr hwnd,ref CompositionData data);
  Forms.TextBox input;Forms.NotifyIcon tray;Forms.Timer debounce,animation,resizeAnimation;CancellationTokenSource cancel;ShellIcons icons;
  List<Result> current=new List<Result>();int selected,generation,hotkey,hotkeyError;bool fileBusy,quitting,closing,reducedMotion,ready,glassEnabled;
  float dpi=1;string message="";double opacity=1,animationStart,animationEnd;Stopwatch animationClock=new Stopwatch(),resizeClock=new Stopwatch();int resizeFrom,resizeTo;int lastLayeredError;
  int clippedWidth=-1,clippedHeight=-1;bool clippedExpanded;
  System.Drawing.Font inputFont,titleFont,detailFont,hintFont;System.Drawing.Bitmap frame;
  readonly System.Drawing.Color ink=System.Drawing.Color.FromArgb(239,240,243);
  protected override Forms.CreateParams CreateParams {get {var cp=base.CreateParams;cp.ExStyle|=0x80000;return cp;}}
  public Launcher(){Text="Lumen";FormBorderStyle=Forms.FormBorderStyle.None;StartPosition=Forms.FormStartPosition.Manual;ShowInTaskbar=false;TopMost=true;AutoScaleMode=Forms.AutoScaleMode.None;
   using(var g=CreateGraphics())dpi=g.DpiX/96f;ClientSize=new System.Drawing.Size(S(664),S(82));
   inputFont=new System.Drawing.Font("Segoe UI",16f);titleFont=new System.Drawing.Font("Segoe UI",11.5f);detailFont=new System.Drawing.Font("Segoe UI",8.5f);hintFont=new System.Drawing.Font("Segoe UI",8f);
   // Native text editing remains responsible for IME, selection, clipboard and keyboard behavior.
   input=new Forms.TextBox {BorderStyle=Forms.BorderStyle.None,Font=inputFont,MaxLength=160,Location=new System.Drawing.Point(-2000,-2000),Width=S(600),AccessibleName="Lumen Search",TabIndex=0};Controls.Add(input);
   debounce=new Forms.Timer {Interval=110};debounce.Tick+=FilesTick;animation=new Forms.Timer {Interval=15};animation.Tick+=AnimateTick;
   resizeAnimation=new Forms.Timer {Interval=15};resizeAnimation.Tick+=delegate {double t=Math.Min(1,resizeClock.Elapsed.TotalMilliseconds/130);Height=resizeFrom+(int)((resizeTo-resizeFrom)*(1-Math.Pow(1-t,3)));Render();if(t>=1)resizeAnimation.Stop();};
   bool enabled;reducedMotion=!SystemParametersInfo(0x1042,0,out enabled,0)||!enabled;
   input.TextChanged+=delegate {UpdateResults();};input.KeyDown+=Keys;input.KeyUp+=delegate {Render();};input.GotFocus+=delegate {Render();};
   Deactivate+=delegate {if(Visible)Dismiss();};MouseDown+=Clicked;MouseMove+=delegate(object sender,Forms.MouseEventArgs e) { Cursor=Pill.Contains(e.Location)?Forms.Cursors.IBeam:Forms.Cursors.Default; };
   icons=new ShellIcons(this,S(28),Render);
   if(RegisterHotKey(Handle,1,0x4001,0x20))hotkey=1;else if(RegisterHotKey(Handle,2,0x4003,0x20))hotkey=2;if(hotkey==0)hotkeyError=Marshal.GetLastWin32Error();
   tray=new Forms.NotifyIcon {Icon=System.Drawing.SystemIcons.Application,Visible=true,Text="Lumen · "+(hotkey==1?"Alt + Space":hotkey==2?"Ctrl + Alt + Space":"open from tray")};
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open Lumen",null,delegate {Reveal();});menu.Items.Add("Refresh applications",null,delegate {RefreshApps();});var autostart=new Forms.ToolStripMenuItem("Start with Windows"){Checked=Startup.Enabled};menu.Opening+=delegate {autostart.Checked=Startup.Enabled;};autostart.Click+=delegate {string error;if(Startup.Set(!autostart.Checked,out error))autostart.Checked=Startup.Enabled;else Forms.MessageBox.Show("Could not change startup: "+error,"Lumen");};menu.Items.Add(autostart);menu.Items.Add("Index details",null,delegate {Forms.MessageBox.Show(Search.IndexStatus+Environment.NewLine+Search.IndexDiagnostic,"Windows Search");});menu.Items.Add("Quit",null,delegate {Quit();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate {Reveal();};
   TryBackdrop();ready=true;RefreshApps();Render();
  }
  int S(int n){return (int)Math.Round(n*dpi);}
  int PillWidth {get {return Width-S(24);}}
  System.Drawing.Rectangle Pill {get {return new System.Drawing.Rectangle(S(12),S(12),PillWidth,S(54));}}
  void TryBackdrop(){try {using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize")){if(key!=null&&Convert.ToInt32(key.GetValue("EnableTransparency",1))==0)reducedMotion=true;}}catch(System.Security.SecurityException){}
   // Accent blur on a layered HWND can create an opaque rectangular backing surface.
   // Let the layered bitmap supply the alpha instead; text and icons retain their own opacity.
   var policy=new Accent {State=0};IntPtr data=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Accent)));try{Marshal.StructureToPtr(policy,data,false);var attr=new CompositionData{Attribute=19,Data=data,Size=Marshal.SizeOf(typeof(Accent))};SetWindowCompositionAttribute(Handle,ref attr);}catch(EntryPointNotFoundException){}finally{Marshal.FreeHGlobal(data);}glassEnabled=false;
  }
  protected override void OnPaintBackground(Forms.PaintEventArgs e){}
  protected override void OnPaint(Forms.PaintEventArgs e){}
  void ClipWindow(){bool expanded=input.Text.Trim().Length>0;if(clippedWidth==Width&&clippedHeight==Height&&clippedExpanded==expanded)return;
   using(var shape=new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding)){
    var pill=Pill;pill.Inflate(S(5),S(5));pill.Offset(0,S(2));using(var path=Rounded(pill,S(32)))shape.AddPath(path,false);
    if(expanded){var panel=new System.Drawing.Rectangle(S(7),S(79),Width-S(14),Math.Max(S(40),Height-S(85)));using(var path=Rounded(panel,S(23)))shape.AddPath(path,false);}
    var old=Region;Region=new System.Drawing.Region(shape);if(old!=null)old.Dispose();
   }clippedWidth=Width;clippedHeight=Height;clippedExpanded=expanded;
  }
  protected override void WndProc(ref Forms.Message m){if(m.Msg==0x14){m.Result=new IntPtr(1);return;}if(m.Msg==0x0312){if(Visible&&!closing)Dismiss();else Reveal();return;}if(m.Msg==0x84){base.WndProc(ref m);if(m.Result==new IntPtr(1)){int x=(short)(m.LParam.ToInt64()&0xffff),y=(short)((m.LParam.ToInt64()>>16)&0xffff);var p=PointToClient(new System.Drawing.Point(x,y));if(!HitSurface(p))m.Result=new IntPtr(-1);}return;}base.WndProc(ref m);}
  bool HitSurface(System.Drawing.Point p){if(Pill.Contains(p))return true;return input!=null&&input.Text.Trim().Length>0&&p.Y>=S(82)&&p.X>=S(12)&&p.X<=Width-S(12);}
  void RefreshApps(){Task.Run(delegate{Search.LoadApps();if(!IsDisposed)try{BeginInvoke(new Action(delegate {if(!IsDisposed)UpdateResults();}));}catch(InvalidOperationException){}});}
  public void Reveal(){closing=false;animation.Stop();opacity=1;var area=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;Width=Math.Min(S(664),area.Width-S(24));input.Clear();UpdateResults();Location=new System.Drawing.Point(area.Left+(area.Width-Width)/2,area.Top+Math.Max(S(16),(int)(area.Height*.27)));if(!reducedMotion)opacity=.04;Show();Activate();SetForegroundWindow(Handle);input.Focus();Render();if(!reducedMotion)Fade(1);}
  void Fade(double end){animationStart=opacity;animationEnd=end;animationClock.Restart();animation.Start();}
  void AnimateTick(object s,EventArgs a){double t=Math.Min(1,animationClock.Elapsed.TotalMilliseconds/(closing?90:150));opacity=animationStart+(animationEnd-animationStart)*(1-Math.Pow(1-t,3));Render();if(t>=1){animation.Stop();if(closing)FinishHide();else opacity=1;}}
  void Dismiss(){if(closing||!Visible)return;closing=true;debounce.Stop();resizeAnimation.Stop();if(cancel!=null)cancel.Cancel();generation++;if(reducedMotion)FinishHide();else Fade(0);}
  void FinishHide(){Hide();current.Clear();closing=false;opacity=1;}
  void UpdateResults(){if(!ready)return;generation++;if(cancel!=null)cancel.Cancel();debounce.Stop();current=Search.Local(input.Text);selected=0;message=Search.IndexStatus;ResizeResults();if(input.Text.Trim().Length>=2)debounce.Start();}
  void ResizeResults(){var area=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;int limit=Math.Max(2,(area.Height-(int)(area.Height*.27)-S(142))/S(54));if(current.Count>limit)current=current.Take(limit).ToList();selected=Math.Min(selected,current.Count-1);resizeTo=input.Text.Trim().Length==0?S(82):S(126+Math.Max(1,current.Count)*54);resizeAnimation.Stop();if(Visible&&!reducedMotion&&!closing){resizeFrom=Height;resizeClock.Restart();resizeAnimation.Start();}else Height=resizeTo;Render();}
  async void FilesTick(object s,EventArgs a){debounce.Stop();if(fileBusy){debounce.Start();return;}fileBusy=true;string query=input.Text.Trim();int version=generation;var request=new CancellationTokenSource();cancel=request;
   try{var files=await Task.Run(()=>Search.Files(query,request.Token));if(version!=generation||!Visible||closing||IsDisposed)return;string chosen=selected>=0&&selected<current.Count?current[selected].Target:null;int index=current.FindIndex(x=>x.Kind=="web");if(index<0)index=current.Count;var targets=new HashSet<string>(current.Select(x=>x.Target),StringComparer.OrdinalIgnoreCase);current.InsertRange(index,files.Where(x=>targets.Add(x.Target)));selected=Math.Max(0,current.FindIndex(x=>x.Target==chosen));message=Search.IndexStatus;ResizeResults();}catch(OperationCanceledException){}finally{fileBusy=false;if(cancel==request)cancel=null;request.Dispose();}}
  void Keys(object s,Forms.KeyEventArgs e){if(e.KeyCode==Forms.Keys.Escape){Dismiss();e.SuppressKeyPress=true;}
   else if(e.KeyCode==Forms.Keys.Tab){e.SuppressKeyPress=true;}
   else if(e.KeyCode==Forms.Keys.Down||e.KeyCode==Forms.Keys.Up){if(current.Count>0)selected=(selected+(e.KeyCode==Forms.Keys.Down?1:-1)+current.Count)%current.Count;Render();e.SuppressKeyPress=true;}
   else if(e.KeyCode==Forms.Keys.Enter){OpenSelected(e.Control);e.SuppressKeyPress=true;}else {Render();}}
  void Clicked(object s,Forms.MouseEventArgs e){if(Pill.Contains(e.Location)){input.Focus();using(var g=CreateGraphics()){int start=Math.Min(input.SelectionStart,input.Text.Length);float scroll=Math.Max(0,Measure(g,input.Text.Substring(0,start))-(PillWidth-S(77))+S(12));float x=e.X-S(67)+scroll;int position=input.Text.Length;for(int i=0;i<input.Text.Length;i++){float before=Measure(g,input.Text.Substring(0,i)),after=Measure(g,input.Text.Substring(0,i+1));if(x<(before+after)/2){position=i;break;}}input.Select(position,0);}Render();return;}int row=(e.Y-S(92))/S(54);if(e.Y>=S(92)&&row>=0&&row<current.Count){selected=row;Render();OpenSelected(false);}}
  void OpenSelected(bool reveal){if(selected<0||selected>=current.Count)return;var r=current[selected];try{if(r.Kind=="copy")Forms.Clipboard.SetText(r.Target);else if(reveal&&r.Kind=="file")Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+r.Target+"\""){UseShellExecute=true});else Process.Start(new ProcessStartInfo(r.Target){UseShellExecute=true});Dismiss();}catch(Exception ex){if(ex is System.ComponentModel.Win32Exception||ex is InvalidOperationException||ex is ExternalException){message="Couldn't open this result. Try another.";Render();}else throw;}}
  public System.Drawing.Bitmap Snapshot(){return MakeFrame();}
  void Render(){if(!ready||IsDisposed)return;ClipWindow();if(frame!=null)frame.Dispose();frame=MakeFrame();PushFrame();}
  void PushFrame(){if(frame==null)return;IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),bitmap=IntPtr.Zero,old=IntPtr.Zero;try{bitmap=frame.GetHbitmap(System.Drawing.Color.FromArgb(0));old=SelectObject(dc,bitmap);var dest=new NativePoint(Left,Top);var size=new NativeSize{Width=Width,Height=Height};var origin=new NativePoint(0,0);var blend=new Blend{Operation=0,Flags=0,Alpha=(byte)(Math.Max(0,Math.Min(1,opacity))*255),Format=1};if(!UpdateLayeredWindow(Handle,screen,ref dest,ref size,dc,ref origin,0,ref blend,2))lastLayeredError=Marshal.GetLastWin32Error();else lastLayeredError=0;}finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}
  System.Drawing.Bitmap MakeFrame(){var image=new System.Drawing.Bitmap(Math.Max(1,Width),Math.Max(1,Height),System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=System.Drawing.Graphics.FromImage(image)){g.Clear(System.Drawing.Color.Transparent);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    Matte(g,Pill,S(27),false);using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(169,176,188),S(2))){g.DrawEllipse(pen,S(31),S(29),S(15),S(15));g.DrawLine(pen,S(44),S(43),S(51),S(50));}
    DrawInput(g);
    if(input.Text.Trim().Length>0){var panel=new System.Drawing.Rectangle(S(12),S(82),Width-S(24),Math.Max(S(30),Height-S(94)));Matte(g,panel,S(18),false);
     for(int i=0;i<current.Count;i++){int y=S(92+i*54);if(i==selected)using(var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(45,47,53)))using(var path=Rounded(new System.Drawing.Rectangle(S(22),y,Width-S(44),S(50)),S(11)))g.FillPath(brush,path);
      var r=current[i];var icon=(r.Kind=="app"||r.Kind=="file"||r.Kind=="setting")?icons.Get(r.Target):null;if(icon!=null)g.DrawImage(icon,new System.Drawing.Rectangle(S(35),y+S(11),S(28),S(28)));else DrawFallback(g,r.Kind,new System.Drawing.Rectangle(S(35),y+S(11),S(28),S(28)));
      DrawText(g,r.Name,titleFont,ink,new System.Drawing.Rectangle(S(77),y+S(5),Width-S(105),S(23)));DrawText(g,r.Detail,detailFont,System.Drawing.Color.FromArgb(163,169,181),new System.Drawing.Rectangle(S(77),y+S(29),Width-S(105),S(17)));
     }if(current.Count==0)DrawText(g,"No matches",titleFont,ink,new System.Drawing.Rectangle(S(32),S(101),Width-S(64),S(35)));
     DrawText(g,message+"   ·   ↑↓ select   ↵ open",hintFont,System.Drawing.Color.FromArgb(138,145,157),new System.Drawing.Rectangle(S(32),Height-S(35),Width-S(64),S(22)));
    }
   }return image;}
  void DrawInput(System.Drawing.Graphics g){var area=new System.Drawing.Rectangle(S(67),S(23),PillWidth-S(77),S(33));var state=g.Save();g.SetClip(area);string text=input.Text;int start=Math.Max(0,Math.Min(input.SelectionStart,text.Length));float caret=Measure(g,text.Substring(0,start));float scroll=Math.Max(0,caret-area.Width+S(12));float x=area.X-scroll;float y=area.Y;
   if(text.Length==0){DrawText(g,"Lumen Search",inputFont,System.Drawing.Color.FromArgb(153,159,172),area);caret=0;}
   else {if(input.SelectionLength>0){float w=Measure(g,text.Substring(start,input.SelectionLength));using(var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(100,107,180,222)))g.FillRectangle(brush,x+caret,y+S(3),w,S(26));}using(var brush=new System.Drawing.SolidBrush(ink))g.DrawString(text,inputFont,brush,x,y,System.Drawing.StringFormat.GenericTypographic);}
   if(input.Focused&&input.SelectionLength==0)using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(207,214,232),Math.Max(1,S(1))))g.DrawLine(pen,x+caret,y+S(3),x+caret,y+S(27));g.Restore(state);
  }
  float Measure(System.Drawing.Graphics g,string text){return text.Length==0?0:g.MeasureString(text,inputFont,new System.Drawing.PointF(0,0),System.Drawing.StringFormat.GenericTypographic).Width;}
  void Matte(System.Drawing.Graphics g,System.Drawing.Rectangle r,int radius,bool active){for(int k=7;k>=1;k--){var shadow=new System.Drawing.Rectangle(r.X-S(k/2),r.Y+S(3-k/2),r.Width+S(k),r.Height+S(k));using(var path=Rounded(shadow,radius+S(k/2)))using(var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(5,0,0,0)))g.FillPath(brush,path);}
   using(var path=Rounded(r,radius)){using(var brush=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(23,24,27)))g.FillPath(brush,path);using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(55,57,64),S(1)))g.DrawPath(pen,path);}
  }
  void DrawFallback(System.Drawing.Graphics g,string kind,System.Drawing.Rectangle r){var state=g.Save();g.TranslateTransform(r.X,r.Y);g.ScaleTransform(dpi,dpi);using(var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(160,170,190),1.5f)){if(kind=="app"){for(int x=0;x<2;x++)for(int y=0;y<2;y++)g.DrawRectangle(pen,3+x*12,3+y*12,8,8);}else if(kind=="web"){g.DrawEllipse(pen,2,2,24,24);g.DrawEllipse(pen,8,2,12,24);g.DrawLine(pen,2,14,26,14);}else if(kind=="copy"){g.DrawLine(pen,5,10,23,10);g.DrawLine(pen,5,18,23,18);}else{g.DrawRectangle(pen,6,2,17,24);g.DrawLine(pen,10,10,19,10);g.DrawLine(pen,10,15,19,15);}}g.Restore(state);}
  static void DrawText(System.Drawing.Graphics g,string text,System.Drawing.Font font,System.Drawing.Color color,System.Drawing.Rectangle r){using(var brush=new System.Drawing.SolidBrush(color))using(var format=new System.Drawing.StringFormat{Trimming=System.Drawing.StringTrimming.EllipsisCharacter,FormatFlags=System.Drawing.StringFormatFlags.NoWrap,LineAlignment=System.Drawing.StringAlignment.Center})g.DrawString(text,font,brush,r,format);}
  static System.Drawing.Drawing2D.GraphicsPath Rounded(System.Drawing.Rectangle r,int radius){var p=new System.Drawing.Drawing2D.GraphicsPath();int d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  protected override void OnFormClosing(Forms.FormClosingEventArgs e){if(!quitting&&e.CloseReason==Forms.CloseReason.UserClosing){e.Cancel=true;Dismiss();}base.OnFormClosing(e);}
  public void Quit(){quitting=true;Close();Forms.Application.ExitThread();}
  public void StartupFailed(string error){tray.ShowBalloonTip(5000,"Lumen startup","Couldn't enable startup. Use Start with Windows in the tray menu to retry.",Forms.ToolTipIcon.Info);}
  protected override void Dispose(bool disposing){if(disposing&&!IsDisposed){ready=false;generation++;if(cancel!=null)cancel.Cancel();debounce.Dispose();animation.Dispose();resizeAnimation.Dispose();icons.Dispose();if(hotkey!=0)UnregisterHotKey(Handle,hotkey);tray.Visible=false;tray.Dispose();inputFont.Dispose();titleFont.Dispose();detailFont.Dispose();hintFont.Dispose();if(frame!=null)frame.Dispose();}base.Dispose(disposing);}
  public void VerifyUi(Action<string,bool> check){if(hotkeyError==5)check("shortcut denied by host; tray fallback available",tray.Visible);else check("global hotkey registered (Win32 error "+hotkeyError+")",hotkey!=0);
   Reveal();check("empty search is compact",current.Count==0&&resizeTo==S(82));using(var image=Snapshot()){image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-compact.png"));check("no category buttons; full width search pill",Pill.Width==Width-S(24));check("window excludes rectangular corners",Region!=null&&!Region.IsVisible(0,0)&&!Region.IsVisible(Width-1,0));check("window retains the rounded pill",Region.IsVisible(Pill.Left+Pill.Width/2,Pill.Top+Pill.Height/2));check("outside pill bitmap is fully transparent",image.GetPixel(0,0).A==0&&image.GetPixel(Width-1,0).A==0);check("matte pill is fully opaque",image.GetPixel(Pill.Left+Pill.Width/2,Pill.Top+S(10)).A==255);}
   check("native text editor focused",input.Focused);input.Clear();foreach(char c in "notepad")SendMessage(input.Handle,0x0102,new IntPtr(c),IntPtr.Zero);check("native character input reaches search",input.Text=="notepad");check("typed query selects Notepad",current.Count>0&&current[0].Name=="Notepad");Keys(input,new Forms.KeyEventArgs(Forms.Keys.Down));check("down arrow advances selection",selected==1);Keys(input,new Forms.KeyEventArgs(Forms.Keys.Up));check("up arrow restores selection",selected==0);
   input.Text="wifi";check("Wi-Fi settings alias ranks first",current.Count>0&&current[0].Target=="ms-settings:network-wifi");input.Text="settings bluetooth";check("settings prefix finds Bluetooth",current.Count>0&&current[0].Target=="ms-settings:bluetooth");input.Text="display settings";check("settings suffix finds Display",current.Count>0&&current[0].Target=="ms-settings:display");input.Text="2+3*4";check("calculator appears first",current[0].Name=="14");
   Dismiss();Reveal();check("reopen during fade stays visible",Visible&&!closing);input.Text="bluetooth";for(int i=0;i<35;i++){Forms.Application.DoEvents();Thread.Sleep(10);}Render();check("native layered frame applied (error "+lastLayeredError+")",lastLayeredError==0);check("real shell icons cached",icons.Count>0);check("icon cache stays bounded",icons.Count<=64);using(var image=Snapshot())image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"glass-status.txt"),"Native blur API accepted: "+glassEnabled+Environment.NewLine+"Matte body: fully opaque; native rounded outline retained; blur disabled"+Environment.NewLine+"Icon cache entries: "+icons.Count);
  }
 }
 public static class Program {
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  [STAThread]public static int Main(string[] args){SetProcessDPIAware();Forms.Application.EnableVisualStyles();Forms.Application.SetCompatibleTextRenderingDefault(false);if(args.Contains("--self-test"))return SelfTest(args);if(args.Contains("--export-startup")){Startup.WriteShortcut(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Lumen.lnk"),Forms.Application.ExecutablePath);return 0;}if(args.Contains("--verify-startup"))return Startup.VerifyShortcut(Startup.PathName,Forms.Application.ExecutablePath)?0:1;bool created;using(var mutex=new Mutex(true,"Local\\LumenLauncher",out created)){if(!created)return 0;using(var w=new Launcher()){string startupError;if(!args.Contains("--no-startup")&&!Startup.Ensure(out startupError))w.StartupFailed(startupError);if(!args.Contains("--background"))w.Reveal();Forms.Application.Run();}}return 0;}
  static int SelfTest(string[] args){var report=new StringBuilder();int failed=0;Action<string,bool> check=(label,b)=>{report.AppendLine((b?"PASS ":"FAIL ")+label);if(!b)failed++;};double n;
   check("arithmetic precedence",Calculator.Try("2+3*4",out n)&&n==14);check("parentheses",Calculator.Try("(2+3)*4",out n)&&n==20);check("percent",Calculator.Try("200*15%",out n)&&n==30);check("division by zero rejected",!Calculator.Try("1/0",out n));check("invalid input rejected",!Calculator.Try("hello",out n));check("trailing syntax rejected",!Calculator.Try("2+2xyz",out n));check("deep nesting rejected",!Calculator.Try(new string('(',40)+"1+1"+new string(')',40),out n));check("exact ranking",Search.Match("Notepad","notepad")>Search.Match("Notepad++","note"));check("fuzzy match",Search.Match("Visual Studio Code","vsc")>0);
   string shortcut=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Lumen.lnk");Startup.WriteShortcut(shortcut,Forms.Application.ExecutablePath);check("startup shortcut targets current app in background mode",Startup.VerifyShortcut(shortcut,Forms.Application.ExecutablePath));var timer=Stopwatch.StartNew();Search.LoadApps();timer.Stop();check("applications discovered",Search.AppCount>=4);report.AppendLine("Apps: "+Search.AppCount+"; discovery ms: "+timer.ElapsedMilliseconds);timer.Restart();for(int i=0;i<1000;i++)Search.Local("note");timer.Stop();report.AppendLine("1000 local queries ms: "+timer.ElapsedMilliseconds);check("local query average under 10 ms",timer.ElapsedMilliseconds<10000);
   timer.Restart();var files=Search.Files("document",CancellationToken.None);timer.Stop();report.AppendLine("File index: "+Search.IndexStatus+"; diagnostic: "+Search.IndexDiagnostic+"; results: "+files.Count+"; ms: "+timer.ElapsedMilliseconds);
   if(args.Contains("--ui-test")){using(var w=new Launcher()){w.VerifyUi(check);Forms.Application.DoEvents();Thread.Sleep(180);Forms.Application.DoEvents();check("window layout created",w.Width>0&&w.Height>0);using(var image=w.Snapshot()){image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}w.Hide();}}
   report.AppendLine("Private memory MiB: "+(Process.GetCurrentProcess().PrivateMemorySize64/1048576.0).ToString("F1",CultureInfo.InvariantCulture));string output=args.FirstOrDefault(x=>x.StartsWith("--report="));if(output!=null)File.WriteAllText(output.Substring(9),report.ToString());return failed==0?0:1;
  }
 }
}










