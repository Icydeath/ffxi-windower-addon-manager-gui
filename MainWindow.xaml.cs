using Dandraka.XmlUtilities;
using MahApps.Metro.Controls;
using NLua;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace ffxi_addon_manager
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : MetroWindow
  {
    private bool UnsavedChanges = false;
    private string PendingStr = "Pending changes...";

    private string Windower_Addons_Path = string.Empty;
    private string Windower_Plugins_Path = string.Empty;
    private string Addon_Manager_Data_Path = string.Empty;
    private string Windower_Res_Path = string.Empty;
    private string Windower_AutoLoad_Path = string.Empty;
    private string Settings_File = "settings.xml";

    private dynamic Settings;
    private List<string> Autoload_Addons;
    private List<string> All_Addons;
    private readonly List<string> All_Jobs = new List<string>
    {
      "WAR","WHM","RDM","PLD","BST","RNG","NIN","SMN","COR","DNC","GEO",
      "MNK","BLM","THF","DRK","BRD","SAM","DRG","BLU","PUP","SCH","RUN"
    };
    private List<string> All_Zones;

    private List<string> Global_Addons;
    private List<CharSettings> Player_Addons;
    private List<JobSettings> By_Job;
    private List<ZoneSettings> By_Zone;
    private List<string> Zone_Groups = new List<string>();

    private readonly string ZoneGroups_Header = " {CREATE NEW}";
    private readonly string Zones_Header = " ";

    public MainWindow()
    {
      InitializeComponent();
      txt_WindowerPath.IsEnabled = false;

      Properties.Settings.Default.Windower_Path = @"C:\Program Files (x86)\Windower4";
      Init();
    }

    private void Btn_WindowerPath_Click(object sender, RoutedEventArgs e)
    {
      var dialog = new FolderBrowserDialog();
      dialog.ShowDialog();
      Properties.Settings.Default.Windower_Path = dialog.SelectedPath;
      Init();
    }

    private void Init()
    {
      if (Validate_Paths())
      {
        // Data
        var xml = File.ReadAllText(Addon_Manager_Data_Path + @"\settings.xml").Replace("<?xml version=\"1.1\" ?>", "<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
        Settings = XmlSlurper.ParseText(xml);

        Autoload_Addons = Get_Autoload_Addons();
        All_Addons = Get_Addons();
        All_Jobs.Sort();
        All_Zones = Get_All_Zones();
        
        Global_Addons = Get_Global_Addons();
        Player_Addons = Get_Character_Addons();
        By_Job = Get_Jobs_Addons();
        By_Zone = Get_ZoneSettings();

        Zone_Groups.Add(ZoneGroups_Header);
        Zone_Groups.AddRange(By_Zone.Select(x => x.GroupId).ToList());

        // UI
        txt_WindowerPath.Text = Properties.Settings.Default.Windower_Path;
        
        Cb_Jobs.ItemsSource = All_Jobs;
        //Cb_Jobs.SelectedIndex = 0;

        Cb_Zones.ItemsSource = All_Zones;
        //Cb_Zones.SelectedIndex = 0;

        Cb_ZoneGroups.ItemsSource = Zone_Groups;
        //Cb_ZoneGroups.SelectedIndex = 0;

        lb_GlobalAddons.ItemsSource = Global_Addons;
        lb_Character.ItemsSource = Player_Addons;
        lb_Jobs.ItemsSource = By_Job;
        lb_Zones.ItemsSource = By_Zone;
        //lb_Zones.ItemsSource = By_Zone.Select(c => { c.Zones = "\t" + c.Zones.Replace(",", "\n\t"); return c; }).Select(c => { c.Addons = c.Addons.Replace(",", "\n"); return c; });
        lb_Addons.ItemsSource = All_Addons;
      }
    }

    private bool Validate_Paths()
    {
      if (!Directory.Exists(Properties.Settings.Default.Windower_Path))
      {
        MessageBox.Show("Click Browse and select your Windower4's base directory");
        return false;
      }

      Windower_Addons_Path = Properties.Settings.Default.Windower_Path + @"\addons";
      if (!Directory.Exists(Windower_Addons_Path))
      {
        MessageBox.Show("Unable to find addons path at:" + Environment.NewLine + Windower_Addons_Path);
        return false;
      }

      Windower_Plugins_Path = Properties.Settings.Default.Windower_Path + @"\plugins";
      if (!Directory.Exists(Windower_Plugins_Path))
      {
        MessageBox.Show("Unable to find plugins path at:" + Environment.NewLine + Windower_Plugins_Path);
        return false;
      }

      Addon_Manager_Data_Path = Windower_Addons_Path + @"\addon_manager\data";
      if (!Directory.Exists(Addon_Manager_Data_Path))
      {
        MessageBox.Show("Unable to find addon_manager path at:" + Environment.NewLine + Addon_Manager_Data_Path);
        return false;
      }
      Settings_File = Addon_Manager_Data_Path + @"\" + Settings_File;

      Windower_Res_Path = Properties.Settings.Default.Windower_Path + @"\res";
      if (!Directory.Exists(Windower_Res_Path))
      {
        MessageBox.Show("Unable to find windower resources path at:" + Environment.NewLine + Windower_Res_Path);
        return false;
      }

      Windower_AutoLoad_Path = Properties.Settings.Default.Windower_Path + @"\scripts\autoload";
      if (!Directory.Exists(Windower_Res_Path))
      {
        MessageBox.Show("Unable to find autoload path at:" + Environment.NewLine + Windower_Res_Path);
        return false;
      }

      return true;
    }




    private List<string> Get_All_Zones()
    {
      var list = new List<string>() { Zones_Header };
      var lua = new Lua();
      var result = lua.DoFile(Windower_Res_Path + @"\zones.lua");

      foreach (var entry in result)
      {
        var tbl = (LuaTable)entry;
        foreach (KeyValuePair<object, object> item in tbl)
        {
          try
          {
            foreach (KeyValuePair<object, object> inner in (LuaTable)item.Value)
            {
              if (inner.Key.ToString() == "en")
              {
                var zone = inner.Value.ToString();
                if (zone != "unknown")
                  list.Add(zone);
              }
            }

          }
          catch { }
        }
      }

      list.Sort();
      return list;
    }
    
    private List<string> Get_Autoload_Addons()
    { 
      var list = new List<string>();
      foreach(var l in File.ReadAllLines(Windower_AutoLoad_Path + @"\autoload.txt"))
      {
        var line = l.ToLower().Replace(";", "");
        if (!string.IsNullOrEmpty(line) && !line.StartsWith("load") && !line.StartsWith("//") && !line.StartsWith("wait") && !line.StartsWith("exec"))
        {
          list.Add(line.Replace("lua load ", "") + " *");
        }
      }
      return list;
    }

    private List<string> Get_Addons(bool includeAutoload = false)
    {
      var list = new List<string>() { " " };
      var basedir = new DirectoryInfo(Windower_Addons_Path);
      list.AddRange(basedir.GetDirectories().Where(x => !x.Name.StartsWith(".") && !Autoload_Addons.Contains(x.Name.ToLower() + " *")).Select(x => x.Name.ToLower()).ToArray());

      if (includeAutoload)
      { 
        list.AddRange(Autoload_Addons); 
      }

      list.Sort();
      return list;
    }

    private List<string> Get_Plugins()
    {
      var list = new List<string>();
      var basedir = new DirectoryInfo(Windower_Plugins_Path);
      list.AddRange(basedir.GetFiles().Where(x => x.Name.EndsWith(".dll")).Select(x => x.Name.Replace(".dll", "")).ToArray());
      return list;
    }

    public List<string> Get_Global_Addons()
    {
      var list = new List<string>();
      foreach (var entry in ((string)Settings.global.globaladdons).Split(','))
      {
        var addon = entry;
        if (Autoload_Addons.Where(x => x.ToLower().Replace(" *", "") == addon.ToLower()).FirstOrDefault() != null)
        {
          addon = entry + " *";
        }
        list.Add(addon);
      }
      return list;
    }

    public List<CharSettings> Get_Character_Addons()
    {
      var csList = new List<CharSettings>();
      foreach (var entry in Settings.global.playeraddons.Members)
      {
        var c = (string)entry.Key;
        if (c != "__value" && c != "ToString")
        {
          try
          {
            var v = (string)entry.Value.ToString();
            if (string.IsNullOrEmpty(v))
              continue;

            var list = new List<string>();
            foreach (var e in v.Split(','))
            {
              var addon = e;
              if (Autoload_Addons.Where(x => x.ToLower().Replace(" *", "") == addon.ToLower()).FirstOrDefault() != null)
              {
                addon = e + " *";
              }
              list.Add(addon);
            }

            csList.Add(new CharSettings { Name = c.ToUpper(), Addons = list });
          }
          catch
          {
            continue;
          }
        }
      }

      return csList;
    }

    public List<JobSettings> Get_Jobs_Addons()
    {
      var jobs = new List<JobSettings>();
      foreach (var entry in Settings.global.byjob.Members)
      {
        var c = (string)entry.Key;
        if (c != "__value" && c != "ToString")
        {
          try
          {
            var v = (string)entry.Value.addons.ToString();
            if (string.IsNullOrEmpty(v))
              continue;

            var list = new List<string>();
            foreach (var e in v.Split(','))
            {
              var addon = e;
              if (Autoload_Addons.Where(x => x.ToLower().Replace(" *", "") == addon.ToLower()).FirstOrDefault() != null)
              {
                addon = e + " *";
              }
              list.Add(addon);
            }

            jobs.Add(new JobSettings
            {
              Job = c.ToUpper(),
              Addons = list
            });
          }
          catch
          {
            continue;
          }
        }
      }

      return jobs;
    }

    private List<ZoneSettings> Get_ZoneSettings()
    {
      var zsList = new List<ZoneSettings>();
      foreach (var entry in Settings.global.byzone.Members)
      {
        var c = (string)entry.Key;
        if (c != "__value" && c != "ToString")
        {
          try
          {
            var z = (string)entry.Value.zonenames;
            var a = (string)entry.Value.addons;
            var zones = !string.IsNullOrEmpty(z) ? z.Split(',').ToList() : new List<string>();
            var addons = !string.IsNullOrEmpty(a) ? a.Split(',').ToList() : new List<string>();

            var list = new List<string>();
            foreach (var e in addons)
            {
              var addon = e;
              if (Autoload_Addons.Where(x => x.ToLower().Replace(" *", "") == addon.ToLower()).FirstOrDefault() != null)
              {
                addon = e + " *";
              }
              list.Add(addon);
            }

            zsList.Add(new ZoneSettings
            {
              GroupId = c.ToUpper(),
              Zones = zones,
              Addons = list
            });
          }
          catch { continue; }
        }
      }

      return zsList;
    }


    

    private void Btn_AddGlobal_Click(object sender, RoutedEventArgs e)
    {
      var selected = lb_Addons.SelectedItem.ToString();
      if (!string.IsNullOrEmpty(selected.Trim()))
      {
        if (!Global_Addons.Contains(selected))
        {
          UnsavedChanges = true;
          Global_Addons.Add(selected);

          Set_Status();

          lb_GlobalAddons.Items.Refresh();
        }
      }
    }

    private void Btn_AddCharacter_Click(object sender, RoutedEventArgs e)
    {
      var ch = Txt_Character.Text;
      var selected = lb_Addons.SelectedItem.ToString();
      if (!string.IsNullOrEmpty(ch.Trim()) && !string.IsNullOrEmpty(selected.Trim()))
      {
        var entry = Player_Addons.Where(x => x.Name.ToUpper() == ch.ToUpper()).FirstOrDefault();
        if (entry != null)
        {
          if (!entry.Addons.Contains(selected))
          {
            UnsavedChanges = true;
            entry.Addons.Add(selected); 
          }
        }
        else
        {
          UnsavedChanges = true;
          Player_Addons.Add(new CharSettings
          {
            Name = ch.ToUpper(),
            Addons = new List<string> { selected }
          });
        }

        Set_Status();

        lb_Character.Items.Refresh();
      }
    }

    private void Btn_AddJob_Click(object sender, RoutedEventArgs e)
    {
      var ch = Cb_Jobs.Items[Cb_Jobs.SelectedIndex].ToString();
      var selected = lb_Addons.SelectedItem.ToString();
      if (!string.IsNullOrEmpty(ch.Trim()) && !string.IsNullOrEmpty(selected.Trim()))
      {
        var entry = By_Job.Where(x => x.Job.ToUpper() == ch.ToUpper()).FirstOrDefault();
        if (entry != null)
        {
          if (!entry.Addons.Contains(selected))
          {
            UnsavedChanges = true;
            entry.Addons.Add(selected);
          } 
        }
        else
        {
          UnsavedChanges = true;
          By_Job.Add(new JobSettings
          {
            Job = ch.ToUpper(),
            Addons = new List<string> { selected }
          });
        }

        Set_Status();

        lb_Jobs.Items.Refresh();
      }
    }

    private void Btn_AddZone_Click(object sender, RoutedEventArgs e)
    {
      var groupid = Cb_ZoneGroups.SelectedIndex != -1 ? Cb_ZoneGroups.Items[Cb_ZoneGroups.SelectedIndex].ToString() : string.Empty;
      var zone = (Cb_Zones.SelectedIndex != -1 && Cb_Zones.Items[Cb_Zones.SelectedIndex].ToString() != Zones_Header) ? Cb_Zones.Items[Cb_Zones.SelectedIndex].ToString() : string.Empty;
      var addon = lb_Addons.SelectedItem != null ? lb_Addons.SelectedItem.ToString() : string.Empty;
      
      if (string.IsNullOrEmpty(zone.Trim()) && string.IsNullOrEmpty(addon.Trim())) return;

      ZoneSettings entry = new ZoneSettings();
      if (string.IsNullOrEmpty(groupid) || groupid.ToLower() == ZoneGroups_Header.ToLower())
      {
        // Generate a new group id.
        groupid = Generate_GroupId().ToUpper();
        Zone_Groups.Add(groupid);

        UnsavedChanges = true;
        entry = new ZoneSettings()
        {
          GroupId = groupid,
          Zones = !string.IsNullOrEmpty(zone) ? new List<string>() { zone } : new List<string>(),
          Addons = !string.IsNullOrEmpty(addon) ? new List<string>() { addon } : new List<string>()
        };
        

        By_Zone.Add(entry);
        Cb_ZoneGroups.Items.Refresh();
      }
      else
      {
        entry = By_Zone.Where(x => x.GroupId.ToLower() == groupid.ToLower()).FirstOrDefault();
        if (entry != null)
        {
          if (!string.IsNullOrEmpty(zone.Trim()) && entry.Zones.Where(x => x.ToLower() == zone.ToLower()).FirstOrDefault() == null)
          { 
            UnsavedChanges = true;
            entry.Zones.Add(zone);
          } 

          if (!string.IsNullOrEmpty(addon.Trim()) && entry.Addons.Where(x => x.ToLower() == addon.ToLower()).FirstOrDefault() == null)
          {
            UnsavedChanges = true;
            entry.Addons.Add(addon);
          } 
        }
        else 
        {
          MessageBox.Show("An error occured while trying to find the GroupId within by_zone.\n" + groupid);
        }
      }

      Set_Status();

      lb_Zones.Items.Refresh();
    }

    private string Generate_GroupId()
    {
      var newid = "group_" + RandomString(5);
      if (By_Zone.Where(x => x.GroupId == newid).FirstOrDefault() != null)
        Generate_GroupId();

      return newid;
    }

    private static Random random = new Random();
    public static string RandomString(int length)
    {
      const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      return new string(Enumerable.Repeat(chars, length)
        .Select(s => s[random.Next(s.Length)]).ToArray());
    }


    private void GlobalListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      if (lb_GlobalAddons.SelectedIndex != -1)
      {
        UnsavedChanges = true;
        Global_Addons.RemoveAt(lb_GlobalAddons.SelectedIndex);

        Set_Status();

        lb_GlobalAddons.Items.Refresh();
      }
    }

    private void CharListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      var lb = (System.Windows.Controls.ListBox)sender;
      var name = ((TextBlock)((StackPanel)lb.Parent).Children[0]).Text;
      //MessageBox.Show(name);
      if (lb.SelectedIndex != -1)
      {
        UnsavedChanges = true;
        Player_Addons.Where(x => x.Name.ToUpper() == name).FirstOrDefault().Addons.Remove(lb.Items[lb.SelectedIndex].ToString());

        Set_Status();

        lb_Character.Items.Refresh();
      }
    }
    
    private void JobListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      var lb = (System.Windows.Controls.ListBox)sender;
      var name = ((TextBlock)((StackPanel)lb.Parent).Children[0]).Text;
      if (lb.SelectedIndex != -1)
      {
        UnsavedChanges = true;
        By_Job.Where(x => x.Job.ToUpper() == name).FirstOrDefault().Addons.Remove(lb.Items[lb.SelectedIndex].ToString());

        Set_Status();

        lb_Jobs.Items.Refresh();
      }
    }

    private void ZoneListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      var lb = (System.Windows.Controls.ListBox)sender;
      var grid = (Grid)lb.Parent;
      var id = ((TextBlock)((StackPanel)grid.Parent).Children[0]).Text;
      if (lb.SelectedIndex != -1)
      {
        try
        {
          var zs = By_Zone.Where(x => x.GroupId.ToUpper() == id).FirstOrDefault();
          if (zs != null && zs.Zones != null)
          {
            zs.Zones.Remove(lb.Items[lb.SelectedIndex].ToString());
            UnsavedChanges = true;

            Set_Status();

            lb_Zones.Items.Refresh();
          }
        }
        catch { } 
      }
    }

    private void ZoneAddonListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      var lb = (System.Windows.Controls.ListBox)sender;
      var grid = (Grid)lb.Parent;
      var name = ((TextBlock)((StackPanel)grid.Parent).Children[0]).Text;
      if (lb.SelectedIndex != -1)
      {
        UnsavedChanges = true;
        By_Zone.Where(x => x.GroupId.ToUpper() == name).FirstOrDefault().Addons.Remove(lb.Items[lb.SelectedIndex].ToString());

        Set_Status();

        lb_Zones.Items.Refresh();
      }
    }
        



    private void Btn_SaveSettings_Click(object sender, RoutedEventArgs e)
    {
      var xml = Generate_SettingsXml();
      Save_To_File(xml, Settings_File);
      UnsavedChanges = false;
    }

    private void Save_To_File(string xml, string filepath)
    {
      File.WriteAllText(filepath, xml);
      //MessageBox.Show("Settings Saved to: \n" + filepath);

      Txt_Status.Foreground = new SolidColorBrush(Colors.Green);
      Txt_Status.Text = "SAVED!";
    }

    private string Generate_SettingsXml()
    {
      // remove the ' *' autoload indicator from the addon names
      var globaladdons = new List<string>();
      foreach (var addon in Global_Addons)
        globaladdons.Add(addon.Replace(" *", ""));

      var xml = "<?xml version=\"1.1\" ?>" + Environment.NewLine;
      xml += "<settings>" + Environment.NewLine;
      xml += "    <global>" + Environment.NewLine;
      xml += "        <global_addons>" + string.Join(",", globaladdons) + "</global_addons>" + Environment.NewLine;


      // CHARACTER
      var playeraddons = new List<CharSettings>();
      foreach (var entry in Player_Addons)
      {
        var cs = new CharSettings
        {
          Name = entry.Name,
          Addons = new List<string>()
        };
        foreach (var addon in entry.Addons)
          cs.Addons.Add(addon.Replace(" *", ""));
        playeraddons.Add(cs);
      }

      xml += "        <player_addons>" + Environment.NewLine;
      foreach (var entry in playeraddons)
      {
        var playername = entry.Name.ToLower();
        var addons = string.Join(",", entry.Addons);
        xml += "            <" + playername + ">" + string.Join(",", entry.Addons) + "</" + playername + ">" + Environment.NewLine;
      }
      xml += "        </player_addons>" + Environment.NewLine;
      xml += "        <player_plugins></player_plugins>" + Environment.NewLine;


      // JOB
      var byjob = new List<JobSettings>();
      foreach (var entry in By_Job)
      {
        var cs = new JobSettings
        {
          Job = entry.Job,
          Addons = new List<string>()
        };
        foreach (var addon in entry.Addons)
          cs.Addons.Add(addon.Replace(" *", ""));
        byjob.Add(cs);
      }

      xml += "        <by_job>" + Environment.NewLine;
      foreach (var entry in byjob)
      {
        var job = entry.Job.ToLower();
        var addons = string.Join(",", entry.Addons);
        xml += "            <" + job + ">" + Environment.NewLine;
        xml += "              <addons>" + string.Join(",", entry.Addons) + "</addons>" + Environment.NewLine;
        xml += "              <plugins></plugins>" + Environment.NewLine;
        xml += "              <ignore></ignore>" + Environment.NewLine;
        xml += "            </" + job + ">" + Environment.NewLine;
      }

      foreach (var entry in All_Jobs) // add empty entries for jobs without addons.
      {
        var job = entry.Trim().ToLower();
        if (string.IsNullOrEmpty(job) || By_Job.Where(x => x.Job.ToLower() == job).FirstOrDefault() != null)
          continue;

        xml += "            <" + job + ">" + Environment.NewLine;
        xml += "              <addons></addons>" + Environment.NewLine;
        xml += "              <plugins></plugins>" + Environment.NewLine;
        xml += "              <ignore></ignore>" + Environment.NewLine;
        xml += "            </" + job + ">" + Environment.NewLine;
      }
      xml += "        </by_job>" + Environment.NewLine;


      // ZONE
      var byzone = new List<ZoneSettings>();
      foreach (var entry in By_Zone)
      {
        var cs = new ZoneSettings
        {
          GroupId = entry.GroupId,
          Zones = entry.Zones,
          Addons = new List<string>()
        };
        foreach (var addon in entry.Addons)
          cs.Addons.Add(addon.Replace(" *", ""));
        byzone.Add(cs);
      }

      xml += "        <by_zone>" + Environment.NewLine;
      foreach (var entry in byzone)
      {
        var key = entry.GroupId.ToLower();
        var addons = string.Join(",", entry.Addons);
        xml += "            <" + key + ">" + Environment.NewLine;
        xml += "              <zone_names>" + string.Join(",", entry.Zones) + "</zone_names>" + Environment.NewLine;
        xml += "              <addons>" + string.Join(",", entry.Addons) + "</addons>" + Environment.NewLine;
        xml += "              <plugins></plugins>" + Environment.NewLine;
        xml += "              <ignore></ignore>" + Environment.NewLine;
        xml += "            </" + key + ">" + Environment.NewLine;
      }
      xml += "        </by_zone>" + Environment.NewLine;

      xml += "        <load_delay>5</load_delay>" + Environment.NewLine;
      xml += "    </global>" + Environment.NewLine;
      xml += "</settings>" + Environment.NewLine;

      return xml;
    }

    private void Set_Status()
    {
      if (UnsavedChanges)
      {
        Txt_Status.Foreground = new SolidColorBrush(Colors.Red);
        Txt_Status.Text = PendingStr;
      }
      else
      {
        Txt_Status.Foreground = new SolidColorBrush(Colors.Green);
        Txt_Status.Text = "";
      }
    }


    private void Deselect_ListBoxItem(object sender, RoutedEventArgs e)
    {
      var lb = (System.Windows.Controls.ListBox)sender;
      lb.SelectedIndex = -1;
    }

    private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      if (UnsavedChanges)
      {
        if (MessageBox.Show("You have pending changes...\n\nWould you like to save your settings?", "Pending Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
          Save_To_File(Generate_SettingsXml(), Settings_File);
        }
      }

      Properties.Settings.Default.Save();
    }

    private void Include_Autoload_Checked(object sender, RoutedEventArgs e)
    {
      All_Addons = Get_Addons(true);
      lb_Addons.ItemsSource = All_Addons;
      lb_Addons.Items.Refresh();
    }

    private void Include_Autoload_Unchecked(object sender, RoutedEventArgs e)
    {
      All_Addons = Get_Addons(false);
      lb_Addons.ItemsSource = All_Addons;
      lb_Addons.Items.Refresh();
    }
  }

  public class CharSettings
  {
    public string Name { get; set; }
    public List<string> Addons { get; set; }
  }

  public class JobSettings
  {
    public string Job { get; set; }
    public List<string> Addons { get; set; }
  }

  public class ZoneSettings
  {
    public string GroupId { get; set; }
    public List<string> Zones { get; set; }
    public List<string> Addons { get; set; }
    public List<string> Ignore { get; set; }
  }

}
