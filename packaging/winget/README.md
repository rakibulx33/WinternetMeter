# winget manifest (template)

Lets people install with `winget install rakibulx33.WinternetMeter`.

Submit after a release exists (winget needs real download URLs and hashes):

1. Copy the three YAML files to `manifests/r/rakibulx33/WinternetMeter/<version>/` in a fork of [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
2. Replace `VERSION` everywhere, and `SHA256_X64` / `SHA256_ARM64` with the setup hashes from the release's `SHA256SUMS.txt`.
3. Check: `winget validate <folder>` then `winget install --manifest <folder>` on a test machine.
4. Open a PR to winget-pkgs.

Shortcut for later versions: `wingetcreate update rakibulx33.WinternetMeter --version <v> --urls <x64-setup-url> <arm64-setup-url> --submit`.

Note: winget's automated checks may flag unsigned installers with SmartScreen warnings; that doesn't block acceptance but code signing would remove it.
