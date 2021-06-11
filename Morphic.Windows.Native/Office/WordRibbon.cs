using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Word;
using System.IO;

namespace Morphic.Windows.Native.Office
{
    public class WordRibbon
    {
        private const int WM_ACTIVATE = 0x6;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(int hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, int uFlags);
        [DllImport("user32.dll")]
        private static extern bool LockSetForegroundWindow(uint uLockCode);
        [DllImport("ole32")]
        private static extern int CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid lpclsid);
        [DllImport("oleaut32")]
        private static extern int GetActiveObject([MarshalAs(UnmanagedType.LPStruct)] Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
        private static object GetActiveObject(string id)
        {
            if (id == null)
            {
                return null;
            }
            var result = CLSIDFromProgIDEx(id, out var clsid);
            if (result < 0)
            {
                return null;
            }
            result = GetActiveObject(clsid, IntPtr.Zero, out var obj);
            if (result < 0)
            {
                return null;
            }
            return obj;
        }
        public WordRibbon()
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\Office\\Word.officeUI";
            backupPath = path + ".bak";
            CapturePrefs(true);
        }

        private void CapturePrefs(bool original = false)
        {
            string capture;
            try
            {
                if (File.Exists(path))
                {
                    capture = File.ReadAllText(path);
                    if (!capture.Contains("<mso:qat>") || !capture.Contains("<mso:tabs>")) //check for if a file is missing key elements, if so just backup and wipe customizations
                    {
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(path, backupPath);
                        }
                        capture = UIFile_EmptyTemplate;
                    }
                }
                else
                {
                    if (!File.Exists(backupPath))
                    {
                        File.WriteAllText(backupPath, "");
                    }
                    capture = UIFile_EmptyTemplate;
                }
                currentSettings = capture;
                if (!currentSettings.Contains(UIFile_SharedControlsNoTag))   //checks for shared controls from Morphic, if it doesn't have them, adds them. No visible impact on interface and duplicates are ignored
                {
                    if (currentSettings.Contains("<mso:sharedControls>"))
                    {
                        currentSettings = currentSettings.Insert(currentSettings.IndexOf("<mso:sharedControls>") + 20, UIFile_SharedControlsNoTag);
                    }
                    else
                    {
                        currentSettings = currentSettings.Insert(currentSettings.IndexOf("<mso:qat>") + 9, UIFile_SharedControlsWithTag);
                    }
                }
            }
            catch
            {
                capture = "";
                currentSettings = "";
            }
            if(original)
            {
                originalSettings = capture;
            }
        }

        public void EnableBasicsTab()
        {
            CapturePrefs();
            if(!currentSettings.Contains(UIFile_BasicToolbar))
            {
                currentSettings = currentSettings.Insert(currentSettings.IndexOf("<mso:tabs>") + 10, UIFile_BasicToolbar);
                savePrefs();
            }
        }

        public void DisableBasicsTab()
        {
            CapturePrefs();
            if (currentSettings.Contains(UIFile_BasicToolbar))
            {
                currentSettings = currentSettings.Remove(currentSettings.IndexOf(UIFile_BasicToolbar), UIFile_BasicToolbar.Length);
                savePrefs();
            }
        }

        public void EnableEssentialsTab()
        {
            CapturePrefs();
            if (!currentSettings.Contains(UIFile_EssentialsToolbar))
            {
                if(currentSettings.Contains(UIFile_BasicToolbar))
                {
                    currentSettings = currentSettings.Insert(currentSettings.IndexOf(UIFile_BasicToolbar) + UIFile_BasicToolbar.Length, UIFile_EssentialsToolbar);
                }
                else
                {
                    currentSettings = currentSettings.Insert(currentSettings.IndexOf("<mso:tabs>") + 10, UIFile_EssentialsToolbar);
                }
                savePrefs();
            }
        }

        public void DisableEssentialsTab()
        {
            CapturePrefs();
            if (currentSettings.Contains(UIFile_EssentialsToolbar))
            {
                currentSettings = currentSettings.Remove(currentSettings.IndexOf(UIFile_EssentialsToolbar), UIFile_EssentialsToolbar.Length);
                savePrefs();
            }
        }

        private void savePrefs()
        {
            try
            {
                if (!File.Exists(backupPath))  //creates a backup if there isn't one
                {
                    if (File.Exists(path))
                    {
                        File.Copy(path, backupPath);
                    }
                    else
                    {
                        File.WriteAllText(path, originalSettings);
                    }
                }
                File.WriteAllText(path, currentSettings);
                RefreshRibbon();
            }
            catch
            {
                return;
            }
        }

        public void RestoreOriginal()
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(backupPath, path);
                }
                else
                {
                    File.WriteAllText(path, originalSettings);
                }
                RefreshRibbon();
            }
            catch
            {
                return;
            }
        }

        public void RefreshRibbon()
        {
            try
            {
                Application? app = GetActiveObject("Word.Application") as Application;
                if(app == null)
                {
                    //will safely fail if there is no word open
                    return;
                }
                if(app.Windows.Count > 0)
                {
                    Window window = app.ActiveWindow;
                    Window nwindow = window.NewWindow();
                    for (int i = 0; i < app.Windows.Count; ++i)
                    {
                        SendMessage((IntPtr)app.Windows[i + 1].Hwnd, WM_ACTIVATE, 1, IntPtr.Zero);
                    }
                    for (int i = 0; i < app.Windows.Count; ++i)
                    {
                        SendMessage((IntPtr)app.Windows[i + 1].Hwnd, WM_ACTIVATE, 1, IntPtr.Zero);
                    }
                    window.Activate();
                    nwindow.Activate();
                    window.Activate();
                    nwindow.Activate();
                    nwindow.Close();
                }
            }
            catch
            {
                return;
            }
        }

        private string path;
        private string backupPath;
        private string originalSettings;
        private string currentSettings;

        private static string UIFile_EmptyTemplate = "<mso:customUI xmlns:mso=\"http://schemas.microsoft.com/office/2009/07/customui\"><mso:ribbon><mso:qat></mso:qat><mso:tabs></mso:tabs></mso:ribbon></mso:customUI>";
        private static string UIFile_BasicToolbar = "<mso:tab id=\"morphic.basics\" label=\" Basics (Morphic)\" insertBeforeQ=\"mso:TabHome\"><mso:group id=\"mso_c2.EDC33\" label=\"                                                                                                                                                                            .                                 .\" autoScale=\"true\"><mso:control idQ=\"mso:FileNewDefault\" visible=\"true\"/><mso:control idQ=\"mso:FileSave\" visible=\"true\"/><mso:control idQ=\"mso:PrintPreviewAndPrint\" visible=\"true\"/></mso:group><mso:group id=\"mso_c1.6FBD983\" label=\"                                                                                                                                                                            .                                 .\" autoScale=\"true\"><mso:control idQ=\"mso:Copy\" visible=\"true\"/><mso:control idQ=\"mso:Cut\" visible=\"true\"/><mso:control idQ=\"mso:PasteMenu\" visible=\"true\"/></mso:group><mso:group id=\"mso_c1.F5839\" label=\"                                                                                                                                                                            .                                 .\" autoScale=\"true\"><mso:control idQ=\"mso:SelectAll\" visible=\"true\"/><mso:gallery idQ=\"mso:ThemeFontsGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:FontSizeIncreaseWord\" label=\"Increase Font\" visible=\"true\"/><mso:control idQ=\"mso:FontSizeDecreaseWord\" label=\"Decrease Font\" visible=\"true\"/><mso:control idQ=\"mso:Bold\" visible=\"true\"/><mso:control idQ=\"mso:Italic\" visible=\"true\"/><mso:gallery idQ=\"mso:TextHighlightColorPicker\" showInRibbon=\"false\" visible=\"true\"/></mso:group><mso:group id=\"mso_c3.10BDD3\" label=\"                                                                                                                                                                            .                                 .\" autoScale=\"true\"><mso:control idQ=\"mso:ZoomClassic\" visible=\"true\"/><mso:control idQ=\"mso:TranslateMenu\" visible=\"true\"/></mso:group><mso:group id=\"mso_c1.6BAB619\" label=\"                                                                                                                                                                            .                                 .\" autoScale=\"true\"/></mso:tab>";
        private static string UIFile_EssentialsToolbar = "<mso:tab id=\"morphic.essentials\" label=\"Essentials (Morphic)\" insertBeforeQ=\"mso:TabHome\"><mso:group id=\"mso_c23.1B75B9\" label=\"FILE         PRINT        FORMAT\" autoScale=\"true\"><mso:control idQ=\"mso:FileNewDialogClassic\" label=\"New \" visible=\"true\"/><mso:gallery idQ=\"mso:PageMarginsGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:PageOrientationGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:FileSave\" visible=\"true\"/><mso:control idQ=\"mso:FileSaveAs\" visible=\"true\"/><mso:control idQ=\"mso:PrintPreviewAndPrint\" label=\"Print\" visible=\"true\"/><mso:control idQ=\"mso:SelectMenuExcel\" visible=\"true\"/><mso:control idQ=\"mso:PasteSpecialDialog\" visible=\"true\"/><mso:control idQ=\"mso:FormatPainter\" visible=\"true\"/></mso:group><mso:group id=\"mso_c25.1C84B8\" label=\"TEXT \" autoScale=\"true\"><mso:control idQ=\"mso:StyleGalleryClassic\" visible=\"true\"/><mso:control idQ=\"mso:Font\" visible=\"true\"/><mso:control idQ=\"mso:FontSize\" visible=\"true\"/><mso:gallery idQ=\"mso:FontColorPicker\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:QuickStylesGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:Bold\" visible=\"true\"/><mso:control idQ=\"mso:Italic\" visible=\"true\"/><mso:control idQ=\"mso:Underline\" visible=\"true\"/><mso:control idQ=\"mso:Strikethrough\" visible=\"true\"/><mso:control idQ=\"mso:Superscript\" visible=\"true\"/></mso:group><mso:group id=\"mso_c26.1CE297\" label=\"PARAGRAPH\" autoScale=\"true\"><mso:control idQ=\"mso:AlignLeft\" label=\"Left\" visible=\"true\"/><mso:control idQ=\"mso:AlignCenter\" visible=\"true\"/><mso:control idQ=\"mso:AlignRight\" label=\"Right\" visible=\"true\"/><mso:control idQ=\"mso:AlignJustify\" visible=\"true\"/><mso:control idQ=\"mso:BulletsAndNumberingBulletsDialog\" label=\"Bullets (plus)\" visible=\"true\"/><mso:gallery idQ=\"mso:NumberingGalleryWord\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:OutdentClassic\" visible=\"true\"/><mso:gallery idQ=\"mso:LineSpacingGallery\" label=\"Line Spacing\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:IndentIncrease\" visible=\"true\"/><mso:control idQ=\"mso:ParagraphMarks\" visible=\"true\"/><mso:gallery idQ=\"mso:ShadingColorPicker\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:BordersSelectionGallery\" showInRibbon=\"false\" visible=\"true\"/></mso:group><mso:group id=\"mso_c28.1E3297\" label=\"INSERT\" autoScale=\"true\"><mso:control idQ=\"mso:TableInsertDialogWord\" label=\"Table...\" visible=\"true\"/><mso:gallery idQ=\"mso:HeaderInsertGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:FooterInsertGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:PictureInsertFromFile\" label=\"Pictures\" visible=\"true\"/><mso:gallery idQ=\"mso:ShapesInsertGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:ChartInsert\" visible=\"true\"/><mso:gallery idQ=\"mso:SymbolInsertGallery\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:EquationInsertGallery\" showInRibbon=\"false\" visible=\"true\"/></mso:group><mso:group id=\"mso_c29.1EE2BD\" label=\"EDITING\" autoScale=\"true\"><mso:control idQ=\"mso:InsertNewComment\" visible=\"true\"/><mso:control idQ=\"mso:ReviewTrackChanges\" visible=\"true\"/><mso:control idQ=\"mso:ReviewAcceptOrRejectChangeDialog\" label=\"Accept/Reject \" visible=\"true\"/><mso:gallery idQ=\"mso:Undo\" showInRibbon=\"false\" visible=\"true\"/><mso:gallery idQ=\"mso:Redo\" showInRibbon=\"false\" visible=\"true\"/><mso:control idQ=\"mso:SpellingAndGrammar\" visible=\"true\"/></mso:group><mso:group id=\"mso_c24.1BC272\" label=\"EASIER TO READ\" autoScale=\"true\"><mso:control idQ=\"mso:TranslateMenu\" visible=\"true\"/><mso:control idQ=\"mso:ZoomClassic\" visible=\"true\"/></mso:group></mso:tab>";
        private static string UIFile_SharedControlsWithTag = "<mso:sharedControls><mso:control idQ=\"mso:FileNewDefault\" visible=\"false\"/><mso:control idQ=\"mso:FileOpenUsingBackstage\" visible=\"false\"/><mso:control idQ=\"mso:FileSave\" visible=\"false\"/><mso:control idQ=\"mso:FileSendAsAttachment\" visible=\"false\"/><mso:control idQ=\"mso:FilePrintQuick\" visible=\"false\"/><mso:control idQ=\"mso:PrintPreviewAndPrint\" visible=\"false\"/><mso:control idQ=\"mso:SpellingAndGrammar\" visible=\"false\"/><mso:control idQ=\"mso:Undo\" visible=\"true\"/><mso:control idQ=\"mso:RedoOrRepeat\" visible=\"true\"/><mso:control idQ=\"mso:TableDrawTable\" visible=\"false\"/><mso:control idQ=\"mso:PointerModeOptions\" visible=\"false\"/><mso:control idQ=\"mso:GroupPrintPreviewPrint\" visible=\"true\"/></mso:sharedControls>";
        private static string UIFile_SharedControlsNoTag = "<mso:control idQ=\"mso:FileNewDefault\" visible=\"false\"/><mso:control idQ=\"mso:FileOpenUsingBackstage\" visible=\"false\"/><mso:control idQ=\"mso:FileSave\" visible=\"false\"/><mso:control idQ=\"mso:FileSendAsAttachment\" visible=\"false\"/><mso:control idQ=\"mso:FilePrintQuick\" visible=\"false\"/><mso:control idQ=\"mso:PrintPreviewAndPrint\" visible=\"false\"/><mso:control idQ=\"mso:SpellingAndGrammar\" visible=\"false\"/><mso:control idQ=\"mso:Undo\" visible=\"true\"/><mso:control idQ=\"mso:RedoOrRepeat\" visible=\"true\"/><mso:control idQ=\"mso:TableDrawTable\" visible=\"false\"/><mso:control idQ=\"mso:PointerModeOptions\" visible=\"false\"/><mso:control idQ=\"mso:GroupPrintPreviewPrint\" visible=\"true\"/>";
    }
}
