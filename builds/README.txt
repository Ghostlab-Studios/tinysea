Per-version build artifacts.

Layout:
  v1/  -> served by default and when URL has ?v=1
  v2/  -> served when URL has ?v=2

In each version folder, drop:
  TinySea-Windows.zip
  TinySea-macOS.zip
  TinySea macOS Setup Guide.pdf

download.php (this folder) reads ?file=windows|macos|macos-guide and ?v=1|2 to pick the file.
Missing files return "Build not available yet" — safe to add v2 assets incrementally.
