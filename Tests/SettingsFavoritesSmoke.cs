using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;

internal static class SettingsFavoritesSmoke
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, Members).GetValue(owner);
    }
    private static object Call(object owner, string name, params object[] args)
    {
        return owner.GetType().GetMethod(name, Members).Invoke(owner, args);
    }
    private static WinServMgr.MainForm NewForm(string path)
    {
        return (WinServMgr.MainForm)Activator.CreateInstance(
            typeof(WinServMgr.MainForm), Members, null, new object[] { path }, null);
    }
    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }
    private static void ExpectRows(WinServMgr.MainForm main, params string[] names)
    {
        var rows = (IList)Field(main, "visibleServices");
        Check(rows.Count == names.Length, "Expected visible rows: " + names.Length + ", actual: " + rows.Count);
        for (int i = 0; i < names.Length; i++)
            Check(((WinServMgr.ServiceEntry)rows[i]).ServiceName == names[i],
                "Incorrect visible service at " + i);
    }

    [STAThread]
    private static int Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WSM 设置 & (test) ^ % ! " + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "WinServiceManager.ini");
        Directory.CreateDirectory(directory);
        try
        {
            Application.EnableVisualStyles();
            File.WriteAllText(path,
                "[Windows]\r\nMainBounds=12,34,900,600\r\nDetailsBounds=52,74,800,700\r\n" +
                "[Filters]\r\nShowStopped=False\r\nFavoritesOnly=True\r\n" +
                "[Favorites]\r\naa_test=1\r\nbb_test=1\r\n");
            using (var main = NewForm(path))
            {
                var favoriteFilter = (CheckBox)Field(main, "favoritesOnlyBox");
                var stoppedFilter = (CheckBox)Field(main, "tbxShowStopped");
                Check(favoriteFilter.Checked && !stoppedFilter.Checked, "Restore both filter states");
                var data = new List<WinServMgr.ServiceEntry> {
                    new WinServMgr.ServiceEntry("aa_test", "Favorite running", ServiceControllerStatus.Running),
                    new WinServMgr.ServiceEntry("bb_test", "Favorite stopped", ServiceControllerStatus.Stopped),
                    new WinServMgr.ServiceEntry("cc_test", "Other running", ServiceControllerStatus.Running)
                };
                Call(main, "ApplySnapshot", data);
                ExpectRows(main, "aa_test");
                stoppedFilter.Checked = true;
                ExpectRows(main, "aa_test", "bb_test");
                Call(main, "ToggleFavorite", "cc_test");
                ExpectRows(main, "aa_test", "bb_test", "cc_test");
                Call(main, "ToggleFavorite", "AA_TEST");
                ExpectRows(main, "bb_test", "cc_test");
                stoppedFilter.Checked = false;
                ExpectRows(main, "cc_test");
                // Background changes must use the same favorite + status filter.
                Call(main, "ApplySnapshot", new List<WinServMgr.ServiceEntry> {
                    new WinServMgr.ServiceEntry("aa_test", "Renamed nonfavorite", ServiceControllerStatus.Running),
                    new WinServMgr.ServiceEntry("bb_test", "Renamed favorite", ServiceControllerStatus.Running),
                    new WinServMgr.ServiceEntry("cc_test", "Other favorite", ServiceControllerStatus.Stopped)
                });
                ExpectRows(main, "bb_test");
                Call(main, "ToggleFavorite", "测试=服务%");
                favoriteFilter.Checked = false;
                ExpectRows(main, "aa_test", "bb_test");
                var settings = Field(main, "settings");
                Check((Rectangle)Field(settings, "MainBounds") == new Rectangle(12, 34, 900, 600),
                    "Load main geometry");
                Check((Rectangle)Field(settings, "DetailsBounds") == new Rectangle(52, 74, 800, 700),
                    "Load detail geometry");
                settings.GetType().GetField("MainBounds", Members).SetValue(settings, new Rectangle(-1200, 35, 920, 610));
                settings.GetType().GetField("DetailsBounds", Members).SetValue(settings, new Rectangle(90, 110, 810, 710));
                Call(settings, "Save");
            }
            using (var main = NewForm(path))
            {
                Check(!((CheckBox)Field(main, "favoritesOnlyBox")).Checked &&
                    !((CheckBox)Field(main, "tbxShowStopped")).Checked, "Persist filter changes");
                var settings = Field(main, "settings");
                var favorites = (HashSet<string>)Field(settings, "Favorites");
                Check(favorites.SetEquals(new[] { "bb_test", "cc_test", "测试=服务%" }),
                    "Persist case-insensitive favorites and escaped names");
                Check((Rectangle)Field(settings, "MainBounds") == new Rectangle(-1200, 35, 920, 610),
                    "Persist main geometry");
                Check((Rectangle)Field(settings, "DetailsBounds") == new Rectangle(90, 110, 810, 710),
                    "Persist detail geometry");
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                Rectangle fit = (Rectangle)settings.GetType().GetMethod("FitToScreen",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {
                        new Rectangle(90000, 90000, 300, 300), new Size(100, 100), area
                    });
                Check(area.Contains(fit), "Restore off-screen window into available work area");
            }
            Console.WriteLine("PASS: favorite add/remove, combined filters, refresh membership, INI roundtrip, special paths, window bounds");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
        finally { Directory.Delete(directory, true); }
    }
}
