# PTBox Windows installer

`scripts/package.ps1` builds a self-contained Windows 11 x64 EXE installer using Inno Setup 6.7.3. The compiler is project-local and is not shipped with PTBox. The stable AppId in `PTBox.iss` must be retained for upgrades.

Fresh staging is essential: do not package `artifacts/PTBox-win-x64`, which may contain personal configuration, logs, website caches, and backups. `Config/config.json` is installed only when absent and is retained on uninstall. All other published application/runtime files are replaced during upgrade. Uninstall only removes the autorun value if it points at the installation being removed.

The setup EXE does not have Windows Authenticode signing. Release manifests use RSA-PSS signatures; only the verification public key is committed. Private release keys and signing certificates must never be committed or bundled.

## Build sources

- [Inno Setup official download](https://jrsoftware.org/isdl.php), pinned compiler 6.7.3: SHA256 `9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732`, verified Authenticode publisher Pyrsys B.V.
- `Languages/ChineseSimplified.isl`: vendored from the [official translation listing](https://jrsoftware.org/files/istrans/) / [upstream file](https://raw.githubusercontent.com/jrsoftware/issrc/refs/heads/main/Files/Languages/ChineseSimplified.isl) on 2026-09-22. Maintainer attribution is preserved in its header. SHA256 `E0B0B350E2245F3C5E65586DFE43D574F6E7F06F2261149ABA284954B3FC9A8D`.
- [Installer command-line reference](https://jrsoftware.org/ishelp/topic_setupcmdline.htm) and [file preservation flags](https://jrsoftware.org/ishelp/topic_filessection.htm).
