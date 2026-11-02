"""Copies rendered sound effects from the Video_Game_Music_Generator_Project into the game.

Render them first with that project's chiptune-composer skill (scripts/sfx.py), then run:

    python tools/import_sfx.py <sfx render folder>
"""
import glob
import os
import shutil
import sys

GAME_SFX = os.path.join(os.path.dirname(__file__), "..", "Assets", "Game", "Resources", "Sfx")


def main(rendered):
    files = glob.glob(os.path.join(rendered, "*.wav"))
    if not files:
        raise SystemExit(f"No sound effects in {rendered}")
    os.makedirs(GAME_SFX, exist_ok=True)
    for path in files:
        shutil.copyfile(path, os.path.join(GAME_SFX, os.path.basename(path)))
    print(f"{len(files)} sound effects copied")


if __name__ == "__main__":
    main(sys.argv[1])
