// Copyright 2020-2021 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using Morphic.Core;
using Microsoft.Office.Interop.Word;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Xml;

namespace Morphic.Integrations.Office
{
    public class WordRibbon
    {
        #region General Office functions

        // NOTE: if we add more Word- or Office-related functionality, we should move this region to a separate class
        public static bool IsOfficeInstalled()
        {
            var path = WordRibbon.GetPathToOfficeUserDataDirectory();
            return System.IO.Directory.Exists(path);
        }

        private static string GetPathToOfficeUserDataDirectory()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Office");
        }

        #endregion General Office functions

        private static string GetPathToWordRibbonFile()
        {
            return System.IO.Path.Combine(WordRibbon.GetPathToOfficeUserDataDirectory(), "Word.officeUI");
        }

        public static IMorphicResult<bool> IsBasicSimplifyRibbonEnabled()
        {
            return WordRibbon.IsRibbonEnabled("morphic.basics");
        }

        public static IMorphicResult<bool> IsEssentialsSimplifyRibbonEnabled()
        {
            return WordRibbon.IsRibbonEnabled("morphic.essentials");
        }

        private static IMorphicResult<bool> IsRibbonEnabled(string ribbonId)
        {
            var path = GetPathToWordRibbonFile();
            if (System.IO.File.Exists(path) == false)
            {
                return new MorphicSuccess<bool>(false);
            }

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(path);
            }
            catch
            {
                return new MorphicError<bool>();
            }

            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlNamespaceManager.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");

            XmlNode msoTabsParentNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", xmlNamespaceManager);
            if (msoTabsParentNode == null)
            {
                // parent tabs node doesn't exist, so the ribbon is not enabled
                return new MorphicSuccess<bool>(false);
            }

            var msoTabNodes = xmlDocument.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", xmlNamespaceManager);
            if (msoTabNodes == null)
            {
                // child tab nodes don't exist, so the ribbon is not enabled
                return new MorphicSuccess<bool>(false);
            }

            foreach (XmlNode? msoTab in msoTabNodes!)
            {
                if (msoTab?.Attributes["id"].Value == ribbonId)
                {
                    return new MorphicSuccess<bool>(true);
                }
            }

            // if we did not find the tab in our list, return false
            return new MorphicSuccess<bool>(false);
        }

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
            originalString = "";
            currentSettings = new XmlDocument();
            CapturePrefs(true);
        }

        private void CapturePrefs(bool original = false)
        {
            string capture = "";
            try
            {
                if (File.Exists(path))
                {
                    capture = File.ReadAllText(path);
                    if (capture == "") //check for empty file (possibly left by earlier Morphic run)
                    {
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(path, backupPath);
                        }
                        currentSettings = LoadEmptyTemplate();
                        return;
                    }
                }
                else
                {
                    if (!File.Exists(backupPath))
                    {
                        File.WriteAllText(backupPath, "");
                    }
                    currentSettings = LoadEmptyTemplate();
                    return;
                }
                currentSettings.LoadXml(capture);
                XmlNamespaceManager ns = new XmlNamespaceManager(currentSettings.NameTable);
                ns.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");
                XmlNode tabs = currentSettings.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", ns) ?? throw new ArgumentNullException();
                XmlNode qat = currentSettings.SelectSingleNode("mso:customUI/mso:ribbon/mso:qat", ns) ?? throw new ArgumentNullException();
                XmlNode sharedControls = qat.SelectSingleNode("mso:sharedControls", ns);
                XmlDocument template = LoadEmptyTemplate();
                if (sharedControls == null)
                {
                    XmlNode scgroup = currentSettings.ImportNode(template.SelectSingleNode("mso:customUI/mso:ribbon/mso:qat/mso:sharedControls", ns), true);
                    qat.AppendChild(scgroup);
                }
                else
                {
                    foreach (XmlNode control in template.SelectNodes("mso:customUI/mso:ribbon/mso:qat/mso:sharedControls/mso:control", ns))
                    {
                        if (control == null) break;
                        bool found = false;
                        foreach (XmlNode existing in sharedControls.ChildNodes)
                        {
                            if (existing == null) break;
                            if (existing.Attributes["idQ"].Value == control.Attributes["idQ"].Value)
                            {
                                found = true;
                            }
                        }
                        if (!found)
                        {
                            XmlNode ncontrol = currentSettings.ImportNode(control, true);
                            sharedControls.AppendChild(ncontrol);
                        }
                    }
                }
            }
            catch
            {
                currentSettings = LoadEmptyTemplate();
                try
                {
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(path, backupPath);
                    }
                }
                catch { }
            }
            if (original)
            {
                originalString = capture;
            }
        }

        public void EnableBasicsTab()
        {
            XmlDocument template = LoadComponentTemplates();
            XmlNamespaceManager ns = new XmlNamespaceManager(template.NameTable);
            ns.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");
            CapturePrefs();
            try
            {
                foreach (XmlNode tab in currentSettings.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.basics")
                    {
                        tab.ParentNode.RemoveChild(tab);
                        break;
                    }
                }
                foreach (XmlNode tab in template.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.basics")
                    {
                        XmlNode node = currentSettings.ImportNode(tab, true);
                        XmlNode parent = currentSettings.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", ns);
                        parent.InsertBefore(node, parent.FirstChild);
                        break;
                    }
                }
            }
            catch
            {
                CapturePrefs();
            }
            savePrefs();
        }

        public void DisableBasicsTab()
        {
            XmlDocument template = LoadComponentTemplates();
            XmlNamespaceManager ns = new XmlNamespaceManager(template.NameTable);
            ns.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");
            CapturePrefs();
            try
            {
                foreach (XmlNode tab in currentSettings.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.basics")
                    {
                        tab.ParentNode.RemoveChild(tab);
                        break;
                    }
                }
            }
            catch
            {
                CapturePrefs();
            }
            savePrefs();
        }

        public void EnableEssentialsTab()
        {
            XmlDocument template = LoadComponentTemplates();
            XmlNamespaceManager ns = new XmlNamespaceManager(template.NameTable);
            ns.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");
            CapturePrefs();
            try
            {
                foreach (XmlNode tab in currentSettings.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.essentials")
                    {
                        tab.ParentNode.RemoveChild(tab);
                        break;
                    }
                }
                foreach (XmlNode tab in template.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.essentials")
                    {
                        XmlNode node = currentSettings.ImportNode(tab, true);
                        XmlNode parent = currentSettings.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", ns);
                        if (parent.FirstChild != null && parent.FirstChild.Attributes["id"].Value == "morphic.basics")
                        {
                            parent.InsertAfter(node, parent.FirstChild);
                        }
                        else
                        {
                            parent.InsertBefore(node, parent.FirstChild);
                        }
                        break;
                    }
                }
            }
            catch
            {
                CapturePrefs();
            }
            savePrefs();
        }

        public void DisableEssentialsTab()
        {
            XmlDocument template = LoadComponentTemplates();
            XmlNamespaceManager ns = new XmlNamespaceManager(template.NameTable);
            ns.AddNamespace("mso", "http://schemas.microsoft.com/office/2009/07/customui");
            CapturePrefs();
            try
            {
                foreach (XmlNode tab in currentSettings.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", ns))
                {
                    if (tab.Attributes["id"].Value == "morphic.essentials")
                    {
                        tab.ParentNode.RemoveChild(tab);
                    }
                }
            }
            catch
            {
                CapturePrefs();
            }
            savePrefs();
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
                        File.WriteAllText(path, originalString);
                    }
                }
                File.WriteAllText(path, currentSettings.OuterXml);
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
                    File.WriteAllText(path, originalString);
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
                if (app == null)
                {
                    //will safely fail if there is no word open
                    return;
                }
                if (app.Windows.Count > 0)
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

        private XmlDocument LoadEmptyTemplate()
        {
            XmlDocument reply = new XmlDocument();
            try
            {
                reply.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Integrations.Office.Word_EmptyTemplate.xml"));
            }
            catch { }
            return reply;
        }

        private XmlDocument LoadComponentTemplates()
        {
            XmlDocument reply = new XmlDocument();
            try
            {
                reply.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Integrations.Office.Word_ComponentTemplates.xml"));
            }
            catch { }
            return reply;
        }

        private string path;
        private string backupPath;
        private string originalString;
        private XmlDocument currentSettings;
    }
}
