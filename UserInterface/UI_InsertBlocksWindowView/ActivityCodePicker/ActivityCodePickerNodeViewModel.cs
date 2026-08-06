using System;
using System.Collections.ObjectModel;
using Autocad_Primavera_P6_Plugin.Services.P6ApiService;
using PropertyChanged;

namespace Autocad_Primavera_P6_Plugin.UserInterface.UI_InsertBlocksWindowView.ActivityCodePicker
{
    [AddINotifyPropertyChangedInterface]
    public sealed class ActivityCodePickerNodeViewModel
    {
        private readonly Action<ActivityCodePickerNodeViewModel> _onSelectedCallback;

        public ActivityCodeType CodeType { get; private set; }
        public ActivityCode Code { get; private set; }
        public ActivityCodePickerNodeViewModel Parent { get; private set; }
        public ObservableCollection<ActivityCodePickerNodeViewModel> Children { get; private set; }

        public bool IsCodeType => CodeType != null && Code == null;
        public bool IsCode => Code != null;
        public bool IsSelected { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsVisible { get; set; }

        public string Id => IsCodeType ? GetNullableInt(CodeType.ObjectId) : GetNullableInt(Code.ObjectId);
        public string Name => IsCodeType ? CodeType.Name : Code.CodeValue;
        public string Description => IsCodeType ? CodeType.Scope : Code.Description;
        public string Path
        {
            get
            {
                if (Parent == null || Parent.IsCodeType)
                {
                    return Name ?? string.Empty;
                }

                string parentPath = Parent.Path;
                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    return Name ?? string.Empty;
                }

                return parentPath + " / " + (Name ?? string.Empty);
            }
        }

        public ActivityCodePickerNodeViewModel(ActivityCodeType codeType, Action<ActivityCodePickerNodeViewModel> onSelectedCallback)
        {
            CodeType = codeType;
            Children = new ObservableCollection<ActivityCodePickerNodeViewModel>();
            IsExpanded = true;
            IsVisible = true;
            _onSelectedCallback = onSelectedCallback;
        }

        public ActivityCodePickerNodeViewModel(ActivityCode code, ActivityCodePickerNodeViewModel parent, Action<ActivityCodePickerNodeViewModel> onSelectedCallback)
        {
            Code = code;
            Parent = parent;
            Children = new ObservableCollection<ActivityCodePickerNodeViewModel>();
            IsExpanded = true;
            IsVisible = true;
            _onSelectedCallback = onSelectedCallback;
        }

        public void OnIsSelectedChanged()
        {
            if (IsSelected)
            {
                _onSelectedCallback?.Invoke(this);
            }
        }

        public void AttachTo(ActivityCodePickerNodeViewModel parent)
        {
            Parent = parent;
            parent.Children.Add(this);
        }

        private static string GetNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : string.Empty;
        }
    }
}
