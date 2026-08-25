using Autodesk.Windows;
using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.IO;
using Autodesk.AutoCAD.Windows;
using System.Windows.Forms.Integration;

namespace Autocad_Primavera_P6_Plugin.Services
{
    public class RibbonService
    {
        private static MyPlugin _pluginInstance = null;
        private static bool _isTabCreated = false;

        private const string _TabId = "Autocad_Primavera_P6_Plugin_Tab";
        private const string _TabTitle = "Autocad P6 Plugin";

        private static RibbonTab _PluginTab = null;
        private static RibbonPanel _ActionsPanel = null;
        private static RibbonPanel _SettingsPanel = null;
        private static RibbonPanel _TestPanel = null;

        private static RibbonButton _PropertiesButton = null;
        private static RibbonButton _InsertBlocksButton = null;

        private static RibbonButton _PluginStatusButton = null;
        private static RibbonButton _P6ConnectionManagerButton = null;
        private static RibbonButton _LinkedFoldersButton = null;

        private static RibbonButton _testButton = null;

        public RibbonService(MyPlugin pluginInstance)
        {
            // Initialize Ribbon UI setup here
            _pluginInstance = pluginInstance;
            InItRibbon();
        }

        public static void InItRibbon()
        {
            if (_isTabCreated) return;

            try
            {
                if (ComponentManager.Ribbon == null) { Application.Idle += OnIdleCreateRibbon; return; }
                CreateRibbon();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private static void OnIdleCreateRibbon(object sender, EventArgs e)
        {
            Application.Idle -= OnIdleCreateRibbon;
            if (ComponentManager.Ribbon != null)
            {
                CreateRibbon();
            }
        }

        private static void CreateRibbon()
        {
            if (_isTabCreated) return;
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            // Avoid duplicate tab creation
            RibbonTab existingTab = ribbon.Tabs.FirstOrDefault(t => t.Id == _TabId);
            if (existingTab != null)
            {
                _isTabCreated = true;
                _PluginTab = existingTab;
                return;
            }

            // Create a new tab
            _PluginTab = new RibbonTab { Title = _TabTitle, Id = _TabId };
            ribbon.Tabs.Add(_PluginTab);

            // PANEL 1 : Actions
            _ActionsPanel = new RibbonPanel { Source = new RibbonPanelSource { Title = "Actions" } };
            _PluginTab.Panels.Add(_ActionsPanel);

            _PropertiesButton = GetRibbonButton_Properties();
            _ActionsPanel.Source.Items.Add(_PropertiesButton);

            _InsertBlocksButton = GetRibbonButton_InsertBlocks();
            _ActionsPanel.Source.Items.Add(_InsertBlocksButton);

            // PANEL 2 : Settings
            _SettingsPanel = new RibbonPanel { Source = new RibbonPanelSource { Title = "Settings" } };
            _PluginTab.Panels.Add(_SettingsPanel);

            _PluginStatusButton = GetRibbonButton_PluginStatus();
            _SettingsPanel.Source.Items.Add(_PluginStatusButton);

            _P6ConnectionManagerButton = GetRibbonButton_P6ConnectionManager();
            _SettingsPanel.Source.Items.Add(_P6ConnectionManagerButton);

            _LinkedFoldersButton = GetRibbonButton_LinkedFolders();
            _SettingsPanel.Source.Items.Add(_LinkedFoldersButton);

            // PANEL 3 : Test
            _TestPanel = new RibbonPanel { Source = new RibbonPanelSource { Title = "Test Panel" } };
            _PluginTab.Panels.Add(_TestPanel);

            _testButton = GetRibbonButton_TestButton();
            _TestPanel.Source.Items.Add(_testButton);

            _isTabCreated = true;
        }

        // Button 1 : Properties
        private static RibbonButton GetRibbonButton_Properties()
        {

            RibbonButton propertiesButton = new RibbonButton
            {
                Text = "Properties",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_MODIFY.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_MODIFY.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_Properties)
            };

            return propertiesButton;

        }

        private static async void ButtonHandler_Properties()
        {
            await _pluginInstance.P6ShowPropertirs();
        }

        // Button 2 : InsertBlocks
        private static RibbonButton GetRibbonButton_InsertBlocks()
        {
            RibbonButton insertBlocksButton = new RibbonButton
            {
                Text = "Insert Blocks",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_BLOCK.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_BLOCK.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_InsertBlocks)
            };
            return insertBlocksButton;
        }

        private static async void ButtonHandler_InsertBlocks()
        {
            await _pluginInstance.P6InsertBlocks();
        }

        // Button 3 : Plugin Status
        private static RibbonButton GetRibbonButton_PluginStatus()
        {
            RibbonButton pluginStatusButton = new RibbonButton
            {
                Text = "Plugin Status",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_CHECKSTD.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_CHECKSTD.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_PluginStatus)
            };
            return pluginStatusButton;
        }

        private static void ButtonHandler_PluginStatus()
        {
            // Create the view
            var view = new Autocad_Primavera_P6_Plugin.UserInterface.UI_PluginStatusWindowView.PluginStatusWindowView();

            // Show as modal window using AutoCAD's Application.ShowModalWindow
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(view);
        }

        // Button 4 : P6 Connection Manager
        private static RibbonButton GetRibbonButton_P6ConnectionManager()
        {
            RibbonButton p6ConnectionManagerButton = new RibbonButton
            {
                Text = "P6 Connection Manager",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_DBCONNECT.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_DBCONNECT.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_P6ConnectionManager)
            };
            return p6ConnectionManagerButton;
        }

        private static void ButtonHandler_P6ConnectionManager()
        {
            _pluginInstance.OpenP6ConnectionManager();   // ← was: new view directly
        }

        // Button 5 : Linked Folders
        private static RibbonButton GetRibbonButton_LinkedFolders()
        {
            RibbonButton linkedFoldersButton = new RibbonButton
            {
                Text = "Linked Folders",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_ONLINE_OPEN_FOLDER.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_ONLINE_OPEN_FOLDER.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_LinkedFolders)
            };
            return linkedFoldersButton;
        }

        private static void ButtonHandler_LinkedFolders()
        {
            _pluginInstance.OpenLinkedFoldersManager();
        }

        // Button 6 : Test Button
        private static RibbonButton GetRibbonButton_TestButton()
        {
            RibbonButton testButton = new RibbonButton
            {
                Text = "Test Button",
                ShowText = true,
                ShowImage = true,
                Orientation = Orientation.Vertical,
                Size = RibbonItemSize.Large,
                Image = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_16_ONLINE_OPEN_FOLDER.png"),
                LargeImage = LoadBitmap("Autocad_Primavera_P6_Plugin.Services.RibbonService.RCDATA_32_ONLINE_OPEN_FOLDER.png"),
                CommandHandler = new RibbonCommandHandler(ButtonHandler_TestButton)
            };
            return testButton;
        }

        private static void ButtonHandler_TestButton()
        {
            _pluginInstance.OpenTestButtonFunction();
        }


        // Utility method to load embedded bitmap resources
        private static BitmapImage LoadBitmap(string resourcePath)
        {
            try
            {
                var assembly = typeof(RibbonService).Assembly;

                using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
                {
                    if (stream == null)
                        return null;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

    }

    public class RibbonCommandHandler : ICommand
    {
        private readonly Action _action;

        public RibbonCommandHandler(Action action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _action();

        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

}
