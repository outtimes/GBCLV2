﻿using LitJson;
using System;
using System.Windows;
using System.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;


namespace GBCLV2.Pages
{
    public partial class ResourcepackPage : Page
    {
        private class ResPack
        {
            public bool         IsEnabled       { get; set; }
            public int          Format          { get; set; }
            public string       Name            { get; set; }
            public string       Description     { get; set; }
            public BitmapImage  Cover           { get; set; }
        }

        private ObservableCollection<ResPack> Enabled_Pack = new ObservableCollection<ResPack>();
        private ObservableCollection<ResPack> Disabled_Pack = new ObservableCollection<ResPack>();

        private string PacksDir;
        private string OptionsDir;
        private static string LineToReplace;
        private static string[] EnabledPackNames;

        public ResourcepackPage()
        {
            InitializeComponent();

            string rootPath;
            if (App.Versions.Any() && App.Config.VersionSplit)
            {
                rootPath = $"{App.Core.GameRootPath}\\versions\\{App.Versions[App.Config.VersionIndex].ID}\\";
            }
            else
            {
                rootPath = App.Core.GameRootPath + "\\";
            }
            PacksDir = rootPath + "resourcepacks\\";
            OptionsDir = rootPath + "options.txt";

            if (!Directory.Exists(PacksDir))
            {
                Directory.CreateDirectory(PacksDir);
            }

            EnabledPacksList.ItemsSource = Enabled_Pack;
            DisabledPacksList.ItemsSource = Disabled_Pack;

            LoadOptions();
            Task.Run(() => LoadResPacks());

            refresh_button.Click    += (s, e) => LoadResPacks();
            openfolder_button.Click += (s, e) => System.Diagnostics.Process.Start("explorer.exe", PacksDir);
        }

        private void LoadOptions()
        {
            if (!File.Exists(OptionsDir)) return;
            StreamReader sr = new StreamReader(OptionsDir, Encoding.Default);
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                if (line.StartsWith("resourcePacks"))
                {
                    LineToReplace = line;
                    if (line.Length > 16)
                    {
                        EnabledPackNames = line.Substring(15, line.Length - 16).Split(',');
                    }
                    break;
                }
            }

            if(EnabledPackNames!= null)
            {
                for(int i = 0; i < EnabledPackNames.Length; i++)
                {
                    EnabledPackNames[i] = EnabledPackNames[i].Substring(1, EnabledPackNames[i].Length - 2);
                }
            }
        }

        private void LoadResPacks()
        {

            Dispatcher.BeginInvoke((Action)delegate ()
            {
                Enabled_Pack.Clear();
                Disabled_Pack.Clear();
            });

            if (EnabledPackNames != null)
            {
                for(int i = EnabledPackNames.Length - 1; i >=0; i--)
                {
                    var path = PacksDir + EnabledPackNames[i];
                    if(File.Exists(path) || Directory.Exists(path))
                    {
                        var pack = GetResPackFromDisk(PacksDir + EnabledPackNames[i]);
                        pack.IsEnabled = true;

                        Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Enabled_Pack.Add(pack);
                        });
                    }
                }
            }

            uint count = 0;

            foreach (var path in Directory.EnumerateFiles(PacksDir,"*.zip").Concat(Directory.EnumerateDirectories(PacksDir)))
            {
                bool NotEnabled = true;
                if(EnabledPackNames != null && count < EnabledPackNames.Length)
                {
                    foreach (var str in EnabledPackNames)
                    {
                        if (Path.GetFileName(path) == str)
                        {
                            NotEnabled = false;
                            count++;
                            break;
                        }
                    }
                }

                if(NotEnabled)
                {
                    var pack = GetResPackFromDisk(path);
                    pack.IsEnabled = false;

                    Dispatcher.BeginInvoke((Action)delegate ()
                    {
                        Disabled_Pack.Add(pack);
                    });
                }
            }
        }

        private ResPack GetResPackFromDisk(string path)
        {
            ResPack pack = new ResPack()
            {
                Name = Path.GetFileName(path),
            };

            if(path.EndsWith(".zip"))
            {
                using (var archive = ZipFile.OpenRead(path))
                {
                    using (var sr = new StreamReader(archive.GetEntry("pack.mcmeta").Open(), Encoding.UTF8))
                    {
                        GetPackInfo(sr.ReadToEnd(), ref pack);
                        sr.Dispose();
                    }

                    using (var stream = archive.GetEntry("pack.png").Open())
                    {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);

                        GetPackCover(ms, ref pack);
                        stream.Dispose();
                        ms.Dispose();
                    }
                    archive.Dispose();
                }
            }
            else
            {
                if (File.Exists(path + @"/pack.mcmeta"))
                {
                    GetPackInfo(File.ReadAllText(path + @"/pack.mcmeta", Encoding.UTF8), ref pack);
                }

                if (File.Exists(path + @"/pack.png"))
                {
                    var fs = File.Open(path + @"/pack.png", FileMode.Open);
                    GetPackCover(fs, ref pack);
                    fs.Dispose();
                }
            }

            return pack;

            void GetPackCover(Stream stream, ref ResPack _pack)
            {
                _pack.Cover = new BitmapImage();
                _pack.Cover.BeginInit();
                _pack.Cover.StreamSource = stream;
                _pack.Cover.DecodePixelWidth = 64;
                _pack.Cover.DecodePixelHeight = 64;
                _pack.Cover.CacheOption = BitmapCacheOption.OnLoad;
                _pack.Cover.EndInit();
                _pack.Cover.Freeze();
            }

            void GetPackInfo(string str, ref ResPack _pack)
            {
                var PackInfo = JsonMapper.ToObject(str)[0];
                _pack.Format = (int)PackInfo["pack_format"];

                string MC_Version = "适用版本：";

                if (_pack.Format == 1)
                {
                    MC_Version += "1.8及以下\n";
                }
                else if (_pack.Format == 2)
                {
                    MC_Version += "1.9-1.10\n";
                }
                else if (_pack.Format == 3)
                {
                    MC_Version += "1.11及以上\n";
                }

                _pack.Description = MC_Version + PackInfo["description"].ToString();
            }
        }

        private void EnablePack(object sender, RoutedEventArgs e)
        {
            var pack = (sender as Button).DataContext as ResPack;
            pack.IsEnabled = true;

            Enabled_Pack.Insert(0, pack);
            Disabled_Pack.Remove(pack);
        }

        private void DisablePack(object sender, RoutedEventArgs e)
        {
            var pack = (sender as Button).DataContext as ResPack;
            pack.IsEnabled = false;

            Disabled_Pack.Insert(0, pack);
            Enabled_Pack.Remove(pack);
        }

        private void MovePackUp(object sender, RoutedEventArgs e)
        {
            int i = Enabled_Pack.IndexOf((sender as Button).DataContext as ResPack);
            if(i > 0)
            {
                Enabled_Pack.Move(i, i - 1);
            }
        }

        private void MovePackDown(object sender, RoutedEventArgs e)
        {
            int i = Enabled_Pack.IndexOf((sender as Button).DataContext as ResPack);
            if (i < Enabled_Pack.Count - 1)
            {
                Enabled_Pack.Move(i, i + 1);
            }
        }

        private void Add_New(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                Multiselect = true,
                Title = "请选择资源包",
                Filter = "Minrcraft资源包|*.zip",
            };

            if (dialog.ShowDialog() ?? false)
            {
                foreach (var path in dialog.FileNames)
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        if (archive.GetEntry("pack.mcmeta") == null)
                        {
                            MessageBox.Show(path + "\n不是有效的资源包文件", "你可能选了假资源包", MessageBoxButton.OK, MessageBoxImage.Information);
                            continue;
                        }
                    }

                    var CopyTo = PacksDir + Path.GetFileName(path);

                    if(!File.Exists(CopyTo))
                    {
                        var pack = GetResPackFromDisk(path);
                        pack.IsEnabled = false;
                        File.Copy(path, CopyTo);
                        Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Disabled_Pack.Add(pack);
                        });
                    }
                }
            }
        }

        private void Go_Back(object sender, RoutedEventArgs e)
        {
            if(Enabled_Pack.Any())
            {
                string s = "resourcePacks:[";
                string options_text;

                for (int i = Enabled_Pack.Count - 1; i >= 0; --i)
                {
                    s += "\"" + Enabled_Pack[i].Name + "\"" + ",";
                }

                s = s.Remove(s.Length - 1);
                s += "]";

                if (LineToReplace != null)
                {
                    if (File.Exists(OptionsDir))
                    {
                        options_text = File.ReadAllText(OptionsDir, Encoding.Default).Replace(LineToReplace, s);
                    }
                    else
                    {
                        options_text = s;
                    }

                    try
                    {
                        File.WriteAllText(OptionsDir, options_text, Encoding.Default);
                    }
                    catch
                    {
                        MessageBox.Show("options.txt可能被占用", "写入失败", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    LineToReplace = null;
                }
            }

            NavigationService.GoBack();
        }
    }
}
