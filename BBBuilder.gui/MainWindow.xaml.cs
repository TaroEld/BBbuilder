using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using BBBuilder;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.ComponentModel.Design;


namespace BBBuilder_gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ConfigCommand ConfigCommand;
        InitCommand InitCommand;
        ExtractCommand ExtractCommand;
        BuildCommand BuildCommand;
        readonly OutputWriter OutputWriter;
        public MainWindow()
        {
            InitializeComponent();
            OutputWriter = new OutputWriter(ConsoleOutput);
            Console.SetOut(OutputWriter);
            Console.SetError(OutputWriter);
            SetupTabs();
        }

        private void SetupTabs()
        {
            SetupConfigTab();
            SetupInitTab();
            SetupExtractTab();
            SetupBuildTab();
        }

        private void RunCommandInTryBlock(Command command, string[] _args)
        {
            try
            {
                command.HandleCommand(_args);
            }
            catch (Exception ex)
            {
                Console.WriteLine("AN ERROR OCCURED IN the '" + command.Name + "' command. Post it on the modding discord.");
                Console.WriteLine(ex.ToString());
            }
        }
        private void SetupConfigTab()
        {
            ConfigCommand = new ConfigCommand();
            Utils.ReadConfigDataFromJSON();
            DataPath.Text = Utils.Data.GamePath;
            DataPath.ToolTip = ConfigCommand.DataPath.Description;
            ModsPath.Text = Utils.Data.ModPath;
            ModsPath.ToolTip = ConfigCommand.ModsPath.Description;
            MoveZip.IsChecked = Utils.Data.MoveZip;
            MoveZip.ToolTip = ConfigCommand.MoveZip.Description;
            UseSteam.IsChecked = Utils.Data.UseSteam;
            UseSteam.ToolTip = ConfigCommand.UseSteam.Description;
            Verbose.IsChecked = Utils.Data.Verbose;
            Verbose.ToolTip = ConfigCommand.Verbose.Description;
            LogTime.IsChecked = Utils.Data.LogTime;
            LogTime.ToolTip = ConfigCommand.LogTime.Description;

            foreach (string folder in Utils.Data.FoldersArray)
            {
                Folders.Items.Add(folder);
            }
            Folders.ToolTip = ConfigCommand.Folders.Description;
        }

        private void Run_Config_Command(object sender, RoutedEventArgs e)
        {
            OutputWriter.Clear();
            if (DataPath.Text == "" || ModsPath.Text == "")
            {
                Console.WriteLine("DataPath and ModsPath musn't be empty!");
                return;
            }
            ConfigCommand = new ConfigCommand();
            var c = ConfigCommand;
            List<string> commands = new() { c.Name };
            commands.AddRange(new List<string> { c.DataPath.Flag, DataPath.Text });
            commands.AddRange(new List<string> { c.ModsPath.Flag, ModsPath.Text });
            commands.AddRange(new List<string> { c.MoveZip.Flag, MoveZip.IsChecked.ToString() });
            commands.AddRange(new List<string> { c.UseSteam.Flag, UseSteam.IsChecked.ToString() });
            commands.AddRange(new List<string> { c.Verbose.Flag, Verbose.IsChecked.ToString() });
            commands.AddRange(new List<string> { c.LogTime.Flag, LogTime.IsChecked.ToString() });
            if (Folders.Items.Count > 0)
            {
                commands.AddRange(new List<string> { c.Folders.Flag, string.Join(",", Folders.Items.Cast<string>()) });
            }
            else
            {
                commands.AddRange(new List<string> { c.Folders.Flag, "clear" });
            }
            RunCommandInTryBlock(ConfigCommand, commands.ToArray());
        }

        private void On(object sender, RoutedEventArgs e)
        {

        }

        private void On_Data_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK) {
                DataPath.Text = dialog.SelectedPath;
            }
        }

        private void Folders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void On_Add_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Folders.Items.Add(dialog.SelectedPath);
            }
        }

        private void On_Clear_Folders_Click(object sender, RoutedEventArgs e)
        {
            Folders.Items.Clear();
        }

        private void On_Mods_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ModsPath.Text = dialog.SelectedPath;
            }
        }

        private void On_Clear_Config_Clicked(object sender, RoutedEventArgs e)
        {
            ConfigCommand = new ConfigCommand();
            var c = ConfigCommand;
            List<string> commands = new() { c.Name, ConfigCommand.Clear.Flag };
            RunCommandInTryBlock(ConfigCommand, commands.ToArray());
            this.SetupConfigTab();
        }

        private void SetupInitTab()
        {
            InitCommand = new InitCommand();
            string[] templateFolders = Directory.GetDirectories(System.IO.Path.Combine(Utils.EXECUTINGFOLDER, "Templates")).Select((f) => new DirectoryInfo(f).Name).ToArray();
            foreach (string folder in templateFolders) {
                SelectTemplateCombo.Items.Add(folder);
                if (folder == "default")
                {
                    SelectTemplateCombo.SelectedIndex = SelectTemplateCombo.Items.IndexOf(folder);
                }
            }
            SelectTemplateCombo.ToolTip = InitCommand.Template.Description;
            InitModName.ToolTip = InitCommand.Arguments[0];
            InitFolder.ToolTip = "Directory in which the new mod is placed. Takes its value from the config option by default.";
            InitReplace.ToolTip = InitCommand.Replace.Description;
        }

        private void On_Init_Select_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                InitFolder.Text = dialog.SelectedPath;
            }
        }

        private void On_Init_Run_Click(object sender, RoutedEventArgs e)
        {
            InitCommand = new InitCommand();
            OutputWriter.Clear();
            if (InitModName.Text.Length == 0)
            {
                Console.WriteLine("Name of the mod mustn't be empty!");
                return;
            }
            var commands = new List<string> { InitCommand.Name, InitModName.Text};
            if (InitReplace.IsChecked == true)
            {
                commands.Add(InitCommand.Replace.Flag);
            }
            
            commands.AddRange(new List<string> { InitCommand.Template.Flag, SelectTemplateCombo.SelectedItem.ToString()});
            commands.AddRange(new List<string> { InitCommand.AltPath.Flag, InitFolder.Text});
            RunCommandInTryBlock(InitCommand, commands.ToArray());
        }

        private void SetupExtractTab()
        {
            ExtractCommand = new ExtractCommand();
            ExtractZip.ToolTip = "Path to the zip file to extract.";
            ExtractName.ToolTip = "New name of the zip file (optional, if none is entered, the name of the zip file is taken instead).";
            ExtractFolder.ToolTip = "Directory in which the new mod is placed.";
            ExtractReplace.ToolTip = ExtractCommand.Replace.Description;
        }

        private void On_Extract_Select_Zip_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.OpenFileDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ExtractZip.Text = dialog.FileName;
            }
        }

        private void On_Extract_Select_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ExtractFolder.Text = dialog.SelectedPath;
            }
        }

        private void On_Extract_Run_Click(object sender, RoutedEventArgs e)
        {
            ExtractCommand = new ExtractCommand();
            OutputWriter.Clear();
            if (ExtractZip.Text.Length == 0)
            {
                Console.WriteLine("Path to zip file mustn't be empty!");
                return;
            }
            var commands = new List<string> { ExtractCommand.Name, ExtractZip.Text };
            if (ExtractReplace.IsChecked == true)
            {
                commands.Add(ExtractCommand.Replace.Flag);
            }
            commands.AddRange(new List<string> { ExtractCommand.AltPath.Flag, ExtractFolder.Text });
            RunCommandInTryBlock(ExtractCommand, commands.ToArray());
        }

        private void SetupBuildTab()
        {
            BuildCommand = new BuildCommand();
            BuildFolder.ToolTip = "Path of the folder of the mod to build.";
            BuildRestart.ToolTip = BuildCommand.StartGame.Description;
            BuildRebuild.ToolTip = BuildCommand.Rebuild.Description;
        }

        private void On_Build_Select_Folder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog();
            Forms.DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                BuildFolder.Text = dialog.SelectedPath;
            }
        }

        private void On_Build_Run_Click(object sender, RoutedEventArgs e)
        {
            Utils.Stopwatch.Reset();
            Utils.Stopwatch.Start();
            Utils.LastTime = 0;
            BuildCommand = new BuildCommand();

            if (BuildFolder.Text.Length == 0)
            {
                Console.WriteLine("Path to the mod to build mustn't be empty!");
                return;
            }
            var commands = new List<string> { BuildCommand.Name, BuildFolder.Text };
            if (BuildRestart.IsChecked == true)
            {
                commands.Add(BuildCommand.StartGame.Flag);
            }
            if (BuildRebuild.IsChecked == true)
            {
                commands.Add(BuildCommand.Rebuild.Flag);
            }
            RunCommandInTryBlock(BuildCommand, commands.ToArray());
        }

        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
