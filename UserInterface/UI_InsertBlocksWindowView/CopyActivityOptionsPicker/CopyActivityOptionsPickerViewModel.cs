using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.CopyActivityOptionsPicker
{
    public partial class CopyActivityOptionsPickerModel : ObservableObject, ICloneable
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

        public object Clone()
        {
            return new CopyActivityOptionsPickerModel
            {
                ResAndRoleAssignments = this.ResAndRoleAssignments,
                ResAndRoleAssignments_AssignmentCodes = this.ResAndRoleAssignments_AssignmentCodes,
                Relationships = this.Relationships,
                Relationships_OnlyBetweenCopiedActivities = this.Relationships_OnlyBetweenCopiedActivities,
                Expenses = this.Expenses,
                ActivityCodes = this.ActivityCodes,
                Notebook = this.Notebook,
                Steps = this.Steps,
                FinancialPeriodData = this.FinancialPeriodData,
                WPsAndDocs = this.WPsAndDocs,
                Risks = this.Risks
            };
        }

    }

    public partial class CopyActivityOptionsPickerViewModel : ObservableObject
    {
        [ObservableProperty]
        private CopyActivityOptionsPickerModel _model;

        public CopyActivityOptionsPickerViewModel(CopyActivityOptionsPickerModel model = null)
        {
            _model = model ?? new CopyActivityOptionsPickerModel();
        }

        [RelayCommand]
        private static void OkButton(Window owner) {
            owner.DialogResult = true;
            owner.Close();
        }

        [RelayCommand]
        private static void CancelButton(Window owner)
        {
            owner.DialogResult = false;
            owner.Close();
        }
    }

}
