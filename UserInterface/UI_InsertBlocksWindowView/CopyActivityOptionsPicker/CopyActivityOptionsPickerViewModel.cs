using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.CopyActivityOptionsPicker
{
    public partial class CopyActivityOptionsPickerModel : ObservableObject
    {
        [ObservableProperty]
        private bool _resAndRoleAssignments  = true;
        partial void OnResAndRoleAssignmentsChanged(bool oldValue, bool newValue) { if (!newValue) { ResAndRoleAssignments_AssignmentCodes = false; }; }

        [ObservableProperty]
        private bool _resAndRoleAssignments_AssignmentCodes  = true;

        [ObservableProperty]
        private bool _relationships  = true;
        partial void OnRelationshipsChanged(bool oldValue, bool newValue) { if (!newValue) { Relationships_OnlyBetweenCopiedActivities = false; }; }

        [ObservableProperty]
        private bool _relationships_OnlyBetweenCopiedActivities  = true;

        [ObservableProperty]
        private bool _expenses  = true;

        [ObservableProperty]
        private bool _activityCodes  = true;

        [ObservableProperty]
        private bool _notebook  = true;

        [ObservableProperty]
        private bool _steps  = true;

        [ObservableProperty]
        private bool _financialPeriodData  = true;

        [ObservableProperty]
        private bool _wPsAndDocs  = true;

        [ObservableProperty]
        private bool _risks  = true;
    }

    public partial class CopyActivityOptionsPickerViewModel : ObservableObject
    {
        [ObservableProperty]
        private CopyActivityOptionsPickerModel _model = new();

        [RelayCommand]
        private void OkButton(object parameter) { }

        [RelayCommand]
        private void CancelButton(object parameter) { }
    }

}
