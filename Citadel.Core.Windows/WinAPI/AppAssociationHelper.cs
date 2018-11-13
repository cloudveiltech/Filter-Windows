/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Citadel.Core.Windows.WinAPI
{
    public static class AppAssociationHelper
    {
        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern uint AssocQueryString(
            AssocF flags,
            AssocStr str,
            string pszAssoc,
            string pszExtra,
            [Out] StringBuilder pszOut,
            ref uint pcchOut
        );

        [Flags]
        private enum AssocF
        {
            None = 0,
            Init_NoRemapCLSID = 0x1,
            Init_ByExeName = 0x2,
            Open_ByExeName = 0x2,
            Init_DefaultToStar = 0x4,
            Init_DefaultToFolder = 0x8,
            NoUserSettings = 0x10,
            NoTruncate = 0x20,
            Verify = 0x40,
            RemapRunDll = 0x80,
            NoFixUps = 0x100,
            IgnoreBaseClass = 0x200,
            Init_IgnoreUnknown = 0x400,
            Init_Fixed_ProgId = 0x800,
            Is_Protocol = 0x1000,
            Init_For_File = 0x2000
        }

        private enum AssocStr
        {
            Command = 1,
            Executable,
            FriendlyDocName,
            FriendlyAppName,
            NoOpen,
            ShellNewValue,
            DDECommand,
            DDEIfExec,
            DDEApplication,
            DDETopic,
            InfoTip,
            QuickTip,
            TileInfo,
            ContentType,
            DefaultIcon,
            ShellExtension,
            DropTarget,
            DelegateExecute,
            Supported_Uri_Protocols,
            ProgID,
            AppID,
            AppPublisher,
            AppIconReference,
            Max
        }

        public static string PathToDefaultBrowser
        {
            get
            {
                const int S_OK = 0;
                const int S_FALSE = 1;

                uint length = 0;
                uint ret = AssocQueryString(AssocF.None, AssocStr.Executable, ".html", null, null, ref length);

                if(ret != S_FALSE)
                {
                    throw new Exception("Failed to recover default browser details from WinAPI.");
                }

                var sb = new StringBuilder((int)length);
                ret = AssocQueryString(AssocF.None, AssocStr.Executable, ".html", null, sb, ref length);
                if(ret != S_OK)
                {
                    throw new Exception("Failed to recover default browser details from WinAPI.");
                }

                return sb.ToString();
            }
        }
    }
}