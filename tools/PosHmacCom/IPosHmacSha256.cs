using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: Guid("09C836BF-D28E-4555-BF66-FFBF04E5BD3E")]

namespace SimpleDiscountVerifier.PosHmacCom;

[ComVisible(true)]
[Guid("691A6EBF-2706-400C-93AE-26FF314EB863")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
public interface IPosHmacSha256
{
    [DispId(1)]
    [return: MarshalAs(UnmanagedType.BStr)]
    string ComputeBase64(
        [MarshalAs(UnmanagedType.BStr)] string canonicalString,
        [MarshalAs(UnmanagedType.BStr)] string secret);
}
