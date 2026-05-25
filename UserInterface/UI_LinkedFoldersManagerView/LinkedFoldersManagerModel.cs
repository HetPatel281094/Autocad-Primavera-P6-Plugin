using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Autocad_Primavera_P6_Plugin.Services.LiteDBService;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_LinkedFoldersManagerView
{
    class LinkedFoldersManagerModel
    {
        public ObservableCollection<string> LinkedFolders { get; set; }
        public ObservableCollection<ProjectConfig> AllProjectConfigs { get; set; }
        public LinkedFoldersManagerModel()
        {
            LinkedFolders = new ObservableCollection<string>();
            AllProjectConfigs = new ObservableCollection<ProjectConfig>();
        }
    }
}
