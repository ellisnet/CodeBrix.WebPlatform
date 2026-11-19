================================================================================
UPGRADE NOTES: Tailwind CSS (the standalone `tailwindcss` program)
================================================================================

Tailwind is NOT vendored. The standalone `tailwindcss` program is installed on
the developer's machine, on PATH, and `dx` runs it during a frontend build.
version.txt beside this file holds the oldest version WorkSite accepts;
WorkSite's build program passes it to the platform as its minimum, and the
build refuses anything older.

WHERE IT COMES FROM
-------------------
The release page of https://github.com/tailwindlabs/tailwindcss. Each release
publishes one self-contained program per platform (for example
tailwindcss-linux-x64, tailwindcss-macos-arm64, tailwindcss-windows-x64.exe)
and a sha256sums.txt. Licensed MIT. The program needs no Node.js and no
package.json; it carries its own JavaScript runtime inside it and is used only
at build time. What it writes is a plain CSS file.

HOW TO UPGRADE
--------------
 1. Download the program for your platform from the release, check it against
    the release's checksum, name it `tailwindcss` (`tailwindcss.exe` on
    Windows), make it executable and put it on PATH.
 2. Change version.txt to the new version (when WorkSite needs it).
 3. Rebuild the frontend and look at every page: a new Tailwind version can
    change default styles.

THINGS TO WATCH
---------------
  - WorkSite uses Tailwind v4: frontend/tailwind.css is the entry file
    (`@import "tailwindcss"`) and names the Rust sources Tailwind scans for
    class names. There is no tailwind.config.js.
  - Tailwind finds class names by reading the source text, so every class
    name in the Rust code is written out in full, never assembled.
  - Stay on stable releases.

================================================================================
END OF UPGRADE NOTES
================================================================================
