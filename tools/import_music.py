"""Copies rendered songs from the Video_Game_Music_Generator_Project into the game.

Render each song first with that project's chiptune-composer skill, into one output folder, then run:

    python tools/import_music.py <render output folder>

A looping song is cut at its loop points into an intro and a loop clip, because Unity ignores the loop
points stored in a WAV. Songs with a second layer keep it as a separate stem for the game to fade in.
"""
import json
import os
import sys
import wave

GAME_MUSIC = os.path.join(os.path.dirname(__file__), "..", "Assets", "Game", "Resources", "Music")
LOOPING = {
    "skyward_regatta": "storm",
    "harbor_of_lanterns": None,
    "broadsides_at_dawn": "danger",
    "cannons_over_cumulus": None,
    "leviathan_of_the_upper_winds": "danger",
    "the_sovereign_descends": "danger",
}
ONE_SHOTS = ["victory_fanfare"]


def write_part(source, start, end, target):
    """Writes the frames from start up to end. An empty range writes nothing."""
    if end <= start:
        return
    source.setpos(start)
    frames = source.readframes(end - start)
    with wave.open(target, "wb") as out:
        out.setnchannels(source.getnchannels())
        out.setsampwidth(source.getsampwidth())
        out.setframerate(source.getframerate())
        out.writeframes(frames)


def find_loop(info):
    """Finds the loop points in a song's report, wherever the renderer put them."""
    for holder in (info, info.get("loop", {}) if isinstance(info.get("loop"), dict) else {}):
        if "loop_start_sample" in holder:
            return holder["loop_start_sample"], holder["loop_end_sample"]
    raise KeyError("No loop points in the song's report")


def main(rendered):
    os.makedirs(GAME_MUSIC, exist_ok=True)
    for song, extra in LOOPING.items():
        folder = os.path.join(rendered, song)
        with open(os.path.join(folder, song + ".json"), encoding="utf-8") as f:
            start, end = find_loop(json.load(f))
        stems = {"base": f"{song}_stem_base.wav"} if extra else {"base": f"{song}.wav"}
        if extra:
            stems["danger"] = f"{song}_stem_{extra}.wav"
        for layer, name in stems.items():
            with wave.open(os.path.join(folder, name), "rb") as source:
                write_part(source, 0, start, os.path.join(GAME_MUSIC, f"{song}_{layer}_intro.wav"))
                write_part(source, start, end, os.path.join(GAME_MUSIC, f"{song}_{layer}_loop.wav"))
        print(f"{song}: intro {start} samples, loop {end - start} samples, layers {list(stems)}")
    for song in ONE_SHOTS:
        with wave.open(os.path.join(rendered, song, song + ".wav"), "rb") as source:
            write_part(source, 0, source.getnframes(), os.path.join(GAME_MUSIC, song + ".wav"))
        print(f"{song}: one shot")


if __name__ == "__main__":
    main(sys.argv[1])
