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
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.Integrations.Office
{
    public class WordRibbon
    {
        private const string BASIC_SIMPLIFY_RIBBON_ID = "morphic.basics";
        private const string ESSENTIALS_SIMPLIFY_RIBBON_ID = "morphic.essentials";

        private const string MSO_NAMESPACE = "http://schemas.microsoft.com/office/2009/07/customui";


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

        #region General Word functions

        public static MorphicResult<bool, MorphicUnit> IsWordRunning()
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName("WINWORD");
            }
            catch
            {
                // we could not detect whether or not Word is running
                return MorphicResult.ErrorResult();
            }

            if (processes.Length > 0)
            {
                return MorphicResult.OkResult(true);
            }
            else
            {
                return MorphicResult.OkResult(false);
            }
        }

        #endregion General Word functions

        private static string GetPathToWordRibbonFile()
        {
            return System.IO.Path.Combine(WordRibbon.GetPathToOfficeUserDataDirectory(), "Word.officeUI");
        }

        //

        public static MorphicResult<bool, MorphicUnit> IsBasicSimplifyRibbonEnabled()
        {
            return WordRibbon.IsRibbonEnabled(WordRibbon.BASIC_SIMPLIFY_RIBBON_ID);
        }

        public static MorphicResult<bool, MorphicUnit> IsEssentialsSimplifyRibbonEnabled()
        {
            return WordRibbon.IsRibbonEnabled(WordRibbon.ESSENTIALS_SIMPLIFY_RIBBON_ID);
        }

        private static MorphicResult<bool, MorphicUnit> IsRibbonEnabled(string ribbonId)
        {
            var path = GetPathToWordRibbonFile();
            if (System.IO.File.Exists(path) == false)
            {
                return MorphicResult.OkResult(false);
            }

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(path);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }

            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlNamespaceManager.AddNamespace("mso", WordRibbon.MSO_NAMESPACE);

            var msoTabsParentNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", xmlNamespaceManager);
            if (msoTabsParentNode is null)
            {
                // parent tabs node doesn't exist, so the ribbon is not enabled
                return MorphicResult.OkResult(false);
            }

            var msoTabNodes = xmlDocument.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", xmlNamespaceManager);
            if (msoTabNodes is null)
            {
                // child tab nodes don't exist, so the ribbon is not enabled
                return MorphicResult.OkResult(false);
            }

            foreach (XmlNode? msoTabNode in msoTabNodes!)
            {
                if (msoTabNode?.Attributes["id"].Value == ribbonId)
                {
                    return MorphicResult.OkResult(true);
                }
            }

            // if we did not find the tab in our list, return false
            return MorphicResult.OkResult(false);
        }

        //

        public static MorphicResult<MorphicUnit, MorphicUnit> DisableBasicSimplifyRibbon()
        {
            var disableRibbonResult = WordRibbon.DisableRibbon(WordRibbon.BASIC_SIMPLIFY_RIBBON_ID);
            if (disableRibbonResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // NOTE: we have removed this functionality for the time being due to limitations in Word automation; we may revisit it in the future
            //await WordRibbon.ReloadRibbonsAsync();

            return MorphicResult.OkResult();
        }

        public static MorphicResult<MorphicUnit, MorphicUnit> DisableEssentialsSimplifyRibbon()
        {
            var disableRibbonResult = WordRibbon.DisableRibbon(WordRibbon.ESSENTIALS_SIMPLIFY_RIBBON_ID);
            if (disableRibbonResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // NOTE: we have removed this functionality for the time being due to limitations in Word automation; we may revisit it in the future
            //await WordRibbon.ReloadRibbonsAsync();

            return MorphicResult.OkResult();
        }

        // NOTE: this function does not make Word update its ribbons in real-time; to do so, call the ReloadRibbons() function after using this function
        private static MorphicResult<MorphicUnit, MorphicUnit> DisableRibbon(string ribbonId)
        {
            var path = GetPathToWordRibbonFile();
            if (System.IO.File.Exists(path) == false)
            {
                return MorphicResult.OkResult();
            }

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(path);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }

            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlNamespaceManager.AddNamespace("mso", WordRibbon.MSO_NAMESPACE);

            var msoTabsParentNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", xmlNamespaceManager);
            if (msoTabsParentNode is null)
            {
                // parent tabs node doesn't exist, so the ribbon also does not exist
                return MorphicResult.OkResult();
            }

            var msoTabNodes = xmlDocument.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", xmlNamespaceManager);
            if (msoTabNodes is null)
            {
                // child tab nodes don't exist, so the ribbon also does not exist
                return MorphicResult.OkResult();
            }

            var ribbonRemoved = false;
            foreach (XmlNode? msoTabNode in msoTabNodes!)
            {
                if (msoTabNode?.Attributes["id"].Value == ribbonId)
                {
                    msoTabNode.ParentNode.RemoveChild(msoTabNode);
                    ribbonRemoved = true;
                }
            }

            if (ribbonRemoved == true)
            {
                // save out the modified XMLdocument
                try
                {
                    xmlDocument.Save(path);
                }
                catch
                {
                    return MorphicResult.ErrorResult();
                }
            }

            // if we reach here, the ribbon is not enabled; return success
            return MorphicResult.OkResult();
        }

        //

        public static MorphicResult<MorphicUnit, MorphicUnit> EnableBasicSimplifyRibbon()
        {
            var disableRibbonResult = WordRibbon.EnableRibbon(WordRibbon.BASIC_SIMPLIFY_RIBBON_ID);
            if (disableRibbonResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // NOTE: we have removed this functionality for the time being due to limitations in Word automation; we may revisit it in the future
            //await WordRibbon.ReloadRibbons();

            return MorphicResult.OkResult();
        }

        public static MorphicResult<MorphicUnit, MorphicUnit> EnableEssentialsSimplifyRibbon()
        {
            var disableRibbonResult = WordRibbon.EnableRibbon(WordRibbon.ESSENTIALS_SIMPLIFY_RIBBON_ID, WordRibbon.BASIC_SIMPLIFY_RIBBON_ID /* insertAfterRibbonId */);
            if (disableRibbonResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // NOTE: we have removed this functionality for the time being due to limitations in Word automation; we may revisit it in the future
            //await WordRibbon.ReloadRibbons();

            return MorphicResult.OkResult();
        }

        // NOTE: this function does not make Word update its ribbons in real-time; to do so, call the ReloadRibbons() function after using this function
        private static MorphicResult<MorphicUnit, MorphicUnit> EnableRibbon(string ribbonId, string? insertAfterRibbonId = null)
        {
            XmlDocument xmlDocument = new XmlDocument();

            var path = GetPathToWordRibbonFile();
            if (System.IO.File.Exists(path) == true)
            {
                try
                {
                    xmlDocument.Load(path);
                }
                catch
                {
                    return MorphicResult.ErrorResult();
                }
            }

            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlNamespaceManager.AddNamespace("mso", WordRibbon.MSO_NAMESPACE);

            // verify that the "mso:customUI" node exists
            var msoCustomUINode = xmlDocument.SelectSingleNode("mso:customUI", xmlNamespaceManager);
            if (msoCustomUINode is null)
            {
                // required root note doesn't exist

                // does a DIFFERENT root node exist?  If so, exit with failure
                var rootNode = xmlDocument.FirstChild;
                if (rootNode is not null)
                {
                    return MorphicResult.ErrorResult();
                }

                // otherwise, if the XML tree is empty, create the required root node
                msoCustomUINode = xmlDocument.CreateNode(XmlNodeType.Element, "mso:customUI", WordRibbon.MSO_NAMESPACE);
                xmlDocument.AppendChild(msoCustomUINode);
            }

            // verify that the "mso:customUI/mso:ribbon" node exists
            var msoRibbonNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon", xmlNamespaceManager);
            if (msoRibbonNode is null)
            {
                // required ribbon node doesn't exist; create it now
                msoRibbonNode = xmlDocument.CreateNode(XmlNodeType.Element, "mso:ribbon", WordRibbon.MSO_NAMESPACE);
                msoCustomUINode.AppendChild(msoRibbonNode);
            }

            // verify that the "mso:customUI/mso:ribbon/mso:tabs" node exists
            var msoTabsParentNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", xmlNamespaceManager);
            if (msoTabsParentNode is null)
            {
                // required tabs node doesn't exist; create it now
                msoTabsParentNode = xmlDocument.CreateNode(XmlNodeType.Element, "mso:tabs", WordRibbon.MSO_NAMESPACE);
                msoRibbonNode.AppendChild(msoTabsParentNode);
            }

            // verify that the tab (ribbon) is not already present; if it is, then return success
            var msoTabNodes = xmlDocument.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", xmlNamespaceManager);
            if (msoTabNodes is not null)
            {
                foreach (XmlNode? msoTabNode in msoTabNodes!)
                {
                    if (msoTabNode?.Attributes["id"].Value == ribbonId)
                    {
                        return MorphicResult.OkResult();
                    }
                }
            }

            // at this point, we know that the tab (i.e. ribbon) is not in the XmlDocument; load it from our template and insert it now

            // get a copy of the appropriate tab (ribbon) node from the template we have embedded as a resource in this library
            var getRibbonNodeResult = WordRibbon.GetRibbonTabNodeFromTemplate(ribbonId);
            if (getRibbonNodeResult.IsError == true)
            {
                // programming error
                throw new Exception("Error: could not get ribbon (resource '" + ribbonId + "')");
            }
            var ribbonNodeToImport = getRibbonNodeResult.Value!;

            XmlNode? insertAfterNode = null;
            if (insertAfterRibbonId is not null)
            {
                var getRibbonResult = GetRibbonTabNodeFromXmlDocument(xmlDocument, insertAfterRibbonId!);
                if ((getRibbonResult.IsSuccess == true) && (getRibbonResult.Value is not null))
                {
                    insertAfterNode = getRibbonResult.Value!; 
                }
            }

            // insert the tab (ribbon) node at the top of our list (or in the appropriate place, if it should go _after_ another node
            // TODO: we actually want to insert ESSENTIALS _after_ BASIC if BASIC exists; add some logic for that!
            var ribbonNode = xmlDocument.ImportNode(ribbonNodeToImport, true);
            if (insertAfterNode is not null)
            {
                msoTabsParentNode.InsertAfter(ribbonNode, insertAfterNode!);
            }
            else
            {
                msoTabsParentNode.InsertBefore(ribbonNode, msoTabsParentNode.FirstChild);
            }

            // save out the modified XMLdocument
            try
            {
                xmlDocument.Save(path);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }

            // if we reach here, the ribbon has been enabled; return success
            return MorphicResult.OkResult();
        }

        //

        private static MorphicResult<XmlNode?, MorphicUnit> GetRibbonTabNodeFromXmlDocument(XmlDocument xmlDocument, string ribbonId)
        {
            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
            xmlNamespaceManager.AddNamespace("mso", WordRibbon.MSO_NAMESPACE);

            var msoTabsParentNode = xmlDocument.SelectSingleNode("mso:customUI/mso:ribbon/mso:tabs", xmlNamespaceManager);
            if (msoTabsParentNode is null)
            {
                // parent tabs node doesn't exist; we are missing our template ribbons
                return MorphicResult.ErrorResult();
            }

            var msoTabNodes = xmlDocument.SelectNodes("mso:customUI/mso:ribbon/mso:tabs/mso:tab", xmlNamespaceManager);
            if (msoTabNodes is null)
            {
                // child tab nodes (i.e. ribbon templates) don't exist
                return MorphicResult.ErrorResult();
            }

            foreach (XmlNode? msoTabNode in msoTabNodes!)
            {
                if (msoTabNode?.Attributes["id"].Value == ribbonId)
                {
                    return MorphicResult.OkResult(msoTabNode);
                }
            }

            // if we did not find the ribbon in our list of tabs, return null
            return MorphicResult.OkResult<XmlNode?>(null);
        }

        private static MorphicResult<XmlNode, MorphicUnit> GetRibbonTabNodeFromTemplate(string ribbonId)
        {
            var ribbonTemplateFileStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Integrations.Office.Templates.Word_ComponentTemplates.xml");
            if (ribbonTemplateFileStream is null)
            {
                return MorphicResult.ErrorResult();
            }

            var xmlDocument = new XmlDocument();
            try
            {
                xmlDocument.Load(ribbonTemplateFileStream);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }

            var ribbonTabNodeResult = WordRibbon.GetRibbonTabNodeFromXmlDocument(xmlDocument, ribbonId);
            if (ribbonTabNodeResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var ribbonTabNode = ribbonTabNodeResult.Value;
            if (ribbonTabNode is null)
            {
                // if we did not find the tab in our list, return an error
                return MorphicResult.ErrorResult();
            }
            else
            {
                return MorphicResult.OkResult(ribbonTabNode!);
            }
        }

        //

        // NOTE: we have removed this functionality for the time being due to limitations in Word automation; we may revisit it in the future
        //private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ReloadRibbonsAsync()
        //{
        //    Microsoft.Office.Interop.Word.Application? wordApplication;
        //    try
        //    {
        //        wordApplication = Morphic.WindowsNative.InteropServices.Marshal.GetActiveObject("Word.Application") as Microsoft.Office.Interop.Word.Application;
        //    }
        //    catch (COMException)
        //    {
        //        // if we get a COM exception, assume that Word is not installed
        //        return MorphicResult.OkResult();
        //    }

        //    if (wordApplication is null)
        //    {
        //        // if Word was not running, the object will be null; there is nothing to do
        //        return MorphicResult.OkResult();
        //    }

        //    if (wordApplication.Windows.Count == 0)
        //    {
        //        // if Word has no active windows, there is nothing to do
        //        return MorphicResult.OkResult();
        //    }

        //    var isSuccess = true;

        //    try
        //    {
        //        // create a new Word window; we'll then toggle activation between all active Word windows (and then close the new window) to convince Word to refresh its ribbons in all windows
        //        // NOTE: this is a bit of a hack, based on observations during the development of the original Morphic software; ideally we can find a proper programmatic way to convince Word to refresh in the future

        //        // first, capture a reference to the active Word window; we'll need to return to this window after our cycling is done
        //        var activeWordWindow = wordApplication.ActiveWindow;

        //        try
        //        {
        //            // create a new window; by observation, this will cause Word to load in the new ribbons
        //            // NOTE: if there were already _multiple_ windows, it _might_ be sufficient to just cycle through existing windows to trigger the reload
        //            var tempWordWindow = wordApplication.NewWindow();

        //            // OBSERVATION: this pattern of switching between windows is based on word derived from earlier Morphic Classic code; we should re-evaluate this methodology, to determine if it is still the best
        //            //              way to trigger Word to let it know that the ribbons file has been updated

        //            // cycle through each Word window...twice
        //            for (var cycleIndex = 0; cycleIndex < 2; cycleIndex += 1)
        //            {
        //                foreach (Window? wordWindow in wordApplication.Windows)
        //                {
        //                    if (wordWindow is null)
        //                    {
        //                        Debug.Assert(false, "Programming error: Word's Windows array is not null, but it is returning null windows");
        //                        continue;
        //                    }

        //                    //Morphic.WindowsNative.SendMessage
        //                    var nativeWordWindow = new Morphic.WindowsNative.Windowing.Window((IntPtr)wordWindow.Hwnd);
        //                    var sendMessageResult = nativeWordWindow.Activate();
        //                    // NOTE: this 250m delay is somewhat arbitrary; we should do further examination to find the right delay (or to ask Word the right questions to know when it is done)
        //                    await System.Threading.Tasks.Task.Delay(250);
        //                }
        //            }

        //            // finally, activate both the original window and the new window _twice_, and then close the new window
        //            for (var cycleIndex = 0; cycleIndex < 2; cycleIndex += 1)
        //            {
        //                activeWordWindow.Activate();
        //                // NOTE: this 250m delay is somewhat arbitrary; we should do further examination to find the right delay (or to ask Word the right questions to know when it is done)
        //                await System.Threading.Tasks.Task.Delay(250);
        //                tempWordWindow.Activate();
        //                // NOTE: this 250m delay is somewhat arbitrary; we should do further examination to find the right delay (or to ask Word the right questions to know when it is done)
        //                await System.Threading.Tasks.Task.Delay(250);
        //            }
        //            //
        //            tempWordWindow.Close();
        //        }
        //        finally
        //        {
        //            // for sanity sake, make sure we end up with the original Word window being active
        //            activeWordWindow.Activate();
        //        }
        //    }
        //    catch
        //    {
        //        // if any operations fail (via COM automation), return an error
        //        isSuccess = false;
        //    }

        //    // if we completed all the steps, assume success
        //    return isSuccess ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        //}
    }
}
