# POS HMAC COM component

`SimpleDiscountVerifier.PosHmacCom` is a small COM-visible .NET Framework
library for the 1C POS integration. It exposes one operation:

```text
ComputeBase64(canonicalString, secret)
```

The operation encodes both arguments as UTF-8, calculates HMAC-SHA256, and
returns the hash as a Base64 string.

The project targets .NET Framework 4.7.2 because that version is included with
Windows Server 2019. The assembly is platform-neutral; its COM registration
must match the bitness of the 1C process.

## Build

```powershell
dotnet build `
  .\tools\PosHmacCom\SimpleDiscountVerifier.PosHmacCom.csproj `
  --configuration Release
```

The assembly is created at:

```text
tools\PosHmacCom\bin\Release\net472\SimpleDiscountVerifier.PosHmacCom.dll
```

Verify the managed assembly before registration:

```powershell
.\scripts\Test-PosHmacAssembly.ps1
```

Copy the DLL to a stable directory on the POS machine before registration.
`RegAsm /codebase` stores the absolute DLL path, so moving or deleting the file
after registration breaks COM activation.

Example installation directory:

```text
C:\Program Files\SimpleDiscountVerifier\PosHmacCom
```

## Register

Run PowerShell as Administrator on the machine where the 1C code executes.

For 32-bit 1C:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Register-PosHmacCom.ps1 `
  -Architecture x86 `
  -AssemblyPath "C:\Program Files\SimpleDiscountVerifier\PosHmacCom\SimpleDiscountVerifier.PosHmacCom.dll"
```

For 64-bit 1C:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Register-PosHmacCom.ps1 `
  -Architecture x64 `
  -AssemblyPath "C:\Program Files\SimpleDiscountVerifier\PosHmacCom\SimpleDiscountVerifier.PosHmacCom.dll"
```

Register both architectures only when both 32-bit and 64-bit 1C processes use
the component.

## Verify

Run the smoke test in PowerShell of the same bitness as the registered
component:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Test-PosHmacCom.ps1
```

Expected output:

```text
COM smoke test passed.
```

Use the component from 1C:

```bsl
Криптография = Новый COMОбъект(
    "SimpleDiscountVerifier.PosHmacSha256"
);

Подпись = Криптография.ComputeBase64(
    КаноническаяСтрока,
    Секрет
);
```

The ProgID is stable:

```text
SimpleDiscountVerifier.PosHmacSha256
```

## Unregister

Run PowerShell as Administrator and use the same architecture and assembly
path that were used for registration:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Unregister-PosHmacCom.ps1 `
  -Architecture x86 `
  -AssemblyPath "C:\Program Files\SimpleDiscountVerifier\PosHmacCom\SimpleDiscountVerifier.PosHmacCom.dll"
```

Replace `x86` with `x64` for a 64-bit registration.
