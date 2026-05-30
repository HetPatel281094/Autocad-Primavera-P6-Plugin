using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_UserControls.UI_P6ProjectSelector
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class P6NodeViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;
        public P6NodeViewModel(string projectId, string projectName, bool isEps, bool isExpanded = false)
        {
            ProjectId = projectId ?? string.Empty;
            ProjectName = projectName ?? string.Empty;
            IsEPS = isEps;
            _isExpanded = isExpanded;
            Children = new ObservableCollection<P6NodeViewModel>();
        }

        public string ProjectId { get; }
        public string ProjectName { get; }
        public bool IsEPS { get; }

        public ObservableCollection<P6NodeViewModel> Children { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public string DisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProjectName))
                {
                    return ProjectId;
                }

                if (string.IsNullOrWhiteSpace(ProjectId))
                {
                    return ProjectName;
                }

                return ProjectId + " - " + ProjectName;
            }
        }

        private string DebuggerDisplay => (IsEPS ? "EPS: " : "Project: ") + DisplayText;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
