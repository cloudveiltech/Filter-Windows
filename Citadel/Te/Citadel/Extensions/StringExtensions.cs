/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Te.Citadel.Extensions
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

        public static byte[] SecureStringBytes(this SecureString str)
        {
            IntPtr bstrThis = IntPtr.Zero;
            byte[] managed = new byte[0];
            try
            {
                bstrThis = Marshal.SecureStringToBSTR(str);

                if(bstrThis != IntPtr.Zero)
                {
                    int thisLen = Marshal.ReadInt32(bstrThis, -4);

                    managed = new byte[thisLen];

                    for(var i = 0; i < thisLen; ++i)
                    {
                        managed[i] = Marshal.ReadByte(bstrThis, i * 2);
                    }
                }
            }
            catch(Exception e)
            {
            }
            finally
            {
                // Always free the secure string byte array.
                if(bstrThis != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(bstrThis);
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