/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Citadel.Core.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Determines whether or not this string is equal to another in an ordinal, case-insensitive
        /// comparison.
        /// </summary>
        /// <param name="str">
        /// This string.
        /// </param>
        /// <param name="other">
        /// Other string to compare to.
        /// </param>
        /// <returns>
        /// True if the two strings are equal in an ordinal, case-insensitive comparison, false
        /// otherwise.
        /// </returns>
        public static bool OIEquals(this string str, string other)
        {
            return string.Equals(str, other, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks for equality between two SecureString objects in an ordinal, case-sensitive
        /// comparison.
        /// </summary>
        /// <param name="str">
        /// This SecureString.
        /// </param>
        /// <param name="other">
        /// Other SecureString to compare to.
        /// </param>
        /// <returns>
        /// True if the two SecureStrings are equal in an ordinal, case-sensitive comparison, false
        /// otherwise.
        /// </returns>
        public static bool OEquals(this SecureString str, SecureString other)
        {
            IntPtr bstrThis = IntPtr.Zero;
            IntPtr bstrOther = IntPtr.Zero;

            try
            {
                bstrThis = Marshal.SecureStringToBSTR(str);
                bstrOther = Marshal.SecureStringToBSTR(other);

                int thisLen = Marshal.ReadInt32(bstrThis, -4);
                int otherLen = Marshal.ReadInt32(bstrOther, -4);

                // Not same length, can't be equal.
                if(thisLen != otherLen)
                {
                    return false;
                }

                for(var i = 0; i < thisLen; ++i)
                {
                    var thisByte = Marshal.ReadByte(bstrThis, i * 2);
                    var otherByte = Marshal.ReadByte(bstrOther, i * 2);

                    if(thisByte != otherByte)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                // Always free the secure string byte array.
                if(bstrThis != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstrThis);
                }

                if(bstrOther != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstrOther);
                }
            }
        }

        /// <summary>
        /// Returns a byte array with encoding.
        /// </summary>
        /// <param name="str">Secure string to get encoded byte array from.</param>
        /// <param name="encoding">encoding to encode byte array with. Defaults to UTF-8.</param>
        /// <returns></returns>
        public static byte[] SecureStringBytes(this SecureString str, Encoding encoding = null)
        {
            IntPtr bstr = IntPtr.Zero;
            int strLength = str.Length;

            char[] arr = new char[str.Length];
            byte[] managed = null;

            encoding = encoding ?? Encoding.UTF8;

            try
            {
                bstr = Marshal.SecureStringToBSTR(str);

                if (bstr != IntPtr.Zero)
                {
                    for (int i = 0; i < str.Length; i++)
                    {
                        // Secure strings are composed of chars (which are 16 bits), so read in all data. We'll convert these to bytes next.
                        arr[i] = Convert.ToChar((ushort)Marshal.ReadInt16(bstr, i * 2));

                        // Check for null char to fix #81.
                        // Issue was that SecureString's length was not accurate and so it appended garbage to the password, causing login to fail.
                        if (arr[i] == 0)
                        {
                            strLength = i;
                            break;
                        }
                    }
                }

                managed = encoding.GetBytes(arr);

                // Zero and release intermediate array immediately.
                for (int i = 0; i < arr.Length; i++)
                {
                    arr[i] = Convert.ToChar(0);
                }
            }
            catch (Exception e)
            {

            }
            finally
            {
                if (arr != null)
                {
                    GCHandle arrHandle = GCHandle.Alloc(arr);
                    arrHandle.Free();
                }

                if (bstr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstr);
                }
            }

            return managed;
        }

        /// <summary>
        /// Determines if the string is non-null, non-empty and non-whitespace.
        /// </summary>
        /// <param name="str">
        /// The string to check.
        /// </param>
        /// <returns>
        /// False if the string is null, empty or whitespace only. True otherwise.
        /// </returns>
        public static bool Valid(string str)
        {
            return !string.IsNullOrEmpty(str) && !string.IsNullOrWhiteSpace(str) && str.Length > 0;
        }
    }
}
 