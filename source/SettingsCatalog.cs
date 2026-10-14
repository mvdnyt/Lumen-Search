using System;
using System.Collections.Generic;
using System.Linq;
namespace Lumen {
 public static class SettingsCatalog {
  sealed class Page {public string Name,Uri,Aliases;public Page(string name,string uri,string aliases){Name=name;Uri="ms-settings:"+uri;Aliases=aliases;}}
  static readonly Page[] pages={
   new Page("Bluetooth settings","bluetooth","bluetooth|pair device"),
   new Page("Wi-Fi settings","network-wifi","wifi|wireless|wi fi"),
   new Page("Display settings","display","display|screen|resolution|brightness|monitor"),
   new Page("Sound settings","sound","sound|audio|volume|speakers|headphones"),
   new Page("Windows Update","windowsupdate","update|windows update|updates"),
   new Page("Volume mixer","apps-volume","volume mixer|app volume"),
   new Page("Night light","nightlight","night light|blue light"),
   new Page("Power and sleep","powersleep","power|sleep|battery"),
   new Page("Storage settings","storagesense","storage|disk space|free space"),
   new Page("Installed apps","appsfeatures","installed apps|uninstall|apps and features"),
   new Page("Default apps","defaultapps","default apps|default browser"),
   new Page("Startup apps","startupapps","startup|startup apps"),
   new Page("Notifications","notifications","notifications|notification"),
   new Page("Mouse settings","mousetouchpad","mouse|cursor|mouse speed"),
   new Page("Touchpad settings","devices-touchpad","touchpad|trackpad"),
   new Page("Printers and scanners","printers","printer|printers|scanner|printing"),
   new Page("Network settings","network-status","network|internet|connection"),
   new Page("Ethernet settings","network-ethernet","ethernet|wired network"),
   new Page("VPN settings","network-vpn","vpn|virtual private network"),
   new Page("Mobile hotspot","network-mobilehotspot","hotspot|tethering"),
   new Page("Proxy settings","network-proxy","proxy"),
   new Page("Background wallpaper","personalization-background","wallpaper|background"),
   new Page("Colors and dark mode","personalization-colors","color|colors|dark mode|light mode"),
   new Page("Themes","themes","theme|themes"),
   new Page("Taskbar settings","taskbar","taskbar"),
   new Page("Lock screen","lockscreen","lock screen"),
   new Page("Sign-in options","signinoptions","sign in|signin|password|windows hello|pin"),
   new Page("Your account","yourinfo","account|profile|your info"),
   new Page("Date and time","dateandtime","date|time|clock|time zone"),
   new Page("Language and region","regionlanguage","language|region"),
   new Page("Microphone permissions","privacy-microphone","microphone|mic|microphone privacy"),
   new Page("Camera permissions","privacy-webcam","camera|webcam|camera privacy"),
   new Page("Location permissions","privacy-location","location|gps|location privacy"),
   new Page("Windows Security","windowsdefender","security|defender|antivirus"),
   new Page("Recovery settings","recovery","recovery|reset pc"),
   new Page("Troubleshoot","troubleshoot","troubleshoot|troubleshooting"),
   new Page("Windows activation","activation","activation|product key"),
   new Page("About this PC","about","about|pc specs|system info|device specifications"),
   new Page("Clipboard settings","clipboard","clipboard|clipboard history"),
   new Page("Multitasking","multitasking","multitasking|snap windows"),
   new Page("Remote Desktop","remotedesktop","remote desktop|rdp"),
   new Page("Game Mode","gaming-gamemode","game mode|gaming"),
   new Page("Keyboard accessibility","easeofaccess-keyboard","keyboard|sticky keys|filter keys"),
   new Page("Magnifier","easeofaccess-magnifier","magnifier|zoom"),
   new Page("Narrator","easeofaccess-narrator","narrator|screen reader"),
   new Page("Search permissions","search-permissions","search permissions|search privacy")
  };
  public static int Count {get {return pages.Length;}}
  static string Normal(string s){return new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());}
  public static IEnumerable<Result> Find(string query){string q=Normal(query);bool browse=q=="settings";if(q.StartsWith("settings")&&q.Length>8)q=q.Substring(8);if(q.EndsWith("settings")&&q.Length>8)q=q.Substring(0,q.Length-8);if(q.Length==0)return new Result[0];
   return pages.Select(p=>new {Page=p,Score=browse?600:Math.Max(Search.Match(Normal(p.Name),q),p.Aliases.Split('|').Max(a=>Search.Match(Normal(a),q)))}).Where(p=>p.Score>=600).Select(p=>new Result {Name=p.Page.Name,Target=p.Page.Uri,Detail="Windows Settings · Enter to open",Kind="setting",Glyph="\u2699",Score=p.Score+100}).OrderByDescending(r=>r.Score).Take(7);
  }
 }
}
