using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using PropertyChanged;


namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector
{
    [AddINotifyPropertyChangedInterface]
    public class P6NodeViewModel
    {
        public Project ProjectInstance { get; set; } 
        public EPS EPSInstance { get; set; }
        public bool IsProject => ProjectInstance != null;
        public bool IsSelected { get; set; } = false;
        public bool IsExpanded { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        public string Id => IsProject ? ProjectInstance.Id : EPSInstance.Id;
        public string Name => IsProject ? ProjectInstance.Name : EPSInstance.Name;
        public ObservableCollection<P6NodeViewModel> Children { get; set; }

        public string DisplayText => _DisplayText();
        public string _DisplayText()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                return Name;
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                return Id;
            }

            return Id + " - " + Name;
        }

        private readonly Action<P6NodeViewModel> _onSelectedCallback;

        public P6NodeViewModel(object P6Instance, Action<P6NodeViewModel> onSelectedCallback)
        {
            Children = new ObservableCollection<P6NodeViewModel>();
            _onSelectedCallback = onSelectedCallback;

            if (P6Instance is Project)
            {
                ProjectInstance = (Project)P6Instance;
            }else //(P6Instance is EPS)
            {
                EPSInstance = (EPS)P6Instance;
            };

        }

        // Fody automatically executes this method whenever IsSelected changes
        public void OnIsSelectedChanged()
        {
            if (IsSelected && _onSelectedCallback != null)
            {
                _onSelectedCallback(this);
            }
        }
    }

}
