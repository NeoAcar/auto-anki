AutoAnki for Windows x64
========================

Requirements:
- Windows 10 or 11 (64-bit)
- Anki Desktop with AnkiConnect add-on 2055492159
- A Gemini API key

Installation:
1. Extract every file from this ZIP.
2. Open PowerShell in the extracted folder.
3. Run:
   powershell -ExecutionPolicy Bypass -File .\Install-AutoAnki.ps1
4. Keep Anki open and complete the AutoAnki setup window.

Usage:
Highlight an English word or short phrase and press the configured shortcut.
The default is Ctrl+Shift+F12. If another app owns it, choose a different key.

Uninstall:
Run:
   powershell -ExecutionPolicy Bypass -File .\Uninstall-AutoAnki.ps1

Add -RemoveUserData to also remove saved settings and the encrypted API key.
