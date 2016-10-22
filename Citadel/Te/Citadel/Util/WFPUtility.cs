using NLog;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Te.Citadel.Util
{
    internal static class WFPUtility
    {
        // A predictable GUID based on our application's path.
        public static Guid INSTALLED_FILTER_ID = GuidUtility.Create(GuidUtility.UrlNamespace, AppDomain.CurrentDomain.BaseDirectory);

        public static readonly Logger s_logger = LogManager.GetLogger("Citadel");

        /// <summary>
        /// Disables all outbound traffic from this device persistently until this action is
        /// explicitly reversed.
        /// </summary>
        public static void DisableInternet()
        {
            try
            {
                IntPtr engineHandle = IntPtr.Zero;
                var nullPtr = IntPtr.Zero;
                var result = NativeMethods.FwpmEngineOpen0(null, NativeConstants.RPC_C_AUTHN_WINNT, IntPtr.Zero, IntPtr.Zero, ref engineHandle);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to open filter engine handle: " + result);
                    }

                    return;
                }

                if(s_logger != null)
                {
                    s_logger.Info("Filter engine handle opened successfully.");
                }

                FWPM_FILTER0_ fwpFilter = new FWPM_FILTER0_();

                // Predefined windows GUID for outbound packet matching.
                var FWPM_LAYER_OUTBOUND_IPPACKET_V4_MANAGED = new Guid("1e5c9fae-8a84-4135-a331-950b54229ecd");
                var FWPM_LAYER_OUTBOUND_IPPACKET_V4 = new GUID();
                using(var ms = new MemoryStream(FWPM_LAYER_OUTBOUND_IPPACKET_V4_MANAGED.ToByteArray()))
                using(var br = new BinaryReader(ms))
                {
                    FWPM_LAYER_OUTBOUND_IPPACKET_V4.Data1 = br.ReadUInt32();
                    FWPM_LAYER_OUTBOUND_IPPACKET_V4.Data2 = br.ReadUInt16();
                    FWPM_LAYER_OUTBOUND_IPPACKET_V4.Data3 = br.ReadUInt16();
                    FWPM_LAYER_OUTBOUND_IPPACKET_V4.Data4 = br.ReadBytes(8);
                }

                fwpFilter.layerKey = FWPM_LAYER_OUTBOUND_IPPACKET_V4;

                using(var ms = new MemoryStream(INSTALLED_FILTER_ID.ToByteArray()))
                using(var br = new BinaryReader(ms))
                {
                    fwpFilter.filterKey.Data1 = br.ReadUInt32();
                    fwpFilter.filterKey.Data2 = br.ReadUInt16();
                    fwpFilter.filterKey.Data3 = br.ReadUInt16();
                    fwpFilter.filterKey.Data4 = br.ReadBytes(8);
                }

                // PERSIST OR BOOT?
                fwpFilter.flags = NativeConstants.FWPM_FILTER_FLAG_PERSISTENT;
                //fwpFilter.flags = NativeConstants.FWPM_FILTER_FLAG_BOOTTIME;

                fwpFilter.action.type = FWP_ACTION_TYPE.FWP_ACTION_BLOCK;
                fwpFilter.weight.type = FWP_DATA_TYPE_.FWP_EMPTY; // auto-weight.
                fwpFilter.numFilterConditions = 0; // this applies to all application traffic
                fwpFilter.displayData.name = "Citadel INet Block";
                fwpFilter.displayData.description = "Enforce filter use for internet access.";

                ulong runtimeId = 0;
                result = NativeMethods.FwpmFilterAdd0(engineHandle, ref fwpFilter, IntPtr.Zero, ref runtimeId);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to add filter: " + result);
                    }

                    NativeMethods.FwpmEngineClose0(engineHandle);
                    return;
                }

                if(s_logger != null)
                {
                    s_logger.Info("Filter added successfully.");
                }

                result = NativeMethods.FwpmEngineClose0(engineHandle);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to close install handle: " + result);
                    }

                    return;
                }
            }
            catch(Exception e)
            {
                if(s_logger != null)
                {
                    s_logger.Error(e.Message);
                    s_logger.Error(e.StackTrace);

                    if(e.InnerException != null)
                    {
                        s_logger.Error(e.InnerException.Message);
                        s_logger.Error(e.InnerException.StackTrace);
                    }
                }
            }
        }

        /// <summary>
        /// Enables outbound traffic on this device if previously blocked.
        /// </summary>
        public static void EnableInternet()
        {
            try
            {
                IntPtr engineHandle = IntPtr.Zero;
                var nullPtr = IntPtr.Zero;
                var result = NativeMethods.FwpmEngineOpen0(null, NativeConstants.RPC_C_AUTHN_WINNT, IntPtr.Zero, IntPtr.Zero, ref engineHandle);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to open filter engine handle: " + result);
                    }

                    return;
                }

                if(s_logger != null)
                {
                    s_logger.Info("Filter engine handle opened successfully.");
                }

                var nativeGuid = new GUID();

                using(var ms = new MemoryStream(INSTALLED_FILTER_ID.ToByteArray()))
                using(var br = new BinaryReader(ms))
                {
                    nativeGuid.Data1 = br.ReadUInt32();
                    nativeGuid.Data2 = br.ReadUInt16();
                    nativeGuid.Data3 = br.ReadUInt16();
                    nativeGuid.Data4 = br.ReadBytes(8);
                }

                result = NativeMethods.FwpmFilterDeleteByKey0(engineHandle, ref nativeGuid);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to delete old installed filter: " + result);
                    }

                    NativeMethods.FwpmEngineClose0(engineHandle);
                    return;
                }

                if(s_logger != null)
                {
                    s_logger.Info("Old handle uninstalled successfully successfully.");
                }

                result = NativeMethods.FwpmEngineClose0(engineHandle);

                if(result != 0)
                {
                    if(s_logger != null)
                    {
                        s_logger.Info("Failed to close uninstall handle: " + result);
                    }

                    return;
                }
            }
            catch(Exception e)
            {
                if(s_logger != null)
                {
                    s_logger.Error(e.Message);
                    s_logger.Error(e.StackTrace);

                    if(e.InnerException != null)
                    {
                        s_logger.Error(e.InnerException.Message);
                        s_logger.Error(e.InnerException.StackTrace);
                    }
                }
            }
        }
    }

    // The following is a huge mess generated by a pinvoke code generation utility, but it's
    // accurate.

    internal partial class NativeConstants
    {
        /// FWP_V6_ADDR_SIZE -> 16
        public const int FWP_V6_ADDR_SIZE = 16;

        /// FWP_OPTION_VALUE_ALLOW_MULTICAST_STATE -> 0x00000000
        public const int FWP_OPTION_VALUE_ALLOW_MULTICAST_STATE = 0;

        /// FWP_OPTION_VALUE_DENY_MULTICAST_STATE -> 0x00000001
        public const int FWP_OPTION_VALUE_DENY_MULTICAST_STATE = 1;

        /// FWP_OPTION_VALUE_ALLOW_GLOBAL_MULTICAST_STATE -> 0x00000002
        public const int FWP_OPTION_VALUE_ALLOW_GLOBAL_MULTICAST_STATE = 2;

        /// FWP_OPTION_VALUE_DISABLE_LOOSE_SOURCE -> 0x00000000
        public const int FWP_OPTION_VALUE_DISABLE_LOOSE_SOURCE = 0;

        /// FWP_OPTION_VALUE_ENABLE_LOOSE_SOURCE -> 0x00000001
        public const int FWP_OPTION_VALUE_ENABLE_LOOSE_SOURCE = 1;

        /// FWP_ACTION_FLAG_TERMINATING -> 0x00001000
        public const int FWP_ACTION_FLAG_TERMINATING = 4096;

        /// FWP_ACTION_FLAG_NON_TERMINATING -> 0x00002000
        public const int FWP_ACTION_FLAG_NON_TERMINATING = 8192;

        /// FWP_ACTION_FLAG_CALLOUT -> 0x00004000
        public const int FWP_ACTION_FLAG_CALLOUT = 16384;

        /// FWPM_FILTER_FLAG_NONE -> (0x00000000)
        public const int FWPM_FILTER_FLAG_NONE = 0;

        /// FWPM_FILTER_FLAG_PERSISTENT -> (0x00000001)
        public const int FWPM_FILTER_FLAG_PERSISTENT = 1;

        /// FWPM_FILTER_FLAG_BOOTTIME -> (0x00000002)
        public const int FWPM_FILTER_FLAG_BOOTTIME = 2;

        /// FWPM_FILTER_FLAG_HAS_PROVIDER_CONTEXT -> (0x00000004)
        public const int FWPM_FILTER_FLAG_HAS_PROVIDER_CONTEXT = 4;

        /// FWPM_FILTER_FLAG_CLEAR_ACTION_RIGHT -> (0x00000008)
        public const int FWPM_FILTER_FLAG_CLEAR_ACTION_RIGHT = 8;

        /// FWPM_FILTER_FLAG_PERMIT_IF_CALLOUT_UNREGISTERED -> (0x00000010)
        public const int FWPM_FILTER_FLAG_PERMIT_IF_CALLOUT_UNREGISTERED = 16;

        /// FWPM_FILTER_FLAG_DISABLED -> (0x00000020)
        public const int FWPM_FILTER_FLAG_DISABLED = 32;

        /// FWPM_FILTER_FLAG_INDEXED -> (0x00000040)
        public const int FWPM_FILTER_FLAG_INDEXED = 64;

        /// FWPM_FILTER_FLAG_HAS_SECURITY_REALM_PROVIDER_CONTEXT -> (0x00000080)
        public const int FWPM_FILTER_FLAG_HAS_SECURITY_REALM_PROVIDER_CONTEXT = 128;

        /// FWPM_FILTER_FLAG_SYSTEMOS_ONLY -> (0x00000100)
        public const int FWPM_FILTER_FLAG_SYSTEMOS_ONLY = 256;

        /// FWPM_FILTER_FLAG_GAMEOS_ONLY -> (0x00000200)
        public const int FWPM_FILTER_FLAG_GAMEOS_ONLY = 512;

        public const uint RPC_C_AUTHN_WINNT = 10;

        public const uint RPC_C_AUTHN_DEFAULT = 0xffffffff;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    public struct FWP_V6_ADDR_AND_MASK_
    {
        /// UINT8[16]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 16)]
        public string addr;

        /// UINT8->unsigned char
        public byte prefixLength;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_V4_ADDR_AND_MASK_
    {
        /// UINT32->unsigned int
        public uint addr;

        /// UINT32->unsigned int
        public uint mask;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_RANGE0_
    {
        /// FWP_VALUE0->FWP_VALUE0_
        public FWP_VALUE0_ valueLow;

        /// FWP_VALUE0->FWP_VALUE0_
        public FWP_VALUE0_ valueHigh;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Explicit)]
    public struct Anonymous_19df022a_78d4_408c_9da7_71cd8feb506a
    {
        /// UINT8->unsigned char
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public byte uint8;

        /// UINT16->unsigned short
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public ushort uint16;

        /// UINT32->unsigned int
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public uint uint32;

        /// UINT64*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr uint64;

        /// INT8->char
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public byte int8;

        /// INT16->short
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public short int16;

        /// INT32->int
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public int int32;

        /// INT64*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr int64;

        /// float
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public float float32;

        /// double*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr double64;

        /// FWP_BYTE_ARRAY16*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteArray16;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteBlob;

        /// SID*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr sid;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr sd;

        /// FWP_TOKEN_INFORMATION*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr tokenInformation;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr tokenAccessInformation;

        /// LPWSTR->WCHAR*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr unicodeString;

        /// FWP_BYTE_ARRAY6*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteArray6;

        /// FWP_V4_ADDR_AND_MASK*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr v4AddrMask;

        /// FWP_V6_ADDR_AND_MASK*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr v6AddrMask;

        /// FWP_RANGE0*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr rangeValue;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_CONDITION_VALUE0_
    {
        /// FWP_DATA_TYPE->FWP_DATA_TYPE_
        public FWP_DATA_TYPE_ type;

        /// Anonymous_19df022a_78d4_408c_9da7_71cd8feb506a
        public Anonymous_19df022a_78d4_408c_9da7_71cd8feb506a Union1;
    }

    public enum FWP_MATCH_TYPE_
    {
        FWP_MATCH_EQUAL,

        FWP_MATCH_GREATER,

        FWP_MATCH_LESS,

        FWP_MATCH_GREATER_OR_EQUAL,

        FWP_MATCH_LESS_OR_EQUAL,

        FWP_MATCH_RANGE,

        FWP_MATCH_FLAGS_ALL_SET,

        FWP_MATCH_FLAGS_ANY_SET,

        FWP_MATCH_FLAGS_NONE_SET,

        FWP_MATCH_EQUAL_CASE_INSENSITIVE,

        FWP_MATCH_NOT_EQUAL,

        FWP_MATCH_TYPE_MAX,
    }

    public enum FWP_ACTION_TYPE
    {
        /// FWP_ACTION_BLOCK -> 0x00000001|0x00001000
        FWP_ACTION_BLOCK = (1 | 4096),

        /// FWP_ACTION_PERMIT -> 0x00000002|0x00001000
        FWP_ACTION_PERMIT = (2 | 4096),

        /// FWP_ACTION_CALLOUT_TERMINATING -> 0x00000003|0x00004000|0x00001000
        FWP_ACTION_CALLOUT_TERMINATING = (3
                    | (16384 | 4096)),

        /// FWP_ACTION_CALLOUT_INSPECTION -> 0x00000004|0x00004000|0x00001000
        FWP_ACTION_CALLOUT_INSPECTION = (4
                    | (16384 | 4096)),

        /// FWP_ACTION_CALLOUT_UNKNOWN -> 0x00000005|0x00004000
        FWP_ACTION_CALLOUT_UNKNOWN = (5 | 16384),
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWPM_FILTER_CONDITION0_
    {
        /// GUID->_GUID
        public GUID fieldKey;

        /// FWP_MATCH_TYPE->FWP_MATCH_TYPE_
        public FWP_MATCH_TYPE_ matchType;

        /// FWP_CONDITION_VALUE->FWP_CONDITION_VALUE0_
        public FWP_CONDITION_VALUE0_ conditionValue;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SID_AND_ATTRIBUTES
    {
        /// PSID->PVOID->void*
        public System.IntPtr Sid;

        /// DWORD->unsigned int
        public uint Attributes;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_TOKEN_INFORMATION
    {
        /// ULONG->unsigned int
        public uint sidCount;

        /// PSID_AND_ATTRIBUTES->_SID_AND_ATTRIBUTES*
        public System.IntPtr sids;

        /// ULONG->unsigned int
        public uint restrictedSidCount;

        /// PSID_AND_ATTRIBUTES->_SID_AND_ATTRIBUTES*
        public System.IntPtr restrictedSids;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWPM_DISPLAY_DATA0_
    {
        /// wchar_t*
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string name;

        /// wchar_t*
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string description;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_BYTE_BLOB_
    {
        /// UINT32->unsigned int
        public uint size;

        /// UINT8*
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPStr)]
        public string data;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    public struct FWP_BYTE_ARRAY16_
    {
        /// UINT8[16]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 16)]
        public string byteArray16;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
    public struct FWP_BYTE_ARRAY6_
    {
        /// UINT8[6]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 6)]
        public string byteArray6;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Explicit)]
    public struct Anonymous_4064529c_5391_47b8_870b_44e804180e54
    {
        /// UINT8->unsigned char
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public byte uint8;

        /// UINT16->unsigned short
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public ushort uint16;

        /// UINT32->unsigned int
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public uint uint32;

        /// UINT64*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr uint64;

        /// INT8->char
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public byte int8;

        /// INT16->short
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public short int16;

        /// INT32->int
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public int int32;

        /// INT64*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr int64;

        /// float
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public float float32;

        /// double*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr double64;

        /// FWP_BYTE_ARRAY16*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteArray16;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteBlob;

        /// SID*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr sid;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr sd;

        /// FWP_TOKEN_INFORMATION*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr tokenInformation;

        /// FWP_BYTE_BLOB*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr tokenAccessInformation;

        /// LPWSTR->WCHAR*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr unicodeString;

        /// FWP_BYTE_ARRAY6*
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public System.IntPtr byteArray6;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWP_VALUE0_
    {
        /// FWP_DATA_TYPE->FWP_DATA_TYPE_
        public FWP_DATA_TYPE_ type;

        /// Anonymous_4064529c_5391_47b8_870b_44e804180e54
        public Anonymous_4064529c_5391_47b8_870b_44e804180e54 Union1;
    }

    /*
    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Explicit)]
    public struct Anonymous_30b81c47_c5e8_4c7b_84b1_dac609afeb21
    {
        /// UINT64->unsigned __int64
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public ulong rawContext;

        /// GUID->_GUID
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public GUID providerContextKey;
    }
    */

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWPM_FILTER0_
    {
        /// GUID->_GUID
        public GUID filterKey;

        /// FWPM_DISPLAY_DATA0->FWPM_DISPLAY_DATA0_
        public FWPM_DISPLAY_DATA0_ displayData;

        /// UINT32->unsigned int
        public uint flags;

        /// GUID*
        public System.IntPtr providerKey;

        /// FWP_BYTE_BLOB->FWP_BYTE_BLOB_
        public FWP_BYTE_BLOB_ providerData;

        /// GUID->_GUID
        public GUID layerKey;

        /// GUID->_GUID
        public GUID subLayerKey;

        /// FWP_VALUE0->FWP_VALUE0_
        public FWP_VALUE0_ weight;

        /// UINT32->unsigned int
        public uint numFilterConditions;

        /// FWPM_FILTER_CONDITION0*
        public System.IntPtr filterCondition;

        /// FWPM_ACTION0->FWPM_ACTION0_
        public FWPM_ACTION0_ action;

        /// Anonymous_30b81c47_c5e8_4c7b_84b1_dac609afeb21
        //public Anonymous_30b81c47_c5e8_4c7b_84b1_dac609afeb21 Union1;

        public GUID providerContextKey;

        /// GUID*
        public System.IntPtr reserved;

        /// UINT64->unsigned __int64
        public ulong filterId;

        /// FWP_VALUE0->FWP_VALUE0_
        public FWP_VALUE0_ effectiveWeight;
    }

    public enum FWP_DATA_TYPE_
    {
        FWP_EMPTY,

        FWP_UINT8,

        FWP_UINT16,

        FWP_UINT32,

        FWP_UINT64,

        FWP_INT8,

        FWP_INT16,

        FWP_INT32,

        FWP_INT64,

        FWP_FLOAT,

        FWP_DOUBLE,

        FWP_BYTE_ARRAY16_TYPE,

        FWP_BYTE_BLOB_TYPE,

        FWP_SID,

        FWP_SECURITY_DESCRIPTOR_TYPE,

        FWP_TOKEN_INFORMATION_TYPE,

        FWP_TOKEN_ACCESS_INFORMATION_TYPE,

        FWP_UNICODE_STRING_TYPE,

        FWP_BYTE_ARRAY6_TYPE,

        /// FWP_SINGLE_DATA_TYPE_MAX -> 0xFF
        FWP_SINGLE_DATA_TYPE_MAX = 255,

        FWP_V4_ADDR_MASK,

        FWP_V6_ADDR_MASK,

        FWP_RANGE_TYPE,

        FWP_DATA_TYPE_MAX,
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Explicit)]
    public struct Anonymous_28bb4be9_0f2d_444f_b241_89a6f2750ff1
    {
        /// GUID->_GUID
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public GUID filterType;

        /// GUID->_GUID
        [System.Runtime.InteropServices.FieldOffsetAttribute(0)]
        public GUID calloutKey;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWPM_ACTION0_
    {
        /// FWP_ACTION_TYPE
        public FWP_ACTION_TYPE type;

        /// Anonymous_28bb4be9_0f2d_444f_b241_89a6f2750ff1
        public Anonymous_28bb4be9_0f2d_444f_b241_89a6f2750ff1 Union1;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FWPM_SESSION0_
    {
        /// GUID->_GUID
        public GUID sessionKey;

        /// FWPM_DISPLAY_DATA0->FWPM_DISPLAY_DATA0_
        public FWPM_DISPLAY_DATA0_ displayData;

        /// UINT32->unsigned int
        public uint flags;

        /// UINT32->unsigned int
        public uint txnWaitTimeoutInMSec;

        /// DWORD->unsigned int
        public uint processId;

        /// SID*
        public System.IntPtr sid;

        /// wchar_t*
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string username;

        /// BOOL->int
        public int kernelMode;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GUID
    {
        /// unsigned int
        public uint Data1;

        /// unsigned short
        public ushort Data2;

        /// unsigned short
        public ushort Data3;

        /// unsigned char[8]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data4;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SID
    {
        /// BYTE->unsigned char
        public byte Revision;

        /// BYTE->unsigned char
        public byte SubAuthorityCount;

        /// SID_IDENTIFIER_AUTHORITY->_SID_IDENTIFIER_AUTHORITY
        public SID_IDENTIFIER_AUTHORITY IdentifierAuthority;

        /// DWORD[1]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = System.Runtime.InteropServices.UnmanagedType.U4)]
        public uint[] SubAuthority;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SECURITY_DESCRIPTOR
    {
        /// BYTE->unsigned char
        public byte Revision;

        /// BYTE->unsigned char
        public byte Sbz1;

        /// SECURITY_DESCRIPTOR_CONTROL->WORD->unsigned short
        public ushort Control;

        /// PSID->PVOID->void*
        public System.IntPtr Owner;

        /// PSID->PVOID->void*
        public System.IntPtr Group;

        /// PACL->ACL*
        public System.IntPtr Sacl;

        /// PACL->ACL*
        public System.IntPtr Dacl;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SEC_WINNT_AUTH_IDENTITY_W
    {
        /// unsigned short*
        public System.IntPtr User;

        /// unsigned int
        public uint UserLength;

        /// unsigned short*
        public System.IntPtr Domain;

        /// unsigned int
        public uint DomainLength;

        /// unsigned short*
        public System.IntPtr Password;

        /// unsigned int
        public uint PasswordLength;

        /// unsigned int
        public uint Flags;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct SID_IDENTIFIER_AUTHORITY
    {
        /// BYTE[6]
        [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = System.Runtime.InteropServices.UnmanagedType.I1)]
        public byte[] Value;
    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct ACL
    {
        /// BYTE->unsigned char
        public byte AclRevision;

        /// BYTE->unsigned char
        public byte Sbz1;

        /// WORD->unsigned short
        public ushort AclSize;

        /// WORD->unsigned short
        public ushort AceCount;

        /// WORD->unsigned short
        public ushort Sbz2;
    }

    internal partial class NativeMethods
    {
        /// Return Type: DWORD->unsigned int
        ///serverName: wchar_t*
        ///authnService: UINT32->unsigned int
        ///authIdentity: SEC_WINNT_AUTH_IDENTITY_W*
        ///session: FWPM_SESSION0*
        ///engineHandle: HANDLE*
        [System.Runtime.InteropServices.DllImportAttribute("Fwpuclnt.dll", EntryPoint = "FwpmEngineOpen0")]
        public static extern uint FwpmEngineOpen0([System.Runtime.InteropServices.InAttribute()] [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string serverName, uint authnService, System.IntPtr authIdentity, System.IntPtr session, ref System.IntPtr engineHandle);

        /// Return Type: DWORD->unsigned int
        ///engineHandle: HANDLE->void*
        [System.Runtime.InteropServices.DllImportAttribute("Fwpuclnt.dll", EntryPoint = "FwpmEngineClose0")]
        public static extern uint FwpmEngineClose0(System.IntPtr engineHandle);

        /// Return Type: DWORD->unsigned int
        ///engineHandle: HANDLE->void*
        ///filter: FWPM_FILTER0*
        ///sd: SECURITY_DESCRIPTOR->_SECURITY_DESCRIPTOR
        ///id: UINT64*
        [System.Runtime.InteropServices.DllImportAttribute("Fwpuclnt.dll", EntryPoint = "FwpmFilterAdd0")]
        public static extern uint FwpmFilterAdd0(System.IntPtr engineHandle, [MarshalAs(UnmanagedType.Struct)]ref FWPM_FILTER0_ filter, IntPtr sd, ref ulong id);

        /// Return Type: DWORD->unsigned int
        ///engineHandle: HANDLE->void*
        ///id: UINT64->unsigned __int64
        [System.Runtime.InteropServices.DllImportAttribute("Fwpuclnt.dll", EntryPoint = "FwpmFilterDeleteById0")]
        public static extern uint FwpmFilterDeleteById0(System.IntPtr engineHandle, ulong id);

        /// Return Type: DWORD->unsigned int
        ///engineHandle: HANDLE->void*
        ///key: GUID*
        [System.Runtime.InteropServices.DllImportAttribute("Fwpuclnt.dll", EntryPoint = "FwpmFilterDeleteByKey0")]
        public static extern uint FwpmFilterDeleteByKey0(System.IntPtr engineHandle, ref GUID key);
    }
}