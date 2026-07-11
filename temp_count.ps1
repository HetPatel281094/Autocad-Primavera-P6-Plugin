$patterns = @(
    'AddINotifyPropertyChangedInterface',
    'using PropertyChanged;',
    'class RelayCommand',
    'class AsyncRelayCommand',
    'new RelayCommand',
    'new AsyncRelayCommand',
    'On.*Changed\(',
    'CommandManager\.InvalidateRequerySuggested'
)
foreach ($p in $patterns) {
    $count = (Get-ChildItem -Recurse -Filter *.cs | Select-String -Pattern $p | Measure-Object).Count
    Write-Output "$p|$count"
}
