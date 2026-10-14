using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Lumen {
 public static class Startup {
  [ComImport,Guid("00021401-0000-0000-C000-000000000046")]class ShellLink {}
  [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellLink {
   void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,IntPtr data,uint flags);
   void GetIDList(out IntPtr list);void SetIDList(IntPtr list);
   void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
   void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);
   void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string text);
   void GetHotkey(out short hotkey);void SetHotkey(short hotkey);void GetShowCmd(out int cmd);void SetShowCmd(int cmd);
   void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int length,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
   void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
  }
  public static string PathName {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Lumen.lnk");}}
  static string Preference {get {return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-preference.txt");}}
  public static void WriteShortcut(string destination,string executable){object obj=new ShellLink();try {var link=(IShellLink)obj;link.SetPath(executable);link.SetArguments("--background");link.SetWorkingDirectory(Path.GetDirectoryName(executable));link.SetDescription("Lumen search · start silently at sign-in");link.SetShowCmd(7);((IPersistFile)obj).Save(destination,true);}finally{Marshal.FinalReleaseComObject(obj);}}
  public static bool VerifyShortcut(string path,string executable){object obj=new ShellLink();try {((IPersistFile)obj).Load(path,0);var link=(IShellLink)obj;var target=new StringBuilder(1024);link.GetPath(target,target.Capacity,IntPtr.Zero,4);var arguments=new StringBuilder(100);link.GetArguments(arguments,arguments.Capacity);return string.Equals(target.ToString(),executable,StringComparison.OrdinalIgnoreCase)&&arguments.ToString()=="--background";}finally{Marshal.FinalReleaseComObject(obj);}}
  public static bool Enabled {get {return File.Exists(PathName);}}
  public static bool Set(bool enabled,out string error){error=null;try {if(enabled)WriteShortcut(PathName,System.Windows.Forms.Application.ExecutablePath);else if(File.Exists(PathName))File.Delete(PathName);File.WriteAllText(Preference,enabled?"enabled":"disabled");return true;}catch(UnauthorizedAccessException ex){error=ex.Message;return false;}catch(IOException ex){error=ex.Message;return false;}catch(COMException ex){error=ex.Message;return false;}}
  public static bool Ensure(out string error){error=null;try {if(File.Exists(Preference)&&File.ReadAllText(Preference).Trim()=="disabled")return true;}catch(IOException ex){error=ex.Message;return false;}catch(UnauthorizedAccessException ex){error=ex.Message;return false;}return Set(true,out error);}
 }
}
