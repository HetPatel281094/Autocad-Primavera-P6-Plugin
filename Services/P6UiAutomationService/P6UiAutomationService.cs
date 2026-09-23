using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

namespace Autocad_Primavera_P6_Plugin.Services.P6UiAutomationService
{
    public class P6UiAutomationService
    {
        private string P6ProcessName = "PM";

        // Session-level cache — lives across many NavigateToCode calls
        private UIA3Automation? _automation;
        private AutomationElement? _cachedMainWindow;
        private AutomationElement? _cachedActivitiesTab;
        private AutomationElement? _cachedLayoutQueryGrid;
        private string? _cachedLayoutName;

        public void NavigateToCode(string code, string projectShortName, string? layoutName = null)
        {
            try
            {
                _automation ??= new UIA3Automation();
                var automation = _automation;

                var p6MainWindow = GetP6MainWindowCached(automation);
                p6MainWindow.Focus();

                var projectShortNameTrimed = projectShortName.Trim();
                var projectShortNames = GetCurrentProjecctShortName(automation, p6MainWindow);

                if (projectShortNames.Count() != 1 && projectShortNames.First() != projectShortNameTrimed)
                    throw new InvalidOperationException("Matching project is not open or more than one project is open.");

                EnsureExactlyOneP6Window(automation, p6MainWindow);

                var activitiesTab = OpenActivitiesTabCached(automation, p6MainWindow);

                // Layout handling is entirely optional now
                if (!string.IsNullOrWhiteSpace(layoutName))
                {
                    var layoutNameTrimmed = layoutName.Trim();

                    var currentLayoutName = GetCurrentLayoutNameCached(automation, activitiesTab);

                    if (currentLayoutName != layoutNameTrimmed)
                    {
                        ChangeLayoutByLayoutName(automation, p6MainWindow, layoutNameTrimmed);
                        _cachedLayoutName = layoutNameTrimmed;
                        _cachedLayoutQueryGrid = null;
                    }
                }

                FindCodeInLayoutForStringCached(automation, p6MainWindow, activitiesTab, code);
            }
            catch (Exception ex)
            {
                Debug.Print(ex.ToString());
                MessageBox.Show(ex.ToString(), "Error navigating to specific code", MessageBoxButton.OK, MessageBoxImage.Error);
                InvalidateCache();
            }

        }

        private void InvalidateCache()
        {
            _cachedMainWindow = _cachedActivitiesTab = _cachedLayoutQueryGrid = null;
            _cachedLayoutName = null;
        }

        private AutomationElement GetP6MainWindowCached(UIA3Automation automation)
        {
            if (IsElementStillValid(_cachedMainWindow)) return _cachedMainWindow!;

            var mainWindow = GetP6MainWindow(automation); // your existing method, unchanged
            _cachedMainWindow = mainWindow;

            // window changed -> everything found under the old one is stale too
            _cachedActivitiesTab = null;
            _cachedLayoutQueryGrid = null;
            _cachedLayoutName = null;

            return mainWindow;
        }

        private AutomationElement OpenActivitiesTabCached(UIA3Automation automation, AutomationElement p6MainWindow)
        {
            if (IsElementStillValid(_cachedActivitiesTab)) return _cachedActivitiesTab!;

            OpenActivitiesTab(automation, p6MainWindow, out var activitiesTab); // unchanged
            _cachedActivitiesTab = activitiesTab;
            return activitiesTab;
        }

        private string GetCurrentLayoutNameCached(UIA3Automation automation, AutomationElement activitiesTab)
        {
            if (_cachedLayoutName != null) return _cachedLayoutName;

            _cachedLayoutName = GetCurrentLayoutName(automation, activitiesTab); // unchanged
            return _cachedLayoutName;
        }

        private void FindCodeInLayoutForStringCached(UIA3Automation automation, AutomationElement p6MainWindow, AutomationElement activitiesTab, string code)
        {
            var cf = automation.ConditionFactory;

            if (!IsElementStillValid(_cachedLayoutQueryGrid))
            {
                var cf_layoutQueryGrid = cf.ByControlType(ControlType.Pane).And(cf.ByClassName("TCVirtualQueryGrid"));
                _cachedLayoutQueryGrid = activitiesTab.FindFirstDescendant(cf_layoutQueryGrid);

                if (_cachedLayoutQueryGrid == null)
                    throw new InvalidOperationException("Unable to find the Querygrid in Layout");
            }

            var layoutQueryGrid = _cachedLayoutQueryGrid!;

            if (layoutQueryGrid == null) { throw new InvalidOperationException("Unable to find the Querygrid in Layout"); }

            double xRatio = 0.05; // 5% from left boundary
            double yRatio = 0.5;  // vertical center

            var bounds = layoutQueryGrid.BoundingRectangle;

            var clickX = (int)(bounds.X + bounds.Width * xRatio);
            var clickY = (int)(bounds.Y + bounds.Height * yRatio);

            Mouse.Position = new System.Drawing.Point(clickX, clickY);
            Mouse.Click(MouseButton.Left);

            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_E);
            Keyboard.Type(VirtualKeyShort.KEY_F);

            var cf_FindWindow = cf.ByControlType(ControlType.Window)
                .And(cf.ByProcessId(p6MainWindow.Properties.ProcessId.ValueOrDefault))
                .And(cf.ByName("Find"));

            var findWindow = Retry.WhileNull(
                () => automation.GetDesktop().FindFirstChild(cf_FindWindow),
                TimeSpan.FromMilliseconds(800),     // ceiling — same as your old 250+100+250 budget, with headroom
                TimeSpan.FromMilliseconds(25)       // poll interval
            ).Result;

            if (findWindow == null) { throw new InvalidOperationException("Unable to get the find window to query code"); }

            var cf_textBox = cf.ByControlType(ControlType.Edit)
                .And(cf.ByName("Find what:"));

            var cf_findNextButton = cf.ByControlType(ControlType.Button)
                .And(cf.ByName("Find Next"));

            var textBox = findWindow.FindFirstChild(cf_textBox);

            var findNextButton = findWindow.FindFirstChild(cf_findNextButton);

            if (textBox == null || findNextButton == null) { throw new InvalidOperationException("Unable to get textbox and find next button of find window"); }

            textBox.Patterns.Value.Pattern.SetValue(code);

            findNextButton.AsButton().Invoke();

            Retry.WhileFalse(
                () => IsAllTextSelected(textBox),
                TimeSpan.FromMilliseconds(1500),
                TimeSpan.FromMilliseconds(25)
            );

            findWindow.AsWindow().Close();
        }

        private bool IsAllTextSelected(AutomationElement textBox)
        {
            var textPattern = textBox.Patterns.Text.PatternOrDefault;
            if (textPattern == null) return false;

            var selection = textPattern.GetSelection();
            if (selection == null || selection.Length == 0) return false;

            var selectedText = selection[0].GetText(-1);
            var fullText = textPattern.DocumentRange.GetText(-1);

            return !string.IsNullOrEmpty(selectedText) && selectedText == fullText;
        }

        private bool IsElementStillValid(AutomationElement? element)
        {
            if (element == null) return false;
            try
            {
                _ = element.Properties.ProcessId.ValueOrDefault; // round-trips; throws if the element is gone
                return !element.Properties.IsOffscreen.ValueOrDefault;
            }
            catch
            {
                return false;
            }
        }

        private void ChangeLayoutByLayoutName(UIA3Automation automation, AutomationElement p6MainWindow, string layoutName)
        {
            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_V);
            Keyboard.Type(VirtualKeyShort.KEY_O);
            Keyboard.Type(VirtualKeyShort.ENTER);

            Thread.Sleep(200);

            var cf = automation.ConditionFactory;

            var cf_saveLayoutPromt = cf.ByControlType(ControlType.Window)
                .And(cf.ByClassName("TfrmERMsg"))
                .And(cf.ByName("TfrmERMsg"));

            var saveLayoutPromt = automation.GetDesktop().FindFirstChild(cf_saveLayoutPromt);

            if (saveLayoutPromt != null)
            {
                var cf_promptText = cf.ByControlType(ControlType.Edit)
                    .And(cf.ByClassName("TMemo"));

                var promptText = saveLayoutPromt.FindFirstDescendant(cf_promptText);

                var promptTextString = promptText.Patterns.Value.Pattern.Value.ValueOrDefault;

                var checkString = "Would you like to save the changes to this layout?";

                if (string.IsNullOrWhiteSpace(promptTextString) || !checkString.Equals(promptTextString, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Unable confirm prompt is save layout prompt while changing layout."); }

                var cf_noButton = cf.ByControlType(ControlType.Button)
                    .And(cf.ByClassName("TBitBtn"))
                    .And(cf.ByName("No"));

                var noButton = saveLayoutPromt.FindFirstDescendant(cf_noButton);

                if (noButton == null) { throw new InvalidOperationException("Unable to close confirm save prompt while changing layout."); }

                noButton.AsButton().Invoke();

                Thread.Sleep(200);
            }

            var cf_layoutWindow = cf.ByControlType(ControlType.Window)
                .And(cf.ByClassName("TFrmActivityViewOpen"))
                .And(cf.ByName("Open Layout"));

            var layoutWindow = p6MainWindow.FindFirstChild(cf_layoutWindow);

            if (layoutWindow == null) { throw new InvalidOperationException("Unable to find layout window while changing layout."); }

            var cf_openButton = cf.ByControlType(ControlType.Pane)
                .And(cf.ByClassName("TCUltraButton"))
                .And(cf.ByName("Open"));

            var openButton = layoutWindow.FindFirstDescendant(cf_openButton);

            if (openButton == null) { throw new InvalidOperationException("Unable to find open layout button while changing layout."); }

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
            Thread.Sleep(500);

            string previousRowText = null;
            bool layoutFound = false;
            bool projectLayoutsFound = false;
            int maxRowsToSearch = 100; // Safety net to prevent infinite loops

            for (int i = 0; i < maxRowsToSearch; i++)
            {
                var copyTextResult = ClipboardCopyHelper.TryCopyTextViaKeyboard(out var copiedTextString);

                if (!copyTextResult) { throw new InvalidOperationException("Unable to get copied text from clipboard in layout names selection iteration."); }

                var copiedLines = copiedTextString.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                var pointerText = copiedLines.Length > 1 ? copiedLines[1] : string.Empty;

                if (string.IsNullOrWhiteSpace(pointerText)) { throw new InvalidOperationException("Unable to find layout pointer text in layout names selection iteration."); }

                if (!projectLayoutsFound && pointerText.StartsWith("Project -")) { projectLayoutsFound = true; continue; }

                string trimmedText = pointerText.Trim();

                // 4. Check for a match
                if (trimmedText.Equals(layoutName, StringComparison.OrdinalIgnoreCase))
                {
                    layoutFound = true;
                    Console.WriteLine($"Found Layout: {layoutName} at row index {i}");
                    break; // Exit the loop - the row is currently selected!
                }

                // 5. Check if we hit the bottom of the grid
                // If the text hasn't changed since the last Down arrow, we are at the end.
                if (trimmedText == previousRowText)
                {
                    Console.WriteLine("Reached the bottom of the grid. Layout not found.");
                    break;
                }

                previousRowText = trimmedText;

                // 6. Move to the next row
                Keyboard.Type(VirtualKeyShort.DOWN);
                Thread.Sleep(100); // Allow UI time to change selection
            }

            if (!layoutFound) { throw new InvalidOperationException("Unable to find layout name in layout names selection iteration."); }

            openButton.Click();
            Thread.Sleep(2000);
        }

        private string GetCurrentLayoutName(UIA3Automation automation, AutomationElement activitiesTab)
        {
            var cf = automation.ConditionFactory;

            var cf_layoutButton = cf.ByControlType(ControlType.Pane)
                .And(cf.ByClassName("TCUltraButton"));

            var layoutButton_inter = activitiesTab
                .FindAllDescendants(cf_layoutButton);

            var layoutButton = layoutButton_inter
                .FirstOrDefault(
                    pane => pane?.Name != null && pane.Name.StartsWith("Layout:", StringComparison.OrdinalIgnoreCase),
                    null
                );

            if (layoutButton == null) { throw new InvalidOperationException("Unable to find the layout button element"); }

            var layoutName = layoutButton.Name.Split(":")[1].Trim();

            return layoutName;
        }

        private void OpenActivitiesTab(UIA3Automation automation, AutomationElement p6MainWindow, out AutomationElement activitiesTab)
        {
            var cf = automation.ConditionFactory;

            var cf_ActivitiesTab = cf.ByControlType(ControlType.Pane)
                .And(cf.ByClassName("TDevxChildFormDockPanel"))
                .And(cf.ByName("Activities"));

            activitiesTab = p6MainWindow.FindFirstDescendant(cf_ActivitiesTab);

            Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_P);
            Thread.Sleep(100);

            Keyboard.Type(VirtualKeyShort.KEY_A);

            if (activitiesTab == null)
            {
                Thread.Sleep(3000);
                activitiesTab = p6MainWindow.FindFirstDescendant(cf_ActivitiesTab);
            }
            else
            {
                Thread.Sleep(100);
            }

            if (activitiesTab == null) { throw new InvalidOperationException("Unable to find Activities Tab of layout"); }
        }

        private List<string> GetCurrentProjecctShortName(UIA3Automation automation, AutomationElement p6MainWindow)
        {
            var cf = automation.ConditionFactory;

            var cf_TitleBar = cf.ByControlType(ControlType.TitleBar);

            var titleBar = p6MainWindow.FindFirstChild(cf_TitleBar);

            if (titleBar == null) { throw new InvalidOperationException("P6 title bar not found."); }

            var titleBarText = titleBar.Patterns.Value.PatternOrDefault.Value;

            var projectIdsMatch = Regex.Match(titleBarText, @":([^:(]+)\(");

            if (!projectIdsMatch.Success) { throw new InvalidOperationException("Project short name not found in title bar."); }

            var projectIdsString = projectIdsMatch.Groups[1].Value.Trim();

            var projectIdsList = projectIdsString
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            return projectIdsList;
        }

        private AutomationElement GetP6MainWindow(UIA3Automation automation)
        {
            var processes = Process.GetProcessesByName(P6ProcessName);

            if (processes.Length == 0) { throw new InvalidOperationException("P6 process not found."); }

            var p6Process = processes[0];

            var cf = automation.ConditionFactory;

            var cf_mainWindow = cf.ByControlType(ControlType.Window)
                .And(cf.ByProcessId(p6Process.Id))
                .And(
                    cf.ByClassName("TDevxMainForm")
                    .Or(cf.ByClassName("TApplication"))
                );

            var mainWindow = automation.GetDesktop().FindFirstChild(cf_mainWindow);

            if (mainWindow == null) { throw new InvalidOperationException("P6 main window not found."); }

            if (mainWindow.Properties.IsOffscreen.ValueOrDefault || mainWindow.ClassName == "TApplication")
            {
                var restoreButton = mainWindow.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Button)
                      .And(cf.ByName("Restore"))
                );

                if (restoreButton == null) { throw new InvalidOperationException("Cannot restore P6 main window."); }

                restoreButton.AsButton().Invoke();

                Thread.Sleep(300);

                cf_mainWindow = cf.ByControlType(ControlType.Window)
                    .And(cf.ByProcessId(p6Process.Id))
                    .And(cf.ByClassName("TDevxMainForm"));

                mainWindow = automation.GetDesktop().FindFirstChild(cf_mainWindow);

                if (mainWindow == null) { throw new InvalidOperationException("P6 main window not found."); }
            }

            return mainWindow;
        }

        private void EnsureExactlyOneP6Window(UIA3Automation automation, AutomationElement p6MainWindow)
        {
            var p6DesktopWindows = automation
                .GetDesktop()
                .FindAllChildren(cf =>
                    cf.ByControlType(ControlType.Window)
                    .And(cf.ByProcessId(p6MainWindow.Properties.ProcessId.ValueOrDefault))
                );

            var p6AppWindows = p6MainWindow
                .FindAllChildren(cf =>
                    cf.ByControlType(ControlType.Window)
                    .And(cf.ByProcessId(p6MainWindow.Properties.ProcessId.ValueOrDefault))
                );

            List<AutomationElement> p6AllWindows = [];
            p6AllWindows.AddRange(p6DesktopWindows);
            p6AllWindows.AddRange(p6AppWindows);

            if (p6AllWindows.Count != 1) { throw new InvalidOperationException("More than one P6 window is open. Please close and try again"); }

        }

        public void Dispose()
        {
            _automation?.Dispose();
            _automation = null;
        }
    }

    /// <summary>
    /// Sends Ctrl+C and synchronously waits for the clipboard to change.
    /// Uses raw Win32 clipboard calls (not WinForms Clipboard) to avoid STA marshaling on every poll.
    /// </summary>
    public static class ClipboardCopyHelper
    {
        /// <summary>Non-throwing version of <see cref="CopyTextViaKeyboard"/>.</summary>
        public static bool TryCopyTextViaKeyboard(out string result, AutomationElement elementToFocus = null, int timeoutMs = 3000, int pollIntervalMs = 100)
        {
            try
            {
                result = CopyTextViaKeyboard(elementToFocus, timeoutMs, pollIntervalMs);
                return true;
            }
            catch (TimeoutException)
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Focuses <paramref name="elementToFocus"/> (if given), sends Ctrl+C, then polls
        /// the clipboard sequence number until it changes.
        /// </summary>
        /// <param name="timeoutMs">Max time to wait for the clipboard to change.</param>
        /// <param name="pollIntervalMs">Delay between clipboard checks.</param>
        /// <param name="settleDelayMs">Delay after focusing, before sending Ctrl+C.</param>
        /// <returns>The copied text.</returns>
        /// <exception cref="TimeoutException">Clipboard never changed within <paramref name="timeoutMs"/>.</exception>
        public static string CopyTextViaKeyboard(AutomationElement elementToFocus = null, int timeoutMs = 3000, int pollIntervalMs = 100, int settleDelayMs = 1000)
        {
            if (elementToFocus != null)
            {
                elementToFocus.Focus();
                Thread.Sleep(settleDelayMs); // let focus land first
            }

            // Sequence number increments on every write, so it can't miss a copy that
            // happens to match the existing clipboard content, and it's cheap to check.
            uint startSeq = GetClipboardSequenceNumber();

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_C);

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                uint currentSeq = GetClipboardSequenceNumber();

                if (currentSeq != startSeq)
                {
                    if (TryReadClipboardText(out string text) && !string.IsNullOrEmpty(text)) { return text; }

                    // Something else wrote a non-text format - keep waiting.
                    startSeq = currentSeq;
                }

                Thread.Sleep(pollIntervalMs);
            }

            throw new TimeoutException($"Ctrl+C did not change the clipboard within {timeoutMs} ms.");
        }

        // Raw Win32 clipboard access - plain User32/Kernel32 calls, no STA thread needed.

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;

        private static bool TryReadClipboardText(out string text)
        {
            text = null;

            // Retry briefly - clipboard may be momentarily locked by the writer or a clipboard manager.
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        IntPtr handle = GetClipboardData(CF_UNICODETEXT);

                        if (handle == IntPtr.Zero) return false;

                        IntPtr ptr = GlobalLock(handle);

                        if (ptr == IntPtr.Zero) return false;

                        try { text = Marshal.PtrToStringUni(ptr); }
                        finally { GlobalUnlock(handle); }

                        return true;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }

                Thread.Sleep(5);
            }

            return false;
        }
    }

}
