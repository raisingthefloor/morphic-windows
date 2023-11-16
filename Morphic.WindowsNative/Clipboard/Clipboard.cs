// Copyright 2022-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Clipboard;

public class Clipboard
{
   public static bool IsHistoryEnabled()
   {
       return Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled();
   }

   public static MorphicResult<MorphicUnit, MorphicUnit> SetHistoryEnabled(bool value)
   {
       var openRegistryKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Clipboard", true);
       if (openRegistryKeyResult.IsError == true)
       {
           return MorphicResult.ErrorResult();
       }
       var registryKey = openRegistryKeyResult.Value!;

       var setValueResult = registryKey.SetValue("EnableClipboardHistory", value ? (uint)1 : (uint)0);
       if (setValueResult.IsError == true)
       {
           return MorphicResult.ErrorResult();
       }

       return MorphicResult.OkResult();
   }

   public record BackupClipboardError : MorphicAssociatedValueEnum<BackupClipboardError.Values>
   {
       // enum members
       public enum Values
       {
           UnhandledException,
           Win32Error,
       }

       // functions to create member instances
       public static BackupClipboardError UnhandledException(Exception exception) => new BackupClipboardError(Values.UnhandledException) { Exception = exception };
       public static BackupClipboardError Win32Error(int win32ErrorCode) => new BackupClipboardError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

       // associated values
       public Exception? Exception { get; private set; }
       public int? Win32ErrorCode { get; private set; }

       // verbatim required constructor implementation for MorphicAssociatedValueEnums
       private BackupClipboardError(Values value) : base(value) { }
   }
   //
   public static async Task<MorphicResult<List<(string, object)>?, BackupClipboardError>> BackupContentAsync()
   {
       var clipboardContentView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
       var clipboardAvailableFormats = clipboardContentView.AvailableFormats;
       //
       if (clipboardAvailableFormats is null)
       {
           return MorphicResult.OkResult<List<(string, object)>?>(null);
       }
       //
       List<(string, object)> clipboardContents = new();
       foreach (var availableFormatId in clipboardAvailableFormats)
       {
           object content;
           try
           {
               content = await clipboardContentView.GetDataAsync(availableFormatId);
           }
           catch (COMException ex)
           {
               return MorphicResult.ErrorResult(BackupClipboardError.Win32Error(ex.ErrorCode));
           }
           catch (Exception ex)
           {
               return MorphicResult.ErrorResult(BackupClipboardError.UnhandledException(ex));
           }
           clipboardContents.Add((availableFormatId, content));
       }

       return MorphicResult.OkResult<List<(string, object)>?>(clipboardContents);
   }

   public static void RestoreContent(List<(string, object)> content)
   {
       // NOTE: we clear the content before restoring new content to make sure that we don't mix new clipboard data entries with existing clipboard data entries
       Windows.ApplicationModel.DataTransfer.Clipboard.Clear();

       var clipboardContent = new Windows.ApplicationModel.DataTransfer.DataPackage();
       foreach ((string formatId, object data) in content)
       {
           clipboardContent.SetData(formatId, data);
       }

       //Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(dataPackageToRestore, new Windows.ApplicationModel.DataTransfer.ClipboardContentOptions() { IsAllowedInHistory = false });
       Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(clipboardContent);
   }

   public static void ClearContent()
   {
       Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
   }

   public static async Task<string?> GetTextAsync()
   {
       var clipboardContentView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
       //
       var clipboardAvailableFormats = clipboardContentView.AvailableFormats;
       if (clipboardAvailableFormats is null)
       {
           return null;
       }
       //
       foreach (var availableFormat in clipboardAvailableFormats!)
       {
           if (availableFormat == Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)
           {
               return await clipboardContentView.GetTextAsync();
           }
       }

       // if we did not find a text representation, return null
       return null;
   }

   public static List<string>? GetContentFormats()
   {
       var clipboardContentView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
       //
       var clipboardAvailableFormats = clipboardContentView.AvailableFormats;
       if (clipboardAvailableFormats is null)
       {
           return null;
       }

       var contentFormats = new List<string>();
       foreach (var availableFormat in clipboardAvailableFormats!)
       {
           contentFormats.Add(availableFormat);
       }
       //
       return contentFormats;
   }

   public record GetHistoryItemsError : MorphicAssociatedValueEnum<GetHistoryItemsError.Values>
   {
       // enum members
       public enum Values
       {
           AccessDenied,
           ClipboardHistoryDisabled,
       }

       // functions to create member instances
       public static GetHistoryItemsError AccessDenied => new GetHistoryItemsError(Values.AccessDenied);
       public static GetHistoryItemsError ClipboardHistoryDisabled => new GetHistoryItemsError(Values.ClipboardHistoryDisabled);

       // associated values

       // verbatim required constructor implementation for MorphicAssociatedValueEnums
       private GetHistoryItemsError(Values value) : base(value) { }
   }
   //
   public static async Task<MorphicResult<List<Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem>, GetHistoryItemsError>> GetHistoryItemsAsync()
   {
       var getHistoryItemsResult = await Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync();
       switch (getHistoryItemsResult.Status)
       {
           case Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.AccessDenied:
               return MorphicResult.ErrorResult(GetHistoryItemsError.AccessDenied);
           case Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled:
               return MorphicResult.ErrorResult(GetHistoryItemsError.ClipboardHistoryDisabled);
           case Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success:
               // success
               break;
           default:
               throw new MorphicUnhandledErrorException();
       }

       var result = new List<Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem>();
       foreach (var clipboardHistoryItem in getHistoryItemsResult.Items)
       {
           result.Add(clipboardHistoryItem);
       }
       return MorphicResult.OkResult(result);
   }


   public record SetHistoryItemAsContentError : MorphicAssociatedValueEnum<SetHistoryItemAsContentError.Values>
   {
       // enum members
       public enum Values
       {
           AccessDenied,
           ItemDeleted,
       }

       // functions to create member instances
       public static SetHistoryItemAsContentError AccessDenied => new SetHistoryItemAsContentError(Values.AccessDenied);
       public static SetHistoryItemAsContentError ItemDeleted => new SetHistoryItemAsContentError(Values.ItemDeleted);

       // associated values

       // verbatim required constructor implementation for MorphicAssociatedValueEnums
       private SetHistoryItemAsContentError(Values value) : base(value) { }
   }
   //
   public static MorphicResult<MorphicUnit, SetHistoryItemAsContentError> SetHistoryItemAsContent(Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem item)
   {
       var setHistoryItemAsContentResult = Windows.ApplicationModel.DataTransfer.Clipboard.SetHistoryItemAsContent(item);
       switch (setHistoryItemAsContentResult)
       {
           case Windows.ApplicationModel.DataTransfer.SetHistoryItemAsContentStatus.AccessDenied:
               return MorphicResult.ErrorResult(SetHistoryItemAsContentError.AccessDenied);
           case Windows.ApplicationModel.DataTransfer.SetHistoryItemAsContentStatus.ItemDeleted:
               return MorphicResult.ErrorResult(SetHistoryItemAsContentError.ItemDeleted);
           case Windows.ApplicationModel.DataTransfer.SetHistoryItemAsContentStatus.Success:
               return MorphicResult.OkResult();
           default:
               throw new MorphicUnhandledErrorException();
       }
   }

   public static MorphicResult<MorphicUnit, MorphicUnit> ClearHistory()
   {
       var success = Windows.ApplicationModel.DataTransfer.Clipboard.ClearHistory();
       return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
   }

   public static MorphicResult<MorphicUnit, MorphicUnit> DeleteItemFromHistory(Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem item)
   {
       var success = Windows.ApplicationModel.DataTransfer.Clipboard.DeleteItemFromHistory(item);
       return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
   }
}
