# MSIX Installer (Per-User)

This folder builds a per-user MSIX installer without admin rights.

## Build
```
./build-msix.ps1
```

Outputs:
- `output/ArduinoFFBControlCenter.msix`
- `cert/ArduinoFFBControlCenter.cer` (self-signed, for development)

## Install
```
./Install.ps1
```

This installs the certificate to **CurrentUser\TrustedPeople** and installs the MSIX for the current user.

## Notes
- Replace the self-signed certificate with your own code-signing cert for production.
- Update `AppxManifest.xml` placeholders via `build-msix.ps1` parameters if you need a different identity or publisher.
